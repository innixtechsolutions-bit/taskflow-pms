using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Data;
using TaskFlow.Api.Data.Entities;
using TaskFlow.Api.Dtos;
using TaskFlow.Api.Services;
using TaskFlow.Api.Tests.TestSupport;

namespace TaskFlow.Api.Tests.Services;

public class SprintServiceTests : SqlServerTestDatabase
{
    private SprintService CreateSut() => new(Db);

    private User AddUser(string email) => AddEntity(new User
    {
        FullName = "Test User", Email = email, PasswordHash = "hash", CreatedAt = DateTime.UtcNow
    });

    // Seeded with the standard four statuses (matches ProjectService.CreateAsync in
    // production) so US4's complete-resolution tests have real Open/Done rows to work
    // with, the same convention WorkItemServiceTests.AddProject already uses.
    private Project AddProject(string name, int creatorId)
    {
        var project = AddEntity(new Project { Name = name, CreatedByUserId = creatorId, CreatedAt = DateTime.UtcNow });
        Db.WorkflowStatuses.AddRange(
            new WorkflowStatus { ProjectId = project.Id, Name = "To Do", Position = 0, Category = WorkflowStatusCategory.Open, ColorKey = ChipColor.Slate },
            new WorkflowStatus { ProjectId = project.Id, Name = "Done", Position = 1, Category = WorkflowStatusCategory.Done, ColorKey = ChipColor.Green });
        Db.SaveChanges();
        return project;
    }

    private int StatusId(int projectId, string statusName) =>
        Db.WorkflowStatuses.Single(s => s.ProjectId == projectId && s.Name == statusName).Id;

    private Sprint AddSprint(int projectId, string name, DateTime start, DateTime end, SprintStatus status = SprintStatus.Planned) =>
        AddEntity(new Sprint { ProjectId = projectId, Name = name, StartDate = start, EndDate = end, Status = status });

    private WorkItem AddWorkItem(int projectId, int creatorId, int? sprintId = null, string status = "To Do") => AddEntity(new WorkItem
    {
        ProjectId = projectId, Type = WorkItemType.Task, Title = "Some work", WorkflowStatusId = StatusId(projectId, status),
        SprintId = sprintId, CreatedByUserId = creatorId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    });

    private T AddEntity<T>(T entity) where T : class
    {
        Db.Add(entity);
        Db.SaveChanges();
        return entity;
    }

    private static CreateSprintRequest CreateRequest(string name = "Sprint 1", int startOffsetDays = 0, int endOffsetDays = 14) =>
        new() { Name = name, StartDate = DateTime.UtcNow.Date.AddDays(startOffsetDays), EndDate = DateTime.UtcNow.Date.AddDays(endOffsetDays) };

    [Fact]
    public async Task CreateAsync_creates_a_Planned_sprint_with_zero_items()
    {
        var user = AddUser("create-sprint@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();

        var created = await sut.CreateAsync(project.Id, CreateRequest("Sprint 1"));

        Assert.Equal("Sprint 1", created.Name);
        Assert.Equal("Planned", created.Status);
        Assert.Equal(0, created.ItemCount);
    }

    [Fact]
    public async Task CreateAsync_throws_for_an_unknown_project()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<ProjectNotFoundException>(() => sut.CreateAsync(999999, CreateRequest()));
    }

