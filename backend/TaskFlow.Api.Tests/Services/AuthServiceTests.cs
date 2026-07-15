using Microsoft.AspNetCore.Identity;
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

    private AuthService CreateSut() => new(Db, BuildConfig());

    private static RegisterRequest ValidRegisterRequest(string email = "ada@example.com") => new()
    {
        FullName = "Ada Lovelace",
        Email = email,
        Password = "Password1"
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
}
