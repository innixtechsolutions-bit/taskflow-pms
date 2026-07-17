using TaskFlow.Api.Data.Entities;
using TaskFlow.Api.Dtos;
using TaskFlow.Api.Services;
using TaskFlow.Api.Tests.TestSupport;

namespace TaskFlow.Api.Tests.Services;

public class ProjectServiceTests : SqlServerTestDatabase
{
    private ProjectService CreateSut() => new(Db);

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

    private Project AddProject(string name, int creatorId, DateTime? createdAt = null)
    {
        var project = new Project
        {
            Name = name,
            CreatedByUserId = creatorId,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
        Db.Projects.Add(project);
        Db.SaveChanges();
        return project;
    }

    private void AddWorkItem(int projectId, int creatorId, WorkItemStatus status = WorkItemStatus.ToDo)
    {
        Db.WorkItems.Add(new WorkItem
        {
            ProjectId = projectId,
            Type = WorkItemType.Task,
            Title = "Some work",
            Status = status,
            CreatedByUserId = creatorId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        Db.SaveChanges();
    }

    private static ProjectRequest ValidRequest(string name = "Website Redesign") => new()
    {
        Name = name,
        Description = "Rebuild the marketing site"
    };

    [Fact]
    public async Task CreateAsync_creates_a_project_recording_creator_and_timestamp()
    {
        var manager = AddUser("manager@example.com", Role.Manager);
        var sut = CreateSut();
        var before = DateTime.UtcNow;

        var result = await sut.CreateAsync(manager.Id, ValidRequest());

        Assert.Equal("Website Redesign", result.Name);
        Assert.Equal("Test User", result.CreatedByName);
        Assert.True(result.CreatedAt >= before);
        Assert.Equal(0, result.TotalWorkItemCount);
        var stored = Db.Projects.Single();
        Assert.Equal(manager.Id, stored.CreatedByUserId);
    }

    [Fact]
    public async Task CreateAsync_rejects_a_duplicate_name_case_insensitively()
    {
        var manager = AddUser("manager@example.com", Role.Manager);
        var sut = CreateSut();
        await sut.CreateAsync(manager.Id, ValidRequest("Website Redesign"));

        var ex = await Assert.ThrowsAsync<DuplicateProjectNameException>(() =>
            sut.CreateAsync(manager.Id, ValidRequest("website redesign")));

        Assert.Equal("A project with this name already exists.", ex.Message);
    }

    [Fact]
    public async Task GetProjectsAsync_returns_the_paginated_shape_sorted_newest_first()
    {
        var user = AddUser("creator@example.com");
        AddProject("Alpha", user.Id, DateTime.UtcNow.AddMinutes(-2));
        AddProject("Beta", user.Id, DateTime.UtcNow.AddMinutes(-1));
        AddProject("Gamma", user.Id, DateTime.UtcNow);
        var sut = CreateSut();

        var result = await sut.GetProjectsAsync(page: 1, pageSize: 2);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal("Gamma", result.Items[0].Name);
        Assert.Equal("Beta", result.Items[1].Name);
    }

    [Fact]
    public async Task GetProjectsAsync_open_work_item_count_excludes_done_items()
    {
        var user = AddUser("creator@example.com");
        var project = AddProject("Alpha", user.Id);
        AddWorkItem(project.Id, user.Id, WorkItemStatus.ToDo);
        AddWorkItem(project.Id, user.Id, WorkItemStatus.InProgress);
        AddWorkItem(project.Id, user.Id, WorkItemStatus.Done);
        var sut = CreateSut();

        var result = await sut.GetProjectsAsync(page: 1, pageSize: 20);

        Assert.Equal(2, result.Items.Single().OpenWorkItemCount);
    }

    [Fact]
    public async Task GetProjectByIdAsync_returns_the_total_work_item_count_regardless_of_status()
    {
        var user = AddUser("creator@example.com");
        var project = AddProject("Alpha", user.Id);
        AddWorkItem(project.Id, user.Id, WorkItemStatus.ToDo);
        AddWorkItem(project.Id, user.Id, WorkItemStatus.Done);
        var sut = CreateSut();

        var result = await sut.GetProjectByIdAsync(project.Id);

        Assert.Equal(2, result.TotalWorkItemCount);
    }

    [Fact]
    public async Task GetProjectByIdAsync_throws_for_an_unknown_id()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<ProjectNotFoundException>(() => sut.GetProjectByIdAsync(999999));
    }
}
