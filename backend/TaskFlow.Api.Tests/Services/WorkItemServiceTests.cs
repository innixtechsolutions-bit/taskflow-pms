using TaskFlow.Api.Data.Entities;
using TaskFlow.Api.Dtos;
using TaskFlow.Api.Services;
using TaskFlow.Api.Tests.TestSupport;

namespace TaskFlow.Api.Tests.Services;

public class WorkItemServiceTests : SqlServerTestDatabase
{
    private WorkItemService CreateSut() => new(Db);

    private User AddUser(string email, Role role = Role.Developer)
    {
        var user = new User
        {
            FullName = "Test User",
            Email = email,
            PasswordHash = "hash",
            Role = role,
            CreatedAt = DateTime.UtcNow
        };
        Db.Users.Add(user);
        Db.SaveChanges();
        return user;
    }

    private Project AddProject(string name, int creatorId)
    {
        var project = new Project
        {
            Name = name,
            CreatedByUserId = creatorId,
            CreatedAt = DateTime.UtcNow
        };
        Db.Projects.Add(project);
        Db.SaveChanges();
        return project;
    }

    private static WorkItemRequest ValidRequest(string title = "Fix the login bug") => new()
    {
        Type = "Task",
        Title = title
    };

    [Fact]
    public async Task CreateAsync_applies_default_priority_and_status_when_omitted()
    {
        var user = AddUser("creator@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();

        var result = await sut.CreateAsync(user.Id, project.Id, ValidRequest());

        Assert.Equal("Medium", result.Priority);
        Assert.Equal("ToDo", result.Status);
    }

    [Fact]
    public async Task CreateAsync_records_creator_project_and_timestamps()
    {
        var user = AddUser("creator@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();
        var before = DateTime.UtcNow;

        var result = await sut.CreateAsync(user.Id, project.Id, ValidRequest());

        Assert.Equal(project.Id, result.ProjectId);
        Assert.Equal(user.Id, result.CreatedByUserId);
        Assert.Equal("Test User", result.CreatedByName);
        Assert.True(result.CreatedAt >= before);
        Assert.Equal(result.CreatedAt, result.UpdatedAt);
    }

    [Fact]
    public async Task CreateAsync_accepts_a_due_date_in_the_past()
    {
        var user = AddUser("creator@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();
        var request = ValidRequest();
        request.DueDate = DateTime.UtcNow.AddYears(-1);

        var result = await sut.CreateAsync(user.Id, project.Id, request);

        Assert.Equal(request.DueDate, result.DueDate);
    }

    [Fact]
    public async Task CreateAsync_rejects_an_assignee_that_is_not_an_existing_user()
    {
        var user = AddUser("creator@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();
        var request = ValidRequest();
        request.AssigneeUserId = 999999;

        await Assert.ThrowsAsync<AssigneeNotFoundException>(() => sut.CreateAsync(user.Id, project.Id, request));
    }

    [Fact]
    public async Task CreateAsync_throws_for_an_unknown_project()
    {
        var user = AddUser("creator@example.com");
        var sut = CreateSut();

        await Assert.ThrowsAsync<ProjectNotFoundException>(() => sut.CreateAsync(user.Id, 999999, ValidRequest()));
    }

    [Fact]
    public async Task CreateAsync_rejects_an_invalid_type()
    {
        var user = AddUser("creator@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();
        var request = ValidRequest();
        request.Type = "NotAType";

        await Assert.ThrowsAsync<InvalidWorkItemTypeException>(() => sut.CreateAsync(user.Id, project.Id, request));
    }
}
