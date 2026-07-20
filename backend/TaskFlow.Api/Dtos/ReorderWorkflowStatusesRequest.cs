using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Api.Dtos;

// Must be a permutation of exactly this project's current status ids -- checked in
// ProjectStatusService.ReorderAsync, not here (data-model.md).
public class ReorderWorkflowStatusesRequest
{
    [Required]
    public required List<int> OrderedStatusIds { get; set; }
}
