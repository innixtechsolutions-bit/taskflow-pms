using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Data.Entities;

namespace TaskFlow.Api.Data;

// DbContext is EF Core's unit-of-work + change tracker: DbSet<User> below maps the
// User entity to the Users table, and EF Core tracks in-memory changes to entities
// loaded through it so SaveChangesAsync() knows what SQL to generate.
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            // Unique index on Email. Uniqueness is case-insensitive because it relies
            // on SQL Server's default collation (SQL_Latin1_General_CP1_CI_AS) rather
            // than a separate normalized column — see data-model.md.
            entity.HasIndex(u => u.Email).IsUnique();

            // Stored as readable text (e.g. "Admin") instead of an int, so the raw
            // table data is self-explanatory — a small, deliberate teaching touch
            // with no added runtime complexity (constitution Principle VI).
            entity.Property(u => u.Role).HasConversion<string>();
        });
    }
}
