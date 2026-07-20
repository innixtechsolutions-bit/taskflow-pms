namespace TaskFlow.Api.Data.Entities;

// Feature 007 — the explicit join entity for the WorkItem <-> Label
// many-to-many relationship (this codebase's first many-to-many; research.md
// #3). Gets its own int identity PK, consistent with every other entity
// here, rather than a composite (WorkItemId, LabelId) key.
public class WorkItemLabel
{
    public int Id { get; set; }

    public int WorkItemId { get; set; }

    public WorkItem? WorkItem { get; set; }

    public int LabelId { get; set; }

    public Label? Label { get; set; }
}
