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

    // Feature 005 regression guard: In Review is not Done, so it must already be
    // counted as open under the existing "!= Done" definition (research.md #7) —
    // this test exists to prove that stays true, not because the production code
    // needs a code change for it.
    [Fact]
    public async Task GetProjectsAsync_open_work_item_count_includes_InReview_items()
    {
        var user = AddUser("creator@example.com");
        var project = AddProject("Alpha", user.Id);
        AddWorkItem(project.Id, user.Id, WorkItemStatus.ToDo);
        AddWorkItem(project.Id, user.Id, WorkItemStatus.InReview);
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

    [Fact]
    public async Task UpdateAsync_updates_the_name_and_description()
    {
        var manager = AddUser("manager2@example.com", Role.Manager);
        var project = AddProject("Old Name", manager.Id);
        var sut = CreateSut();

        var result = await sut.UpdateAsync(project.Id, new ProjectRequest { Name = "New Name", Description = "New description" });

        Assert.Equal("New Name", result.Name);
        Assert.Equal("New description", result.Description);
    }

    [Fact]
    public async Task UpdateAsync_allows_keeping_its_own_unchanged_name()
    {
        var manager = AddUser("manager3@example.com", Role.Manager);
        var project = AddProject("Same Name", manager.Id);
        var sut = CreateSut();

        var result = await sut.UpdateAsync(project.Id, new ProjectRequest { Name = "Same Name" });

        Assert.Equal("Same Name", result.Name);
    }

    [Fact]
    public async Task UpdateAsync_rejects_a_name_that_duplicates_a_different_project()
    {
        var manager = AddUser("manager4@example.com", Role.Manager);
        AddProject("Taken Name", manager.Id);
        var project = AddProject("Other Name", manager.Id);
        var sut = CreateSut();

        await Assert.ThrowsAsync<DuplicateProjectNameException>(
            () => sut.UpdateAsync(project.Id, new ProjectRequest { Name = "Taken Name" }));
    }

    [Fact]
    public async Task UpdateAsync_throws_for_an_unknown_id()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<ProjectNotFoundException>(
            () => sut.UpdateAsync(999999, new ProjectRequest { Name = "Whatever" }));
    }

    [Fact]
    public async Task DeleteAsync_removes_the_project_and_cascades_to_its_work_items()
    {
        var manager = AddUser("manager5@example.com", Role.Manager);
        var project = AddProject("Doomed", manager.Id);
        AddWorkItem(project.Id, manager.Id);
        AddWorkItem(project.Id, manager.Id);
        var sut = CreateSut();

        await sut.DeleteAsync(project.Id);

        Assert.Empty(Db.Projects);
        Assert.Empty(Db.WorkItems);
    }

    [Fact]
    public async Task DeleteAsync_throws_for_an_unknown_id()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<ProjectNotFoundException>(() => sut.DeleteAsync(999999));
    }
}
