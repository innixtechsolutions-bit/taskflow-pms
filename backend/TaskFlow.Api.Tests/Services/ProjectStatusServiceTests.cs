using TaskFlow.Api.Data.Entities;
using TaskFlow.Api.Dtos;
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

    private static CreateWorkflowStatusRequest CreateRequest(string name, string category = "Open", int? position = null) =>
        new() { Name = name, Category = category, Position = position };

    [Fact]
    public async Task CreateAsync_adds_a_status_at_the_requested_explicit_position_and_shifts_the_rest()
    {
        var user = AddUser("create-explicit-position@example.com");
        var project = AddProject("Alpha", user.Id);
        AddStatus(project.Id, "To Do", 0, WorkflowStatusCategory.Open);
        AddStatus(project.Id, "Done", 1, WorkflowStatusCategory.Done, ChipColor.Green);
        var sut = CreateSut();

        var created = await sut.CreateAsync(project.Id, CreateRequest("In Progress", "Open", position: 1));

        Assert.Equal("In Progress", created.Name);
        Assert.Equal("Open", created.Category);
        Assert.Equal(1, created.Position);
        Assert.Equal(0, created.ItemCount);
        var all = await sut.GetStatusesAsync(project.Id);
        Assert.Equal(new[] { "To Do", "In Progress", "Done" }, all.OrderBy(s => s.Position).Select(s => s.Name));
    }

    [Fact]
    public async Task CreateAsync_defaults_to_the_position_immediately_before_the_first_Done_status_when_omitted()
    {
        var user = AddUser("create-default-position@example.com");
        var project = AddProject("Alpha", user.Id);
        AddStatus(project.Id, "To Do", 0, WorkflowStatusCategory.Open);
        AddStatus(project.Id, "Done", 1, WorkflowStatusCategory.Done, ChipColor.Green);
        var sut = CreateSut();

        var created = await sut.CreateAsync(project.Id, CreateRequest("QA"));

        var all = await sut.GetStatusesAsync(project.Id);
        Assert.Equal(new[] { "To Do", "QA", "Done" }, all.OrderBy(s => s.Position).Select(s => s.Name));
        Assert.Equal(1, created.Position);
    }

    [Fact]
    public async Task CreateAsync_rejects_a_case_insensitive_duplicate_name()
    {
        var user = AddUser("create-duplicate@example.com");
        var project = AddProject("Alpha", user.Id);
        AddStatus(project.Id, "To Do", 0, WorkflowStatusCategory.Open);
        AddStatus(project.Id, "Done", 1, WorkflowStatusCategory.Done, ChipColor.Green);
        var sut = CreateSut();

        await Assert.ThrowsAsync<DuplicateStatusNameException>(
            () => sut.CreateAsync(project.Id, CreateRequest("to do")));
    }

    [Theory]
    [InlineData("A")]
    [InlineData("This name is definitely far too long to be valid")]
    public async Task CreateAsync_rejects_a_name_outside_2_to_30_characters(string name)
    {
        var user = AddUser($"create-badname-{name.Length}@example.com");
        var project = AddProject("Alpha", user.Id);
        AddStatus(project.Id, "Done", 0, WorkflowStatusCategory.Done, ChipColor.Green);
        var sut = CreateSut();

        await Assert.ThrowsAsync<InvalidStatusNameException>(
            () => sut.CreateAsync(project.Id, CreateRequest(name)));
    }

    [Fact]
    public async Task CreateAsync_rejects_the_11th_status_in_a_project()
    {
        var user = AddUser("create-max@example.com");
        var project = AddProject("Alpha", user.Id);
        for (var i = 0; i < 9; i++)
        {
            AddStatus(project.Id, $"Open {i}", i, WorkflowStatusCategory.Open);
        }
        AddStatus(project.Id, "Done", 9, WorkflowStatusCategory.Done, ChipColor.Green);
        var sut = CreateSut();

        await Assert.ThrowsAsync<MaxStatusCountExceededException>(
            () => sut.CreateAsync(project.Id, CreateRequest("One Too Many")));
    }

    [Fact]
    public async Task CreateAsync_throws_for_an_unknown_project()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<ProjectNotFoundException>(() => sut.CreateAsync(999999, CreateRequest("QA")));
    }

    [Fact]
    public async Task CreateAsync_throws_for_an_invalid_category()
    {
        var user = AddUser("create-badcategory@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();

        await Assert.ThrowsAsync<InvalidStatusCategoryException>(
            () => sut.CreateAsync(project.Id, CreateRequest("QA", category: "Bogus")));
    }

    // research.md #3 -- Open cycles Slate/Blue/Violet/Amber/Teal/Rose/Indigo/Cyan,
    // Done uses Green/Emerald, skipping colors already used by that project's own
    // other statuses in the same category.
    [Fact]
    public async Task CreateAsync_assigns_the_next_unused_color_in_the_status_own_category_cycle()
    {
        var user = AddUser("create-color-cycle@example.com");
        var project = AddProject("Alpha", user.Id);
        AddStatus(project.Id, "To Do", 0, WorkflowStatusCategory.Open, ChipColor.Slate);
        AddStatus(project.Id, "Done", 1, WorkflowStatusCategory.Done, ChipColor.Green);
        var sut = CreateSut();

        var created = await sut.CreateAsync(project.Id, CreateRequest("In Progress"));

        Assert.Equal("Blue", created.ColorKey);
    }

    [Fact]
    public async Task CreateAsync_assigns_a_Done_family_color_for_a_Done_category_status()
    {
        var user = AddUser("create-color-done@example.com");
        var project = AddProject("Alpha", user.Id);
        AddStatus(project.Id, "Done", 0, WorkflowStatusCategory.Done, ChipColor.Green);
        var sut = CreateSut();

        var created = await sut.CreateAsync(project.Id, CreateRequest("Archived", category: "Done"));

        Assert.Equal("Emerald", created.ColorKey);
    }
}
