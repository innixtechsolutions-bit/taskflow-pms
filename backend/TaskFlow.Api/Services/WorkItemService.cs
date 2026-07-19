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

        await ValidateParentAsync(projectId, type, request.ParentWorkItemId);

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
            UpdatedAt = now,
            ParentWorkItemId = request.ParentWorkItemId
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

        // Checked against the item's *current*, pre-update parent/children — not the
        // incoming request — so a type change is refused whenever it would strand an
        // existing relationship, independent of whatever ParentWorkItemId this same
        // request happens to carry (research.md §3).
        if (type != workItem.Type)
        {
            if (workItem.ParentWorkItemId.HasValue)
            {
                var currentParent = await dbContext.WorkItems.FindAsync(workItem.ParentWorkItemId.Value);
                if (currentParent is null || currentParent.Type != RequiredParentType(type))
                {
                    throw new TypeChangeInvalidatesParentException();
                }
            }

            var childTypes = await dbContext.WorkItems
                .Where(w => w.ParentWorkItemId == workItem.Id)
                .Select(w => w.Type)
                .Distinct()
                .ToListAsync();
            if (childTypes.Any(childType => RequiredParentType(childType) != type))
            {
                throw new TypeChangeInvalidatesChildrenException();
            }
        }

        await ValidateParentAsync(workItem.ProjectId, type, request.ParentWorkItemId);

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
        workItem.ParentWorkItemId = request.ParentWorkItemId;

        await dbContext.SaveChangesAsync();

        return await ToDtoAsync(workItem.Id);
    }

    public async Task<WorkItemDetailDto> GetByIdAsync(int id) => await ToDetailDtoAsync(id);

    // Narrower than UpdateAsync's check: the current assignee alone cannot delete
    // (FR-017/FR-018) — only the creator or a Manager/Admin. That check applies only
    // to the item being deleted, not to each descendant (FR-022) — the subtree goes
    // with it under this one authorization check.
    public async Task DeleteAsync(int callerId, string callerRole, int id)
    {
        var workItem = await dbContext.WorkItems.FindAsync(id) ?? throw new WorkItemNotFoundException();

        var isCreator = workItem.CreatedByUserId == callerId;
        var isManagerOrAdmin = callerRole is "Manager" or "Admin";
        if (!isCreator && !isManagerOrAdmin)
        {
            throw new NotAuthorizedToDeleteWorkItemException();
        }

        // SQL Server won't cascade a self-referencing FK (research.md §1), so the
        // subtree is collected and removed here, in application code, as one
        // SaveChangesAsync — not a database ON DELETE CASCADE.
        var descendantIds = await CollectDescendantIdsAsync(id);
        if (descendantIds.Count > 0)
        {
            var descendants = await dbContext.WorkItems.Where(w => descendantIds.Contains(w.Id)).ToListAsync();
            dbContext.WorkItems.RemoveRange(descendants);
        }

        dbContext.WorkItems.Remove(workItem);
        await dbContext.SaveChangesAsync();
    }

    // Breadth-first collection of every descendant id, not just direct children —
    // used both for the cascade delete above and the detail view's
    // TotalDescendantCount below. A handful of small queries (one per tree level) is
    // simpler than a recursive SQL CTE (which would need justification for raw SQL
    // per the constitution) and is cheap at this feature's scale — a hierarchy is at
    // most 3 levels deep beneath any item.
    private async Task<List<int>> CollectDescendantIdsAsync(int id)
    {
        var descendantIds = new List<int>();
        var frontier = new List<int> { id };
        while (frontier.Count > 0)
        {
            var childIds = await dbContext.WorkItems
                .Where(w => w.ParentWorkItemId != null && frontier.Contains(w.ParentWorkItemId.Value))
                .Select(w => w.Id)
                .ToListAsync();
            descendantIds.AddRange(childIds);
            frontier = childIds;
        }
        return descendantIds;
    }

    private async Task<WorkItemDetailDto> ToDetailDtoAsync(int id)
    {
        var workItem = await dbContext.WorkItems
            .Where(w => w.Id == id)
            .Select(w => new
            {
                w.Id,
                w.ProjectId,
                Type = w.Type.ToString(),
                w.Title,
                w.Description,
                Priority = w.Priority.ToString(),
                Status = w.Status.ToString(),
                w.AssigneeUserId,
                AssigneeName = w.Assignee != null ? w.Assignee.FullName : null,
                w.DueDate,
                w.CreatedByUserId,
                CreatedByName = w.CreatedBy!.FullName,
                w.CreatedAt,
                w.UpdatedAt,
                w.ParentWorkItemId,
                ParentTitle = w.ParentWorkItem != null ? w.ParentWorkItem.Title : null
            })
            .SingleOrDefaultAsync() ?? throw new WorkItemNotFoundException();

        var children = await dbContext.WorkItems
            .Where(w => w.ParentWorkItemId == id)
            .OrderByDescending(w => w.UpdatedAt)
            .Select(w => new WorkItemChildDto(
                w.Id, w.Title, w.Type.ToString(), w.Status.ToString(), w.Assignee != null ? w.Assignee.FullName : null))
            .ToListAsync();

        var descendantIds = await CollectDescendantIdsAsync(id);

        return new WorkItemDetailDto(
            workItem.Id, workItem.ProjectId, workItem.Type, workItem.Title, workItem.Description,
            workItem.Priority, workItem.Status, workItem.AssigneeUserId, workItem.AssigneeName,
            workItem.DueDate, workItem.CreatedByUserId, workItem.CreatedByName, workItem.CreatedAt, workItem.UpdatedAt,
            workItem.ParentWorkItemId, workItem.ParentTitle, descendantIds.Count, children);
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
                w.UpdatedAt,
                w.ParentWorkItemId))
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
                w.UpdatedAt,
                w.ParentWorkItemId))
            .SingleOrDefaultAsync() ?? throw new WorkItemNotFoundException();

    // The chain's rank: Epic(0) < Story(1) < Task(2) < SubTask(3). A valid parent is
    // always exactly one rank below the child — null means "no parent allowed at all"
    // (Epic only). Because rank strictly decreases walking up any parent chain, and
    // Epic (rank 0) can never itself have a parent, no item can ever reach itself
    // again by following parent references — cycles are unreachable by construction,
    // so no separate ancestor-walk/visited-set check is needed anywhere this helper
    // is used (research.md §2). The same fact means a parent-candidates query filtered
    // to just "the required type, same project" can never include the item itself or
    // any of its own descendants either, since a descendant's type is always at or
    // below the item's own rank, never one rank above it.
    private static WorkItemType? RequiredParentType(WorkItemType type) => type switch
    {
        WorkItemType.Epic => null,
        WorkItemType.Story => WorkItemType.Epic,
        WorkItemType.Task => WorkItemType.Story,
        WorkItemType.SubTask => WorkItemType.Task,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    // Story/SubTask require a parent; Task's is optional; Epic forbids one entirely.
    private static bool ParentIsRequired(WorkItemType type) => type is WorkItemType.Story or WorkItemType.SubTask;

    private async Task ValidateParentAsync(int projectId, WorkItemType type, int? parentWorkItemId)
    {
        var requiredParentType = RequiredParentType(type);

        if (requiredParentType is null)
        {
            if (parentWorkItemId.HasValue)
            {
                throw new EpicCannotHaveParentException();
            }
            return;
        }

        if (!parentWorkItemId.HasValue)
        {
            if (ParentIsRequired(type))
            {
                throw new ParentRequiredException(type);
            }
            return;
        }

        var parent = await dbContext.WorkItems.FindAsync(parentWorkItemId.Value)
            ?? throw new ParentWorkItemNotFoundException();

        if (parent.ProjectId != projectId)
        {
            throw new ParentMustBeSameProjectException();
        }

        if (parent.Type != requiredParentType.Value)
        {
            throw new InvalidParentTypeException(type, requiredParentType.Value);
        }
    }

    private record WorkItemTreeRow(int Id, string Type, string Title, string Status, string Priority, string? AssigneeName, int? ParentWorkItemId);

    public async Task<List<WorkItemTreeNodeDto>> GetTreeAsync(int projectId)
    {
        var projectExists = await dbContext.Projects.AnyAsync(p => p.Id == projectId);
        if (!projectExists)
        {
            throw new ProjectNotFoundException();
        }

        // One query for the whole project, pre-sorted — nesting happens in memory
        // below. A self-referencing tree can't be shaped by SQL alone without a
        // recursive CTE (raw SQL needs justification per the constitution), and at
        // this feature's scale (tens to low hundreds of items per project) a single
        // in-memory grouping pass is simpler and needs none (research.md §4).
        // LINQ-to-Objects' GroupBy preserves each group's original relative order, so
        // sorting once here is enough for every level of the resulting tree (§7) —
        // no per-level re-sort is needed after grouping.
        var items = await dbContext.WorkItems
            .Where(w => w.ProjectId == projectId)
            .OrderByDescending(w => w.UpdatedAt)
            .Select(w => new WorkItemTreeRow(
                w.Id,
                w.Type.ToString(),
                w.Title,
                w.Status.ToString(),
                w.Priority.ToString(),
                w.Assignee != null ? w.Assignee.FullName : null,
                w.ParentWorkItemId))
            .ToListAsync();

        var childrenByParent = items
            .Where(w => w.ParentWorkItemId.HasValue)
            .GroupBy(w => w.ParentWorkItemId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var roots = items.Where(w => w.ParentWorkItemId is null);
        return roots.Select(r => BuildTreeNode(r, childrenByParent)).ToList();
    }

    private static WorkItemTreeNodeDto BuildTreeNode(WorkItemTreeRow row, Dictionary<int, List<WorkItemTreeRow>> childrenByParent)
    {
        var childRows = childrenByParent.TryGetValue(row.Id, out var found) ? found : [];
        var doneCount = childRows.Count(c => c.Status == "Done");
        var childNodes = childRows.Select(c => BuildTreeNode(c, childrenByParent)).ToList();

        return new WorkItemTreeNodeDto(
            row.Id, row.Type, row.Title, row.Status, row.Priority, row.AssigneeName,
            childRows.Count, doneCount, childNodes);
    }

    private record WorkItemBoardRow(
        int Id, string Type, string Title, string Status, string Priority,
        int? AssigneeUserId, string? AssigneeName, DateTime? DueDate, DateTime UpdatedAt,
        int CreatedByUserId, int? ParentWorkItemId);

    // Feature 005 (Kanban Board). Same shape as GetTreeAsync above: one query for
    // the whole project, then an in-memory Dictionary/GroupBy pass -- but applied
    // to compute DirectChildrenCount/DirectChildrenDoneCount for *every* item, not
    // just tree roots, since the board shows every item as its own card regardless
    // of depth (research.md #2).
    public async Task<WorkItemBoardDto> GetBoardAsync(int projectId)
    {
        var projectExists = await dbContext.Projects.AnyAsync(p => p.Id == projectId);
        if (!projectExists)
        {
            throw new ProjectNotFoundException();
        }

        var rows = await dbContext.WorkItems
            .Where(w => w.ProjectId == projectId)
            .OrderByDescending(w => w.UpdatedAt)
            .Select(w => new WorkItemBoardRow(
                w.Id,
                w.Type.ToString(),
                w.Title,
                w.Status.ToString(),
                w.Priority.ToString(),
                w.AssigneeUserId,
                w.Assignee != null ? w.Assignee.FullName : null,
                w.DueDate,
                w.UpdatedAt,
                w.CreatedByUserId,
                w.ParentWorkItemId))
            .ToListAsync();

        var childrenByParent = rows
            .Where(w => w.ParentWorkItemId.HasValue)
            .GroupBy(w => w.ParentWorkItemId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var items = rows.Select(row =>
        {
            var childRows = childrenByParent.TryGetValue(row.Id, out var found) ? found : [];
            var doneCount = childRows.Count(c => c.Status == "Done");
            return new WorkItemBoardCardDto(
                row.Id, row.Type, row.Title, row.Status, row.Priority,
                row.AssigneeUserId, row.AssigneeName, row.DueDate, row.UpdatedAt,
                row.CreatedByUserId, childRows.Count, doneCount);
        }).ToList();

        // Enum.GetValues preserves declaration order (ToDo, InProgress, InReview,
        // Done), so this list is already the board's intended column order without
        // a separately-maintained array that could drift from the enum itself.
        var columns = Enum.GetValues<WorkItemStatus>()
            .Select(status => new BoardColumnDto(status.ToString(), BoardColumnLabel(status)))
            .ToList();

        return new WorkItemBoardDto(columns, items);
    }

    // Mirrors the frontend's StatusChipComponent label map exactly (M1) -- the
    // one place this feature intentionally accepts label text existing in two
    // places, since chips and board columns are conceptually different things
    // long-term (research.md #2, revised).
    private static string BoardColumnLabel(WorkItemStatus status) => status switch
    {
        WorkItemStatus.ToDo => "To Do",
        WorkItemStatus.InProgress => "In Progress",
        WorkItemStatus.InReview => "In Review",
        WorkItemStatus.Done => "Done",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    public async Task<List<WorkItemLookupItemDto>> GetParentCandidatesAsync(int projectId, string type)
    {
        var projectExists = await dbContext.Projects.AnyAsync(p => p.Id == projectId);
        if (!projectExists)
        {
            throw new ProjectNotFoundException();
        }

        if (!Enum.TryParse<WorkItemType>(type, ignoreCase: true, out var typeValue))
        {
            throw new InvalidWorkItemTypeException();
        }

        var requiredParentType = RequiredParentType(typeValue);
        if (requiredParentType is null)
        {
            // Epic never has a parent — an always-empty list, not an error, so the
            // frontend can simply disable the picker rather than special-case this.
            return [];
        }

        return await dbContext.WorkItems
            .Where(w => w.ProjectId == projectId && w.Type == requiredParentType.Value)
            .Select(w => new WorkItemLookupItemDto(w.Id, w.Title))
            .ToListAsync();
    }
}
