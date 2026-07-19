namespace TaskFlow.Api.Dtos;

// Feature 005 (Kanban Board) -- the PATCH .../status endpoint's request body.
// StatusId, not a name (Feature 006/research.md #7) -- validated inside
// WorkItemService, not here (must belong to the item's own project).
public record UpdateWorkItemStatusRequest(int StatusId);
