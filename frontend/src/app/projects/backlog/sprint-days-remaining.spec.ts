import { vi } from 'vitest';
import { sprintDaysRemaining } from './sprint-days-remaining';

describe('sprintDaysRemaining', () => {
  beforeEach(() => {
    // Fixed "today" so every test is deterministic regardless of when it runs.
    vi.useFakeTimers();
    vi.setSystemTime(new Date(2026, 6, 18, 14, 30)); // Jul 18, 2026, 2:30pm local
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('returns the number of days remaining for a future end date on an Active sprint', () => {
    expect(sprintDaysRemaining('2026-07-21T00:00:00Z', 'Active')).toEqual({ overdue: false, days: 3 });
  });

  it('returns days: 0 (due today), not overdue, when the end date is today', () => {
    expect(sprintDaysRemaining('2026-07-18T00:00:00Z', 'Active')).toEqual({ overdue: false, days: 0 });
  });

  it('returns overdue for a past end date on an Active sprint', () => {
    expect(sprintDaysRemaining('2026-07-17T00:00:00Z', 'Active')).toEqual({ overdue: true, days: 0 });
  });

  it('returns null for a Planned sprint, regardless of dates', () => {
    expect(sprintDaysRemaining('2020-01-01T00:00:00Z', 'Planned')).toBeNull();
  });

  it('returns null for a Completed sprint, regardless of dates', () => {
    expect(sprintDaysRemaining('2026-07-21T00:00:00Z', 'Completed')).toBeNull();
  });

  // Same UTC/local-shift pitfall isOverdue.spec.ts documents — reads the ISO
  // string's literal digits rather than converting to local time first.
  it('reads the date literally off the ISO string rather than converting to local time first', () => {
    expect(sprintDaysRemaining('2026-07-18T00:00:00Z', 'Active')).toEqual({ overdue: false, days: 0 });
  });
});
