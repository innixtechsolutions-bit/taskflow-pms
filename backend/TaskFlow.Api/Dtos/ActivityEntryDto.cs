namespace TaskFlow.Api.Dtos;

// Same shape for both the paginated project feed and the unpaginated
// per-item history (research.md #15) -- WorkItemTitle/WorkItemType are
// direct column reads (the entity's own snapshot); ActorName is the one
// live join (research.md #3).
public record ActivityEntryDto(
    int Id,
    int WorkItemId,
    string WorkItemTitle,
    string WorkItemType,
    string EventType,
    string? Field,
    string? OldValue,
    string? NewValue,
    int ActorUserId,
    string ActorName,
    DateTime CreatedAt);
