using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using TaskFlow.Api.Dtos;
using TaskFlow.Api.Tests.TestSupport;

namespace TaskFlow.Api.Tests.Integration;

public class AuthEndpointsTests(TaskFlowApiFactory factory) : IClassFixture<TaskFlowApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // Matches the Jwt:SigningKey/Issuer/Audience TaskFlowApiFactory configures for every
    // test in this class — needed to hand-build a token whose signature the app's JWT
    // bearer handler accepts, but with an `exp` already in the past.
    private static string BuildExpiredToken()
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes("integration-test-signing-key-at-least-32-bytes!!")),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "TaskFlow.Api.Tests",
            audience: "TaskFlow.Api.Tests",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(-1),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<string> RegisterAndGetTokenAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Grace Hopper",
            email,
            password = "Password1"
        });
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.Token;
    }

    [Fact]
    public async Task Register_returns_201_with_token_and_developer_role()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Grace Hopper",
            email = $"grace-{Guid.NewGuid():N}@example.com",
            password = "Password1"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
        Assert.Equal("Developer", body.Role);
    }

    [Fact]
    public async Task Register_returns_409_on_duplicate_email()
    {
        var email = $"dup-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register", new { fullName = "First User", email, password = "Password1" });

        var response = await _client.PostAsJsonAsync(
            "/api/auth/register", new { fullName = "Second User", email, password = "Password1" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("", "valid@example.com", "Password1")] // missing name
    [InlineData("Valid Name", "not-an-email", "Password1")] // invalid email
    [InlineData("Valid Name", "valid2@example.com", "short")] // invalid password
    public async Task Register_returns_400_on_invalid_input(string fullName, string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new { fullName, email, password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_returns_200_with_token_on_correct_credentials()
    {
        var email = $"login-ok-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register", new { fullName = "Grace Hopper", email, password = "Password1" });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "Password1" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
    }

    [Fact]
    public async Task Login_returns_401_with_generic_message_for_wrong_password()
    {
        var email = $"login-wrongpw-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register", new { fullName = "Grace Hopper", email, password = "Password1" });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongPassword1" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_returns_401_with_the_same_generic_message_for_an_unknown_email()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login", new { email = $"nobody-{Guid.NewGuid():N}@example.com", password = "Password1" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Invalid email or password.", problem!.Detail);
    }

    [Fact]
    public async Task Login_returns_429_after_five_failed_attempts_within_the_window()
    {
        var email = $"login-ratelimit-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register", new { fullName = "Grace Hopper", email, password = "Password1" });

        for (var i = 0; i < 5; i++)
        {
            await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongPassword1" });
        }

        // Even the correct password is refused once the rate limit trips (FR-019).
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "Password1" });

        Assert.Equal((HttpStatusCode)429, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Too many attempts, try again later.", problem!.Detail);
    }

    [Fact]
    public async Task Login_401_response_has_the_full_ProblemDetails_shape()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login", new { email = $"nobody-{Guid.NewGuid():N}@example.com", password = "Password1" });

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.False(string.IsNullOrWhiteSpace(problem!.Type));
        Assert.False(string.IsNullOrWhiteSpace(problem.Title));
        Assert.Equal(401, problem.Status);
        Assert.False(string.IsNullOrWhiteSpace(problem.Detail));
    }

    [Fact]
    public async Task Logout_returns_204_for_an_authenticated_caller()
    {
        var token = await RegisterAndGetTokenAsync($"logout-{Guid.NewGuid():N}@example.com");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Logout_returns_401_without_a_token()
    {
        var response = await _client.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_returns_200_with_name_and_role_for_a_valid_token()
    {
        var token = await RegisterAndGetTokenAsync($"me-ok-{Guid.NewGuid():N}@example.com");
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MeResponse>();
        Assert.Equal("Grace Hopper", body!.FullName);
        Assert.Equal("Developer", body.Role);
    }

    [Fact]
    public async Task Me_returns_401_without_a_token()
    {
        var response = await _client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_returns_401_for_an_expired_token()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", BuildExpiredToken());

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Feature 002 needs the frontend to compare "who am I" against a work item's
    // creator/assignee ids (research.md §8) — Register/Login/Me must all agree on the
    // same numeric id for that comparison to mean anything.
    [Fact]
    public async Task Register_Login_and_Me_all_return_the_same_caller_id()
    {
        var email = $"id-check-{Guid.NewGuid():N}@example.com";

        var registerResponse = await _client.PostAsJsonAsync(
            "/api/auth/register", new { fullName = "Grace Hopper", email, password = "Password1" });
        var registerBody = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "Password1" });
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();

        var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginBody!.Token);
        var meResponse = await _client.SendAsync(meRequest);
        var meBody = await meResponse.Content.ReadFromJsonAsync<MeResponse>();

        Assert.True(registerBody!.Id > 0);
        Assert.Equal(registerBody.Id, loginBody!.Id);
        Assert.Equal(registerBody.Id, meBody!.Id);
    }
}
