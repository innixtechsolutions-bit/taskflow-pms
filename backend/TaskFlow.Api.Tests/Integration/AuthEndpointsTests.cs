using System.Net;
using System.Net.Http.Json;
using TaskFlow.Api.Dtos;
using TaskFlow.Api.Tests.TestSupport;

namespace TaskFlow.Api.Tests.Integration;

public class AuthEndpointsTests(TaskFlowApiFactory factory) : IClassFixture<TaskFlowApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

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
}
