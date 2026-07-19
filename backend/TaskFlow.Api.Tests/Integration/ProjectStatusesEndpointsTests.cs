using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TaskFlow.Api.Dtos;
using TaskFlow.Api.Tests.TestSupport;

namespace TaskFlow.Api.Tests.Integration;

public class ProjectStatusesEndpointsTests(TaskFlowApiFactory factory) : IClassFixture<TaskFlowApiFactory>
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

    private async Task<int> CreateProjectAsync(string adminToken, string name)
    {
        var request = AuthedRequest(HttpMethod.Post, "/api/projects", adminToken);
        request.Content = JsonContent.Create(new { name });
        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<ProjectDetailDto>();
        return body!.Id;
    }

    [Fact]
    public async Task GetStatuses_returns_200_with_the_standard_four_in_position_order_for_any_authenticated_role()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        // Feature 006/FR-008 (analyze triage) — the read endpoint is intentionally
        // open to any authenticated user, not just Manager/Admin, since the board,
        // dropdowns, and filters all depend on it (FR-020).
        var developerToken = await RegisterAndGetTokenAsync($"statuses-developer-{Guid.NewGuid():N}@example.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/projects/{projectId}/statuses", developerToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<WorkflowStatusDto>>();
        Assert.Equal(
            new[] { ("To Do", "Open"), ("In Progress", "Open"), ("In Review", "Open"), ("Done", "Done") },
            body!.Select(s => (s.Name, s.Category)).ToArray());
    }

    [Fact]
    public async Task GetStatuses_returns_404_for_an_unknown_project()
    {
        var token = await RegisterAndGetTokenAsync($"statuses-noproject-{Guid.NewGuid():N}@example.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/projects/999999/statuses", token));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetStatuses_returns_401_without_a_token()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");

        var response = await _client.GetAsync($"/api/projects/{projectId}/statuses");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
