using TaskFlow.Api.Data;
using TaskFlow.Api.Data.Entities;

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
}
