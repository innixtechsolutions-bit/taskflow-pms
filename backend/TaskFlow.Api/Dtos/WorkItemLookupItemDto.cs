namespace TaskFlow.Api.Dtos;

// Same minimal id+title shape as UserLookupItemDto (Feature 002) — a small,
// role-agnostic lookup for populating a picker.
public record WorkItemLookupItemDto(int Id, string Title);

public record WorkItemParentCandidatesResponse(List<WorkItemLookupItemDto> Candidates);
