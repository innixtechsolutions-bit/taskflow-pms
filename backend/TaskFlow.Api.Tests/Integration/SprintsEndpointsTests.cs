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

    // ---------------------------------------------------------------------
    // US4 — Start / Complete / Delete
    // ---------------------------------------------------------------------

    private async Task<int> CreateItemInSprintAsync(string token, int projectId, int sprintId, string title = "Some item")
    {
        var request = AuthedRequest(HttpMethod.Post, $"/api/projects/{projectId}/work-items", token);
        request.Content = JsonContent.Create(new { type = "Task", title, sprintId });
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<WorkItemDto>();
        return body!.Id;
    }

    [Fact]
    public async Task StartSprint_returns_200_for_a_Manager_or_Admin_and_403_for_a_Developer()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var token = await RegisterAndGetTokenAsync($"start-{Guid.NewGuid():N}@example.com");
        var sprintId = (await CreateSprintAsync(adminToken, projectId, "Sprint 1")).Id;
        await CreateItemInSprintAsync(token, projectId, sprintId);

        var denied = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/projects/{projectId}/sprints/{sprintId}/start", token));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var allowed = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/projects/{projectId}/sprints/{sprintId}/start", adminToken));
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        var body = await allowed.Content.ReadFromJsonAsync<SprintDto>();
        Assert.Equal("Active", body!.Status);
    }

    [Fact]
    public async Task StartSprint_returns_409_when_another_sprint_is_already_Active()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var token = await RegisterAndGetTokenAsync($"start-conflict-{Guid.NewGuid():N}@example.com");
        var firstSprintId = (await CreateSprintAsync(adminToken, projectId, "Sprint 1")).Id;
        await CreateItemInSprintAsync(token, projectId, firstSprintId);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/projects/{projectId}/sprints/{firstSprintId}/start", adminToken));
        var secondSprintId = (await CreateSprintAsync(adminToken, projectId, "Sprint 2", startOffsetDays: 20, endOffsetDays: 34)).Id;
        await CreateItemInSprintAsync(token, projectId, secondSprintId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/projects/{projectId}/sprints/{secondSprintId}/start", adminToken));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task StartSprint_returns_400_for_an_empty_sprint()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var sprintId = (await CreateSprintAsync(adminToken, projectId, "Sprint 1")).Id;

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/projects/{projectId}/sprints/{sprintId}/start", adminToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CompleteSprint_returns_200_for_a_Manager_or_Admin_and_403_for_a_Developer()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var token = await RegisterAndGetTokenAsync($"complete-{Guid.NewGuid():N}@example.com");
        var sprintId = (await CreateSprintAsync(adminToken, projectId, "Sprint 1")).Id;
        var itemId = await CreateItemInSprintAsync(token, projectId, sprintId);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/projects/{projectId}/sprints/{sprintId}/start", adminToken));
        var doneStatusId = await FindStatusIdAsync(token, projectId, "Done");
        await _client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, $"/api/work-items/{itemId}/status")
        {
            Content = JsonContent.Create(new { statusId = doneStatusId }),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) }
        });

        var denied = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/projects/{projectId}/sprints/{sprintId}/complete", token));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var request = AuthedRequest(HttpMethod.Put, $"/api/projects/{projectId}/sprints/{sprintId}/complete", adminToken);
        request.Content = JsonContent.Create(new { });
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SprintDto>();
        Assert.Equal("Completed", body!.Status);
    }

    private async Task<int> FindStatusIdAsync(string token, int projectId, string statusName)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/projects/{projectId}/statuses", token));
        var body = await response.Content.ReadFromJsonAsync<List<WorkflowStatusDto>>();
        return body!.Single(s => s.Name == statusName).Id;
    }

    [Fact]
    public async Task CompleteSprint_returns_400_with_itemCount_when_a_resolution_is_required_but_missing()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var token = await RegisterAndGetTokenAsync($"complete-needsdest-{Guid.NewGuid():N}@example.com");
        var sprintId = (await CreateSprintAsync(adminToken, projectId, "Sprint 1")).Id;
        await CreateItemInSprintAsync(token, projectId, sprintId);
        await CreateItemInSprintAsync(token, projectId, sprintId, "Second item");
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/projects/{projectId}/sprints/{sprintId}/start", adminToken));

        var request = AuthedRequest(HttpMethod.Put, $"/api/projects/{projectId}/sprints/{sprintId}/complete", adminToken);
        request.Content = JsonContent.Create(new { });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(2, problem.GetProperty("itemCount").GetInt32());
    }

    [Fact]
    public async Task DeleteSprint_returns_204_for_a_Manager_or_Admin_and_403_for_a_Developer()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var token = await RegisterAndGetTokenAsync($"delete-{Guid.NewGuid():N}@example.com");
        var sprintId = (await CreateSprintAsync(adminToken, projectId, "Sprint 1")).Id;

        var denied = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/projects/{projectId}/sprints/{sprintId}", token));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var allowed = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/projects/{projectId}/sprints/{sprintId}", adminToken));
        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
    }

    [Fact]
    public async Task DeleteSprint_returns_400_for_a_sprint_that_has_items()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var token = await RegisterAndGetTokenAsync($"delete-hasitems-{Guid.NewGuid():N}@example.com");
        var sprintId = (await CreateSprintAsync(adminToken, projectId, "Sprint 1")).Id;
        await CreateItemInSprintAsync(token, projectId, sprintId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/projects/{projectId}/sprints/{sprintId}", adminToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
