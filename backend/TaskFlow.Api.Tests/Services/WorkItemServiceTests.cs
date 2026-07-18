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

    private WorkItem AddWorkItem(
        int projectId, int creatorId, int? assigneeUserId = null,
        WorkItemStatus status = WorkItemStatus.ToDo, WorkItemPriority priority = WorkItemPriority.Medium,
        WorkItemType type = WorkItemType.Task, string title = "Some work", DateTime? updatedAt = null)
    {
        var workItem = new WorkItem
        {
            ProjectId = projectId,
            Type = type,
            Title = title,
            Status = status,
            Priority = priority,
            AssigneeUserId = assigneeUserId,
            CreatedByUserId = creatorId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = updatedAt ?? DateTime.UtcNow
        };
        Db.WorkItems.Add(workItem);
        Db.SaveChanges();
        return workItem;
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

    private static WorkItemRequest EditRequest(string title = "Updated title", string status = "Done") => new()
    {
        Type = "Task",
        Title = title,
        Status = status
    };

    [Theory]
    [InlineData(true, false, "Developer")] // creator
    [InlineData(false, true, "Developer")] // current assignee
    [InlineData(false, false, "Manager")]
    [InlineData(false, false, "Admin")]
    public async Task UpdateAsync_allows_creator_assignee_or_manager_or_admin(bool asCreator, bool asAssignee, string callerRole)
    {
        var creator = AddUser("creator@example.com");
        var assignee = AddUser("assignee@example.com");
        var caller = asCreator ? creator : asAssignee ? assignee : AddUser($"caller-{callerRole}@example.com", Enum.Parse<Role>(callerRole));
        var project = AddProject("Alpha", creator.Id);
        var item = AddWorkItem(project.Id, creator.Id, assigneeUserId: assignee.Id, updatedAt: DateTime.UtcNow.AddDays(-1));
        var sut = CreateSut();

        var result = await sut.UpdateAsync(caller.Id, callerRole, item.Id, EditRequest());

        Assert.Equal("Updated title", result.Title);
        Assert.Equal("Done", result.Status);
        Assert.True(result.UpdatedAt > item.CreatedAt);
    }

    [Fact]
    public async Task UpdateAsync_rejects_a_caller_who_is_neither_creator_assignee_nor_manager_or_admin()
    {
        var creator = AddUser("creator2@example.com");
        var stranger = AddUser("stranger@example.com");
        var project = AddProject("Alpha", creator.Id);
        var item = AddWorkItem(project.Id, creator.Id);
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotAuthorizedToEditWorkItemException>(
            () => sut.UpdateAsync(stranger.Id, "Developer", item.Id, EditRequest()));
    }

    [Fact]
    public async Task UpdateAsync_throws_for_an_unknown_work_item()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<WorkItemNotFoundException>(
            () => sut.UpdateAsync(1, "Admin", 999999, EditRequest()));
    }

    [Theory]
    [InlineData(true, "Developer")] // creator
    [InlineData(false, "Manager")]
    [InlineData(false, "Admin")]
    public async Task DeleteAsync_allows_creator_or_manager_or_admin(bool asCreator, string callerRole)
    {
        var creator = AddUser("creator3@example.com");
        var caller = asCreator ? creator : AddUser($"deleter-{callerRole}@example.com", Enum.Parse<Role>(callerRole));
        var project = AddProject("Alpha", creator.Id);
        var item = AddWorkItem(project.Id, creator.Id);
        var sut = CreateSut();

        await sut.DeleteAsync(caller.Id, callerRole, item.Id);

        Assert.Empty(Db.WorkItems);
    }

    [Fact]
    public async Task DeleteAsync_rejects_the_current_assignee_who_is_not_also_creator_manager_or_admin()
    {
        var creator = AddUser("creator4@example.com");
        var assignee = AddUser("assignee2@example.com");
        var project = AddProject("Alpha", creator.Id);
        var item = AddWorkItem(project.Id, creator.Id, assigneeUserId: assignee.Id);
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotAuthorizedToDeleteWorkItemException>(
            () => sut.DeleteAsync(assignee.Id, "Developer", item.Id));
    }

    [Fact]
    public async Task DeleteAsync_rejects_an_unrelated_caller()
    {
        var creator = AddUser("creator5@example.com");
        var stranger = AddUser("stranger2@example.com");
        var project = AddProject("Alpha", creator.Id);
        var item = AddWorkItem(project.Id, creator.Id);
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotAuthorizedToDeleteWorkItemException>(
            () => sut.DeleteAsync(stranger.Id, "Developer", item.Id));
    }

    [Fact]
    public async Task DeleteAsync_throws_for_an_unknown_work_item()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<WorkItemNotFoundException>(() => sut.DeleteAsync(1, "Admin", 999999));
    }

    [Fact]
    public async Task GetWorkItemsAsync_returns_the_paginated_shape_sorted_by_updated_at_descending()
    {
        var user = AddUser("lister@example.com");
        var project = AddProject("Alpha", user.Id);
        AddWorkItem(project.Id, user.Id, title: "Oldest", updatedAt: DateTime.UtcNow.AddMinutes(-2));
        AddWorkItem(project.Id, user.Id, title: "Newest", updatedAt: DateTime.UtcNow);
        AddWorkItem(project.Id, user.Id, title: "Middle", updatedAt: DateTime.UtcNow.AddMinutes(-1));
        var sut = CreateSut();

        var result = await sut.GetWorkItemsAsync(project.Id, page: 1, pageSize: 20, status: null, type: null, priority: null, assigneeUserId: null, search: null);

        Assert.Equal(3, result.TotalCount);
        Assert.Equal("Newest", result.Items[0].Title);
        Assert.Equal("Middle", result.Items[1].Title);
        Assert.Equal("Oldest", result.Items[2].Title);
    }

    [Fact]
    public async Task GetWorkItemsAsync_filters_by_status_type_priority_and_assignee_individually_and_combined()
    {
        var user = AddUser("filterer@example.com");
        var assignee = AddUser("assignee3@example.com");
        var project = AddProject("Alpha", user.Id);
        var match = AddWorkItem(project.Id, user.Id, assigneeUserId: assignee.Id, status: WorkItemStatus.InProgress, priority: WorkItemPriority.High, type: WorkItemType.Story, title: "Match");
        AddWorkItem(project.Id, user.Id, status: WorkItemStatus.ToDo, priority: WorkItemPriority.Low, type: WorkItemType.Task, title: "NoMatch");
        var sut = CreateSut();

        var byStatus = await sut.GetWorkItemsAsync(project.Id, 1, 20, "InProgress", null, null, null, null);
        var byType = await sut.GetWorkItemsAsync(project.Id, 1, 20, null, "Story", null, null, null);
        var byPriority = await sut.GetWorkItemsAsync(project.Id, 1, 20, null, null, "High", null, null);
        var byAssignee = await sut.GetWorkItemsAsync(project.Id, 1, 20, null, null, null, assignee.Id, null);
        var combined = await sut.GetWorkItemsAsync(project.Id, 1, 20, "InProgress", "Story", "High", assignee.Id, null);

        Assert.Equal(match.Id, byStatus.Items.Single().Id);
        Assert.Equal(match.Id, byType.Items.Single().Id);
        Assert.Equal(match.Id, byPriority.Items.Single().Id);
        Assert.Equal(match.Id, byAssignee.Items.Single().Id);
        Assert.Equal(match.Id, combined.Items.Single().Id);
    }

    [Fact]
    public async Task GetWorkItemsAsync_search_is_a_case_insensitive_title_substring_match()
    {
        var user = AddUser("searcher@example.com");
        var project = AddProject("Alpha", user.Id);
        AddWorkItem(project.Id, user.Id, title: "Fix the LOGIN bug");
        AddWorkItem(project.Id, user.Id, title: "Unrelated item");
        var sut = CreateSut();

        var result = await sut.GetWorkItemsAsync(project.Id, 1, 20, null, null, null, null, search: "login");

        Assert.Equal("Fix the LOGIN bug", result.Items.Single().Title);
    }

    [Fact]
    public async Task GetWorkItemsAsync_clamps_a_pageSize_beyond_100_rather_than_rejecting_it()
    {
        var user = AddUser("clamper@example.com");
        var project = AddProject("Alpha", user.Id);
        for (var i = 0; i < 5; i++)
        {
            AddWorkItem(project.Id, user.Id, title: $"Item {i}");
        }
        var sut = CreateSut();

        var result = await sut.GetWorkItemsAsync(project.Id, 1, pageSize: 500, null, null, null, null, null);

        Assert.Equal(100, result.PageSize);
    }

    [Fact]
    public async Task GetWorkItemsAsync_throws_for_an_unknown_project()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<ProjectNotFoundException>(
            () => sut.GetWorkItemsAsync(999999, 1, 20, null, null, null, null, null));
    }

    [Fact]
    public async Task GetWorkItemsAsync_throws_for_an_unparseable_status_filter()
    {
        var user = AddUser("badfilter@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();

        await Assert.ThrowsAsync<InvalidWorkItemStatusException>(
            () => sut.GetWorkItemsAsync(project.Id, 1, 20, "NotAStatus", null, null, null, null));
    }
}
