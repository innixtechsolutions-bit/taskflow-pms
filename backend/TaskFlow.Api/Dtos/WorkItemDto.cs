namespace TaskFlow.Api.Dtos;

public record WorkItemDto(
    int Id,
    int ProjectId,
    string Type,
    string Title,
    string? Description,
    string Priority,
    string Status,
    int? AssigneeUserId,
    string? AssigneeName,
    DateTime? DueDate,
    int CreatedByUserId,
    string CreatedByName,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int? ParentWorkItemId);
