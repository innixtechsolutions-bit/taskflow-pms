namespace TaskFlow.Api.Dtos;

// Field-scoped, mirrors UpdateWorkItemStatusRequest exactly — the Backlog view's
// drag interaction only ever changes SprintId, never any other field.
public class UpdateWorkItemSprintRequest
{
    public int? SprintId { get; set; }
}
