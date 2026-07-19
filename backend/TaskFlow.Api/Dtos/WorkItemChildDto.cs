namespace TaskFlow.Api.Dtos;

// Lightweight row for a detail view's children list — direct children only (FR-018).
public record WorkItemChildDto(int Id, string Title, string Type, int StatusId, string StatusName, string StatusCategory, string StatusColorKey, string? AssigneeName);
