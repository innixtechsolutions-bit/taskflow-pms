using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TaskFlow.Api.Data.Entities;
using TaskFlow.Api.Startup;
using TaskFlow.Api.Tests.TestSupport;

namespace TaskFlow.Api.Tests.Startup;

public class AdminSeederTests : SqlServerTestDatabase
{
    private static IConfiguration BuildConfig(string? email, string? password) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Admin:Email"] = email,
                ["Admin:Password"] = password
            })
            .Build();

    [Fact]
    public async Task SeedAsync_throws_when_admin_email_is_missing_and_no_admin_exists()
    {
        var config = BuildConfig(email: null, password: "SomePassword1");
        var seeder = new AdminSeeder(Db, config, NullLogger<AdminSeeder>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => seeder.SeedAsync());
    }

    [Fact]
    public async Task SeedAsync_throws_when_admin_password_is_empty_and_no_admin_exists()
    {
        var config = BuildConfig(email: "admin@taskflow.local", password: "");
        var seeder = new AdminSeeder(Db, config, NullLogger<AdminSeeder>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => seeder.SeedAsync());
    }

    [Fact]
    public async Task SeedAsync_creates_hashed_admin_when_config_is_present()
    {
        var config = BuildConfig(email: "admin@taskflow.local", password: "TaskFlow!Admin2026");
        var seeder = new AdminSeeder(Db, config, NullLogger<AdminSeeder>.Instance);

        await seeder.SeedAsync();

        var admin = Db.Users.Single(u => u.Email == "admin@taskflow.local");
        Assert.Equal(Role.Admin, admin.Role);
        Assert.NotEqual("TaskFlow!Admin2026", admin.PasswordHash);

        var hasher = new PasswordHasher<User>();
        Assert.Equal(
            PasswordVerificationResult.Success,
            hasher.VerifyHashedPassword(admin, admin.PasswordHash, "TaskFlow!Admin2026"));
    }

    [Fact]
    public async Task SeedAsync_does_not_reseed_when_an_admin_already_exists()
    {
        Db.Users.Add(new User
        {
            FullName = "Existing Admin",
            Email = "existing-admin@taskflow.local",
            PasswordHash = "already-hashed",
            Role = Role.Admin,
            CreatedAt = DateTime.UtcNow
        });
        await Db.SaveChangesAsync();

        // Config is missing on purpose: if the seeder tried to seed again, it would throw.
        var config = BuildConfig(email: null, password: null);
        var seeder = new AdminSeeder(Db, config, NullLogger<AdminSeeder>.Instance);

        await seeder.SeedAsync();

        Assert.Single(Db.Users);
    }
}
