using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Dtos;
using TaskFlow.Api.Tests.TestSupport;

namespace TaskFlow.Api.Tests.Integration;

// Extends Feature 001's Login_401_response_has_the_full_ProblemDetails_shape (AuthEndpointsTests)
// to Feature 002's Projects and Work Items endpoints — every error response across this feature
// must be a genuine RFC 7807 ProblemDetails body, not just the right status code.
public class ProblemDetailsShapeTests(TaskFlowApiFactory factory) : IClassFixture<TaskFlowApiFactory>
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

    private static void AssertFullProblemDetailsShape(ProblemDetails? problem, int expectedStatus)
    {
        Assert.NotNull(problem);
        Assert.False(string.IsNullOrWhiteSpace(problem!.Type));
        Assert.False(string.IsNullOrWhiteSpace(problem.Title));
        Assert.Equal(expectedStatus, problem.Status);
        Assert.False(string.IsNullOrWhiteSpace(problem.Detail));
    }

    // A 400 from a data-annotation failure (e.g. [Required]/[StringLength]) is
    // intercepted by [ApiController]'s automatic model validation before the action
    // body runs, and is a ValidationProblemDetails — Type/Title/Status are populated,
    // but per-field messages live in Errors rather than a single Detail string. This
    // is the documented contract (contracts/projects-api.md, contracts/work-items-api.md:
    // "ProblemDetails / ValidationProblemDetails"), not an inconsistency to fix.
    private static void AssertValidationProblemDetailsShape(ValidationProblemDetails? problem, int expectedStatus)
    {
        Assert.NotNull(problem);
        Assert.False(string.IsNullOrWhiteSpace(problem!.Type));
        Assert.False(string.IsNullOrWhiteSpace(problem.Title));
        Assert.Equal(expectedStatus, problem.Status);
        Assert.NotEmpty(problem.Errors);
    }

    [Fact]
    public async Task Projects_400_response_has_the_full_ValidationProblemDetails_shape()
    {
        var managerToken = await LoginAsSeededAdminAsync();

        var request = AuthedRequest(HttpMethod.Post, "/api/projects", managerToken);
        request.Content = JsonContent.Create(new { name = "ab" });
        var response = await _client.SendAsync(request);

        AssertValidationProblemDetailsShape(await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(), 400);
    }

    [Fact]
    public async Task Projects_403_response_has_a_ProblemDetails_body_not_an_empty_one()
    {
        var token = await RegisterAndGetTokenAsync($"problemdetails-403-{Guid.NewGuid():N}@example.com");

        var request = AuthedRequest(HttpMethod.Post, "/api/projects", token);
        request.Content = JsonContent.Create(new { name = $"Project {Guid.NewGuid():N}" });
        var response = await _client.SendAsync(request);

        // 403 Forbidden from [Authorize(Roles = ...)] is produced by ASP.NET Core's
        // authorization middleware itself — it never reaches a controller action's own
        // Problem(detail: ...) call, so there's no endpoint-specific message to put in
        // Detail (unlike the feature's own thrown business exceptions). Program.cs's
        // UseStatusCodePages() still converts this from a completely empty 403 into a
        // real ProblemDetails body with Type/Title/Status — the fix this test caught the
        // need for — so Detail is the one field this case doesn't populate.
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.False(string.IsNullOrWhiteSpace(problem!.Type));
        Assert.False(string.IsNullOrWhiteSpace(problem.Title));
        Assert.Equal(403, problem.Status);
    }

    [Fact]
    public async Task Projects_404_response_has_the_full_ProblemDetails_shape()
    {
        var token = await RegisterAndGetTokenAsync($"problemdetails-404-{Guid.NewGuid():N}@example.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/projects/999999", token));

        AssertFullProblemDetailsShape(await response.Content.ReadFromJsonAsync<ProblemDetails>(), 404);
    }

    [Fact]
    public async Task Projects_409_response_has_the_full_ProblemDetails_shape()
    {
        var managerToken = await LoginAsSeededAdminAsync();
        var name = $"Duplicate {Guid.NewGuid():N}";
        var first = AuthedRequest(HttpMethod.Post, "/api/projects", managerToken);
        first.Content = JsonContent.Create(new { name });
        await _client.SendAsync(first);

        var second = AuthedRequest(HttpMethod.Post, "/api/projects", managerToken);
        second.Content = JsonContent.Create(new { name });
        var response = await _client.SendAsync(second);

        AssertFullProblemDetailsShape(await response.Content.ReadFromJsonAsync<ProblemDetails>(), 409);
    }

    [Fact]
    public async Task WorkItems_400_response_has_the_full_ValidationProblemDetails_shape()
    {
        var managerToken = await LoginAsSeededAdminAsync();
        var createProject = AuthedRequest(HttpMethod.Post, "/api/projects", managerToken);
        createProject.Content = JsonContent.Create(new { name = $"Project {Guid.NewGuid():N}" });
        var projectResponse = await _client.SendAsync(createProject);
        var project = await projectResponse.Content.ReadFromJsonAsync<ProjectDetailDto>();

        var request = AuthedRequest(HttpMethod.Post, $"/api/projects/{project!.Id}/work-items", managerToken);
        request.Content = JsonContent.Create(new { type = "Task", title = "ab" });
        var response = await _client.SendAsync(request);

        AssertValidationProblemDetailsShape(await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(), 400);
    }

    // Unlike the annotation-driven 400 above, an unrecognized type/priority/status
    // enum value is rejected by WorkItemService itself (a business exception, not a
    // data-annotation failure) — so this 400 does carry a plain Detail message.
    [Fact]
    public async Task WorkItems_400_from_an_invalid_enum_value_has_a_Detail_message()
    {
        var managerToken = await LoginAsSeededAdminAsync();
        var createProject = AuthedRequest(HttpMethod.Post, "/api/projects", managerToken);
        createProject.Content = JsonContent.Create(new { name = $"Project {Guid.NewGuid():N}" });
        var projectResponse = await _client.SendAsync(createProject);
        var project = await projectResponse.Content.ReadFromJsonAsync<ProjectDetailDto>();

        var request = AuthedRequest(HttpMethod.Post, $"/api/projects/{project!.Id}/work-items", managerToken);
        request.Content = JsonContent.Create(new { type = "NotAType", title = "A valid title" });
        var response = await _client.SendAsync(request);

        AssertFullProblemDetailsShape(await response.Content.ReadFromJsonAsync<ProblemDetails>(), 400);
    }

    [Fact]
    public async Task WorkItems_403_response_has_the_full_ProblemDetails_shape()
    {
        var managerToken = await LoginAsSeededAdminAsync();
        var createProject = AuthedRequest(HttpMethod.Post, "/api/projects", managerToken);
        createProject.Content = JsonContent.Create(new { name = $"Project {Guid.NewGuid():N}" });
        var projectResponse = await _client.SendAsync(createProject);
        var project = await projectResponse.Content.ReadFromJsonAsync<ProjectDetailDto>();

        var creatorToken = await RegisterAndGetTokenAsync($"problemdetails-wi-creator-{Guid.NewGuid():N}@example.com");
        var createItem = AuthedRequest(HttpMethod.Post, $"/api/projects/{project!.Id}/work-items", creatorToken);
        createItem.Content = JsonContent.Create(new { type = "Task", title = "Some item" });
        var itemResponse = await _client.SendAsync(createItem);
        var item = await itemResponse.Content.ReadFromJsonAsync<WorkItemDto>();

        var strangerToken = await RegisterAndGetTokenAsync($"problemdetails-wi-stranger-{Guid.NewGuid():N}@example.com");
        var request = AuthedRequest(HttpMethod.Put, $"/api/work-items/{item!.Id}", strangerToken);
        request.Content = JsonContent.Create(new { type = "Task", title = "Should not apply" });
        var response = await _client.SendAsync(request);

        AssertFullProblemDetailsShape(await response.Content.ReadFromJsonAsync<ProblemDetails>(), 403);
    }

    [Fact]
    public async Task WorkItems_404_response_has_the_full_ProblemDetails_shape()
    {
        var token = await RegisterAndGetTokenAsync($"problemdetails-wi-404-{Guid.NewGuid():N}@example.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/work-items/999999", token));

        AssertFullProblemDetailsShape(await response.Content.ReadFromJsonAsync<ProblemDetails>(), 404);
    }

    // Feature 003 additions: hierarchy rule violations and the type-change guard are
    // thrown from WorkItemService (business exceptions), same shape as the enum-value
    // case above — a plain Detail message, not the Errors dictionary of an
    // annotation-driven ValidationProblemDetails.
    [Fact]
    public async Task WorkItems_400_from_a_hierarchy_rule_violation_has_a_Detail_message()
    {
        var managerToken = await LoginAsSeededAdminAsync();
        var createProject = AuthedRequest(HttpMethod.Post, "/api/projects", managerToken);
        createProject.Content = JsonContent.Create(new { name = $"Project {Guid.NewGuid():N}" });
        var projectResponse = await _client.SendAsync(createProject);
        var project = await projectResponse.Content.ReadFromJsonAsync<ProjectDetailDto>();

        var request = AuthedRequest(HttpMethod.Post, $"/api/projects/{project!.Id}/work-items", managerToken);
        request.Content = JsonContent.Create(new { type = "SubTask", title = "Missing its required parent" });
        var response = await _client.SendAsync(request);

        AssertFullProblemDetailsShape(await response.Content.ReadFromJsonAsync<ProblemDetails>(), 400);
    }

    [Fact]
    public async Task WorkItems_400_from_the_type_change_guard_has_a_Detail_message()
    {
        var managerToken = await LoginAsSeededAdminAsync();
        var createProject = AuthedRequest(HttpMethod.Post, "/api/projects", managerToken);
        createProject.Content = JsonContent.Create(new { name = $"Project {Guid.NewGuid():N}" });
        var projectResponse = await _client.SendAsync(createProject);
        var project = await projectResponse.Content.ReadFromJsonAsync<ProjectDetailDto>();

        var createTask = AuthedRequest(HttpMethod.Post, $"/api/projects/{project!.Id}/work-items", managerToken);
        createTask.Content = JsonContent.Create(new { type = "Task", title = "Has a subtask child" });
        var taskResponse = await _client.SendAsync(createTask);
        var task = await taskResponse.Content.ReadFromJsonAsync<WorkItemDto>();

        var createSubTask = AuthedRequest(HttpMethod.Post, $"/api/projects/{project.Id}/work-items", managerToken);
        createSubTask.Content = JsonContent.Create(new { type = "SubTask", title = "Child", parentWorkItemId = task!.Id });
        await _client.SendAsync(createSubTask);

        var request = AuthedRequest(HttpMethod.Put, $"/api/work-items/{task.Id}", managerToken);
        request.Content = JsonContent.Create(new { type = "Story", title = "Has a subtask child", parentWorkItemId = (int?)null });
        var response = await _client.SendAsync(request);

        AssertFullProblemDetailsShape(await response.Content.ReadFromJsonAsync<ProblemDetails>(), 400);
    }
}
