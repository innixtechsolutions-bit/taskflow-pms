namespace TaskFlow.Api.Dtos;

// Status is flattened (StatusId/StatusName/StatusCategory/StatusColorKey), not a
// nested object -- the same convention this DTO already uses for AssigneeUserId/
// AssigneeName (Feature 006).
public record WorkItemDto(
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
    List<string> Labels,
    int? SprintId,
    string? SprintName);
