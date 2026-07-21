namespace TaskFlow.Api.Data.Entities;

// A sprint's lifecycle (Feature 008). No transition back to Planned exists
// once Started, and no transition out of Completed -- see data-model.md's
// state transition table.
public enum SprintStatus
{
    Planned,
    Active,
    Completed
}

// Per-project sprint (Feature 008). Unlike WorkflowStatus, display order is
// never arbitrary -- it's always soonest-StartDate-first -- so there is no
// Position column here (research.md #2).
public class Sprint
{
    public int Id { get; set; }

    // Immutable after creation -- a sprint always belongs to the project it
    // was created in, same convention as WorkflowStatus.ProjectId.
    public int ProjectId { get; set; }

    public Project? Project { get; set; }

    // Unique within ProjectId (case-insensitive, via SQL Server's default
    // collation -- same mechanism as Project.Name/User.Email/WorkflowStatus.Name),
    // 2-50 chars (data-model.md).
    public required string Name { get; set; }

    public DateTime StartDate { get; set; }

    // Must be strictly after StartDate -- enforced in SprintService, not the
    // database, for consistency with every other cross-field rule in this
    // codebase (data-model.md).
    public DateTime EndDate { get; set; }

    public SprintStatus Status { get; set; } = SprintStatus.Planned;

    // Reverse navigation -- every work item currently assigned to this sprint.
    public ICollection<WorkItem> WorkItems { get; set; } = new List<WorkItem>();
}
