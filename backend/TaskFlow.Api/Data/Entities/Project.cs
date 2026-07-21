namespace TaskFlow.Api.Data.Entities;

public class Project
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public int CreatedByUserId { get; set; }

    public User? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    // EF Core convention: a WorkItems collection navigation property alongside
    // WorkItem's ProjectId foreign key is enough for EF Core to infer the
    // one-to-many relationship — no separate join configuration needed.
    public ICollection<WorkItem> WorkItems { get; set; } = new List<WorkItem>();

    // Feature 006 — replaces the prior system-wide fixed WorkItemStatus enum with a
    // per-project, managed list. Every project must have at least one Open-category
    // and one Done-category row at all times (FR-003), enforced in ProjectStatusService.
    public ICollection<WorkflowStatus> WorkflowStatuses { get; set; } = new List<WorkflowStatus>();

    // Feature 008 — a project's own sprints. At most one may have Status ==
    // Active at any time, enforced in SprintService.
    public ICollection<Sprint> Sprints { get; set; } = new List<Sprint>();
}
