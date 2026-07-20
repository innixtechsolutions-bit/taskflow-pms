using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Api.Dtos;

// Both fields optional -- a request with neither is a tolerant no-op 200, matching
// ProjectService.UpdateAsync's style. No Category field: fixed at creation (FR-021),
// no service method ever changes it. Position isn't settable here either -- see the
// dedicated reorder endpoint (ReorderWorkflowStatusesRequest).
public class UpdateWorkflowStatusRequest
{
    [StringLength(30, MinimumLength = 2)]
    public string? Name { get; set; }

    public string? ColorKey { get; set; }
}
