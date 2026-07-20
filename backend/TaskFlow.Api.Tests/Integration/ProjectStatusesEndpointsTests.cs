using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TaskFlow.Api.Dtos;
using TaskFlow.Api.Tests.TestSupport;
using System.Linq;

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

    [Fact]
    public async Task CreateStatus_returns_201_for_a_Manager_or_Admin()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");

        var request = AuthedRequest(HttpMethod.Post, $"/api/projects/{projectId}/statuses", adminToken);
        request.Content = JsonContent.Create(new { name = "QA", category = "Open" });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WorkflowStatusDto>();
        Assert.Equal("QA", body!.Name);
        Assert.Equal(0, body.ItemCount);
    }

    [Fact]
    public async Task CreateStatus_returns_403_for_a_Developer()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var developerToken = await RegisterAndGetTokenAsync($"create-status-dev-{Guid.NewGuid():N}@example.com");

        var request = AuthedRequest(HttpMethod.Post, $"/api/projects/{projectId}/statuses", developerToken);
        request.Content = JsonContent.Create(new { name = "QA", category = "Open" });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateStatus_returns_400_for_a_name_outside_2_to_30_characters()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");

        var request = AuthedRequest(HttpMethod.Post, $"/api/projects/{projectId}/statuses", adminToken);
        request.Content = JsonContent.Create(new { name = "A", category = "Open" });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateStatus_returns_400_for_an_invalid_category()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");

        var request = AuthedRequest(HttpMethod.Post, $"/api/projects/{projectId}/statuses", adminToken);
        request.Content = JsonContent.Create(new { name = "QA", category = "Bogus" });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateStatus_returns_409_for_a_duplicate_name()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");

        var request = AuthedRequest(HttpMethod.Post, $"/api/projects/{projectId}/statuses", adminToken);
        request.Content = JsonContent.Create(new { name = "to do", category = "Open" });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateStatus_returns_409_when_the_project_already_has_10_statuses()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        for (var i = 0; i < 6; i++)
        {
            var addRequest = AuthedRequest(HttpMethod.Post, $"/api/projects/{projectId}/statuses", adminToken);
            addRequest.Content = JsonContent.Create(new { name = $"Extra {i}", category = "Open" });
            await _client.SendAsync(addRequest);
        }

        var request = AuthedRequest(HttpMethod.Post, $"/api/projects/{projectId}/statuses", adminToken);
        request.Content = JsonContent.Create(new { name = "One Too Many", category = "Open" });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateStatus_returns_404_for_an_unknown_project()
    {
        var adminToken = await LoginAsSeededAdminAsync();

        var request = AuthedRequest(HttpMethod.Post, "/api/projects/999999/statuses", adminToken);
        request.Content = JsonContent.Create(new { name = "QA", category = "Open" });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<int> CreateStatusAsync(string adminToken, int projectId, string name, string category = "Open")
    {
        var request = AuthedRequest(HttpMethod.Post, $"/api/projects/{projectId}/statuses", adminToken);
        request.Content = JsonContent.Create(new { name, category });
        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<WorkflowStatusDto>();
        return body!.Id;
    }

    [Fact]
    public async Task UpdateStatus_returns_200_for_a_Manager_or_Admin()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var statusId = await CreateStatusAsync(adminToken, projectId, "QA");

        var request = AuthedRequest(HttpMethod.Put, $"/api/projects/{projectId}/statuses/{statusId}", adminToken);
        request.Content = JsonContent.Create(new { name = "Quality Assurance", colorKey = "Amber" });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WorkflowStatusDto>();
        Assert.Equal("Quality Assurance", body!.Name);
        Assert.Equal("Amber", body.ColorKey);
    }

    [Fact]
    public async Task UpdateStatus_returns_403_for_a_Developer()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var statusId = await CreateStatusAsync(adminToken, projectId, "QA");
        var developerToken = await RegisterAndGetTokenAsync($"update-status-dev-{Guid.NewGuid():N}@example.com");

        var request = AuthedRequest(HttpMethod.Put, $"/api/projects/{projectId}/statuses/{statusId}", developerToken);
        request.Content = JsonContent.Create(new { name = "Quality Assurance" });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_returns_400_for_a_name_outside_2_to_30_characters()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var statusId = await CreateStatusAsync(adminToken, projectId, "QA");

        var request = AuthedRequest(HttpMethod.Put, $"/api/projects/{projectId}/statuses/{statusId}", adminToken);
        request.Content = JsonContent.Create(new { name = "A" });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_returns_404_for_a_statusId_that_does_not_belong_to_the_project()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectAId = await CreateProjectAsync(adminToken, $"Project A {Guid.NewGuid():N}");
        var projectBId = await CreateProjectAsync(adminToken, $"Project B {Guid.NewGuid():N}");
        var statusInB = await CreateStatusAsync(adminToken, projectBId, "QA");

        var request = AuthedRequest(HttpMethod.Put, $"/api/projects/{projectAId}/statuses/{statusInB}", adminToken);
        request.Content = JsonContent.Create(new { name = "Hijack" });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_returns_409_for_a_duplicate_name()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var statusId = await CreateStatusAsync(adminToken, projectId, "QA");

        var request = AuthedRequest(HttpMethod.Put, $"/api/projects/{projectId}/statuses/{statusId}", adminToken);
        request.Content = JsonContent.Create(new { name = "to do" });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Reorder_returns_200_with_the_new_order_for_a_Manager_or_Admin()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var getResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/projects/{projectId}/statuses", adminToken));
        var current = await getResponse.Content.ReadFromJsonAsync<List<WorkflowStatusDto>>();
        var reversedIds = current!.OrderByDescending(s => s.Position).Select(s => s.Id).ToList();

        var request = AuthedRequest(HttpMethod.Put, $"/api/projects/{projectId}/statuses/reorder", adminToken);
        request.Content = JsonContent.Create(new { orderedStatusIds = reversedIds });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<WorkflowStatusDto>>();
        Assert.Equal(reversedIds, body!.OrderBy(s => s.Position).Select(s => s.Id).ToList());
    }

    [Fact]
    public async Task Reorder_returns_403_for_a_Developer()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var getResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/projects/{projectId}/statuses", adminToken));
        var current = await getResponse.Content.ReadFromJsonAsync<List<WorkflowStatusDto>>();
        var developerToken = await RegisterAndGetTokenAsync($"reorder-dev-{Guid.NewGuid():N}@example.com");

        var request = AuthedRequest(HttpMethod.Put, $"/api/projects/{projectId}/statuses/reorder", developerToken);
        request.Content = JsonContent.Create(new { orderedStatusIds = current!.Select(s => s.Id).ToList() });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Reorder_returns_400_for_a_non_permutation()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");

        var request = AuthedRequest(HttpMethod.Put, $"/api/projects/{projectId}/statuses/reorder", adminToken);
        request.Content = JsonContent.Create(new { orderedStatusIds = new[] { 999999 } });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reorder_returns_404_for_an_unknown_project()
    {
        var adminToken = await LoginAsSeededAdminAsync();

        var request = AuthedRequest(HttpMethod.Put, "/api/projects/999999/statuses/reorder", adminToken);
        request.Content = JsonContent.Create(new { orderedStatusIds = Array.Empty<int>() });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteStatus_returns_204_for_an_empty_status()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var statusId = await CreateStatusAsync(adminToken, projectId, "QA");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/projects/{projectId}/statuses/{statusId}", adminToken));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteStatus_returns_403_for_a_Developer()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var statusId = await CreateStatusAsync(adminToken, projectId, "QA");
        var developerToken = await RegisterAndGetTokenAsync($"delete-status-dev-{Guid.NewGuid():N}@example.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/projects/{projectId}/statuses/{statusId}", developerToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteStatus_returns_400_with_the_item_count_when_the_status_has_items_and_no_destination_is_given()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var getResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/projects/{projectId}/statuses", adminToken));
        var current = await getResponse.Content.ReadFromJsonAsync<List<WorkflowStatusDto>>();
        var toDoId = current!.Single(s => s.Name == "To Do").Id;
        var createItemRequest = AuthedRequest(HttpMethod.Post, $"/api/projects/{projectId}/work-items", adminToken);
        createItemRequest.Content = JsonContent.Create(new { type = "Task", title = "Some work", statusId = toDoId });
        await _client.SendAsync(createItemRequest);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/projects/{projectId}/statuses/{toDoId}", adminToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"itemCount\":1", body);
    }

    [Fact]
    public async Task DeleteStatus_returns_400_when_deleting_the_last_Open_category_status()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var getResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/projects/{projectId}/statuses", adminToken));
        var current = await getResponse.Content.ReadFromJsonAsync<List<WorkflowStatusDto>>();
        foreach (var status in current!.Where(s => s.Category == "Open" && s.Name != "To Do"))
        {
            await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/projects/{projectId}/statuses/{status.Id}", adminToken));
        }
        var toDoId = current.Single(s => s.Name == "To Do").Id;

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/projects/{projectId}/statuses/{toDoId}", adminToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteStatus_returns_204_and_moves_items_when_a_valid_destination_is_given()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var getResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/projects/{projectId}/statuses", adminToken));
        var current = await getResponse.Content.ReadFromJsonAsync<List<WorkflowStatusDto>>();
        var toDoId = current!.Single(s => s.Name == "To Do").Id;
        var inProgressId = current.Single(s => s.Name == "In Progress").Id;
        var createItemRequest = AuthedRequest(HttpMethod.Post, $"/api/projects/{projectId}/work-items", adminToken);
        createItemRequest.Content = JsonContent.Create(new { type = "Task", title = "Some work", statusId = toDoId });
        await _client.SendAsync(createItemRequest);

        var response = await _client.SendAsync(
            AuthedRequest(HttpMethod.Delete, $"/api/projects/{projectId}/statuses/{toDoId}?destinationStatusId={inProgressId}", adminToken));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteStatus_returns_404_for_an_unknown_project()
    {
        var adminToken = await LoginAsSeededAdminAsync();

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, "/api/projects/999999/statuses/1", adminToken));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
