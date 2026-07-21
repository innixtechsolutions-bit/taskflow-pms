namespace TaskFlow.Api.Dtos;

// The Backlog view's response shape (Feature 008). Sprints come pre-ordered
// soonest-start-first (WorkItemService.GetBacklogAsync); BacklogItems are every
// filtered item with no sprint assignment (SprintId == null), including every
// Epic, which can never have one.
public record WorkItemBacklogDto(
    List<BacklogSprintSectionDto> Sprints,
    List<WorkItemDto> BacklogItems);

public record BacklogSprintSectionDto(
    int Id,
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    string Status,
    List<WorkItemDto> Items);
