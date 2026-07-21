namespace TaskFlow.Api.Dtos;

// Returned only by GET /api/work-items/{id} — a superset of WorkItemDto (used by
// POST/PUT/list/tree instead) with the extra fields only a single-item detail page
// needs: the parent to link to, direct children to navigate to, and the descendant
// count a delete confirmation needs before the user commits to it (research.md §6,
// mirroring Feature 002's ProjectDetailDto.TotalWorkItemCount).
public record WorkItemDetailDto(
    int Id,
    int ProjectId,
    string Type,
    string Title,
    string? Description,
    string Priority,
    int StatusId,
    string StatusName,
    string StatusCategory,
    string StatusColorKey,
    int? AssigneeUserId,
    string? AssigneeName,
    DateTime? DueDate,
    DateTime? StartDate,
    int CreatedByUserId,
    string CreatedByName,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int? ParentWorkItemId,
    string? ParentTitle,
    int TotalDescendantCount,
    List<WorkItemChildDto> Children);
