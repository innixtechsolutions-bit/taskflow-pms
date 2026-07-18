using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Data.Entities;

namespace TaskFlow.Api.Data;

// DbContext is EF Core's unit-of-work + change tracker: DbSet<User> below maps the
// User entity to the Users table, and EF Core tracks in-memory changes to entities
// loaded through it so SaveChangesAsync() knows what SQL to generate.
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<WorkItem> WorkItems => Set<WorkItem>();

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

        modelBuilder.Entity<Project>(entity =>
        {
            // Same case-insensitive-by-collation trick as User.Email above.
            entity.HasIndex(p => p.Name).IsUnique();

            // Restrict, not the EF Core default of Cascade for a required FK: there is
            // no user-deletion feature yet, so this never actually fires today, but it
            // must be Restrict regardless — see the WorkItem config below for why.
            entity.HasOne(p => p.CreatedBy)
                .WithMany()
                .HasForeignKey(p => p.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkItem>(entity =>
        {
            // Every "list this project's work items" query filters on ProjectId —
            // this is the feature's single highest-volume query path, so the index
            // is called out explicitly even though EF Core would add one for the FK
            // regardless.
            entity.HasIndex(w => w.ProjectId);

            // The only Cascade in this feature: deleting a Project deletes all of its
            // WorkItems (FR-009).
            entity.HasOne(w => w.Project)
                .WithMany(p => p.WorkItems)
                .HasForeignKey(w => w.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // Both foreign keys pointing at User are Restrict, not Cascade. SQL Server
            // refuses to create a schema where the same table could be cascade-deleted
            // through two different paths — if these were Cascade, deleting a User
            // could reach WorkItem two ways: directly (CreatedByUserId) and indirectly
            // through their Project (Project's own Cascade to WorkItem). Restrict here
            // avoids that "multiple cascade paths" error entirely, and is the safer
            // default anyway since no feature can delete a User yet.
            entity.HasOne(w => w.CreatedBy)
                .WithMany()
                .HasForeignKey(w => w.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(w => w.Assignee)
                .WithMany()
                .HasForeignKey(w => w.AssigneeUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Every tree-build and descendant-collection query filters or groups on
            // ParentWorkItemId — this feature's single highest-volume new query path.
            entity.HasIndex(w => w.ParentWorkItemId);

            // Self-referencing FK (a WorkItem's parent is another WorkItem). SQL
            // Server flatly refuses ON DELETE CASCADE on a self-join (error 1785 —
            // it can't prove the cascade terminates), unlike the Project->WorkItem
            // cascade above. So subtree deletion happens explicitly in
            // WorkItemService.DeleteAsync instead of at the database level.
            entity.HasOne(w => w.ParentWorkItem)
                .WithMany(w => w.Children)
                .HasForeignKey(w => w.ParentWorkItemId)
                .OnDelete(DeleteBehavior.Restrict);

            // Same readable-text-over-int rationale as User.Role above, for all three
            // of this entity's enums.
            entity.Property(w => w.Type).HasConversion<string>();
            entity.Property(w => w.Priority).HasConversion<string>();
            entity.Property(w => w.Status).HasConversion<string>();
        });
    }
}
