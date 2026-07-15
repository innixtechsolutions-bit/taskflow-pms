using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Api.Data;

namespace TaskFlow.Api.Tests.TestSupport;

// Spins up the real ASP.NET Core host (WebApplicationFactory<Program> — Program.cs is
// marked `public partial class Program` for exactly this reason) against a real,
// uniquely-named, disposable SQL Server database, so integration tests exercise the
// actual middleware pipeline, [Authorize] attributes, and ProblemDetails output.
public class TaskFlowApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _databaseName = $"TaskFlowDb_Test_{Guid.NewGuid():N}";

    private string ConnectionString =>
        $"Server=localhost;Database={_databaseName};Trusted_Connection=True;TrustServerCertificate=True";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ConnectionString,
                ["Jwt:SigningKey"] = "integration-test-signing-key-at-least-32-bytes!!",
                ["Jwt:Issuer"] = "TaskFlow.Api.Tests",
                ["Jwt:Audience"] = "TaskFlow.Api.Tests",
                ["Admin:Email"] = "admin@taskflow.local",
                ["Admin:Password"] = "IntegrationTest!Admin1"
            });
        });
    }

    public async Task InitializeAsync()
    {
        // Schema must exist before the host is first accessed, since Program.cs's
        // AdminSeeder runs at startup and needs the Users table to already be there.
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
    }

    public new async Task DisposeAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options;
        await using (var db = new AppDbContext(options))
        {
            await db.Database.EnsureDeletedAsync();
        }
        await base.DisposeAsync();
    }
}
