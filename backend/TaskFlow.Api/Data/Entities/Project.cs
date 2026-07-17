namespace TaskFlow.Api.Data.Entities;

public class Project
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public int CreatedByUserId { get; set; }

    public User? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    // EF Core convention: a WorkItems collection navigation property alongside
    // WorkItem's ProjectId foreign key is enough for EF Core to infer the
    // one-to-many relationship — no separate join configuration needed.
    public ICollection<WorkItem> WorkItems { get; set; } = new List<WorkItem>();
}
