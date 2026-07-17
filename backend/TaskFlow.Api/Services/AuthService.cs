using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TaskFlow.Api.Data;
using TaskFlow.Api.Data.Entities;
using TaskFlow.Api.Dtos;

namespace TaskFlow.Api.Services;

public partial class AuthService(AppDbContext dbContext, IConfiguration configuration, ILoginAttemptTracker loginAttemptTracker)
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);
    private static readonly PasswordHasher<User> PasswordHasher = new();

    // Every EF Core query and controller action that touches the database is async so
    // the thread isn't blocked waiting on I/O — under load, that's the difference
    // between a thread pool that keeps up and one that starves.
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        // Re-checked here, not only via the RegisterRequest data annotation: this method
        // is also called directly by unit tests and could be called by future callers
        // that bypass HTTP model binding entirely.
        if (!PasswordPattern().IsMatch(request.Password))
        {
            throw new InvalidPasswordException();
        }

        // SQL Server's default collation is case-insensitive, so this comparison (and the
        // unique index from AppDbContext) already treats "Ada@x.com" and "ada@x.com" as
        // the same email — see data-model.md.
        var emailExists = await dbContext.Users.AnyAsync(u => u.Email == request.Email);
        if (emailExists)
        {
            throw new EmailAlreadyExistsException();
        }

        var user = new User
        {
            FullName = request.FullName,
            Email = request.Email,
            Role = Role.Developer,
            PasswordHash = string.Empty,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = PasswordHasher.HashPassword(user, request.Password);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        return IssueToken(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        // Checked before touching the database or the password hasher: an email that's
        // already blocked shouldn't pay the cost of either, and must be refused (429)
        // even when the password given happens to be correct (FR-019).
        if (loginAttemptTracker.IsBlocked(request.Email))
        {
            throw new TooManyAttemptsException();
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.Email == request.Email);

        // Same exception (and message) whether the email doesn't exist or the password
        // is wrong (FR-008) — the caller must not learn which one it was.
        if (user is null || PasswordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password)
            == PasswordVerificationResult.Failed)
        {
            loginAttemptTracker.RecordFailure(request.Email);
            throw new InvalidCredentialsException();
        }

        loginAttemptTracker.RecordSuccess(request.Email);
        return IssueToken(user);
    }

    private AuthResponse IssueToken(User user)
    {
        var expiresAt = DateTime.UtcNow.Add(SessionLifetime);

        // Claims are what the JWT bearer handler (Program.cs) reads back on every later
        // request — identity, name, and role travel inside the signed token itself,
        // which is what lets authentication be stateless (no server-side session store).
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var signingKey = configuration["Jwt:SigningKey"]!;
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return new AuthResponse(user.Id, tokenString, expiresAt, user.FullName, user.Role.ToString());
    }

    [GeneratedRegex(@"^(?=.*[A-Za-z])(?=.*\d).{8,}$")]
    private static partial Regex PasswordPattern();
}
