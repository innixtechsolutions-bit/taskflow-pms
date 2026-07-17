using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TaskFlow.Api.Dtos;
using TaskFlow.Api.Tests.TestSupport;

namespace TaskFlow.Api.Tests.Integration;

public class ProjectsEndpointsTests(TaskFlowApiFactory factory) : IClassFixture<TaskFlowApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<string> LoginAsSeededAdminAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login", new { email = "admin@taskflow.local", password = "IntegrationTest!Admin1" });
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.Token;
    }

    private async Task<string> RegisterAndGetTokenAsync(string email)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/register", new { fullName = "Regular User", email, password = "Password1" });
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.Token;
    }

    private static HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task<int> FindUserIdByEmailAsync(string adminToken, string email)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/users?page=1&pageSize=200", adminToken));
        var body = await response.Content.ReadFromJsonAsync<PagedResult<UserListItemDto>>();
        return body!.Items.Single(u => u.Email == email).Id;
    }

    // Registers a fresh user, promotes them to Manager as the seeded Admin, then logs
    // in again — a role change only takes effect on the *next* login (Feature 001,
    // FR-015), since the JWT's role claim is baked in at issuance.
    private async Task<string> RegisterManagerAndGetTokenAsync(string email)
    {
        await RegisterAndGetTokenAsync(email);
        var adminToken = await LoginAsSeededAdminAsync();
        var userId = await FindUserIdByEmailAsync(adminToken, email);

        var promote = AuthedRequest(HttpMethod.Put, $"/api/users/{userId}/role", adminToken);
        promote.Content = JsonContent.Create(new { role = "Manager" });
        await _client.SendAsync(promote);

        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "Password1" });
        var body = await login.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.Token;
    }

    [Fact]
    public async Task Create_returns_201_for_a_manager_caller()
    {
        var managerToken = await RegisterManagerAndGetTokenAsync($"manager-create-{Guid.NewGuid():N}@example.com");
        var name = $"Website Redesign {Guid.NewGuid():N}";

        var request = AuthedRequest(HttpMethod.Post, "/api/projects", managerToken);
        request.Content = JsonContent.Create(new { name, description = "Rebuild the site" });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProjectDetailDto>();
        Assert.Equal(name, body!.Name);
        Assert.Equal(0, body.TotalWorkItemCount);
    }

    [Fact]
    public async Task Create_returns_409_on_a_duplicate_name()
    {
        var managerToken = await RegisterManagerAndGetTokenAsync($"manager-dup-{Guid.NewGuid():N}@example.com");
        var name = $"Duplicate Project {Guid.NewGuid():N}";
        var first = AuthedRequest(HttpMethod.Post, "/api/projects", managerToken);
        first.Content = JsonContent.Create(new { name });
        await _client.SendAsync(first);

        var second = AuthedRequest(HttpMethod.Post, "/api/projects", managerToken);
        second.Content = JsonContent.Create(new { name = name.ToUpperInvariant() });
        var response = await _client.SendAsync(second);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("ab")] // too short
    [InlineData("")] // required
    public async Task Create_returns_400_on_invalid_name(string invalidName)
    {
        var managerToken = await RegisterManagerAndGetTokenAsync($"manager-invalid-{Guid.NewGuid():N}@example.com");

        var request = AuthedRequest(HttpMethod.Post, "/api/projects", managerToken);
        request.Content = JsonContent.Create(new { name = invalidName });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_returns_403_for_a_developer_caller()
    {
        var token = await RegisterAndGetTokenAsync($"developer-create-{Guid.NewGuid():N}@example.com");

        var request = AuthedRequest(HttpMethod.Post, "/api/projects", token);
        request.Content = JsonContent.Create(new { name = "Some Project" });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
