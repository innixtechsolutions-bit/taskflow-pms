using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Data;
using TaskFlow.Api.Data.Entities;
using TaskFlow.Api.Dtos;

namespace TaskFlow.Api.Services;

// RecordCreated/RecordFieldChange only Add() -- neither calls SaveChangesAsync
// itself (research.md #6). WorkItemService's own single SaveChangesAsync call
// (already present at the end of CreateAsync/UpdateAsync/UpdateStatusAsync/
// UpdateSprintAsync) persists both the work item mutation and the new log
// entry together, in one transaction -- the same pattern this codebase
// already relies on for Label attachment (new Label/WorkItemLabel rows are
// Add()-ed without their own SaveChanges call too).
public class ActivityLogService(AppDbContext dbContext)
{
    public void RecordCreated(int projectId, int workItemId, string workItemTitle, string workItemType, int actorUserId)
    {
        dbContext.ActivityLogEntries.Add(new ActivityLogEntry
        {
            ProjectId = projectId,
            WorkItemId = workItemId,
            WorkItemTitle = workItemTitle,
            WorkItemType = workItemType,
            ActorUserId = actorUserId,
            EventType = ActivityEventType.Created,
            CreatedAt = DateTime.UtcNow
        });
    }

    public void RecordFieldChange(
        int projectId, int workItemId, string workItemTitle, string workItemType, int actorUserId,
        ActivityField field, string oldValue, string newValue)
    {
        dbContext.ActivityLogEntries.Add(new ActivityLogEntry
        {
            ProjectId = projectId,
            WorkItemId = workItemId,
            WorkItemTitle = workItemTitle,
            WorkItemType = workItemType,
            ActorUserId = actorUserId,
            EventType = ActivityEventType.FieldChanged,
            Field = field,
            OldValue = oldValue,
            NewValue = newValue,
            CreatedAt = DateTime.UtcNow
        });
    }

    private record ActivityEntryRow(
        int Id, int WorkItemId, string WorkItemTitle, string WorkItemType, ActivityEventType EventType,
        ActivityField? Field, string? OldValue, string? NewValue, int ActorUserId, string ActorName, DateTime CreatedAt);

    // EventType/Field's .ToString() calls happen here, in memory, after the row is
    // materialized -- not inside the EF Core query below -- to avoid relying on
    // provider translation of .ToString() over a *nullable* converted enum (Field).
    private static ActivityEntryDto ToDto(ActivityEntryRow row) => new(
        row.Id, row.WorkItemId, row.WorkItemTitle, row.WorkItemType, row.EventType.ToString(),
        row.Field?.ToString(), row.OldValue, row.NewValue, row.ActorUserId, row.ActorName, row.CreatedAt);

    // The project Summary tab's activity feed (FR-019/FR-020) -- newest first,
    // paginated the same way GetWorkItemsAsync's own listing already is.
    public async Task<PagedResult<ActivityEntryDto>> GetProjectFeedAsync(int projectId, int page, int pageSize)
    {
        var projectExists = await dbContext.Projects.AnyAsync(p => p.Id == projectId);
        if (!projectExists)
        {
            throw new ProjectNotFoundException();
        }

        pageSize = Math.Min(pageSize, 100);
        var query = dbContext.ActivityLogEntries.Where(a => a.ProjectId == projectId);

        var totalCount = await query.CountAsync();
        var rows = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new ActivityEntryRow(
                a.Id, a.WorkItemId, a.WorkItemTitle, a.WorkItemType, a.EventType, a.Field, a.OldValue, a.NewValue,
                a.ActorUserId, a.Actor!.FullName, a.CreatedAt))
            .ToListAsync();

        return new PagedResult<ActivityEntryDto>(rows.Select(ToDto).ToList(), page, pageSize, totalCount);
    }

    // A work item's own activity history (FR-021) -- unpaginated by design
    // (contracts/summary-and-activity-api.md): a single item's history is
    // naturally small. 404s only while the item still exists -- a deleted
    // item's entries remain queryable via the project feed above instead
    // (FR-018), since this route itself needs a live id to resolve.
    public async Task<List<ActivityEntryDto>> GetWorkItemHistoryAsync(int workItemId)
    {
        var workItemExists = await dbContext.WorkItems.AnyAsync(w => w.Id == workItemId);
        if (!workItemExists)
        {
            throw new WorkItemNotFoundException();
        }

        var rows = await dbContext.ActivityLogEntries
            .Where(a => a.WorkItemId == workItemId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new ActivityEntryRow(
                a.Id, a.WorkItemId, a.WorkItemTitle, a.WorkItemType, a.EventType, a.Field, a.OldValue, a.NewValue,
                a.ActorUserId, a.Actor!.FullName, a.CreatedAt))
            .ToListAsync();

        return rows.Select(ToDto).ToList();
    }
}
