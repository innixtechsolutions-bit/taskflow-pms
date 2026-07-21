using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Api.Dtos;

// EndDate > StartDate is not a data-annotation check (this codebase's existing
// cross-field rules -- WorkItem's start<=due, WorkflowStatus's name uniqueness --
// all live in the service layer, not here), validated in SprintService.CreateAsync.
public class CreateSprintRequest
{
    [Required]
    [StringLength(50, MinimumLength = 2)]
    public required string Name { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }
}
