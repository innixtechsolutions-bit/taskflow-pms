using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Data;

namespace TaskFlow.Api.Tests.TestSupport;

// Constitution Principle I requires EF Core tests to run against the real SQL Server
// provider, not the InMemory provider, because InMemory doesn't enforce things like
// unique indexes the same way SQL Server does. Each test class gets its own,
// uniquely-named, disposable database — created in InitializeAsync, dropped in
// DisposeAsync — so tests never see another test's leftover data.
public abstract class SqlServerTestDatabase : IAsyncLifetime
{
    private readonly string _databaseName = $"TaskFlowDb_Test_{Guid.NewGuid():N}";

    protected AppDbContext Db { get; private set; } = null!;

    // Feature 008 -- exposed so a test can open a second, independent DbContext
    // against this same disposable database (e.g. to genuinely race two concurrent
    // SprintService.StartAsync calls against real SQL Server, not just one context).
    protected string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        ConnectionString =
            $"Server=localhost;Database={_databaseName};Trusted_Connection=True;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        Db = new AppDbContext(options);
        await Db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await Db.Database.EnsureDeletedAsync();
        await Db.DisposeAsync();
    }
}
