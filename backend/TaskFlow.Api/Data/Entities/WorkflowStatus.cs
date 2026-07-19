namespace TaskFlow.Api.Data.Entities;

// What the system reasons about (open-item counts, tree "n/m done", overdue
// highlighting) — Name is for humans, Category is for logic. See data-model.md.
public enum WorkflowStatusCategory
{
    Open,
    Done
}

// A fixed, curated palette (design system's approved chip colors) rather than a
// free-form color, so every status stays accessible/on-brand regardless of what a
// Manager names it (research.md #3). Ten members covers the max-10-columns case
// (FR-004) with no repeats. Open-category statuses cycle Slate/Blue/Violet/Amber/
// Teal/Rose/Indigo/Cyan; Done-category statuses use Green/Emerald.
public enum ChipColor
{
    Slate,
    Blue,
    Violet,
    Amber,
    Teal,
    Rose,
    Indigo,
    Cyan,
    Green,
    Emerald
}

// Per-project workflow column (Feature 006). Replaces the prior system-wide fixed
// WorkItemStatus enum -- every project now owns its own ordered list of these.
public class WorkflowStatus
{
    public int Id { get; set; }

    // Immutable after creation -- a status always belongs to the project it was
    // created in.
    public int ProjectId { get; set; }

    public Project? Project { get; set; }

    // Unique within ProjectId (case-insensitive, via SQL Server's default collation --
    // same mechanism as Project.Name/User.Email), 2-30 chars (data-model.md).
    public required string Name { get; set; }

    // 0-based, dense, unique within ProjectId. Defines column order everywhere
    // (FR-002/FR-012). Resequenced on every add/reorder/delete (research.md #1).
    public int Position { get; set; }

    // Fixed at creation (FR-021) -- no service method ever changes this after
    // the row is first created.
    public WorkflowStatusCategory Category { get; set; }

    // Assigned at creation (research.md #3); editable afterward via rename/recolor
    // (FR-015), independent of Name.
    public ChipColor ColorKey { get; set; }
}
