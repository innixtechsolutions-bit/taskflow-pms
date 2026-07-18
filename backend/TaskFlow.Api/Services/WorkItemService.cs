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

    // Caller's role/ownership checked here, not via an [Authorize] attribute — "the
    // caller is this item's creator or current assignee" isn't expressible as a role
    // (research.md §1), the same shape as UserService.ChangeRoleAsync's last-admin guard.
    public async Task<WorkItemDto> UpdateAsync(int callerId, string callerRole, int id, WorkItemRequest request)
    {
        var workItem = await dbContext.WorkItems.FindAsync(id) ?? throw new WorkItemNotFoundException();

        var isCreator = workItem.CreatedByUserId == callerId;
        var isCurrentAssignee = workItem.AssigneeUserId == callerId;
        var isManagerOrAdmin = callerRole is "Manager" or "Admin";
        if (!isCreator && !isCurrentAssignee && !isManagerOrAdmin)
        {
            throw new NotAuthorizedToEditWorkItemException();
        }

        if (!Enum.TryParse<WorkItemType>(request.Type, ignoreCase: true, out var type))
        {
            throw new InvalidWorkItemTypeException();
        }

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

        // ProjectId is never assigned here — it's immutable after creation (FR-014) and
        // WorkItemRequest doesn't even carry one, so there's no path that could change it.
        workItem.Type = type;
        workItem.Title = request.Title;
        workItem.Description = request.Description;
        workItem.Priority = priority;
        workItem.Status = status;
        workItem.AssigneeUserId = request.AssigneeUserId;
        workItem.DueDate = request.DueDate;
        workItem.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        return await ToDtoAsync(workItem.Id);
    }

    public async Task<WorkItemDto> GetByIdAsync(int id) => await ToDtoAsync(id);

    // Narrower than UpdateAsync's check: the current assignee alone cannot delete
    // (FR-017/FR-018) — only the creator or a Manager/Admin.
    public async Task DeleteAsync(int callerId, string callerRole, int id)
    {
        var workItem = await dbContext.WorkItems.FindAsync(id) ?? throw new WorkItemNotFoundException();

        var isCreator = workItem.CreatedByUserId == callerId;
        var isManagerOrAdmin = callerRole is "Manager" or "Admin";
        if (!isCreator && !isManagerOrAdmin)
        {
            throw new NotAuthorizedToDeleteWorkItemException();
        }

        dbContext.WorkItems.Remove(workItem);
        await dbContext.SaveChangesAsync();
    }

    // Bare-minimum listing (pulled forward into US4 since edit/delete controls need
    // rows to render next to — tasks.md's discovered-dependency note) extended here
    // with the full filter/search set.
    public async Task<PagedResult<WorkItemDto>> GetWorkItemsAsync(
        int projectId, int page, int pageSize,
        string? status, string? type, string? priority, int? assigneeUserId, string? search)
    {
        var projectExists = await dbContext.Projects.AnyAsync(p => p.Id == projectId);
        if (!projectExists)
        {
            throw new ProjectNotFoundException();
        }

        // Each .Where() below only appends a predicate to this query's expression tree —
        // nothing touches the database until it's enumerated (.CountAsync()/.ToListAsync()
        // further down), so five conditionally-appended .Where() calls still produce one
        // SQL query with up to five AND conditions, not five separate round-trips
        // (research.md §4).
        var query = dbContext.WorkItems.Where(w => w.ProjectId == projectId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<WorkItemStatus>(status, ignoreCase: true, out var statusValue))
            {
                throw new InvalidWorkItemStatusException();
            }
            query = query.Where(w => w.Status == statusValue);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            if (!Enum.TryParse<WorkItemType>(type, ignoreCase: true, out var typeValue))
            {
                throw new InvalidWorkItemTypeException();
            }
            query = query.Where(w => w.Type == typeValue);
        }

        if (!string.IsNullOrWhiteSpace(priority))
        {
            if (!Enum.TryParse<WorkItemPriority>(priority, ignoreCase: true, out var priorityValue))
            {
                throw new InvalidWorkItemPriorityException();
            }
            query = query.Where(w => w.Priority == priorityValue);
        }

        if (assigneeUserId.HasValue)
        {
            query = query.Where(w => w.AssigneeUserId == assigneeUserId.Value);
        }

        // Case-insensitive via SQL Server's default collation, same mechanism as
        // Project.Name/User.Email elsewhere in this codebase — no extra call needed.
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(w => w.Title.Contains(search));
        }

        // Clamped, never rejected (spec.md Edge Cases) — a caller asking for too much
        // just gets the maximum instead of an error.
        pageSize = Math.Min(pageSize, 100);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(w => w.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
            .ToListAsync();

        return new PagedResult<WorkItemDto>(items, page, pageSize, totalCount);
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
            .SingleOrDefaultAsync() ?? throw new WorkItemNotFoundException();
}
