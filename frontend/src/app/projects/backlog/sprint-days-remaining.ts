import { SprintStatus } from '../sprints.service';

export interface SprintDaysRemaining {
  overdue: boolean;
  // Valid only when overdue is false; 0 means "due today".
  days: number;
}

// Same date-only-string convention as board/overdue.ts's isOverdue — built
// from local year/month/day, never `new Date(iso)`'s local getters, which
// would convert a UTC instant to local time first and can shift the
// calendar date by a day for a user behind UTC (research.md #3).
function dateOnlyLocal(value: string): Date {
  const [year, month, day] = value.slice(0, 10).split('-').map(Number);
  return new Date(year, month - 1, day);
}

// US6 — a pure function (test-first, per spec's own non-functional
// requirement), mirroring isOverdue's technique but for a sprint's end date:
// null for a Planned/Completed sprint (the indicator is Active-sprint-only,
// FR-019), otherwise the days remaining (0 = due today) or an overdue flag.
export function sprintDaysRemaining(endDate: string, status: SprintStatus): SprintDaysRemaining | null {
  if (status !== 'Active') {
    return null;
  }

  const end = dateOnlyLocal(endDate);
  const today = new Date();
  const todayOnly = new Date(today.getFullYear(), today.getMonth(), today.getDate());
  const diffDays = Math.round((end.getTime() - todayOnly.getTime()) / 86400000);

  return diffDays < 0 ? { overdue: true, days: 0 } : { overdue: false, days: diffDays };
}
