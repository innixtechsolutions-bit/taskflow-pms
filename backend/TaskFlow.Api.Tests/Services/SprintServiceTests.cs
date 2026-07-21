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

    private Project AddProject(string name, int creatorId) => AddEntity(new Project
    {
        Name = name, CreatedByUserId = creatorId, CreatedAt = DateTime.UtcNow
    });

    private Sprint AddSprint(int projectId, string name, DateTime start, DateTime end, SprintStatus status = SprintStatus.Planned) =>
        AddEntity(new Sprint { ProjectId = projectId, Name = name, StartDate = start, EndDate = end, Status = status });

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
}
