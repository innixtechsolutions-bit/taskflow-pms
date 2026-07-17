using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TaskFlow.Api.Dtos;
using TaskFlow.Api.Tests.TestSupport;

namespace TaskFlow.Api.Tests.Integration;

public class WorkItemsEndpointsTests(TaskFlowApiFactory factory) : IClassFixture<TaskFlowApiFactory>
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

    // The seeded Admin holds [Authorize(Roles = "Manager,Admin")] access, so it's used
    // directly here to create fixture projects rather than promoting a second user.
    private async Task<int> CreateProjectAsync(string adminToken, string name)
    {
        var request = AuthedRequest(HttpMethod.Post, "/api/projects", adminToken);
        request.Content = JsonContent.Create(new { name });
        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<ProjectDetailDto>();
        return body!.Id;
    }

    [Fact]
    public async Task Create_returns_201_on_success()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var token = await RegisterAndGetTokenAsync($"creator-{Guid.NewGuid():N}@example.com");

        var request = AuthedRequest(HttpMethod.Post, $"/api/projects/{projectId}/work-items", token);
        request.Content = JsonContent.Create(new { type = "Task", title = "Fix the login bug" });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WorkItemDto>();
        Assert.Equal("Fix the login bug", body!.Title);
        Assert.Equal("Medium", body.Priority);
        Assert.Equal("ToDo", body.Status);
    }

    [Fact]
    public async Task Create_returns_400_for_an_invalid_title()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var token = await RegisterAndGetTokenAsync($"creator-invalid-{Guid.NewGuid():N}@example.com");

        var request = AuthedRequest(HttpMethod.Post, $"/api/projects/{projectId}/work-items", token);
        request.Content = JsonContent.Create(new { type = "Task", title = "ab" });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_returns_400_for_an_unknown_assignee()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var token = await RegisterAndGetTokenAsync($"creator-badassignee-{Guid.NewGuid():N}@example.com");

        var request = AuthedRequest(HttpMethod.Post, $"/api/projects/{projectId}/work-items", token);
        request.Content = JsonContent.Create(new { type = "Task", title = "Fix the login bug", assigneeUserId = 999999 });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_returns_404_for_an_unknown_project()
    {
        var token = await RegisterAndGetTokenAsync($"creator-noproject-{Guid.NewGuid():N}@example.com");

        var request = AuthedRequest(HttpMethod.Post, "/api/projects/999999/work-items", token);
        request.Content = JsonContent.Create(new { type = "Task", title = "Fix the login bug" });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_returns_401_without_a_token()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/work-items", new { type = "Task", title = "Fix the login bug" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
