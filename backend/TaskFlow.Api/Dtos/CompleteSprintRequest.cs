namespace TaskFlow.Api.Dtos;

// Resolution ("Backlog" | "Sprint") and DestinationSprintId travel as plain
// optional fields, not data-annotated -- both are only required conditionally
// (when the sprint has not-Done items, and only for "Sprint" respectively),
// validated in SprintService.CompleteAsync, same convention as every other
// cross-field/conditional rule in this codebase.
public class CompleteSprintRequest
{
    public string? Resolution { get; set; }

    public int? DestinationSprintId { get; set; }
}
