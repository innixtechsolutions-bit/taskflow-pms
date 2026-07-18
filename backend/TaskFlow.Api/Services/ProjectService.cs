using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Data;
using TaskFlow.Api.Data.Entities;
using TaskFlow.Api.Dtos;

namespace TaskFlow.Api.Services;

public class ProjectService(AppDbContext dbContext)
{
    public async Task<ProjectDetailDto> CreateAsync(int creatorUserId, ProjectRequest request)
    {
        // Case-insensitive because it relies on SQL Server's default collation, the
        // same mechanism as User.Email's uniqueness in Feature 001 — see data-model.md.
        var nameExists = await dbContext.Projects.AnyAsync(p => p.Name == request.Name);
        if (nameExists)
        {
            throw new DuplicateProjectNameException();
        }

        var creator = await dbContext.Users.FindAsync(creatorUserId);

        var project = new Project
        {
            Name = request.Name,
            Description = request.Description,
            CreatedByUserId = creatorUserId,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync();

        return new ProjectDetailDto(project.Id, project.Name, project.Description, creator!.FullName, project.CreatedAt, TotalWorkItemCount: 0);
    }

    public async Task<PagedResult<ProjectListItemDto>> GetProjectsAsync(int page, int pageSize)
    {
        var query = dbContext.Projects.OrderByDescending(p => p.CreatedAt);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProjectListItemDto(
                p.Id,
                p.Name,
                p.CreatedBy!.FullName,
                p.CreatedAt,
                p.WorkItems.Count(w => w.Status != WorkItemStatus.Done)))
            .ToListAsync();

        return new PagedResult<ProjectListItemDto>(items, page, pageSize, totalCount);
    }

    public async Task<ProjectDetailDto> GetProjectByIdAsync(int id)
    {
        var project = await dbContext.Projects
            .Where(p => p.Id == id)
            .Select(p => new ProjectDetailDto(
                p.Id,
                p.Name,
                p.Description,
                p.CreatedBy!.FullName,
                p.CreatedAt,
                p.WorkItems.Count))
            .SingleOrDefaultAsync();

        return project ?? throw new ProjectNotFoundException();
    }

    public async Task<ProjectDetailDto> UpdateAsync(int id, ProjectRequest request)
    {
        var project = await dbContext.Projects.FindAsync(id) ?? throw new ProjectNotFoundException();

        // Excludes the project being edited itself — renaming a project to the name it
        // already has must not be treated as a duplicate.
        var nameExists = await dbContext.Projects.AnyAsync(p => p.Id != id && p.Name == request.Name);
        if (nameExists)
        {
            throw new DuplicateProjectNameException();
        }

        project.Name = request.Name;
        project.Description = request.Description;
        await dbContext.SaveChangesAsync();

        return new ProjectDetailDto(
            project.Id, project.Name, project.Description,
            (await dbContext.Users.FindAsync(project.CreatedByUserId))!.FullName,
            project.CreatedAt,
            await dbContext.WorkItems.CountAsync(w => w.ProjectId == id));
    }

    // Deleting the project cascades to its work items (DeleteBehavior.Cascade,
    // research.md §2, FR-009) — no separate cleanup of WorkItems needed here.
    public async Task DeleteAsync(int id)
    {
        var project = await dbContext.Projects.FindAsync(id) ?? throw new ProjectNotFoundException();

        dbContext.Projects.Remove(project);
        await dbContext.SaveChangesAsync();
    }
}