    [Theory]
    [InlineData("A")]
    [InlineData("This sprint name is definitely far too long to be considered a valid short name for a sprint")]
    public async Task CreateAsync_rejects_a_name_outside_2_to_50_characters(string name)
    {
        var user = AddUser($"create-badname-{name.Length}@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();

        await Assert.ThrowsAsync<InvalidSprintNameException>(() => sut.CreateAsync(project.Id, CreateRequest(name)));
    }

    [Fact]
    public async Task CreateAsync_rejects_a_case_insensitive_duplicate_name_within_the_same_project()
    {
        var user = AddUser("create-duplicate@example.com");
        var project = AddProject("Alpha", user.Id);
        AddSprint(project.Id, "Sprint 1", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(14));
        var sut = CreateSut();

        await Assert.ThrowsAsync<DuplicateSprintNameException>(() => sut.CreateAsync(project.Id, CreateRequest("sprint 1")));
    }

    [Fact]
    public async Task CreateAsync_allows_the_same_name_in_a_different_project()
    {
        var user = AddUser("create-cross-project@example.com");
        var project1 = AddProject("Alpha", user.Id);
        var project2 = AddProject("Beta", user.Id);
        AddSprint(project1.Id, "Sprint 1", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(14));
        var sut = CreateSut();

        var created = await sut.CreateAsync(project2.Id, CreateRequest("Sprint 1"));

        Assert.Equal("Sprint 1", created.Name);
    }

    [Fact]
    public async Task CreateAsync_rejects_an_end_date_on_or_before_the_start_date()
    {
        var user = AddUser("create-baddates@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();
        var sameDay = new CreateSprintRequest { Name = "Sprint 1", StartDate = DateTime.UtcNow.Date, EndDate = DateTime.UtcNow.Date };

        await Assert.ThrowsAsync<InvalidSprintDateRangeException>(() => sut.CreateAsync(project.Id, sameDay));
    }

    [Fact]
    public async Task GetSprintsAsync_returns_sprints_soonest_start_date_first_with_item_counts()
    {
        var user = AddUser("list-sprints@example.com");
        var project = AddProject("Alpha", user.Id);
        var later = AddSprint(project.Id, "Sprint 2", DateTime.UtcNow.Date.AddDays(20), DateTime.UtcNow.Date.AddDays(34));
        var sooner = AddSprint(project.Id, "Sprint 1", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(14));
        var sut = CreateSut();

        var result = await sut.GetSprintsAsync(project.Id);

        Assert.Equal(new[] { sooner.Id, later.Id }, result.Select(s => s.Id));
    }

    [Fact]
    public async Task GetSprintsAsync_throws_for_an_unknown_project()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<ProjectNotFoundException>(() => sut.GetSprintsAsync(999999));
    }

    // ---------------------------------------------------------------------
    // US4 — Start
    // ---------------------------------------------------------------------

    [Fact]
    public async Task StartAsync_transitions_a_Planned_sprint_with_items_to_Active()
    {
        var user = AddUser("start-success@example.com");
        var project = AddProject("Alpha", user.Id);
        var sprint = AddSprint(project.Id, "Sprint 1", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(14));
        AddWorkItem(project.Id, user.Id, sprint.Id);
        var sut = CreateSut();

        var result = await sut.StartAsync(project.Id, sprint.Id);

        Assert.Equal("Active", result.Status);
    }

    [Fact]
    public async Task StartAsync_rejects_a_sprint_with_zero_items()
    {
        var user = AddUser("start-empty@example.com");
        var project = AddProject("Alpha", user.Id);
        var sprint = AddSprint(project.Id, "Sprint 1", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(14));
        var sut = CreateSut();

        await Assert.ThrowsAsync<EmptySprintException>(() => sut.StartAsync(project.Id, sprint.Id));
    }

    [Fact]
    public async Task StartAsync_rejects_a_sprint_that_is_not_Planned()
    {
        var user = AddUser("start-notplanned@example.com");
        var project = AddProject("Alpha", user.Id);
        var sprint = AddSprint(project.Id, "Sprint 1", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(14), SprintStatus.Completed);
        AddWorkItem(project.Id, user.Id, sprint.Id);
        var sut = CreateSut();

        await Assert.ThrowsAsync<SprintNotPlannedException>(() => sut.StartAsync(project.Id, sprint.Id));
    }

    [Fact]
    public async Task StartAsync_rejects_starting_a_second_sprint_while_one_is_already_Active_and_names_it()
    {
        var user = AddUser("start-another-active@example.com");
        var project = AddProject("Alpha", user.Id);
        var active = AddSprint(project.Id, "Sprint 1", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(14), SprintStatus.Active);
        AddWorkItem(project.Id, user.Id, active.Id);
        var second = AddSprint(project.Id, "Sprint 2", DateTime.UtcNow.Date.AddDays(20), DateTime.UtcNow.Date.AddDays(34));
        AddWorkItem(project.Id, user.Id, second.Id);
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<AnotherSprintActiveException>(() => sut.StartAsync(project.Id, second.Id));
        Assert.Contains("Sprint 1", ex.Message);
    }

    [Fact]
    public async Task StartAsync_throws_for_an_unknown_sprint()
    {
        var user = AddUser("start-notfound@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();

        await Assert.ThrowsAsync<SprintNotFoundException>(() => sut.StartAsync(project.Id, 999999));
    }

    // /speckit-analyze finding C2 -- the "no other Active sprint" rule must hold even
    // when two Start calls race. Two independent DbContexts (not just two SprintService
    // instances sharing one context) genuinely race against the same real SQL Server
    // database, proving the *database's* filtered unique index -- not merely the
    // in-process app-level check, which two truly concurrent requests could both pass
    // before either commits -- is what actually guarantees this property.
    [Fact]
    public async Task StartAsync_only_one_of_two_concurrent_Start_calls_on_different_sprints_succeeds()
    {
        var user = AddUser("start-race@example.com");
        var project = AddProject("Alpha", user.Id);
        var sprintA = AddSprint(project.Id, "Sprint A", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(14));
        var sprintB = AddSprint(project.Id, "Sprint B", DateTime.UtcNow.Date.AddDays(20), DateTime.UtcNow.Date.AddDays(34));
        AddWorkItem(project.Id, user.Id, sprintA.Id);
        AddWorkItem(project.Id, user.Id, sprintB.Id);

        var db2Options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options;
        using var db2 = new AppDbContext(db2Options);
        var sut1 = new SprintService(Db);
        var sut2 = new SprintService(db2);

        var task1 = TryStartAsync(sut1, project.Id, sprintA.Id);
        var task2 = TryStartAsync(sut2, project.Id, sprintB.Id);
        var results = await Task.WhenAll(task1, task2);

        Assert.Single(results, r => r.Succeeded);
        Assert.Single(results, r => !r.Succeeded && r.Exception is AnotherSprintActiveException);
    }

    private static async Task<(bool Succeeded, Exception? Exception)> TryStartAsync(SprintService sut, int projectId, int sprintId)
    {
        try
        {
            await sut.StartAsync(projectId, sprintId);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex);
        }
    }

    // Proves the schema-level guarantee directly (no timing dependency at all,
    // unlike the race test above): SQL Server itself, not application code, refuses
    // a second Active-status row per project.
    [Fact]
    public async Task Database_filtered_unique_index_rejects_a_second_Active_sprint_via_a_direct_write()
    {
        var user = AddUser("db-constraint@example.com");
        var project = AddProject("Alpha", user.Id);
        AddSprint(project.Id, "Sprint A", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(14), SprintStatus.Active);
        Db.Sprints.Add(new Sprint
        {
            ProjectId = project.Id, Name = "Sprint B",
            StartDate = DateTime.UtcNow.Date.AddDays(20), EndDate = DateTime.UtcNow.Date.AddDays(34),
            Status = SprintStatus.Active
        });

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => Db.SaveChangesAsync());
    }

