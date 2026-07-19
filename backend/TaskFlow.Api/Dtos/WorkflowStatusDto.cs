namespace TaskFlow.Api.Dtos;

// A project's own workflow column, with its current item count (Feature 006). At
// most 10 rows per project (FR-004), so this is always cheap to return in full --
// no separate single-status endpoint exists (contracts/workflow-api.md).
public record WorkflowStatusDto(int Id, string Name, string Category, string ColorKey, int Position, int ItemCount);
