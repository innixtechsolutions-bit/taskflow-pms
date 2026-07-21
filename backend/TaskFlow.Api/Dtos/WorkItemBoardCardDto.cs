namespace TaskFlow.Api.Dtos;

// Exactly what a board card displays (FR-009/FR-011) plus CreatedByUserId/
// AssigneeUserId (needed client-side for the drag-permission check, mirroring
// the same fields WorkItemDto already exposes) -- deliberately excludes
// Description/ParentWorkItemId, which the card never shows and which the
// status-only PATCH endpoint makes unnecessary to carry around (research.md #3).
public record WorkItemBoardCardDto(
    int Id,
    string Type,
    string Title,
    int StatusId,
    string StatusName,
    string StatusCategory,
    string StatusColorKey,
    string Priority,
    int? AssigneeUserId,
    string? AssigneeName,
    DateTime? DueDate,
    DateTime UpdatedAt,
    int CreatedByUserId,
    int DirectChildrenCount,
    int DirectChildrenDoneCount,
    List<string> Labels);
