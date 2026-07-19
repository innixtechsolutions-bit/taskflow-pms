namespace TaskFlow.Api.Dtos;

// Feature 005 (Kanban Board) -- the PATCH .../status endpoint's request body.
// Validated the same way WorkItemRequest.Status already is (must parse to a
// defined WorkItemStatus value), inside WorkItemService, not here.
public record UpdateWorkItemStatusRequest(string Status);
