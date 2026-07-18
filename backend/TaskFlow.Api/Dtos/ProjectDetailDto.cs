namespace TaskFlow.Api.Dtos;

// TotalWorkItemCount (every item, regardless of status) is distinct from the project
// list's OpenWorkItemCount (not-Done only, see ProjectListItemDto) — this is the count
// the frontend uses for the delete confirmation, "This will also delete N work items."
public record ProjectDetailDto(int Id, string Name, string? Description, string CreatedByName, DateTime CreatedAt, int TotalWorkItemCount);
