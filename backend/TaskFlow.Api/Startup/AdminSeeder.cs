using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Data;
using TaskFlow.Api.Data.Entities;

namespace TaskFlow.Api.Startup;

// Registered Scoped (see Program.cs) because it depends on AppDbContext, which is
// itself Scoped — a Scoped service can only be injected into other Scoped (or
// Transient) services, never into a Singleton, so AdminSeeder follows AppDbContext's
// lifetime rather than picking an arbitrary one.
public class AdminSeeder(AppDbContext dbContext, IConfiguration configuration, ILogger<AdminSeeder> logger)
{
    public async Task SeedAsync()
    {
        if (await dbContext.Users.AnyAsync(u => u.Role == Role.Admin))
        {
            logger.LogInformation("An Admin account already exists; skipping seed.");
            return;
        }

        var email = configuration["Admin:Email"];
        var password = configuration["Admin:Password"];

        // FR-018a: fail fast rather than start without an Admin or fall back to any
        // default credentials — an unreachable admin area is safer than a guessable one.
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "No Admin account exists and the seed Admin credentials are not configured. " +
                "Set 'Admin:Email' and 'Admin:Password' via 'dotnet user-secrets set' in " +
                "development, or as the Admin__Email / Admin__Password environment variables " +
                "in other environments, then restart the application.");
        }

        var admin = new User
        {
            FullName = "Admin",
            Email = email,
            Role = Role.Admin,
            PasswordHash = string.Empty,
            CreatedAt = DateTime.UtcNow
        };
        // PasswordHasher is used directly (not the rest of ASP.NET Core Identity —
        // no UserManager, no Identity DbContext) per the constitution's fixed stack.
        admin.PasswordHash = new PasswordHasher<User>().HashPassword(admin, password);

        dbContext.Users.Add(admin);
        await dbContext.SaveChangesAsync();
        logger.LogInformation("Seeded initial Admin account {Email}.", email);
    }
}
