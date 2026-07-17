using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Api.Dtos;

// One DTO for both create and edit: POST and PUT bodies are identically shaped
// and validated, so a second, near-duplicate type would add nothing.
public class ProjectRequest
{
    [Required]
    [StringLength(100, MinimumLength = 3)]
    public required string Name { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }
}