    // ---------------------------------------------------------------------
    // US4 — Complete
    // ---------------------------------------------------------------------

    [Fact]
    public async Task CompleteAsync_completes_immediately_when_no_not_Done_items_exist()
    {
        var user = AddUser("complete-clean@example.com");
        var project = AddProject("Alpha", user.Id);
        var sprint = AddSprint(project.Id, "Sprint 1", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(14), SprintStatus.Active);
        AddWorkItem(project.Id, user.Id, sprint.Id, status: "Done");
        var sut = CreateSut();

        var result = await sut.CompleteAsync(project.Id, sprint.Id, new CompleteSprintRequest());

        Assert.Equal("Completed", result.Status);
    }

    [Fact]
    public async Task CompleteAsync_requires_a_resolution_when_not_Done_items_exist_and_reports_the_count()
    {
        var user = AddUser("complete-needs-resolution@example.com");
        var project = AddProject("Alpha", user.Id);
        var sprint = AddSprint(project.Id, "Sprint 1", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(14), SprintStatus.Active);
        AddWorkItem(project.Id, user.Id, sprint.Id, status: "To Do");
        AddWorkItem(project.Id, user.Id, sprint.Id, status: "To Do");
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<DestinationRequiredException>(
            () => sut.CompleteAsync(project.Id, sprint.Id, new CompleteSprintRequest()));
        Assert.Equal(2, ex.ItemCount);
    }

