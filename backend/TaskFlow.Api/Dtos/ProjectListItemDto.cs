namespace TaskFlow.Api.Dtos;

// OpenWorkItemCount (not-Done only) is distinct from ProjectDetailDto's
// TotalWorkItemCount (every item) — see data-model.md.
public record ProjectListItemDto(int Id, string Name, string CreatedByName, DateTime CreatedAt, int OpenWorkItemCount);
