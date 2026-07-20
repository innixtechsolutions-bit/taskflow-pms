using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Api.Dtos;

// Category travels as a string (same convention as WorkItemRequest.Type/Priority) --
// parsed and validated against WorkflowStatusCategory in ProjectStatusService, not
// here. ColorKey is never client-supplied -- ProjectStatusService assigns it
// automatically (research.md #3).
public class CreateWorkflowStatusRequest
{
    [Required]
    [StringLength(30, MinimumLength = 2)]
    public required string Name { get; set; }

    [Required]
    public required string Category { get; set; }

    // Optional -- when omitted, ProjectStatusService inserts the new status
    // immediately before the project's first Done-category status (FR-010).
    public int? Position { get; set; }
}