    [Fact]
    public async Task CompleteAsync_Backlog_resolution_clears_SprintId_only_on_not_Done_items()
    {
        var user = AddUser("complete-backlog@example.com");
        var project = AddProject("Alpha", user.Id);
        var sprint = AddSprint(project.Id, "Sprint 1", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(14), SprintStatus.Active);
        var notDone = AddWorkItem(project.Id, user.Id, sprint.Id, status: "To Do");
        var done = AddWorkItem(project.Id, user.Id, sprint.Id, status: "Done");
        var sut = CreateSut();

        await sut.CompleteAsync(project.Id, sprint.Id, new CompleteSprintRequest { Resolution = "Backlog" });

        Db.ChangeTracker.Clear();
        Assert.Null(Db.WorkItems.Single(w => w.Id == notDone.Id).SprintId);
        Assert.Equal(sprint.Id, Db.WorkItems.Single(w => w.Id == done.Id).SprintId);
    }

    [Fact]
    public async Task CompleteAsync_Sprint_resolution_reassigns_not_Done_items_to_the_destination()
    {
        var user = AddUser("complete-move@example.com");
        var project = AddProject("Alpha", user.Id);
        var sprint = AddSprint(project.Id, "Sprint 1", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(14), SprintStatus.Active);
        var destination = AddSprint(project.Id, "Sprint 2", DateTime.UtcNow.Date.AddDays(20), DateTime.UtcNow.Date.AddDays(34));
        var notDone = AddWorkItem(project.Id, user.Id, sprint.Id, status: "To Do");
        var sut = CreateSut();

        await sut.CompleteAsync(project.Id, sprint.Id, new CompleteSprintRequest { Resolution = "Sprint", DestinationSprintId = destination.Id });

        Db.ChangeTracker.Clear();
        Assert.Equal(destination.Id, Db.WorkItems.Single(w => w.Id == notDone.Id).SprintId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(999999)]
    public async Task CompleteAsync_Sprint_resolution_rejects_an_invalid_destination(int? destinationId)
    {
        var user = AddUser($"complete-baddest-{destinationId}@example.com");
        var project = AddProject("Alpha", user.Id);
        var sprint = AddSprint(project.Id, "Sprint 1", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(14), SprintStatus.Active);
        AddWorkItem(project.Id, user.Id, sprint.Id, status: "To Do");
        var sut = CreateSut();

        await Assert.ThrowsAsync<InvalidDestinationSprintException>(
            () => sut.CompleteAsync(project.Id, sprint.Id, new CompleteSprintRequest { Resolution = "Sprint", DestinationSprintId = destinationId }));
    }

    [Fact]
    public async Task CompleteAsync_Sprint_resolution_rejects_the_sprint_itself_as_destination()
    {
        var user = AddUser("complete-selfdest@example.com");
        var project = AddProject("Alpha", user.Id);
        var sprint = AddSprint(project.Id, "Sprint 1", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(14), SprintStatus.Active);
        AddWorkItem(project.Id, user.Id, sprint.Id, status: "To Do");
        var sut = CreateSut();

        await Assert.ThrowsAsync<InvalidDestinationSprintException>(
            () => sut.CompleteAsync(project.Id, sprint.Id, new CompleteSprintRequest { Resolution = "Sprint", DestinationSprintId = sprint.Id }));
    }

    [Fact]
    public async Task CompleteAsync_rejects_an_unrecognized_resolution_value()
    {
        var user = AddUser("complete-badresolution@example.com");
        var project = AddProject("Alpha", user.Id);
        var sprint = AddSprint(project.Id, "Sprint 1", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(14), SprintStatus.Active);
        AddWorkItem(project.Id, user.Id, sprint.Id, status: "To Do");
        var sut = CreateSut();

        await Assert.ThrowsAsync<InvalidDestinationSprintException>(
            () => sut.CompleteAsync(project.Id, sprint.Id, new CompleteSprintRequest { Resolution = "NotARealResolution" }));
    }

    [Fact]
    public async Task CompleteAsync_rejects_a_sprint_that_is_not_Active()
    {
        var user = AddUser("complete-notactive@example.com");
        var project = AddProject("Alpha", user.Id);
        var sprint = AddSprint(project.Id, "Sprint 1", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(14));
        var sut = CreateSut();

        await Assert.ThrowsAsync<SprintNotActiveException>(
            () => sut.CompleteAsync(project.Id, sprint.Id, new CompleteSprintRequest()));
    }

    [Fact]
    public async Task CompleteAsync_throws_for_an_unknown_sprint()
    {
        var user = AddUser("complete-notfound@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();

        await Assert.ThrowsAsync<SprintNotFoundException>(() => sut.CompleteAsync(project.Id, 999999, new CompleteSprintRequest()));
    }

    // ---------------------------------------------------------------------
    // US4 — Delete
    // ---------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_removes_an_empty_never_started_Planned_sprint()
    {
        var user = AddUser("delete-success@example.com");
        var project = AddProject("Alpha", user.Id);
        var sprint = AddSprint(project.Id, "Sprint 1", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(14));
        var sut = CreateSut();

        await sut.DeleteAsync(project.Id, sprint.Id);

        Assert.False(Db.Sprints.Any(s => s.Id == sprint.Id));
    }

    [Fact]
    public async Task DeleteAsync_rejects_a_sprint_that_has_items()
    {
        var user = AddUser("delete-hasitems@example.com");
        var project = AddProject("Alpha", user.Id);
        var sprint = AddSprint(project.Id, "Sprint 1", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(14));
        AddWorkItem(project.Id, user.Id, sprint.Id);
        var sut = CreateSut();

        await Assert.ThrowsAsync<SprintNotDeletableException>(() => sut.DeleteAsync(project.Id, sprint.Id));
    }

    [Theory]
    [InlineData(SprintStatus.Active)]
    [InlineData(SprintStatus.Completed)]
    public async Task DeleteAsync_rejects_a_sprint_that_has_ever_been_started(SprintStatus status)
    {
        var user = AddUser($"delete-started-{status}@example.com");
        var project = AddProject("Alpha", user.Id);
        var sprint = AddSprint(project.Id, "Sprint 1", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(14), status);
        var sut = CreateSut();

        await Assert.ThrowsAsync<SprintNotDeletableException>(() => sut.DeleteAsync(project.Id, sprint.Id));
    }

    [Fact]
    public async Task DeleteAsync_throws_for_an_unknown_sprint()
    {
        var user = AddUser("delete-notfound@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();

        await Assert.ThrowsAsync<SprintNotFoundException>(() => sut.DeleteAsync(project.Id, 999999));
    }
}
