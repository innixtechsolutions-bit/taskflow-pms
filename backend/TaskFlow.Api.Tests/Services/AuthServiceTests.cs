using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using TaskFlow.Api.Data.Entities;
using TaskFlow.Api.Dtos;
using TaskFlow.Api.Services;
using TaskFlow.Api.Tests.TestSupport;

namespace TaskFlow.Api.Tests.Services;

public class AuthServiceTests : SqlServerTestDatabase
{
    private static IConfiguration BuildConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = "unit-test-signing-key-at-least-32-bytes-long!!",
                ["Jwt:Issuer"] = "TaskFlow.Api.Tests",
                ["Jwt:Audience"] = "TaskFlow.Api.Tests"
            })
            .Build();

    private static ILoginAttemptTracker CreateTracker() => new LoginAttemptTracker(new MemoryCache(new MemoryCacheOptions()));

    private AuthService CreateSut(ILoginAttemptTracker? tracker = null) =>
        new(Db, BuildConfig(), tracker ?? CreateTracker());

    private static RegisterRequest ValidRegisterRequest(string email = "ada@example.com") => new()
    {
        FullName = "Ada Lovelace",
        Email = email,
        Password = "Password1"
    };

    private static LoginRequest LoginRequestFor(string email, string password) => new()
    {
        Email = email,
        Password = password
    };

    [Fact]
    public async Task RegisterAsync_creates_account_with_developer_role_by_default()
    {
        var sut = CreateSut();

        var response = await sut.RegisterAsync(ValidRegisterRequest());

        Assert.Equal("Developer", response.Role);
        var stored = Db.Users.Single(u => u.Email == "ada@example.com");
        Assert.Equal(Role.Developer, stored.Role);
        Assert.False(string.IsNullOrWhiteSpace(response.Token));
    }

    [Fact]
    public async Task RegisterAsync_rejects_duplicate_email_case_insensitively()
    {
        var sut = CreateSut();
        await sut.RegisterAsync(ValidRegisterRequest("ada@example.com"));

        await Assert.ThrowsAsync<EmailAlreadyExistsException>(() =>
            sut.RegisterAsync(ValidRegisterRequest("ADA@EXAMPLE.com")));
    }

    [Fact]
    public async Task RegisterAsync_hashes_the_password()
    {
        var sut = CreateSut();
        await sut.RegisterAsync(ValidRegisterRequest());

        var stored = Db.Users.Single(u => u.Email == "ada@example.com");
        Assert.NotEqual("Password1", stored.PasswordHash);

        var hasher = new PasswordHasher<User>();
        Assert.Equal(
            PasswordVerificationResult.Success,
            hasher.VerifyHashedPassword(stored, stored.PasswordHash, "Password1"));
    }

    [Theory]
    [InlineData("short1")] // too short
    [InlineData("alllettersnodigits")] // no digit
    [InlineData("12345678")] // no letter
    public async Task RegisterAsync_rejects_password_that_fails_the_rules(string invalidPassword)
    {
        var sut = CreateSut();
        var request = ValidRegisterRequest();
        request.Password = invalidPassword;

        await Assert.ThrowsAsync<InvalidPasswordException>(() => sut.RegisterAsync(request));
    }

    [Fact]
    public async Task LoginAsync_returns_a_token_for_correct_credentials()
    {
        var sut = CreateSut();
        await sut.RegisterAsync(ValidRegisterRequest());

        var response = await sut.LoginAsync(LoginRequestFor("ada@example.com", "Password1"));

        Assert.False(string.IsNullOrWhiteSpace(response.Token));
        Assert.Equal("Developer", response.Role);
    }

    [Fact]
    public async Task LoginAsync_sets_an_eight_hour_expiry()
    {
        var sut = CreateSut();
        await sut.RegisterAsync(ValidRegisterRequest());
        var before = DateTime.UtcNow;

        var response = await sut.LoginAsync(LoginRequestFor("ada@example.com", "Password1"));

        var expectedExpiry = before.AddHours(8);
        Assert.True(Math.Abs((response.ExpiresAt - expectedExpiry).TotalSeconds) < 5);
    }

    [Fact]
    public async Task LoginAsync_throws_the_same_generic_error_for_an_unknown_email()
    {
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            sut.LoginAsync(LoginRequestFor("nobody@example.com", "Password1")));

        Assert.Equal("Invalid email or password.", ex.Message);
    }

    [Fact]
    public async Task LoginAsync_throws_the_same_generic_error_for_a_wrong_password()
    {
        var sut = CreateSut();
        await sut.RegisterAsync(ValidRegisterRequest());

        var ex = await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            sut.LoginAsync(LoginRequestFor("ada@example.com", "WrongPassword1")));

        Assert.Equal("Invalid email or password.", ex.Message);
    }

    [Fact]
    public async Task LoginAsync_blocks_the_sixth_attempt_within_the_rate_limit_window()
    {
        var tracker = CreateTracker();
        var sut = CreateSut(tracker);
        await sut.RegisterAsync(ValidRegisterRequest());

        for (var i = 0; i < 5; i++)
        {
            await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
                sut.LoginAsync(LoginRequestFor("ada@example.com", "WrongPassword1")));
        }

        // Even the correct password is blocked once the rate limit trips (FR-019).
        var ex = await Assert.ThrowsAsync<TooManyAttemptsException>(() =>
            sut.LoginAsync(LoginRequestFor("ada@example.com", "Password1")));

        Assert.Equal("Too many attempts, try again later.", ex.Message);
    }

    [Fact]
    public async Task LoginAsync_clears_the_attempt_counter_on_a_successful_login()
    {
        var tracker = CreateTracker();
        var sut = CreateSut(tracker);
        await sut.RegisterAsync(ValidRegisterRequest());

        for (var i = 0; i < 4; i++)
        {
            await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
                sut.LoginAsync(LoginRequestFor("ada@example.com", "WrongPassword1")));
        }

        await sut.LoginAsync(LoginRequestFor("ada@example.com", "Password1"));

        // Should take 5 fresh failures to block again, not just one more.
        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            sut.LoginAsync(LoginRequestFor("ada@example.com", "WrongPassword1")));
    }
}
