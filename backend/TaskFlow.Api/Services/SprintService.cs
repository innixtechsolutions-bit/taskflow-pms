using Microsoft.Data.SqlClient;
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

    // Start requires Planned + >=1 item + no other Active sprint in the project
    // (FR-006). The "no other Active sprint" check here is a check-then-act -- two
    // truly concurrent Start calls can both pass it before either commits -- so it is
    // backed by a DB-level filtered unique index (AppDbContext, IX_Sprints_
    // ProjectId_ActiveOnly) as the actual source of truth; the catch below only
    // translates that DB-level rejection into the same domain exception the ordinary
    // path already throws (/speckit-analyze finding C2).
    public async Task<SprintDto> StartAsync(int projectId, int sprintId)
    {
        var project = await dbContext.Projects.Include(p => p.Sprints).FirstOrDefaultAsync(p => p.Id == projectId);
        if (project is null)
        {
            throw new ProjectNotFoundException();
        }

        var sprint = project.Sprints.FirstOrDefault(s => s.Id == sprintId) ?? throw new SprintNotFoundException();

        if (sprint.Status != SprintStatus.Planned)
        {
            throw new SprintNotPlannedException();
        }

        var itemCount = await dbContext.WorkItems.CountAsync(w => w.SprintId == sprintId);
        if (itemCount == 0)
        {
            throw new EmptySprintException();
        }

        var currentlyActive = project.Sprints.FirstOrDefault(s => s.Status == SprintStatus.Active);
        if (currentlyActive is not null)
        {
            throw new AnotherSprintActiveException(currentlyActive.Name);
        }

        sprint.Status = SprintStatus.Active;
        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsActiveUniqueIndexViolation(ex))
        {
            var nowActiveName = await dbContext.Sprints
                .Where(s => s.ProjectId == projectId && s.Status == SprintStatus.Active && s.Id != sprintId)
                .Select(s => s.Name)
                .FirstOrDefaultAsync();
            throw new AnotherSprintActiveException(nowActiveName ?? "another sprint");
        }

        return new SprintDto(sprint.Id, sprint.ProjectId, sprint.Name, sprint.StartDate, sprint.EndDate, sprint.Status.ToString(), itemCount);
    }

    // SQL Server error 2601 ("Cannot insert duplicate key row ... with unique index")
    // and 2627 ("Violation of ... constraint") both signal a unique-index conflict --
    // 2601 is what a filtered unique index like this one actually raises.
    private static bool IsActiveUniqueIndexViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException sqlEx && sqlEx.Number is 2601 or 2627;

    // Complete requires Active. Not-Done items (WorkflowStatus.Category != Done) need
    // an explicit resolution only when at least one exists; Done items keep their
    // SprintId unchanged (history, FR-008). Move + status change happen in one
    // SaveChangesAsync (both succeed or neither does), the same pattern
    // ProjectStatusService.DeleteAsync already established for its own move+delete.
    public async Task<SprintDto> CompleteAsync(int projectId, int sprintId, CompleteSprintRequest request)
    {
        var project = await dbContext.Projects.Include(p => p.Sprints).FirstOrDefaultAsync(p => p.Id == projectId);
        if (project is null)
        {
            throw new ProjectNotFoundException();
        }

        var sprint = project.Sprints.FirstOrDefault(s => s.Id == sprintId) ?? throw new SprintNotFoundException();

        if (sprint.Status != SprintStatus.Active)
        {
            throw new SprintNotActiveException();
        }

        var notDoneIds = await dbContext.WorkItems
            .Where(w => w.SprintId == sprintId && w.WorkflowStatus!.Category != WorkflowStatusCategory.Done)
            .Select(w => w.Id)
            .ToListAsync();

        if (notDoneIds.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(request.Resolution))
            {
                throw new DestinationRequiredException(notDoneIds.Count);
            }

            int? destinationSprintId = null;
            if (string.Equals(request.Resolution, "Sprint", StringComparison.OrdinalIgnoreCase))
            {
                if (!request.DestinationSprintId.HasValue || request.DestinationSprintId.Value == sprintId)
                {
                    throw new InvalidDestinationSprintException();
                }

                var destination = project.Sprints.FirstOrDefault(s => s.Id == request.DestinationSprintId.Value);
                if (destination is null || destination.Status == SprintStatus.Completed)
                {
                    throw new InvalidDestinationSprintException();
                }

                destinationSprintId = destination.Id;
            }
            else if (!string.Equals(request.Resolution, "Backlog", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDestinationSprintException();
            }

            var notDoneItems = await dbContext.WorkItems.Where(w => notDoneIds.Contains(w.Id)).ToListAsync();
            foreach (var item in notDoneItems)
            {
                item.SprintId = destinationSprintId;
            }
        }

        sprint.Status = SprintStatus.Completed;
        await dbContext.SaveChangesAsync();

        var remainingItemCount = await dbContext.WorkItems.CountAsync(w => w.SprintId == sprintId);
        return new SprintDto(sprint.Id, sprint.ProjectId, sprint.Name, sprint.StartDate, sprint.EndDate, sprint.Status.ToString(), remainingItemCount);
    }

    // Delete requires Planned (which, given this feature's one-way transition graph,
    // already means "never started" -- research.md #9) and zero items.
    public async Task DeleteAsync(int projectId, int sprintId)
    {
        var project = await dbContext.Projects.Include(p => p.Sprints).FirstOrDefaultAsync(p => p.Id == projectId);
        if (project is null)
        {
            throw new ProjectNotFoundException();
        }

        var sprint = project.Sprints.FirstOrDefault(s => s.Id == sprintId) ?? throw new SprintNotFoundException();

        if (sprint.Status != SprintStatus.Planned)
        {
            throw new SprintNotDeletableException();
        }

        var hasItems = await dbContext.WorkItems.AnyAsync(w => w.SprintId == sprintId);
        if (hasItems)
        {
            throw new SprintNotDeletableException();
        }

        dbContext.Sprints.Remove(sprint);
        await dbContext.SaveChangesAsync();
    }
}
