namespace TaskFlow.Api.Data.Entities;

// Feature 007 — a project-scoped tag. Never deleted by any code path in this
// feature (no rename/delete endpoint exists in v1); an unused label's row
// persists but is excluded from suggestions by a query-time filter instead
// (research.md #5), so there is no lifecycle beyond creation here.
public class Label
{
    public int Id { get; set; }

    public int ProjectId { get; set; }

    public Project? Project { get; set; }

    // 1-30 characters (enforced in WorkItemService), unique per project
    // case-insensitively via AppDbContext's (ProjectId, Name) index — the
    // same SQL Server default-collation mechanism as WorkflowStatus.Name,
    // Project.Name, and User.Email (research.md #4).
    public required string Name { get; set; }

    public DateTime CreatedAt { get; set; }

    public ICollection<WorkItemLabel> WorkItemLabels { get; set; } = new List<WorkItemLabel>();
}
