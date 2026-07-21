namespace TaskFlow.Api.Dtos;

public record SprintDto(
    int Id,
    int ProjectId,
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    string Status,
    int ItemCount);
