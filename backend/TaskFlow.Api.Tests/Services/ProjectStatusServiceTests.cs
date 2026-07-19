using TaskFlow.Api.Data.Entities;
using TaskFlow.Api.Services;
using TaskFlow.Api.Tests.TestSupport;

namespace TaskFlow.Api.Tests.Services;

public class ProjectStatusServiceTests : SqlServerTestDatabase
{
    private ProjectStatusService CreateSut() => new(Db);

    private User AddUser(string email) => AddEntity(new User
    {
        FullName = "Test User", Email = email, PasswordHash = "hash", CreatedAt = DateTime.UtcNow
    });

    private Project AddProject(string name, int creatorId) => AddEntity(new Project
    {
        Name = name, CreatedByUserId = creatorId, CreatedAt = DateTime.UtcNow
    });

    private T AddEntity<T>(T entity) where T : class
    {
        Db.Add(entity);
        Db.SaveChanges();
        return entity;
    }

    private WorkflowStatus AddStatus(int projectId, string name, int position, WorkflowStatusCategory category, ChipColor colorKey = ChipColor.Slate) =>
        AddEntity(new WorkflowStatus { ProjectId = projectId, Name = name, Position = position, Category = category, ColorKey = colorKey });

    private void AddWorkItem(int projectId, int creatorId, int statusId) => AddEntity(new WorkItem
    {
        ProjectId = projectId, Type = WorkItemType.Task, Title = "Some work", WorkflowStatusId = statusId,
        CreatedByUserId = creatorId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    });

    [Fact]
    public async Task GetStatusesAsync_returns_statuses_in_position_order_with_item_counts()
    {
        var user = AddUser("statuses-list@example.com");
        var project = AddProject("Alpha", user.Id);
        var toDo = AddStatus(project.Id, "To Do", 0, WorkflowStatusCategory.Open);
        var done = AddStatus(project.Id, "Done", 1, WorkflowStatusCategory.Done, ChipColor.Green);
        AddWorkItem(project.Id, user.Id, toDo.Id);
        AddWorkItem(project.Id, user.Id, toDo.Id);
        AddWorkItem(project.Id, user.Id, done.Id);
        var sut = CreateSut();

        var result = await sut.GetStatusesAsync(project.Id);

        Assert.Equal(
            new[] { ("To Do", "Open", 2), ("Done", "Done", 1) },
            result.Select(s => (s.Name, s.Category, s.ItemCount)).ToArray());
    }

    [Fact]
    public async Task GetStatusesAsync_throws_for_an_unknown_project()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<ProjectNotFoundException>(() => sut.GetStatusesAsync(999999));
    }

    // FR-024 — computed on demand: the first Done-category status by position, not a
    // stored/reassigned flag.
    [Fact]
    public async Task GetDefaultCompletionStatusId_returns_the_lowest_position_Done_category_status()
    {
        var user = AddUser("default-completion@example.com");
        var project = AddProject("Alpha", user.Id);
        AddStatus(project.Id, "To Do", 0, WorkflowStatusCategory.Open);
        var firstDone = AddStatus(project.Id, "Done", 1, WorkflowStatusCategory.Done, ChipColor.Green);
        AddStatus(project.Id, "Archived", 2, WorkflowStatusCategory.Done, ChipColor.Emerald);
        var sut = CreateSut();

        var result = await sut.GetDefaultCompletionStatusId(project.Id);

        Assert.Equal(firstDone.Id, result);
    }

    // No stored flag to reassign -- deleting the current "first" Done status just
    // means recomputing which one is first now returns a different id.
    [Fact]
    public async Task GetDefaultCompletionStatusId_recomputes_after_the_first_Done_status_is_removed()
    {
        var user = AddUser("default-completion-recompute@example.com");
        var project = AddProject("Alpha", user.Id);
        var firstDone = AddStatus(project.Id, "Done", 0, WorkflowStatusCategory.Done, ChipColor.Green);
        var secondDone = AddStatus(project.Id, "Archived", 1, WorkflowStatusCategory.Done, ChipColor.Emerald);
        AddStatus(project.Id, "To Do", 2, WorkflowStatusCategory.Open);
        var sut = CreateSut();

        Db.WorkflowStatuses.Remove(firstDone);
        Db.SaveChanges();
        var result = await sut.GetDefaultCompletionStatusId(project.Id);

        Assert.Equal(secondDone.Id, result);
    }
}
