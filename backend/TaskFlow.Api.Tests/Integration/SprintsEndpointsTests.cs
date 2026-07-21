using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TaskFlow.Api.Dtos;
using TaskFlow.Api.Tests.TestSupport;

namespace TaskFlow.Api.Tests.Integration;

public class SprintsEndpointsTests(TaskFlowApiFactory factory) : IClassFixture<TaskFlowApiFactory>
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

    private async Task<SprintDto> CreateSprintAsync(string adminToken, int projectId, string name, int startOffsetDays = 0, int endOffsetDays = 14)
    {
        var request = AuthedRequest(HttpMethod.Post, $"/api/projects/{projectId}/sprints", adminToken);
        request.Content = JsonContent.Create(new
        {
            name,
            startDate = DateTime.UtcNow.Date.AddDays(startOffsetDays),
            endDate = DateTime.UtcNow.Date.AddDays(endOffsetDays)
        });
        var response = await _client.SendAsync(request);
        return (await response.Content.ReadFromJsonAsync<SprintDto>())!;
    }

    [Fact]
    public async Task CreateSprint_returns_201_for_a_Manager_or_Admin()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");

        var request = AuthedRequest(HttpMethod.Post, $"/api/projects/{projectId}/sprints", adminToken);
        request.Content = JsonContent.Create(new
        {
            name = "Sprint 1",
            startDate = DateTime.UtcNow.Date,
            endDate = DateTime.UtcNow.Date.AddDays(14)
        });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SprintDto>();
        Assert.Equal("Sprint 1", body!.Name);
        Assert.Equal("Planned", body.Status);
        Assert.Equal(0, body.ItemCount);
    }

    [Fact]
    public async Task CreateSprint_returns_403_for_a_Developer()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var developerToken = await RegisterAndGetTokenAsync($"create-sprint-dev-{Guid.NewGuid():N}@example.com");

        var request = AuthedRequest(HttpMethod.Post, $"/api/projects/{projectId}/sprints", developerToken);
        request.Content = JsonContent.Create(new
        {
            name = "Sprint 1",
            startDate = DateTime.UtcNow.Date,
            endDate = DateTime.UtcNow.Date.AddDays(14)
        });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateSprint_returns_400_for_a_name_outside_2_to_50_characters()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");

        var request = AuthedRequest(HttpMethod.Post, $"/api/projects/{projectId}/sprints", adminToken);
        request.Content = JsonContent.Create(new { name = "A", startDate = DateTime.UtcNow.Date, endDate = DateTime.UtcNow.Date.AddDays(14) });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateSprint_returns_400_for_an_end_date_on_or_before_the_start_date()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");

        var request = AuthedRequest(HttpMethod.Post, $"/api/projects/{projectId}/sprints", adminToken);
        request.Content = JsonContent.Create(new { name = "Sprint 1", startDate = DateTime.UtcNow.Date, endDate = DateTime.UtcNow.Date });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateSprint_returns_409_for_a_duplicate_name_in_the_same_project()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        await CreateSprintAsync(adminToken, projectId, "Sprint 1");

        var request = AuthedRequest(HttpMethod.Post, $"/api/projects/{projectId}/sprints", adminToken);
        request.Content = JsonContent.Create(new { name = "sprint 1", startDate = DateTime.UtcNow.Date, endDate = DateTime.UtcNow.Date.AddDays(14) });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateSprint_returns_404_for_an_unknown_project()
    {
        var adminToken = await LoginAsSeededAdminAsync();

        var request = AuthedRequest(HttpMethod.Post, "/api/projects/999999/sprints", adminToken);
        request.Content = JsonContent.Create(new { name = "Sprint 1", startDate = DateTime.UtcNow.Date, endDate = DateTime.UtcNow.Date.AddDays(14) });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSprints_returns_200_soonest_first_for_any_authenticated_role()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        await CreateSprintAsync(adminToken, projectId, "Sprint 2", startOffsetDays: 20, endOffsetDays: 34);
        await CreateSprintAsync(adminToken, projectId, "Sprint 1", startOffsetDays: 0, endOffsetDays: 14);
        // Feature 006/FR-008-style pattern (analyze triage) — the read endpoint is
        // intentionally open to any authenticated user, not just Manager/Admin.
        var developerToken = await RegisterAndGetTokenAsync($"sprints-developer-{Guid.NewGuid():N}@example.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/projects/{projectId}/sprints", developerToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<SprintDto>>();
        Assert.Equal(new[] { "Sprint 1", "Sprint 2" }, body!.Select(s => s.Name));
    }

    [Fact]
    public async Task GetSprints_returns_404_for_an_unknown_project()
    {
        var token = await RegisterAndGetTokenAsync($"sprints-noproject-{Guid.NewGuid():N}@example.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/projects/999999/sprints", token));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSprints_returns_401_without_a_token()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");

        var response = await _client.GetAsync($"/api/projects/{projectId}/sprints");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
