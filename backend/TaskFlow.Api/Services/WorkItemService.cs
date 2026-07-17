using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Data;
using TaskFlow.Api.Data.Entities;
using TaskFlow.Api.Dtos;

namespace TaskFlow.Api.Services;

public class WorkItemService(AppDbContext dbContext)
{
    public async Task<WorkItemDto> CreateAsync(int creatorUserId, int projectId, WorkItemRequest request)
    {
        var projectExists = await dbContext.Projects.AnyAsync(p => p.Id == projectId);
        if (!projectExists)
        {
            throw new ProjectNotFoundException();
        }

        if (!Enum.TryParse<WorkItemType>(request.Type, ignoreCase: true, out var type))
        {
            throw new InvalidWorkItemTypeException();
        }

        // Priority/Status are optional in the request — default to Medium/ToDo when
        // omitted, but still reject a supplied value that isn't a real enum member.
        var priority = WorkItemPriority.Medium;
        if (!string.IsNullOrWhiteSpace(request.Priority) && !Enum.TryParse(request.Priority, ignoreCase: true, out priority))
        {
            throw new InvalidWorkItemPriorityException();
        }

        var status = WorkItemStatus.ToDo;
        if (!string.IsNullOrWhiteSpace(request.Status) && !Enum.TryParse(request.Status, ignoreCase: true, out status))
        {
            throw new InvalidWorkItemStatusException();
        }

        if (request.AssigneeUserId.HasValue)
        {
            var assigneeExists = await dbContext.Users.AnyAsync(u => u.Id == request.AssigneeUserId.Value);
            if (!assigneeExists)
            {
                throw new AssigneeNotFoundException();
            }
        }

        var now = DateTime.UtcNow;
        var workItem = new WorkItem
        {
            ProjectId = projectId,
            Type = type,
            Title = request.Title,
            Description = request.Description,
            Priority = priority,
            Status = status,
            AssigneeUserId = request.AssigneeUserId,
            DueDate = request.DueDate,
            CreatedByUserId = creatorUserId,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.WorkItems.Add(workItem);
        await dbContext.SaveChangesAsync();

        return await ToDtoAsync(workItem.Id);
    }

    private async Task<WorkItemDto> ToDtoAsync(int id) =>
        await dbContext.WorkItems
            .Where(w => w.Id == id)
            .Select(w => new WorkItemDto(
                w.Id,
                w.ProjectId,
                w.Type.ToString(),
                w.Title,
                w.Description,
                w.Priority.ToString(),
                w.Status.ToString(),
                w.AssigneeUserId,
                w.Assignee != null ? w.Assignee.FullName : null,
                w.DueDate,
                w.CreatedByUserId,
                w.CreatedBy!.FullName,
                w.CreatedAt,
                w.UpdatedAt))
            .SingleAsync();
}
