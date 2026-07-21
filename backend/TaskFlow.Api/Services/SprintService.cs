using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Data;
using TaskFlow.Api.Data.Entities;
using TaskFlow.Api.Dtos;

namespace TaskFlow.Api.Services;

// A project's own sprints (Feature 008) -- create/list here (US1); Start/Complete/
// Delete (US4) extend this class in place, the same "build read-only-first, extend
// per story" approach WorkflowComponent/ProjectStatusService already used in
// Feature 006.
public class SprintService(AppDbContext dbContext)
{
    public async Task<SprintDto> CreateAsync(int projectId, CreateSprintRequest request)
    {
        var projectExists = await dbContext.Projects.AnyAsync(p => p.Id == projectId);
        if (!projectExists)
        {
            throw new ProjectNotFoundException();
        }

        if (request.Name.Length is < 2 or > 50)
        {
            throw new InvalidSprintNameException();
        }

        if (request.EndDate <= request.StartDate)
        {
            throw new InvalidSprintDateRangeException();
        }

        // Case-insensitive via SQL Server's default collation, same mechanism as
        // every other project-scoped name-uniqueness check in this codebase
        // (WorkflowStatus.Name, Label.Name).
        var duplicateExists = await dbContext.Sprints
            .AnyAsync(s => s.ProjectId == projectId && s.Name == request.Name);
        if (duplicateExists)
        {
            throw new DuplicateSprintNameException();
        }

        var sprint = new Sprint
        {
            ProjectId = projectId,
            Name = request.Name,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = SprintStatus.Planned
        };
        dbContext.Sprints.Add(sprint);
        await dbContext.SaveChangesAsync();

        return new SprintDto(sprint.Id, sprint.ProjectId, sprint.Name, sprint.StartDate, sprint.EndDate, sprint.Status.ToString(), ItemCount: 0);
    }

    public async Task<List<SprintDto>> GetSprintsAsync(int projectId)
    {
        var projectExists = await dbContext.Projects.AnyAsync(p => p.Id == projectId);
        if (!projectExists)
        {
            throw new ProjectNotFoundException();
        }

        return await dbContext.Sprints
            .Where(s => s.ProjectId == projectId)
            .OrderBy(s => s.StartDate)
            .Select(s => new SprintDto(
                s.Id, s.ProjectId, s.Name, s.StartDate, s.EndDate, s.Status.ToString(),
                dbContext.WorkItems.Count(w => w.SprintId == s.Id)))
            .ToListAsync();
    }
}
