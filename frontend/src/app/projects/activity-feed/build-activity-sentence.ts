import { ActivityEntry, ActivityField } from '../work-items.service';

const FIELD_LABELS: Record<ActivityField, string> = {
  Status: 'status',
  Priority: 'priority',
  Assignee: 'assignee',
  Sprint: 'sprint',
};

/**
 * Builds the one-line, human-readable sentence for an activity entry
 * (FR-019/FR-021) — e.g. "Jane changed Task 'Fix login' status from To Do to
 * In Progress" or "Jane created Task 'Fix login'". No timestamp here — that's
 * rendered separately via RelativeTimePipe (research.md #15).
 */
export function buildActivitySentence(entry: ActivityEntry): string {
  const subject = `${entry.actorName} ${entry.eventType === 'Created' ? 'created' : 'changed'} ${entry.workItemType} '${entry.workItemTitle}'`;
  if (entry.eventType === 'Created' || entry.field === null) {
    return subject;
  }
  return `${subject} ${FIELD_LABELS[entry.field]} from ${entry.oldValue} to ${entry.newValue}`;
}
