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
    // research.md #3 -- Open-category statuses cycle through these 8 hues; Done-category
    // statuses use this 2-member green family. 8 + 2 = 10, exactly the max-status cap
    // (FR-004), so no two statuses in the same project need ever share a color.
    private static readonly ChipColor[] OpenColorCycle =
        [ChipColor.Slate, ChipColor.Blue, ChipColor.Violet, ChipColor.Amber, ChipColor.Teal, ChipColor.Rose, ChipColor.Indigo, ChipColor.Cyan];

    private static readonly ChipColor[] DoneColorCycle = [ChipColor.Green, ChipColor.Emerald];

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

    public async Task<WorkflowStatusDto> CreateAsync(int projectId, CreateWorkflowStatusRequest request)
    {
        var project = await dbContext.Projects.Include(p => p.WorkflowStatuses).FirstOrDefaultAsync(p => p.Id == projectId);
        if (project is null)
        {
            throw new ProjectNotFoundException();
        }

        if (request.Name.Length < 2 || request.Name.Length > 30)
        {
            throw new InvalidStatusNameException();
        }

        if (!Enum.TryParse<WorkflowStatusCategory>(request.Category, ignoreCase: true, out var category))
        {
            throw new InvalidStatusCategoryException();
        }

        var existing = project.WorkflowStatuses.OrderBy(s => s.Position).ToList();

        if (existing.Count >= 10)
        {
            throw new MaxStatusCountExceededException();
        }

        if (existing.Any(s => string.Equals(s.Name, request.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DuplicateStatusNameException();
        }

        // FR-010: default position is immediately before the first Done-category
        // status; an explicit position is clamped to the valid range instead of
        // rejected outright, matching the tolerant style of other "position" inputs
        // in this codebase.
        int insertPosition;
        if (request.Position.HasValue)
        {
            insertPosition = Math.Clamp(request.Position.Value, 0, existing.Count);
        }
        else
        {
            var firstDone = existing.FirstOrDefault(s => s.Category == WorkflowStatusCategory.Done);
            insertPosition = firstDone?.Position ?? existing.Count;
        }

        foreach (var status in existing.Where(s => s.Position >= insertPosition))
        {
            status.Position += 1;
        }

        var usedColorsInCategory = existing.Where(s => s.Category == category).Select(s => s.ColorKey).ToHashSet();
        var colorKey = AssignColor(category, existing.Count(s => s.Category == category), usedColorsInCategory);

        var newStatus = new WorkflowStatus
        {
            ProjectId = projectId,
            Name = request.Name,
            Position = insertPosition,
            Category = category,
            ColorKey = colorKey,
        };
        dbContext.WorkflowStatuses.Add(newStatus);

        await dbContext.SaveChangesAsync();

        return new WorkflowStatusDto(
            newStatus.Id, newStatus.Name, newStatus.Category.ToString(), newStatus.ColorKey.ToString(), newStatus.Position, ItemCount: 0);
    }

    public async Task<WorkflowStatusDto> UpdateAsync(int projectId, int statusId, UpdateWorkflowStatusRequest request)
    {
        var project = await dbContext.Projects.Include(p => p.WorkflowStatuses).FirstOrDefaultAsync(p => p.Id == projectId);
        if (project is null)
        {
            throw new ProjectNotFoundException();
        }

        var status = project.WorkflowStatuses.FirstOrDefault(s => s.Id == statusId) ?? throw new WorkflowStatusNotFoundException();

        // Re-validates length and uniqueness here too, not just via the request DTO's
        // annotation, so a rename behaves identically to CreateAsync's own rules
        // (analyze-triage U1) regardless of caller.
        if (request.Name is not null)
        {
            if (request.Name.Length < 2 || request.Name.Length > 30)
            {
                throw new InvalidStatusNameException();
            }

            if (project.WorkflowStatuses.Any(s => s.Id != statusId && string.Equals(s.Name, request.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new DuplicateStatusNameException();
            }

            status.Name = request.Name;
        }

        if (request.ColorKey is not null)
        {
            if (!Enum.TryParse<ChipColor>(request.ColorKey, ignoreCase: true, out var colorKey))
            {
                throw new InvalidStatusColorException();
            }

            status.ColorKey = colorKey;
        }

        await dbContext.SaveChangesAsync();

        var itemCount = await dbContext.WorkItems.CountAsync(w => w.WorkflowStatusId == statusId);
        return new WorkflowStatusDto(status.Id, status.Name, status.Category.ToString(), status.ColorKey.ToString(), status.Position, itemCount);
    }

    public async Task<List<WorkflowStatusDto>> ReorderAsync(int projectId, ReorderWorkflowStatusesRequest request)
    {
        var project = await dbContext.Projects.Include(p => p.WorkflowStatuses).FirstOrDefaultAsync(p => p.Id == projectId);
        if (project is null)
        {
            throw new ProjectNotFoundException();
        }

        var existingIds = project.WorkflowStatuses.Select(s => s.Id).ToHashSet();
        var orderedIds = request.OrderedStatusIds;

        // Must be an exact permutation of the project's current status ids -- no more,
        // no fewer, no duplicates, no unknown ids (contracts/workflow-api.md).
        if (orderedIds.Count != existingIds.Count
            || orderedIds.Distinct().Count() != orderedIds.Count
            || orderedIds.Any(id => !existingIds.Contains(id)))
        {
            throw new InvalidStatusOrderException();
        }

        for (var i = 0; i < orderedIds.Count; i++)
        {
            project.WorkflowStatuses.First(s => s.Id == orderedIds[i]).Position = i;
        }

        await dbContext.SaveChangesAsync();

        return await GetStatusesAsync(projectId);
    }

    private static ChipColor AssignColor(WorkflowStatusCategory category, int existingCountInCategory, IReadOnlySet<ChipColor> usedInCategory)
    {
        var cycle = category == WorkflowStatusCategory.Open ? OpenColorCycle : DoneColorCycle;
        foreach (var candidate in cycle)
        {
            if (!usedInCategory.Contains(candidate))
            {
                return candidate;
            }
        }

        // Every color in the cycle is already used (only reachable once a category has
        // more statuses than its own cycle's length) -- round-robin back to the start
        // rather than throwing (research.md #3: "skipping ... when possible").
        return cycle[existingCountInCategory % cycle.Length];
    }
}
