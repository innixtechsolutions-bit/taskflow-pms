using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using TaskFlow.Api.Data;
using TaskFlow.Api.Services;
using TaskFlow.Api.Startup;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// The Angular dev server (localhost:4300) and the API (a different port) are
// different origins, so the browser blocks the register/login fetch calls
// unless the API explicitly allows that origin. No credentials mode needed —
// the JWT travels in an Authorization header, not a cookie.
const string FrontendDevCorsPolicy = "FrontendDev";
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendDevCorsPolicy, policy =>
        policy.WithOrigins("http://localhost:4300")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Scoped, not Singleton: AdminSeeder depends on AppDbContext, which is itself
// Scoped, and a Scoped service can't be injected into a longer-lived Singleton.
builder.Services.AddScoped<AdminSeeder>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<WorkItemService>();

// AddMemoryCache registers the shared IMemoryCache that LoginAttemptTracker wraps.
// The tracker itself is Singleton, not Scoped: it needs to accumulate failed attempts
// across requests, and a new instance per request would never remember anything.
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ILoginAttemptTracker, LoginAttemptTracker>();

// ProblemDetails (RFC 7807) gives every error response — validation failures,
// auth failures, unhandled exceptions — the same shape (FR-020), instead of each
// endpoint inventing its own error format.
builder.Services.AddProblemDetails();

// Read eagerly, purely to fail fast: a real deployment missing these keys should
// refuse to start, not serve traffic no one can ever authenticate against.
_ = builder.Configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");
_ = builder.Configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
_ = builder.Configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience is not configured.");

// JWT bearer auth: the token's signature and claims (identity, role, exp) are all
// the server needs to authenticate/authorize a request — no server-side session
// store. AddAuthentication/AddJwtBearer register the *scheme*; AddAuthorization
// registers the policy engine that [Authorize] attributes use. Neither does
// anything until the UseAuthentication/UseAuthorization middleware below runs.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Reads builder.Configuration fresh here, at the point this delegate actually
        // runs — which the options framework defers until the JWT bearer handler is
        // first resolved (the first incoming request), not when this line is reached
        // during startup. That matters because WebApplicationFactory-based integration
        // tests layer in configuration overrides (a different signing key) partway
        // through host startup: capturing the value in a variable up here, before that
        // layering is guaranteed complete, produced a handler validating against the
        // wrong key while AuthService (a live IConfiguration read per request) issued
        // tokens signed with the right one — "signature key was not found" on every
        // protected endpoint. Reading here, lazily, sees the same final configuration
        // AuthService sees.
        var signingKey = builder.Configuration["Jwt:SigningKey"]!;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
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
    // Two different things: MapOpenApi() serves the *document* — a machine-readable
    // JSON description of every endpoint, generated from the controllers/DTOs, at
    // /openapi/v1.json. MapScalarApiReference() serves a *UI* — an interactive,
    // human-browsable page (at /scalar) that reads that same JSON document and
    // renders it as a browsable, "try it out" API explorer. Dev-only: this isn't
    // something an end user of TaskFlow should ever see.
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();

// CORS must run before authentication/authorization: the browser's preflight
// OPTIONS request carries no Authorization header, so if UseCors ran later,
// the preflight itself would be rejected before ever reaching this policy.
app.UseCors(FrontendDevCorsPolicy);

// Order matters: authentication must run before authorization (you can't check
// what role a request has until you know who's making it), and both must run
// before MapControllers so [Authorize] attributes are enforced.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
