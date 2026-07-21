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

        var statusId = await ResolveStatusIdAsync(projectId, request.StatusId);

        if (request.AssigneeUserId.HasValue)
        {
            var assigneeExists = await dbContext.Users.AnyAsync(u => u.Id == request.AssigneeUserId.Value);
            if (!assigneeExists)
            {
                throw new AssigneeNotFoundException();
            }
        }

        await ValidateParentAsync(projectId, type, request.ParentWorkItemId);
        EnsureValidDateRange(request.StartDate, request.DueDate);
        var labels = await NormalizeAndAttachLabelsAsync(projectId, request.Labels);
        var sprintId = await ResolveSprintIdAsync(projectId, type, request.SprintId, currentSprintId: null);

        var now = DateTime.UtcNow;
        var workItem = new WorkItem
        {
            ProjectId = projectId,
            Type = type,
            Title = request.Title,
            Description = request.Description,
            Priority = priority,
            WorkflowStatusId = statusId,
            AssigneeUserId = request.AssigneeUserId,
            DueDate = request.DueDate,
            StartDate = request.StartDate,
            CreatedByUserId = creatorUserId,
            CreatedAt = now,
            UpdatedAt = now,
            ParentWorkItemId = request.ParentWorkItemId,
            Labels = labels,
            SprintId = sprintId
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
        EnsureCanEdit(workItem, callerId, callerRole);

        if (!Enum.TryParse<WorkItemType>(request.Type, ignoreCase: true, out var type))
        {
            throw new InvalidWorkItemTypeException();
        }

        var priority = WorkItemPriority.Medium;
        if (!string.IsNullOrWhiteSpace(request.Priority) && !Enum.TryParse(request.Priority, ignoreCase: true, out priority))
        {
            throw new InvalidWorkItemPriorityException();
        }

        var statusId = await ResolveStatusIdAsync(workItem.ProjectId, request.StatusId);

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
        EnsureValidDateRange(request.StartDate, request.DueDate);
        var newLabels = await NormalizeAndAttachLabelsAsync(workItem.ProjectId, request.Labels);
        var sprintId = await ResolveSprintIdAsync(workItem.ProjectId, type, request.SprintId, workItem.SprintId);

        // ProjectId is never assigned here — it's immutable after creation (FR-014) and
        // WorkItemRequest doesn't even carry one, so there's no path that could change it.
        workItem.Type = type;
        workItem.Title = request.Title;
        workItem.Description = request.Description;
        workItem.Priority = priority;
        workItem.WorkflowStatusId = statusId;
        workItem.AssigneeUserId = request.AssigneeUserId;
        workItem.DueDate = request.DueDate;
        workItem.StartDate = request.StartDate;
        workItem.UpdatedAt = DateTime.UtcNow;
        workItem.ParentWorkItemId = request.ParentWorkItemId;
        workItem.SprintId = sprintId;

        // Replace-the-whole-set (PUT semantics, same as every other field here) --
        // every existing attachment for this item is removed and the newly
        // normalized set is attached in its place, rather than diffing the two.
        var existingLabelRows = await dbContext.WorkItemLabels.Where(wl => wl.WorkItemId == workItem.Id).ToListAsync();
        dbContext.WorkItemLabels.RemoveRange(existingLabelRows);
        foreach (var wl in newLabels)
        {
            wl.WorkItemId = workItem.Id;
        }
        dbContext.WorkItemLabels.AddRange(newLabels);

        await dbContext.SaveChangesAsync();

        return await ToDtoAsync(workItem.Id);
    }

    // Feature 005 (Kanban Board). A field-scoped sibling to UpdateAsync above --
    // reuses the exact same authorization rule (EnsureCanEdit) but only ever
    // touches Status, so the board's drag interaction never has to submit (and
    // risk silently clobbering) fields a card doesn't carry, like Description or
    // ParentWorkItemId (research.md #3).
    public async Task<WorkItemDto> UpdateStatusAsync(int callerId, string callerRole, int id, int statusId)
    {
        var workItem = await dbContext.WorkItems.FindAsync(id) ?? throw new WorkItemNotFoundException();
        EnsureCanEdit(workItem, callerId, callerRole);

        // Rejects a statusId that doesn't belong to this item's own project -- an
        // explicit id, unlike Create/Update, so no "default when omitted" case here.
        workItem.WorkflowStatusId = await ResolveStatusIdAsync(workItem.ProjectId, statusId);
        workItem.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        return await ToDtoAsync(workItem.Id);
    }

    // Feature 008 (US3). A field-scoped sibling to UpdateAsync, the same shape as
    // UpdateStatusAsync just above -- the Backlog view's drag interaction only ever
    // changes SprintId, never any other field (research.md #4).
    public async Task<WorkItemDto> UpdateSprintAsync(int callerId, string callerRole, int id, int? sprintId)
    {
        var workItem = await dbContext.WorkItems.FindAsync(id) ?? throw new WorkItemNotFoundException();
        EnsureCanEdit(workItem, callerId, callerRole);

        workItem.SprintId = await ResolveSprintIdAsync(workItem.ProjectId, workItem.Type, sprintId, workItem.SprintId);
        workItem.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        return await ToDtoAsync(workItem.Id);
    }

    // Shared by Create/Update/UpdateStatus. A null requestedStatusId means "use the
    // project's default" -- its first Open-category status by position, mirroring
    // this field's old "defaults to ToDo" behavior before Feature 006. An explicit
    // value must belong to the target project (Feature 006/FR-018/research.md #7 --
    // status is identity-based, and identities aren't shared across projects).
    private async Task<int> ResolveStatusIdAsync(int projectId, int? requestedStatusId)
    {
        if (requestedStatusId.HasValue)
        {
            var belongsToProject = await dbContext.WorkflowStatuses
                .AnyAsync(s => s.Id == requestedStatusId.Value && s.ProjectId == projectId);
            if (!belongsToProject)
            {
                throw new InvalidWorkItemStatusException();
            }
            return requestedStatusId.Value;
        }

        var defaultStatus = await dbContext.WorkflowStatuses
            .Where(s => s.ProjectId == projectId && s.Category == WorkflowStatusCategory.Open)
            .OrderBy(s => s.Position)
            .FirstOrDefaultAsync();
        // FR-003 guarantees every project always has >=1 Open-category status, so this
        // should never be null in practice -- InvalidWorkItemStatusException here would
        // only mean the project itself doesn't exist, which callers already check first.
        return defaultStatus?.Id ?? throw new InvalidWorkItemStatusException();
    }

    // Enforced only when both dates are set (US3) -- a start date with no due date,
    // or a due date with no start date, is unconstrained.
    private static void EnsureValidDateRange(DateTime? startDate, DateTime? dueDate)
    {
        if (startDate.HasValue && dueDate.HasValue && startDate.Value > dueDate.Value)
        {
            throw new InvalidDateRangeException();
        }
    }

    // Feature 008 (US2/US3) — shared by CreateAsync/UpdateAsync/UpdateSprintAsync
    // (data-model.md's validation table). currentSprintId is the item's sprint
    // *before* this call — null on create. When the caller is moving the item away
    // from its current sprint (clearing it or reassigning elsewhere), that current
    // sprint must not be Completed (FR-009's read-only rule applies to both sides of
    // a move, not just the destination). Returns the resolved SprintId to assign
    // (null means "no sprint").
    private async Task<int?> ResolveSprintIdAsync(int projectId, WorkItemType type, int? requestedSprintId, int? currentSprintId)
    {
        if (currentSprintId.HasValue && currentSprintId != requestedSprintId)
        {
            var currentSprint = await dbContext.Sprints.FindAsync(currentSprintId.Value);
            if (currentSprint?.Status == SprintStatus.Completed)
            {
                throw new SprintReadOnlyException();
            }
        }

        if (!requestedSprintId.HasValue)
        {
            return null;
        }

        if (type == WorkItemType.Epic)
        {
            throw new EpicCannotBeInSprintException();
        }

        var sprint = await dbContext.Sprints.FirstOrDefaultAsync(s => s.Id == requestedSprintId.Value && s.ProjectId == projectId);
        if (sprint is null)
        {
            throw new SprintNotFoundException();
        }
        if (sprint.Status == SprintStatus.Completed)
        {
            throw new SprintReadOnlyException();
        }

        return sprint.Id;
    }

    // Shared by Create/UpdateAsync (US5, data-model.md's Label validation rules).
    // Trims, rejects out-of-range names, dedupes case-insensitively within the
    // request, caps at 5, and finds-or-creates each project-scoped Label row —
    // never attaches the same label twice and never duplicates an existing one
    // on a case-insensitive name match. Omitted/null requestedNames means "no
    // labels", matching every other optional field on this PUT-replaces-the-
    // resource request.
    private async Task<List<WorkItemLabel>> NormalizeAndAttachLabelsAsync(int projectId, List<string>? requestedNames)
    {
        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in requestedNames ?? [])
        {
            var trimmed = raw.Trim();
            if (trimmed.Length is < 1 or > 30)
            {
                throw new InvalidLabelException();
            }
            if (seen.Add(trimmed))
            {
                normalized.Add(trimmed);
            }
        }
        if (normalized.Count > 5)
        {
            throw new TooManyLabelsException();
        }

        var result = new List<WorkItemLabel>();
        foreach (var name in normalized)
        {
            // Case-insensitive via SQL Server's default collation, same mechanism as
            // every other name-uniqueness lookup in this codebase (research.md #4).
            var label = await dbContext.Labels.FirstOrDefaultAsync(l => l.ProjectId == projectId && l.Name == name);
            if (label is null)
            {
                label = new Label { ProjectId = projectId, Name = name, CreatedAt = DateTime.UtcNow };
                dbContext.Labels.Add(label);
            }
            result.Add(new WorkItemLabel { Label = label });
        }
        return result;
    }

    // US5 — labels currently attached to >=1 work item, for the modal's
    // autocomplete suggestions and the List view's filter dropdown
    // (research.md #5: query-time filter, no orphan-cleanup bookkeeping).
    public async Task<List<string>> GetProjectLabelsAsync(int projectId)
    {
        var projectExists = await dbContext.Projects.AnyAsync(p => p.Id == projectId);
        if (!projectExists)
        {
            throw new ProjectNotFoundException();
        }

        return await dbContext.Labels
            .Where(l => l.ProjectId == projectId && l.WorkItemLabels.Any())
            .OrderBy(l => l.Name)
            .Select(l => l.Name)
            .ToListAsync();
    }

    // Shared by UpdateAsync and UpdateStatusAsync -- "the caller is this item's
    // creator or current assignee" isn't expressible as a role (research.md §1),
    // so it's checked here once rather than duplicated between the two update
    // paths.
    private static void EnsureCanEdit(WorkItem workItem, int callerId, string callerRole)
    {
        var isCreator = workItem.CreatedByUserId == callerId;
        var isCurrentAssignee = workItem.AssigneeUserId == callerId;
        var isManagerOrAdmin = callerRole is "Manager" or "Admin";
        if (!isCreator && !isCurrentAssignee && !isManagerOrAdmin)
        {
            throw new NotAuthorizedToEditWorkItemException();
        }
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
                StatusId = w.WorkflowStatusId,
                StatusName = w.WorkflowStatus!.Name,
                StatusCategory = w.WorkflowStatus.Category.ToString(),
                StatusColorKey = w.WorkflowStatus.ColorKey.ToString(),
                w.AssigneeUserId,
                AssigneeName = w.Assignee != null ? w.Assignee.FullName : null,
                w.DueDate,
                w.StartDate,
                w.CreatedByUserId,
                CreatedByName = w.CreatedBy!.FullName,
                w.CreatedAt,
                w.UpdatedAt,
                w.ParentWorkItemId,
                ParentTitle = w.ParentWorkItem != null ? w.ParentWorkItem.Title : null,
                Labels = w.Labels.OrderBy(wl => wl.Label!.Name).Select(wl => wl.Label!.Name).ToList(),
                w.SprintId,
                SprintName = w.Sprint != null ? w.Sprint.Name : null
            })
            .SingleOrDefaultAsync() ?? throw new WorkItemNotFoundException();

        var children = await dbContext.WorkItems
            .Where(w => w.ParentWorkItemId == id)
            .OrderByDescending(w => w.UpdatedAt)
            .Select(w => new WorkItemChildDto(
                w.Id, w.Title, w.Type.ToString(), w.WorkflowStatusId, w.WorkflowStatus!.Name,
                w.WorkflowStatus.Category.ToString(), w.WorkflowStatus.ColorKey.ToString(),
                w.Assignee != null ? w.Assignee.FullName : null))
            .ToListAsync();

        var descendantIds = await CollectDescendantIdsAsync(id);

        return new WorkItemDetailDto(
            workItem.Id, workItem.ProjectId, workItem.Type, workItem.Title, workItem.Description,
            workItem.Priority, workItem.StatusId, workItem.StatusName, workItem.StatusCategory, workItem.StatusColorKey,
            workItem.AssigneeUserId, workItem.AssigneeName,
            workItem.DueDate, workItem.StartDate, workItem.CreatedByUserId, workItem.CreatedByName, workItem.CreatedAt, workItem.UpdatedAt,
            workItem.ParentWorkItemId, workItem.ParentTitle, descendantIds.Count, children, workItem.Labels,
            workItem.SprintId, workItem.SprintName);
    }

    // Bare-minimum listing (pulled forward into US4 since edit/delete controls need
    // rows to render next to — tasks.md's discovered-dependency note) extended here
    // with the full filter/search set.
    public async Task<PagedResult<WorkItemDto>> GetWorkItemsAsync(
        int projectId, int page, int pageSize,
        int? statusId, string? type, string? priority, int? assigneeUserId, string? search, string? label = null)
    {
        var projectExists = await dbContext.Projects.AnyAsync(p => p.Id == projectId);
        if (!projectExists)
        {
            throw new ProjectNotFoundException();
        }

        var query = BuildFilteredQuery(projectId, statusId, type, priority, assigneeUserId, search, label);

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
                w.WorkflowStatusId,
                w.WorkflowStatus!.Name,
                w.WorkflowStatus.Category.ToString(),
                w.WorkflowStatus.ColorKey.ToString(),
                w.AssigneeUserId,
                w.Assignee != null ? w.Assignee.FullName : null,
                w.DueDate,
                w.StartDate,
                w.CreatedByUserId,
                w.CreatedBy!.FullName,
                w.CreatedAt,
                w.UpdatedAt,
                w.ParentWorkItemId,
                w.Labels.OrderBy(wl => wl.Label!.Name).Select(wl => wl.Label!.Name).ToList(),
                w.SprintId,
                w.Sprint != null ? w.Sprint.Name : null))
            .ToListAsync();

        return new PagedResult<WorkItemDto>(items, page, pageSize, totalCount);
    }

    // Feature 008 (research.md #5) — extracted out of GetWorkItemsAsync so it and
    // GetBacklogAsync share one WHERE-clause definition instead of maintaining two
    // near-identical filter chains that could silently drift apart. Each .Where()
    // below only appends a predicate to the expression tree -- nothing touches the
    // database until the caller enumerates it.
    private IQueryable<WorkItem> BuildFilteredQuery(
        int projectId, int? statusId, string? type, string? priority, int? assigneeUserId, string? search, string? label)
    {
        var query = dbContext.WorkItems.Where(w => w.ProjectId == projectId);

        if (statusId.HasValue)
        {
            query = query.Where(w => w.WorkflowStatusId == statusId.Value);
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

        // US5 — case-insensitive exact match within the project (same collation
        // mechanism as every other name lookup here), AND-ed with the filters above.
        if (!string.IsNullOrWhiteSpace(label))
        {
            query = query.Where(w => w.Labels.Any(wl => wl.Label!.Name == label));
        }

        return query;
    }

    // Feature 008 (US2) — one query for the whole project's filtered items (same
    // predicate GetWorkItemsAsync uses, projected straight to WorkItemDto the same
    // way GetWorkItemsAsync itself does — a method call like a shared "ToDto" helper
    // can't appear inside an EF Core Select(), so the projection is written out
    // in full here as well), then an in-memory grouping pass by SprintId -- the same
    // "one query + Dictionary/GroupBy" shape GetBoardAsync/GetTreeAsync already use
    // for their own groupings (research.md #5). Items with SprintId == null
    // (including every Epic, which can never have one) land in BacklogItems.
    public async Task<WorkItemBacklogDto> GetBacklogAsync(
        int projectId, int? statusId, string? type, string? priority, int? assigneeUserId, string? search, string? label = null)
    {
        var projectExists = await dbContext.Projects.AnyAsync(p => p.Id == projectId);
        if (!projectExists)
        {
            throw new ProjectNotFoundException();
        }

        var query = BuildFilteredQuery(projectId, statusId, type, priority, assigneeUserId, search, label);

        var items = await query
            .OrderByDescending(w => w.UpdatedAt)
            .Select(w => new
            {
                w.SprintId,
                Dto = new WorkItemDto(
                    w.Id, w.ProjectId, w.Type.ToString(), w.Title, w.Description, w.Priority.ToString(),
                    w.WorkflowStatusId, w.WorkflowStatus!.Name, w.WorkflowStatus.Category.ToString(), w.WorkflowStatus.ColorKey.ToString(),
                    w.AssigneeUserId, w.Assignee != null ? w.Assignee.FullName : null,
                    w.DueDate, w.StartDate, w.CreatedByUserId, w.CreatedBy!.FullName, w.CreatedAt, w.UpdatedAt,
                    w.ParentWorkItemId, w.Labels.OrderBy(wl => wl.Label!.Name).Select(wl => wl.Label!.Name).ToList(),
                    w.SprintId, w.Sprint != null ? w.Sprint.Name : null)
            })
            .ToListAsync();

        var itemsBySprint = items
            .Where(i => i.SprintId.HasValue)
            .GroupBy(i => i.SprintId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(i => i.Dto).ToList());

        var sprints = await dbContext.Sprints
            .Where(s => s.ProjectId == projectId)
            .OrderBy(s => s.StartDate)
            .Select(s => new { s.Id, s.Name, s.StartDate, s.EndDate, Status = s.Status.ToString() })
            .ToListAsync();

        var sections = sprints
            .Select(s => new BacklogSprintSectionDto(
                s.Id, s.Name, s.StartDate, s.EndDate, s.Status,
                itemsBySprint.TryGetValue(s.Id, out var sprintItems) ? sprintItems : []))
            .ToList();

        var backlogItems = items.Where(i => !i.SprintId.HasValue).Select(i => i.Dto).ToList();

        return new WorkItemBacklogDto(sections, backlogItems);
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
                w.WorkflowStatusId,
                w.WorkflowStatus!.Name,
                w.WorkflowStatus.Category.ToString(),
                w.WorkflowStatus.ColorKey.ToString(),
                w.AssigneeUserId,
                w.Assignee != null ? w.Assignee.FullName : null,
                w.DueDate,
                w.StartDate,
                w.CreatedByUserId,
                w.CreatedBy!.FullName,
                w.CreatedAt,
                w.UpdatedAt,
                w.ParentWorkItemId,
                w.Labels.OrderBy(wl => wl.Label!.Name).Select(wl => wl.Label!.Name).ToList(),
                w.SprintId,
                w.Sprint != null ? w.Sprint.Name : null))
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

    private record WorkItemTreeRow(int Id, string Type, string Title, int StatusId, string StatusName, string StatusCategory, string StatusColorKey, string Priority, string? AssigneeName, int? ParentWorkItemId, List<string> Labels);

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
                w.WorkflowStatusId,
                w.WorkflowStatus!.Name,
                w.WorkflowStatus.Category.ToString(),
                w.WorkflowStatus.ColorKey.ToString(),
                w.Priority.ToString(),
                w.Assignee != null ? w.Assignee.FullName : null,
                w.ParentWorkItemId,
                w.Labels.OrderBy(wl => wl.Label!.Name).Select(wl => wl.Label!.Name).ToList()))
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
        // Category, not name (FR-019/spec.md) — a renamed or custom Done-category
        // status still counts as done here.
        var doneCount = childRows.Count(c => c.StatusCategory == nameof(WorkflowStatusCategory.Done));
        var childNodes = childRows.Select(c => BuildTreeNode(c, childrenByParent)).ToList();

        return new WorkItemTreeNodeDto(
            row.Id, row.Type, row.Title, row.StatusId, row.StatusName, row.StatusCategory, row.StatusColorKey,
            row.Priority, row.AssigneeName, childRows.Count, doneCount, childNodes, row.Labels);
    }

    private record WorkItemBoardRow(
        int Id, string Type, string Title, int StatusId, string StatusName, string StatusCategory, string StatusColorKey, string Priority,
        int? AssigneeUserId, string? AssigneeName, DateTime? DueDate, DateTime UpdatedAt,
        int CreatedByUserId, int? ParentWorkItemId, List<string> Labels);

    // Feature 005 (Kanban Board). Same shape as GetTreeAsync above: one query for
    // the whole project, then an in-memory Dictionary/GroupBy pass -- but applied
    // to compute DirectChildrenCount/DirectChildrenDoneCount for *every* item, not
    // just tree roots, since the board shows every item as its own card regardless
    // of depth (research.md #2).
    // Feature 008 (US5) — sprintId is optional; when present, only that sprint's
    // items are included and the column list is unaffected (spec FR-017/FR-020) —
    // "All items" mode (sprintId omitted) is the exact same query as before this
    // feature, unchanged.
    public async Task<WorkItemBoardDto> GetBoardAsync(int projectId, int? sprintId = null)
    {
        var projectExists = await dbContext.Projects.AnyAsync(p => p.Id == projectId);
        if (!projectExists)
        {
            throw new ProjectNotFoundException();
        }

        var query = dbContext.WorkItems.Where(w => w.ProjectId == projectId);
        if (sprintId.HasValue)
        {
            query = query.Where(w => w.SprintId == sprintId.Value);
        }

        var rows = await query
            .OrderByDescending(w => w.UpdatedAt)
            .Select(w => new WorkItemBoardRow(
                w.Id,
                w.Type.ToString(),
                w.Title,
                w.WorkflowStatusId,
                w.WorkflowStatus!.Name,
                w.WorkflowStatus.Category.ToString(),
                w.WorkflowStatus.ColorKey.ToString(),
                w.Priority.ToString(),
                w.AssigneeUserId,
                w.Assignee != null ? w.Assignee.FullName : null,
                w.DueDate,
                w.UpdatedAt,
                w.CreatedByUserId,
                w.ParentWorkItemId,
                w.Labels.OrderBy(wl => wl.Label!.Name).Select(wl => wl.Label!.Name).ToList()))
            .ToListAsync();

        var childrenByParent = rows
            .Where(w => w.ParentWorkItemId.HasValue)
            .GroupBy(w => w.ParentWorkItemId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var items = rows.Select(row =>
        {
            var childRows = childrenByParent.TryGetValue(row.Id, out var found) ? found : [];
            var doneCount = childRows.Count(c => c.StatusCategory == nameof(WorkflowStatusCategory.Done));
            return new WorkItemBoardCardDto(
                row.Id, row.Type, row.Title, row.StatusId, row.StatusName, row.StatusCategory, row.StatusColorKey, row.Priority,
                row.AssigneeUserId, row.AssigneeName, row.DueDate, row.UpdatedAt,
                row.CreatedByUserId, childRows.Count, doneCount, row.Labels);
        }).ToList();

        // Feature 006 — columns come from this project's own WorkflowStatus rows,
        // ordered by Position, instead of a fixed system-wide enum (FR-006/FR-017).
        var columns = await dbContext.WorkflowStatuses
            .Where(s => s.ProjectId == projectId)
            .OrderBy(s => s.Position)
            .Select(s => new BoardColumnDto(s.Id, s.Name, s.Category.ToString(), s.ColorKey.ToString()))
            .ToListAsync();

        return new WorkItemBoardDto(columns, items);
    }

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
