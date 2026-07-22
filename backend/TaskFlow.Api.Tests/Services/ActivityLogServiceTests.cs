using TaskFlow.Api.Data.Entities;
using TaskFlow.Api.Services;
using TaskFlow.Api.Tests.TestSupport;

namespace TaskFlow.Api.Tests.Services;

public class ActivityLogServiceTests : SqlServerTestDatabase
{
    private ActivityLogService CreateSut() => new(Db);

    private User AddUser(string email) => AddEntity(new User
    {
        FullName = "Test User", Email = email, PasswordHash = "hash", CreatedAt = DateTime.UtcNow
    });

    private Project AddProject(string name, int creatorId) =>
        AddEntity(new Project { Name = name, CreatedByUserId = creatorId, CreatedAt = DateTime.UtcNow });

    private WorkItem AddWorkItem(int projectId, int creatorId, string title = "Some work")
    {
        var status = AddEntity(new WorkflowStatus
        {
            ProjectId = projectId, Name = $"Status {Guid.NewGuid():N}", Position = 0,
            Category = WorkflowStatusCategory.Open, ColorKey = ChipColor.Slate
        });
        return AddEntity(new WorkItem
        {
            ProjectId = projectId, Type = WorkItemType.Task, Title = title, WorkflowStatusId = status.Id,
            CreatedByUserId = creatorId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
    }

    // Directly seeds a row (bypassing RecordCreated/RecordFieldChange) so the
    // read-path tests below can control CreatedAt precisely, the same reason
    // WorkItemServiceTests' own ordering tests pass an explicit updatedAt rather
    // than relying on DateTime.UtcNow's ordering between two rapid calls.
    private ActivityLogEntry AddActivityEntry(
        int projectId, int workItemId, int actorUserId, string title = "Some work", string type = "Task",
        ActivityEventType eventType = ActivityEventType.Created, ActivityField? field = null,
        string? oldValue = null, string? newValue = null, DateTime? createdAt = null) => AddEntity(new ActivityLogEntry
        {
            ProjectId = projectId,
            WorkItemId = workItemId,
            WorkItemTitle = title,
            WorkItemType = type,
            ActorUserId = actorUserId,
            EventType = eventType,
            Field = field,
            OldValue = oldValue,
            NewValue = newValue,
            CreatedAt = createdAt ?? DateTime.UtcNow
        });

    private T AddEntity<T>(T entity) where T : class
    {
        Db.Add(entity);
        Db.SaveChanges();
        return entity;
    }

    [Fact]
    public void RecordCreated_adds_a_row_with_the_correct_event_type_and_snapshot()
    {
        var user = AddUser("record-created@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();

        sut.RecordCreated(project.Id, 42, "Fix the login bug", "Task", user.Id);
        Db.SaveChanges();

        var entry = Db.ActivityLogEntries.Single();
        Assert.Equal(project.Id, entry.ProjectId);
        Assert.Equal(42, entry.WorkItemId);
        Assert.Equal("Fix the login bug", entry.WorkItemTitle);
        Assert.Equal("Task", entry.WorkItemType);
        Assert.Equal(user.Id, entry.ActorUserId);
        Assert.Equal(ActivityEventType.Created, entry.EventType);
        Assert.Null(entry.Field);
        Assert.Null(entry.OldValue);
        Assert.Null(entry.NewValue);
    }

    [Fact]
    public void RecordFieldChange_adds_a_row_with_the_field_old_and_new_values()
    {
        var user = AddUser("record-fieldchange@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();

        sut.RecordFieldChange(project.Id, 42, "Fix the login bug", "Task", user.Id, ActivityField.Status, "To Do", "In Progress");
        Db.SaveChanges();

        var entry = Db.ActivityLogEntries.Single();
        Assert.Equal(ActivityEventType.FieldChanged, entry.EventType);
        Assert.Equal(ActivityField.Status, entry.Field);
        Assert.Equal("To Do", entry.OldValue);
        Assert.Equal("In Progress", entry.NewValue);
    }

    [Fact]
    public async Task GetProjectFeedAsync_returns_entries_newest_first_scoped_to_the_project()
    {
        var user = AddUser("feed-order@example.com");
        var projectA = AddProject("Alpha", user.Id);
        var projectB = AddProject("Beta", user.Id);
        AddActivityEntry(projectA.Id, 1, user.Id, title: "First", createdAt: DateTime.UtcNow.AddMinutes(-2));
        AddActivityEntry(projectA.Id, 2, user.Id, title: "Second", createdAt: DateTime.UtcNow.AddMinutes(-1));
        AddActivityEntry(projectB.Id, 3, user.Id, title: "Other project", createdAt: DateTime.UtcNow);
        var sut = CreateSut();

        var feed = await sut.GetProjectFeedAsync(projectA.Id, page: 1, pageSize: 20);

        Assert.Equal(2, feed.TotalCount);
        Assert.Equal("Second", feed.Items[0].WorkItemTitle);
        Assert.Equal("First", feed.Items[1].WorkItemTitle);
        Assert.Equal(user.Id, feed.Items[0].ActorUserId);
        Assert.Equal("Test User", feed.Items[0].ActorName);
    }

    [Fact]
    public async Task GetProjectFeedAsync_paginates()
    {
        var user = AddUser("feed-paginate@example.com");
        var project = AddProject("Alpha", user.Id);
        for (var i = 0; i < 5; i++)
        {
            AddActivityEntry(project.Id, i, user.Id, title: $"Item {i}", createdAt: DateTime.UtcNow.AddMinutes(-5 + i));
        }
        var sut = CreateSut();

        var firstPage = await sut.GetProjectFeedAsync(project.Id, page: 1, pageSize: 2);
        var secondPage = await sut.GetProjectFeedAsync(project.Id, page: 2, pageSize: 2);

        Assert.Equal(5, firstPage.TotalCount);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.Equal("Item 4", firstPage.Items[0].WorkItemTitle);
        Assert.Equal("Item 2", secondPage.Items[0].WorkItemTitle);
    }

    [Fact]
    public async Task GetProjectFeedAsync_throws_for_an_unknown_project()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<ProjectNotFoundException>(() => sut.GetProjectFeedAsync(999999, page: 1, pageSize: 20));
    }

    [Fact]
    public async Task GetProjectFeedAsync_keeps_an_entrys_snapshot_readable_after_its_work_item_is_deleted()
    {
        var user = AddUser("feed-deleted-item@example.com");
        var project = AddProject("Alpha", user.Id);
        var workItem = AddWorkItem(project.Id, user.Id, title: "Fix the login bug");
        AddActivityEntry(project.Id, workItem.Id, user.Id, title: "Fix the login bug");

        Db.WorkItems.Remove(workItem);
        Db.SaveChanges();

        var sut = CreateSut();
        var feed = await sut.GetProjectFeedAsync(project.Id, page: 1, pageSize: 20);

        var entry = Assert.Single(feed.Items);
        Assert.Equal(workItem.Id, entry.WorkItemId);
        Assert.Equal("Fix the login bug", entry.WorkItemTitle);
    }

    [Fact]
    public async Task GetWorkItemHistoryAsync_returns_only_that_items_entries_newest_first()
    {
        var user = AddUser("history-scoped@example.com");
        var project = AddProject("Alpha", user.Id);
        var itemA = AddWorkItem(project.Id, user.Id, title: "Item A");
        var itemB = AddWorkItem(project.Id, user.Id, title: "Item B");
        AddActivityEntry(project.Id, itemA.Id, user.Id, title: "Item A", eventType: ActivityEventType.Created, createdAt: DateTime.UtcNow.AddMinutes(-2));
        AddActivityEntry(
            project.Id, itemA.Id, user.Id, title: "Item A", eventType: ActivityEventType.FieldChanged,
            field: ActivityField.Priority, oldValue: "Low", newValue: "High", createdAt: DateTime.UtcNow.AddMinutes(-1));
        AddActivityEntry(project.Id, itemB.Id, user.Id, title: "Item B", createdAt: DateTime.UtcNow);
        var sut = CreateSut();

        var history = await sut.GetWorkItemHistoryAsync(itemA.Id);

        Assert.Equal(2, history.Count);
        Assert.Equal("FieldChanged", history[0].EventType);
        Assert.Equal("Created", history[1].EventType);
        Assert.All(history, e => Assert.Equal(itemA.Id, e.WorkItemId));
    }

    [Fact]
    public async Task GetWorkItemHistoryAsync_throws_for_an_unknown_work_item()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<WorkItemNotFoundException>(() => sut.GetWorkItemHistoryAsync(999999));
    }
}
