using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Data;
using TaskFlow.Api.Data.Entities;
using TaskFlow.Api.Dtos;

namespace TaskFlow.Api.Services;

// A project's own workflow columns (Feature 006) -- add/rename/reorder/delete-with-move
// (US3-US6) extend this class; this slice (US1) only needs the read path every other
// status-aware surface (board, dropdowns, filters, the future management screen) depends on.
public class ProjectStatusService(AppDbContext dbContext)
{
    public async Task<List<WorkflowStatusDto>> GetStatusesAsync(int projectId)
    {
        var projectExists = await dbContext.Projects.AnyAsync(p => p.Id == projectId);
        if (!projectExists)
        {
            throw new ProjectNotFoundException();
        }

        return await dbContext.WorkflowStatuses
            .Where(s => s.ProjectId == projectId)
            .OrderBy(s => s.Position)
            .Select(s => new WorkflowStatusDto(
                s.Id, s.Name, s.Category.ToString(), s.ColorKey.ToString(), s.Position,
                dbContext.WorkItems.Count(w => w.WorkflowStatusId == s.Id)))
            .ToListAsync();
    }

    // FR-024 — computed on demand, never stored: the first Done-category status in
    // position order. No dedicated UI sets this explicitly; it exists so a future
    // feature (sprint completion, quick-complete) has a deterministic answer.
    public async Task<int> GetDefaultCompletionStatusId(int projectId)
    {
        var defaultStatus = await dbContext.WorkflowStatuses
            .Where(s => s.ProjectId == projectId && s.Category == WorkflowStatusCategory.Done)
            .OrderBy(s => s.Position)
            .FirstOrDefaultAsync();

        // FR-003 guarantees every project always has >=1 Done-category status.
        return defaultStatus?.Id ?? throw new ProjectNotFoundException();
    }
}
