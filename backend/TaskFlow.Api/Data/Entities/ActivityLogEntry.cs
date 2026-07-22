namespace TaskFlow.Api.Data.Entities;

// Either a brand-new work item, or one of the four tracked field changes below.
// Field/OldValue/NewValue are all null when this is Created (data-model.md).
public enum ActivityEventType
{
    Created,
    FieldChanged
}

// The only fields this feature tracks (spec FR-013/FR-014) — title, description,
// dates, labels, and hierarchy changes deliberately produce no entry at all.
public enum ActivityField
{
    Status,
    Priority,
    Assignee,
    Sprint
}

// An immutable, append-only record of one tracked event. No service method on
// ActivityLogService ever updates or removes a row of this table -- only
// RecordCreated/RecordFieldChange (both Add-only) exist, so immutability
// (FR-017) is structural, not a guarded permission check: there is simply no
// code path that could edit or delete an entry.
public class ActivityLogEntry
{
    public int Id { get; set; }

    // Derived from the work item's project *at the time of the change* (FR-015)
    // -- never re-derived later. Real FK, Cascade (research.md #2).
    public int ProjectId { get; set; }

    public Project? Project { get; set; }

    // Deliberately a plain column, not a real FK/navigation property
    // (research.md #1) -- this must survive the referenced WorkItem row being
    // deleted later (FR-018), which no EF Core delete behavior can express for
    // a relationship that has to outlive its target.
    public int WorkItemId { get; set; }

    // Snapshots, captured at write time -- not a live join. Both survive the
    // work item's later deletion or rename, which a join through WorkItemId
    // could never do once that row is gone.
    public required string WorkItemTitle { get; set; }

    public required string WorkItemType { get; set; }

    // Real FK, Restrict -- same convention as WorkItem.CreatedByUserId. The
    // actor's display name is resolved via a live join at read time, not
    // snapshotted (research.md #3): no user-deletion feature exists yet, so a
    // live join can never actually go stale.
    public int ActorUserId { get; set; }

    public User? Actor { get; set; }

    public ActivityEventType EventType { get; set; }

    // Null when EventType == Created.
    public ActivityField? Field { get; set; }

    // Display-ready text (e.g. "To Do", "Jane Doe", "Unassigned", "Backlog"),
    // not an internal id -- both null when EventType == Created.
    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public DateTime CreatedAt { get; set; }
}
