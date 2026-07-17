using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Api.Dtos;

// One DTO for both create and edit — POST and PUT bodies are identically shaped.
// Type/Priority/Status travel as strings (same convention as Role in Feature 001),
// parsed and validated against the actual enums in WorkItemService, not here.
public class WorkItemRequest
{
    [Required]
    public required string Type { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 3)]
    public required string Title { get; set; }

    [StringLength(5000)]
    public string? Description { get; set; }

    public string? Priority { get; set; }

    public string? Status { get; set; }

    public int? AssigneeUserId { get; set; }

    public DateTime? DueDate { get; set; }
}
