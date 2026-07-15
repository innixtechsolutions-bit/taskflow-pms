using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TaskFlow.Api.Data;
using TaskFlow.Api.Services;
using TaskFlow.Api.Startup;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Scoped, not Singleton: AdminSeeder depends on AppDbContext, which is itself
// Scoped, and a Scoped service can't be injected into a longer-lived Singleton.
builder.Services.AddScoped<AdminSeeder>();
builder.Services.AddScoped<AuthService>();

// ProblemDetails (RFC 7807) gives every error response — validation failures,
// auth failures, unhandled exceptions — the same shape (FR-020), instead of each
// endpoint inventing its own error format.
builder.Services.AddProblemDetails();

var jwtSigningKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("Jwt:Audience is not configured.");

// JWT bearer auth: the token's signature and claims (identity, role, exp) are all
// the server needs to authenticate/authorize a request — no server-side session
// store. AddAuthentication/AddJwtBearer register the *scheme*; AddAuthorization
// registers the policy engine that [Authorize] attributes use. Neither does
// anything until the UseAuthentication/UseAuthorization middleware below runs.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// Runs once at startup, before the app accepts traffic: fails fast (FR-018a) if no
// Admin exists yet and the seed credentials aren't configured, rather than an
// IHostedService — this is a one-shot check, not an ongoing background process.
using (var startupScope = app.Services.CreateScope())
{
    var adminSeeder = startupScope.ServiceProvider.GetRequiredService<AdminSeeder>();
    await adminSeeder.SeedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();

// Order matters: authentication must run before authorization (you can't check
// what role a request has until you know who's making it), and both must run
// before MapControllers so [Authorize] attributes are enforced.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
