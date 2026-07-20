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

    public DbSet<WorkflowStatus> WorkflowStatuses => Set<WorkflowStatus>();

    public DbSet<Label> Labels => Set<Label>();

    public DbSet<WorkItemLabel> WorkItemLabels => Set<WorkItemLabel>();

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

            // Same readable-text-over-int rationale as User.Role above, for both of
            // this entity's remaining enums (Status became a FK in Feature 006).
            entity.Property(w => w.Type).HasConversion<string>();
            entity.Property(w => w.Priority).HasConversion<string>();

            // Every board/filter query groups or filters on this — same rationale as
            // the ProjectId/ParentWorkItemId indexes above.
            entity.HasIndex(w => w.WorkflowStatusId);

            // Restrict, not Cascade: the database is a safety net only.
            // ProjectStatusService.DeleteAsync always reassigns every referencing
            // WorkItem's WorkflowStatusId before removing a WorkflowStatus row, so this
            // should never actually fire in practice (data-model.md).
            entity.HasOne(w => w.WorkflowStatus)
                .WithMany()
                .HasForeignKey(w => w.WorkflowStatusId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkflowStatus>(entity =>
        {
            // Case-insensitive per project (relies on SQL Server's default collation,
            // same mechanism as Project.Name/User.Email) -- not globally unique, since
            // two different projects may each have a "QA" column (FR-002).
            entity.HasIndex(s => new { s.ProjectId, s.Name }).IsUnique();

            // Deleting a project deletes its statuses -- consistent with the existing
            // Project -> WorkItem cascade just above.
            entity.HasOne(s => s.Project)
                .WithMany(p => p.WorkflowStatuses)
                .HasForeignKey(s => s.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(s => s.Category).HasConversion<string>();
            entity.Property(s => s.ColorKey).HasConversion<string>();
        });

        modelBuilder.Entity<Label>(entity =>
        {
            // Case-insensitive per project (SQL Server default collation), same
            // mechanism as WorkflowStatus.Name/Project.Name/User.Email above.
            entity.HasIndex(l => new { l.ProjectId, l.Name }).IsUnique();

            // Deleting a project deletes its labels -- consistent with the existing
            // Project -> WorkItem/WorkflowStatus cascades above.
            entity.HasOne(l => l.Project)
                .WithMany()
                .HasForeignKey(l => l.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkItemLabel>(entity =>
        {
            // Prevents attaching the same label to an item twice (FR-019) and makes
            // the 0-5 cap a plain Count() against this index (data-model.md).
            entity.HasIndex(wl => new { wl.WorkItemId, wl.LabelId }).IsUnique();

            // Deleting a work item removes its label attachments -- needed for
            // WorkItemService.DeleteAsync's existing subtree-delete path to work
            // without a separate cleanup step.
            entity.HasOne(wl => wl.WorkItem)
                .WithMany(w => w.Labels)
                .HasForeignKey(wl => wl.WorkItemId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict, not Cascade: SQL Server refuses a schema where this table
            // could be cascade-deleted through two different paths from Project
            // (directly via Project -> Label -> WorkItemLabel, and indirectly via
            // Project -> WorkItem -> WorkItemLabel above) -- error 1785, the same
            // "multiple cascade paths" problem already documented for WorkItem's
            // CreatedBy/Assignee FKs elsewhere in this file. Restrict here is also
            // harmless in practice: no code path in this feature ever deletes a
            // Label (research.md #5), so this FK's delete behavior never fires.
            entity.HasOne(wl => wl.Label)
                .WithMany(l => l.WorkItemLabels)
                .HasForeignKey(wl => wl.LabelId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
