namespace TaskFlow.Api.Dtos;

public record ProjectSummaryDto(
    StatCardsDto StatCards,
    List<StatusBreakdownItemDto> StatusBreakdown,
    List<PriorityBreakdownItemDto> PriorityBreakdown,
    List<WorkloadRowDto> Workload);

public record StatCardsDto(int Total, int Completed, double CompletedPercent, int InProgress, int DueSoon);

public record StatusBreakdownItemDto(int StatusId, string Name, string ColorKey, int Count);

public record PriorityBreakdownItemDto(string Priority, int Count);

public record WorkloadRowDto(int? UserId, string DisplayName, int OpenItemCount);
