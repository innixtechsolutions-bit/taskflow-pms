using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Api.Data;
using TaskFlow.Api.Data.Entities;

namespace TaskFlow.Api.Tests.Migrations;

// Verifies the AddPerProjectWorkflowStatuses migration itself (FR-023/SC-003), not
// just the final model shape -- every other test in this suite creates its schema
// via EnsureCreatedAsync() (straight from the current C# model), which never touches
// a real migration at all. This is the one test that actually replays the migration
// history against a real database: bring the schema to the point immediately before
// this feature's migration (using the old, pre-Feature-006 Status column), seed data
// shaped like a real pre-existing installation via raw SQL (the current entity model
// has already moved past that shape, so the ORM itself can't write it anymore), apply
// this feature's migration, then assert the resulting per-project statuses and each
// work item's backfilled reference are exactly right.
public class WorkflowStatusMigrationTests : IAsyncLifetime
{
    private readonly string _databaseName = $"TaskFlowDb_MigrationTest_{Guid.NewGuid():N}";

    private string ConnectionString =>
        $"Server=localhost;Database={_databaseName};Trusted_Connection=True;TrustServerCertificate=True";

    private AppDbContext _db = null!;

    public Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options;
        _db = new AppDbContext(options);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    private IMigrator Migrator => ((IInfrastructure<IServiceProvider>)_db.Database).Instance.GetRequiredService<IMigrator>();

    private async Task ExecuteSqlAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Migration_seeds_standard_four_statuses_and_backfills_every_work_items_status_exactly()
    {
        // Schema as it existed immediately before this feature -- the old Status
        // column still exists, WorkflowStatuses does not.
        await Migrator.MigrateAsync("20260718013330_AddWorkItemHierarchy");

        await using (var connection = new SqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            await ExecuteSqlAsync(connection,
                "INSERT INTO Users (FullName, Email, PasswordHash, Role, CreatedAt) " +
                "VALUES ('Test User', 'migration-test@example.com', 'hash', 'Developer', SYSUTCDATETIME())");
            await ExecuteSqlAsync(connection,
                "INSERT INTO Projects (Name, CreatedByUserId, CreatedAt) VALUES ('Migrated Project', 1, SYSUTCDATETIME())");
            await ExecuteSqlAsync(connection, """
                INSERT INTO WorkItems (ProjectId, Type, Title, Priority, Status, CreatedByUserId, CreatedAt, UpdatedAt)
                VALUES
                    (1, 'Task', 'A todo item', 'Medium', 'ToDo', 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
                    (1, 'Task', 'An in-progress item', 'Medium', 'InProgress', 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
                    (1, 'Task', 'An in-review item', 'Medium', 'InReview', 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
                    (1, 'Task', 'A done item', 'Medium', 'Done', 1, SYSUTCDATETIME(), SYSUTCDATETIME())
                """);
        }

        // Apply this feature's migration (schema change + same-migration data backfill).
        await Migrator.MigrateAsync();

        var statuses = await _db.WorkflowStatuses.Where(s => s.ProjectId == 1).OrderBy(s => s.Position).ToListAsync();
        Assert.Equal(
            new[]
            {
                ("To Do", WorkflowStatusCategory.Open),
                ("In Progress", WorkflowStatusCategory.Open),
                ("In Review", WorkflowStatusCategory.Open),
                ("Done", WorkflowStatusCategory.Done)
            },
            statuses.Select(s => (s.Name, s.Category)).ToArray());

        var items = await _db.WorkItems.Include(w => w.WorkflowStatus).Where(w => w.ProjectId == 1).ToListAsync();
        Assert.Equal(4, items.Count);
        Assert.Equal("To Do", items.Single(i => i.Title == "A todo item").WorkflowStatus!.Name);
        Assert.Equal("In Progress", items.Single(i => i.Title == "An in-progress item").WorkflowStatus!.Name);
        Assert.Equal("In Review", items.Single(i => i.Title == "An in-review item").WorkflowStatus!.Name);
        Assert.Equal("Done", items.Single(i => i.Title == "A done item").WorkflowStatus!.Name);
    }

    [Fact]
    public async Task Migration_seeds_the_standard_four_statuses_for_every_pre_existing_project_independently()
    {
        await Migrator.MigrateAsync("20260718013330_AddWorkItemHierarchy");

        await using (var connection = new SqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            await ExecuteSqlAsync(connection,
                "INSERT INTO Users (FullName, Email, PasswordHash, Role, CreatedAt) " +
                "VALUES ('Test User', 'migration-multi@example.com', 'hash', 'Developer', SYSUTCDATETIME())");
            await ExecuteSqlAsync(connection,
                "INSERT INTO Projects (Name, CreatedByUserId, CreatedAt) VALUES ('Project A', 1, SYSUTCDATETIME())");
            await ExecuteSqlAsync(connection,
                "INSERT INTO Projects (Name, CreatedByUserId, CreatedAt) VALUES ('Project B', 1, SYSUTCDATETIME())");
        }

        await Migrator.MigrateAsync();

        var countA = await _db.WorkflowStatuses.CountAsync(s => s.ProjectId == 1);
        var countB = await _db.WorkflowStatuses.CountAsync(s => s.ProjectId == 2);
        Assert.Equal(4, countA);
        Assert.Equal(4, countB);
    }
}
