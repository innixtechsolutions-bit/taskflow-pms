namespace TaskFlow.Api.Data.Entities;

// A label only in this feature — no parent/child hierarchy between types yet
// (that arrives in a later feature), so a flat enum is all this slice needs
// (constitution Principle III — Clarity Over Cleverness).
public enum WorkItemType
{
    Epic,
    Story,
    Task,
    SubTask
}

public enum WorkItemPriority
{
    Low,
    Medium,
    High,
    Critical
}

// No restricted transitions in this feature — any status may be set to any
// other directly; there is no state machine here (see data-model.md).
// InReview added by Feature 005 (Kanban Board) — a plain enum addition, no
// migration: Status is a HasConversion<string>() column with no constraint
// on which strings are valid (research.md #1).
public enum WorkItemStatus
{
    ToDo,
    InProgress,
    InReview,
    Done
}

public class WorkItem
{
    public int Id { get; set; }

    // Immutable after creation (FR-014) — no service method ever assigns this
    // after the entity is first created.
    public int ProjectId { get; set; }

    public Project? Project { get; set; }

    public WorkItemType Type { get; set; }

    public required string Title { get; set; }

    public string? Description { get; set; }

    public WorkItemPriority Priority { get; set; } = WorkItemPriority.Medium;

    public WorkItemStatus Status { get; set; } = WorkItemStatus.ToDo;

    // Optional: a work item need not be assigned to anyone.
    public int? AssigneeUserId { get; set; }

    public User? Assignee { get; set; }

    public DateTime? DueDate { get; set; }

    public int CreatedByUserId { get; set; }

    public User? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    // Self-referencing FK: required for SubTask, optional for Task, forbidden for
    // Epic/Story-with-no-Epic (enforced in WorkItemService, not the database — a
    // column can't express "required depending on Type"). SQL Server won't let this
    // relationship cascade on delete (see AppDbContext), so subtree deletion is
    // application code in WorkItemService.
    public int? ParentWorkItemId { get; set; }

    public WorkItem? ParentWorkItem { get; set; }

    // Direct children only — deeper levels are reached by querying, not by walking
    // this collection recursively.
    public ICollection<WorkItem> Children { get; set; } = new List<WorkItem>();
}
