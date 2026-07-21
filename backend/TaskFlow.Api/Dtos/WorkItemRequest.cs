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

    public DateTime? StartDate { get; set; }

    // 0-5 free-form, project-scoped names -- validated/normalized in
    // WorkItemService, not here (same reasoning as Type/Priority/Status above).
    // Omitted entirely means "no labels", the same as every other optional field
    // on this PUT-replaces-the-resource request.
    public List<string>? Labels { get; set; }

    // Required/optional/forbidden depending on Type — checked in WorkItemService
    // against data-model.md's Hierarchy rules table, not via a data annotation
    // (the same reasoning already applied to Priority/Status above).
    public int? ParentWorkItemId { get; set; }

    // Feature 008 — optional; null means "no sprint" (the backlog). Must belong to
    // this item's own project, must not be set on an Epic, and must not target a
    // Completed sprint — validated in WorkItemService.ResolveSprintIdAsync, not here
    // (same reasoning as every other cross-entity reference on this DTO).
    public int? SprintId { get; set; }
}
