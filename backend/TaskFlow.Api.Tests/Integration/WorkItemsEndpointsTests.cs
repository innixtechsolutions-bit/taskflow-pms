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

    private async Task<int> FindUserIdByEmailAsync(string adminToken, string email)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/users?page=1&pageSize=200", adminToken));
        var body = await response.Content.ReadFromJsonAsync<PagedResult<UserListItemDto>>();
        return body!.Items.Single(u => u.Email == email).Id;
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

    private async Task<int> CreateItemOfTypeAsync(string token, int projectId, string type, int? parentWorkItemId = null, string title = "Some item")
    {
        var request = AuthedRequest(HttpMethod.Post, $"/api/projects/{projectId}/work-items", token);
        request.Content = JsonContent.Create(new { type, title, parentWorkItemId });
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<WorkItemDto>();
        return body!.Id;
    }

    [Fact]
    public async Task Create_a_full_four_level_chain_succeeds_with_correct_parents()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var token = await RegisterAndGetTokenAsync($"chain-{Guid.NewGuid():N}@example.com");

        var epicId = await CreateItemOfTypeAsync(token, projectId, "Epic", title: "Epic");
        var storyId = await CreateItemOfTypeAsync(token, projectId, "Story", epicId, "Story");
        var taskId = await CreateItemOfTypeAsync(token, projectId, "Task", storyId, "Task");
        var subTaskId = await CreateItemOfTypeAsync(token, projectId, "SubTask", taskId, "SubTask");

        Assert.True(epicId > 0);
        Assert.True(storyId > 0);
        Assert.True(taskId > 0);
        Assert.True(subTaskId > 0);
    }

    [Fact]
    public async Task Create_returns_400_when_an_epic_is_given_a_parent()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var token = await RegisterAndGetTokenAsync($"badepic-{Guid.NewGuid():N}@example.com");
        var otherEpicId = await CreateItemOfTypeAsync(token, projectId, "Epic", title: "Other epic");

        var request = AuthedRequest(HttpMethod.Post, $"/api/projects/{projectId}/work-items", token);
        request.Content = JsonContent.Create(new { type = "Epic", title = "Bad epic", parentWorkItemId = otherEpicId });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_returns_400_when_a_subtask_has_no_parent()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var token = await RegisterAndGetTokenAsync($"nosubtaskparent-{Guid.NewGuid():N}@example.com");

        var request = AuthedRequest(HttpMethod.Post, $"/api/projects/{projectId}/work-items", token);
        request.Content = JsonContent.Create(new { type = "SubTask", title = "Bad subtask" });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_returns_400_for_a_wrong_type_parent()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var token = await RegisterAndGetTokenAsync($"wrongtype-{Guid.NewGuid():N}@example.com");
        var epicId = await CreateItemOfTypeAsync(token, projectId, "Epic", title: "Epic");

        var request = AuthedRequest(HttpMethod.Post, $"/api/projects/{projectId}/work-items", token);
        request.Content = JsonContent.Create(new { type = "SubTask", title = "Bad subtask", parentWorkItemId = epicId });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_returns_400_for_a_cross_project_parent()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var otherProjectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var token = await RegisterAndGetTokenAsync($"crossproject-{Guid.NewGuid():N}@example.com");
        var epicInOtherProjectId = await CreateItemOfTypeAsync(token, otherProjectId, "Epic", title: "Other project epic");

        var request = AuthedRequest(HttpMethod.Post, $"/api/projects/{projectId}/work-items", token);
        request.Content = JsonContent.Create(new { type = "Story", title = "Bad story", parentWorkItemId = epicInOtherProjectId });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetParentCandidates_returns_200_with_correct_candidates()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var token = await RegisterAndGetTokenAsync($"candidates-{Guid.NewGuid():N}@example.com");
        var epicId = await CreateItemOfTypeAsync(token, projectId, "Epic", title: "Epic");

        var response = await _client.SendAsync(AuthedRequest(
            HttpMethod.Get, $"/api/projects/{projectId}/work-items/parent-candidates?type=Story", token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WorkItemParentCandidatesResponse>();
        var candidate = Assert.Single(body!.Candidates);
        Assert.Equal(epicId, candidate.Id);
    }

    [Fact]
    public async Task GetParentCandidates_returns_200_with_an_empty_array_for_epic()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var token = await RegisterAndGetTokenAsync($"candidates-epic-{Guid.NewGuid():N}@example.com");

        var response = await _client.SendAsync(AuthedRequest(
            HttpMethod.Get, $"/api/projects/{projectId}/work-items/parent-candidates?type=Epic", token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WorkItemParentCandidatesResponse>();
        Assert.Empty(body!.Candidates);
    }

    [Fact]
    public async Task GetParentCandidates_returns_400_for_an_invalid_type()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var token = await RegisterAndGetTokenAsync($"candidates-badtype-{Guid.NewGuid():N}@example.com");

        var response = await _client.SendAsync(AuthedRequest(
            HttpMethod.Get, $"/api/projects/{projectId}/work-items/parent-candidates?type=NotAType", token));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetParentCandidates_returns_404_for_an_unknown_project()
    {
        var token = await RegisterAndGetTokenAsync($"candidates-noproject-{Guid.NewGuid():N}@example.com");

        var response = await _client.SendAsync(AuthedRequest(
            HttpMethod.Get, "/api/projects/999999/work-items/parent-candidates?type=Story", token));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetParentCandidates_returns_401_without_a_token()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");

        var response = await _client.GetAsync($"/api/projects/{projectId}/work-items/parent-candidates?type=Story");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetTree_returns_200_with_a_correctly_nested_shape()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var token = await RegisterAndGetTokenAsync($"tree-{Guid.NewGuid():N}@example.com");
        var epicId = await CreateItemOfTypeAsync(token, projectId, "Epic", title: "Epic");
        var storyId = await CreateItemOfTypeAsync(token, projectId, "Story", epicId, "Story");

        var response = await _client.SendAsync(AuthedRequest(
            HttpMethod.Get, $"/api/projects/{projectId}/work-items/tree", token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<WorkItemTreeNodeDto>>();
        var epicNode = Assert.Single(body!);
        Assert.Equal(epicId, epicNode.Id);
        var storyNode = Assert.Single(epicNode.Children);
        Assert.Equal(storyId, storyNode.Id);
    }

    [Fact]
    public async Task GetTree_returns_404_for_an_unknown_project()
    {
        var token = await RegisterAndGetTokenAsync($"tree-noproject-{Guid.NewGuid():N}@example.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/projects/999999/work-items/tree", token));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetTree_returns_401_without_a_token()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");

        var response = await _client.GetAsync($"/api/projects/{projectId}/work-items/tree");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetBoard_returns_200_with_columns_and_items()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var token = await RegisterAndGetTokenAsync($"board-{Guid.NewGuid():N}@example.com");
        var itemId = await CreateItemOfTypeAsync(token, projectId, "Task", title: "Board item");

        var response = await _client.SendAsync(AuthedRequest(
            HttpMethod.Get, $"/api/projects/{projectId}/work-items/board", token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WorkItemBoardDto>();
        Assert.Equal(4, body!.Columns.Count);
        // M1: each column carries its display label, not just the raw status value.
        Assert.Contains(body.Columns, c => c.Status == "InReview" && c.Label == "In Review");
        Assert.Contains(body.Items, i => i.Id == itemId);
    }

    [Fact]
    public async Task GetBoard_returns_404_for_an_unknown_project()
    {
        var token = await RegisterAndGetTokenAsync($"board-noproject-{Guid.NewGuid():N}@example.com");

        var response = await _client.SendAsync(AuthedRequest(
            HttpMethod.Get, "/api/projects/999999/work-items/board", token));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBoard_returns_401_without_a_token()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");

        var response = await _client.GetAsync($"/api/projects/{projectId}/work-items/board");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_returns_the_enriched_detail_shape()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var token = await RegisterAndGetTokenAsync($"detail-{Guid.NewGuid():N}@example.com");
        var epicId = await CreateItemOfTypeAsync(token, projectId, "Epic", title: "Epic");
        var storyId = await CreateItemOfTypeAsync(token, projectId, "Story", epicId, "Story");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/work-items/{storyId}", token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WorkItemDetailDto>();
        Assert.Equal(epicId, body!.ParentWorkItemId);
        Assert.Equal("Epic", body.ParentTitle);
        Assert.Empty(body.Children);
        Assert.Equal(0, body.TotalDescendantCount);
    }

    [Fact]
    public async Task Update_reparents_a_task_to_a_different_story()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var token = await RegisterAndGetTokenAsync($"reparent-{Guid.NewGuid():N}@example.com");
        var epicId = await CreateItemOfTypeAsync(token, projectId, "Epic", title: "Epic");
        var newStoryId = await CreateItemOfTypeAsync(token, projectId, "Story", epicId, "New story");
        var taskId = await CreateItemOfTypeAsync(token, projectId, "Task", title: "Task");

        var request = AuthedRequest(HttpMethod.Put, $"/api/work-items/{taskId}", token);
        request.Content = JsonContent.Create(new { type = "Task", title = "Task", parentWorkItemId = newStoryId });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WorkItemDto>();
        Assert.Equal(newStoryId, body!.ParentWorkItemId);
    }

    [Fact]
    public async Task Update_returns_400_when_reparenting_to_the_wrong_type()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var token = await RegisterAndGetTokenAsync($"reparent-badtype-{Guid.NewGuid():N}@example.com");
        var epicId = await CreateItemOfTypeAsync(token, projectId, "Epic", title: "Epic");
        var taskId = await CreateItemOfTypeAsync(token, projectId, "Task", title: "Task");

        var request = AuthedRequest(HttpMethod.Put, $"/api/work-items/{taskId}", token);
        request.Content = JsonContent.Create(new { type = "Task", title = "Task", parentWorkItemId = epicId });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_returns_400_when_setting_an_item_as_its_own_parent()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var token = await RegisterAndGetTokenAsync($"selfparent-{Guid.NewGuid():N}@example.com");
        var taskId = await CreateItemOfTypeAsync(token, projectId, "Task", title: "Task");

        var request = AuthedRequest(HttpMethod.Put, $"/api/work-items/{taskId}", token);
        request.Content = JsonContent.Create(new { type = "Task", title = "Task", parentWorkItemId = taskId });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_returns_400_for_a_type_change_that_invalidates_existing_children()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var token = await RegisterAndGetTokenAsync($"typechange-{Guid.NewGuid():N}@example.com");
        var epicId = await CreateItemOfTypeAsync(token, projectId, "Epic", title: "Epic");
        var storyId = await CreateItemOfTypeAsync(token, projectId, "Story", epicId, "Story");
        var taskId = await CreateItemOfTypeAsync(token, projectId, "Task", storyId, "Task");
        await CreateItemOfTypeAsync(token, projectId, "SubTask", taskId, "SubTask");

        var request = AuthedRequest(HttpMethod.Put, $"/api/work-items/{taskId}", token);
        request.Content = JsonContent.Create(new { type = "Story", title = "Task", parentWorkItemId = epicId });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delete_removes_the_entire_subtree()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var token = await RegisterAndGetTokenAsync($"cascadedelete-{Guid.NewGuid():N}@example.com");
        var epicId = await CreateItemOfTypeAsync(token, projectId, "Epic", title: "Epic");
        var storyId = await CreateItemOfTypeAsync(token, projectId, "Story", epicId, "Story");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/work-items/{epicId}", token));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var storyResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/work-items/{storyId}", token));
        Assert.Equal(HttpStatusCode.NotFound, storyResponse.StatusCode);
    }

    private async Task<WorkItemDto> CreateWorkItemAsync(string token, int projectId, string title = "Fix the login bug", int? assigneeUserId = null)
    {
        var request = AuthedRequest(HttpMethod.Post, $"/api/projects/{projectId}/work-items", token);
        request.Content = JsonContent.Create(new { type = "Task", title, assigneeUserId });
        var response = await _client.SendAsync(request);
        return (await response.Content.ReadFromJsonAsync<WorkItemDto>())!;
    }

    [Fact]
    public async Task Update_returns_200_for_the_creator()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var creatorToken = await RegisterAndGetTokenAsync($"editor-creator-{Guid.NewGuid():N}@example.com");
        var item = await CreateWorkItemAsync(creatorToken, projectId);

        var request = AuthedRequest(HttpMethod.Put, $"/api/work-items/{item.Id}", creatorToken);
        request.Content = JsonContent.Create(new { type = "Task", title = "Updated title", status = "Done" });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WorkItemDto>();
        Assert.Equal("Updated title", body!.Title);
        Assert.Equal("Done", body.Status);
    }

    [Fact]
    public async Task Update_returns_200_for_the_current_assignee()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var creatorToken = await RegisterAndGetTokenAsync($"editor-creator2-{Guid.NewGuid():N}@example.com");
        var assigneeEmail = $"editor-assignee-{Guid.NewGuid():N}@example.com";
        var assigneeToken = await RegisterAndGetTokenAsync(assigneeEmail);
        var assigneeId = await FindUserIdByEmailAsync(adminToken, assigneeEmail);
        var item = await CreateWorkItemAsync(creatorToken, projectId, assigneeUserId: assigneeId);

        var request = AuthedRequest(HttpMethod.Put, $"/api/work-items/{item.Id}", assigneeToken);
        request.Content = JsonContent.Create(new { type = "Task", title = "Updated by assignee" });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_returns_403_for_an_unrelated_caller()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var creatorToken = await RegisterAndGetTokenAsync($"editor-creator3-{Guid.NewGuid():N}@example.com");
        var strangerToken = await RegisterAndGetTokenAsync($"editor-stranger-{Guid.NewGuid():N}@example.com");
        var item = await CreateWorkItemAsync(creatorToken, projectId);

        var request = AuthedRequest(HttpMethod.Put, $"/api/work-items/{item.Id}", strangerToken);
        request.Content = JsonContent.Create(new { type = "Task", title = "Should not apply" });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_returns_400_for_invalid_input()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var creatorToken = await RegisterAndGetTokenAsync($"editor-creator4-{Guid.NewGuid():N}@example.com");
        var item = await CreateWorkItemAsync(creatorToken, projectId);

        var request = AuthedRequest(HttpMethod.Put, $"/api/work-items/{item.Id}", creatorToken);
        request.Content = JsonContent.Create(new { type = "Task", title = "ab" });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_returns_404_for_an_unknown_work_item()
    {
        var adminToken = await LoginAsSeededAdminAsync();

        var request = AuthedRequest(HttpMethod.Put, "/api/work-items/999999", adminToken);
        request.Content = JsonContent.Create(new { type = "Task", title = "Fix the login bug" });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_returns_200_with_the_full_item_for_any_authenticated_caller()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var creatorToken = await RegisterAndGetTokenAsync($"getter-creator-{Guid.NewGuid():N}@example.com");
        var viewerToken = await RegisterAndGetTokenAsync($"getter-viewer-{Guid.NewGuid():N}@example.com");
        var item = await CreateWorkItemAsync(creatorToken, projectId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/work-items/{item.Id}", viewerToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WorkItemDto>();
        Assert.Equal(item.Id, body!.Id);
    }

    [Fact]
    public async Task Get_returns_404_for_an_unknown_work_item()
    {
        var token = await RegisterAndGetTokenAsync($"getter-unknown-{Guid.NewGuid():N}@example.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/work-items/999999", token));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_returns_204_for_the_creator()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var creatorToken = await RegisterAndGetTokenAsync($"deleter-creator-{Guid.NewGuid():N}@example.com");
        var item = await CreateWorkItemAsync(creatorToken, projectId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/work-items/{item.Id}", creatorToken));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_returns_204_for_a_manager_or_admin()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var creatorToken = await RegisterAndGetTokenAsync($"deleter-creator2-{Guid.NewGuid():N}@example.com");
        var item = await CreateWorkItemAsync(creatorToken, projectId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/work-items/{item.Id}", adminToken));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_returns_403_for_the_assignee_alone()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var creatorToken = await RegisterAndGetTokenAsync($"deleter-creator3-{Guid.NewGuid():N}@example.com");
        var assigneeEmail = $"deleter-assignee-{Guid.NewGuid():N}@example.com";
        var assigneeToken = await RegisterAndGetTokenAsync(assigneeEmail);
        var assigneeId = await FindUserIdByEmailAsync(adminToken, assigneeEmail);
        var item = await CreateWorkItemAsync(creatorToken, projectId, assigneeUserId: assigneeId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/work-items/{item.Id}", assigneeToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_returns_403_for_an_unrelated_caller()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var creatorToken = await RegisterAndGetTokenAsync($"deleter-creator4-{Guid.NewGuid():N}@example.com");
        var strangerToken = await RegisterAndGetTokenAsync($"deleter-stranger-{Guid.NewGuid():N}@example.com");
        var item = await CreateWorkItemAsync(creatorToken, projectId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/work-items/{item.Id}", strangerToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_returns_404_for_an_unknown_work_item()
    {
        var adminToken = await LoginAsSeededAdminAsync();

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, "/api/work-items/999999", adminToken));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetWorkItems_returns_200_with_filters_search_and_pagination()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var creatorToken = await RegisterAndGetTokenAsync($"lister-creator-{Guid.NewGuid():N}@example.com");
        await CreateWorkItemAsync(creatorToken, projectId, title: "Fix the login bug");
        await CreateWorkItemAsync(creatorToken, projectId, title: "Unrelated item");

        var response = await _client.SendAsync(AuthedRequest(
            HttpMethod.Get, $"/api/projects/{projectId}/work-items?search=login", creatorToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<WorkItemDto>>();
        Assert.Equal("Fix the login bug", body!.Items.Single().Title);
    }

    [Fact]
    public async Task GetWorkItems_filters_by_the_InReview_status()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var token = await RegisterAndGetTokenAsync($"lister-inreview-{Guid.NewGuid():N}@example.com");
        var item = await CreateWorkItemAsync(token, projectId, title: "Needs review");
        var editRequest = AuthedRequest(HttpMethod.Put, $"/api/work-items/{item.Id}", token);
        editRequest.Content = JsonContent.Create(new { type = "Task", title = item.Title, status = "InReview" });
        await _client.SendAsync(editRequest);
        await CreateWorkItemAsync(token, projectId, title: "Still in progress");

        var response = await _client.SendAsync(AuthedRequest(
            HttpMethod.Get, $"/api/projects/{projectId}/work-items?status=InReview", token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<WorkItemDto>>();
        Assert.Equal("Needs review", body!.Items.Single().Title);
    }

    [Fact]
    public async Task GetWorkItems_returns_404_for_an_unknown_project()
    {
        var token = await RegisterAndGetTokenAsync($"lister-noproject-{Guid.NewGuid():N}@example.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/projects/999999/work-items", token));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetWorkItems_returns_400_for_an_unparseable_status_filter()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var token = await RegisterAndGetTokenAsync($"lister-badfilter-{Guid.NewGuid():N}@example.com");

        var response = await _client.SendAsync(AuthedRequest(
            HttpMethod.Get, $"/api/projects/{projectId}/work-items?status=NotAStatus", token));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // User Story 5 (non-regression): the flat list endpoint is untouched by this
    // feature — confirms search still matches a child item by title even though its
    // parent's title doesn't match, exactly as Feature 002 specified.
    [Fact]
    public async Task GetWorkItems_search_matches_a_child_item_even_when_its_parent_does_not()
    {
        var adminToken = await LoginAsSeededAdminAsync();
        var projectId = await CreateProjectAsync(adminToken, $"Project {Guid.NewGuid():N}");
        var token = await RegisterAndGetTokenAsync($"regression-search-{Guid.NewGuid():N}@example.com");
        var epicId = await CreateItemOfTypeAsync(token, projectId, "Epic", title: "Unrelated epic title");
        await CreateItemOfTypeAsync(token, projectId, "Story", epicId, "Findable story title");

        var response = await _client.SendAsync(AuthedRequest(
            HttpMethod.Get, $"/api/projects/{projectId}/work-items?search=Findable", token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<WorkItemDto>>();
        Assert.Equal("Findable story title", body!.Items.Single().Title);
    }
}
