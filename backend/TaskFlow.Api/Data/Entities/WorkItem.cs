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

    // Feature 006 — replaces the prior system-wide fixed WorkItemStatus enum. Always
    // references a row belonging to this item's own ProjectId (enforced in
    // WorkItemService, not the database — a column can't express "same project as
    // ProjectId above").
    public int WorkflowStatusId { get; set; }

    public WorkflowStatus? WorkflowStatus { get; set; }

    // Optional: a work item need not be assigned to anyone.
    public int? AssigneeUserId { get; set; }

    public User? Assignee { get; set; }

    public DateTime? DueDate { get; set; }

    // Feature 007 — optional, date-only by convention (same as DueDate above).
    // start <= due (when both are set) is enforced in WorkItemService, not the
    // database — a column can't express "less than or equal to another
    // nullable column" the way this rule needs when either may be absent.
    public DateTime? StartDate { get; set; }

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

    // Feature 007 — join rows to this item's attached labels (0-5, enforced in
    // WorkItemService). The first many-to-many relationship in this codebase,
    // modeled with an explicit join entity rather than EF Core's implicit
    // UsingEntity<>() table (research.md #3) so the (WorkItemId, LabelId)
    // unique index has a natural place to live (data-model.md).
    public ICollection<WorkItemLabel> Labels { get; set; } = new List<WorkItemLabel>();
}
