using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Api.Dtos;

// One DTO for both create and edit — POST and PUT bodies are identically shaped.
// Type/Priority travel as strings (same convention as Role in Feature 001), parsed
// and validated against the actual enums in WorkItemService, not here. Status is
// identity-based (Feature 006) — StatusId, not a name — since a per-project status
// can be renamed at any time (FR-018/research.md #7): a name-keyed reference would
// silently break the moment a Manager renamed a column.
public class WorkItemRequest
{
    [Required]
    public required string Type { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 3)]
    public required string Title { get; set; }

    [StringLength(5000)]
    public string? Description { get; set; }

    public string? Priority { get; set; }

    // Optional -- defaults to the target project's first Open-category status (by
    // position) when omitted, matching this field's old "defaults to ToDo" behavior.
    public int? StatusId { get; set; }

    public int? AssigneeUserId { get; set; }

    public DateTime? DueDate { get; set; }

    // Required/optional/forbidden depending on Type — checked in WorkItemService
    // against data-model.md's Hierarchy rules table, not via a data annotation
    // (the same reasoning already applied to Priority/Status above).
    public int? ParentWorkItemId { get; set; }
}
