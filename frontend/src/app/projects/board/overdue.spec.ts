import { vi } from 'vitest';
import { isOverdue } from './overdue';

describe('isOverdue', () => {
  beforeEach(() => {
    // Fixed "today" so every test is deterministic regardless of when it runs.
    vi.useFakeTimers();
    vi.setSystemTime(new Date(2026, 6, 18, 14, 30)); // Jul 18, 2026, 2:30pm local
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('is true for a due date strictly before today on a non-Done item', () => {
    expect(isOverdue('2026-07-17T00:00:00Z', 'ToDo')).toBe(true);
    expect(isOverdue('2026-07-17T00:00:00Z', 'InProgress')).toBe(true);
    expect(isOverdue('2026-07-17T00:00:00Z', 'InReview')).toBe(true);
  });

  it('is false for a due date equal to today, no matter the time of day (due-today boundary)', () => {
    expect(isOverdue('2026-07-18T00:00:00Z', 'ToDo')).toBe(false);
    expect(isOverdue('2026-07-18T23:59:59Z', 'ToDo')).toBe(false);
  });

  it('is false for a future due date', () => {
    expect(isOverdue('2026-07-19T00:00:00Z', 'ToDo')).toBe(false);
  });

  it('is false whenever status is Done, regardless of how far past the due date is', () => {
    expect(isOverdue('2020-01-01T00:00:00Z', 'Done')).toBe(false);
  });

  it('is false when there is no due date', () => {
    expect(isOverdue(null, 'ToDo')).toBe(false);
  });

  // The naive `new Date(dueDate).getDate()` approach converts a UTC instant to
  // local time first, which shifts the calendar date by a day for a user behind
  // UTC. This due date's own digits say "2026-07-18" (= today, not overdue),
  // but new Date('2026-07-18T00:00:00Z') interpreted in a timezone behind UTC
  // (e.g. US Pacific, UTC-7/8) lands on 2026-07-17 locally, which a
  // getDate()-based comparison would wrongly call overdue. Reading the ISO
  // string's literal digits instead of converting through local getters is
  // what data-model.md's isOverdue algorithm specifically avoids.
  it('reads the date literally off the ISO string rather than converting to local time first', () => {
    expect(isOverdue('2026-07-18T00:00:00Z', 'ToDo')).toBe(false);
  });
});
