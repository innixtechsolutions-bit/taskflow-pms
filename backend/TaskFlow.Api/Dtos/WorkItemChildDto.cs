namespace TaskFlow.Api.Dtos;

// Lightweight row for a detail view's children list — direct children only (FR-018).
public record WorkItemChildDto(int Id, string Title, string Type, string Status, string? AssigneeName);
