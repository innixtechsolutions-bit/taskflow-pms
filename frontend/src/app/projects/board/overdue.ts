import { WorkItemStatus } from '../work-items.service';

// A due date is a date-only concept, not an instant — this deliberately never
// runs `dueDate` through `new Date(dueDate)`'s local getters, which would
// convert a UTC instant to local time first and can shift the calendar date
// by a day for a user behind UTC (data-model.md's isOverdue algorithm). Only
// the ISO string's own YYYY-MM-DD digits are compared, against *today's*
// local calendar date — "local" applies to today, never to dueDate.
export function isOverdue(dueDate: string | null, status: WorkItemStatus): boolean {
  if (!dueDate || status === 'Done') {
    return false;
  }

  const dueDateOnly = dueDate.slice(0, 10);
  const today = new Date();
  const todayOnly = [
    today.getFullYear(),
    String(today.getMonth() + 1).padStart(2, '0'),
    String(today.getDate()).padStart(2, '0'),
  ].join('-');

  return dueDateOnly < todayOnly;
}
