import { FriendlyDatePipe } from './friendly-date.pipe';

describe('FriendlyDatePipe', () => {
  let pipe: FriendlyDatePipe;

  beforeEach(() => {
    pipe = new FriendlyDatePipe();
  });

  it('formats an ISO date string as a short, friendly date', () => {
    expect(pipe.transform('2026-07-17T00:00:00Z')).toBe('Jul 17, 2026');
  });

  it('formats a Date object the same way', () => {
    expect(pipe.transform(new Date('2026-01-05T00:00:00Z'))).toBe('Jan 5, 2026');
  });

  it('renders a placeholder for null', () => {
    expect(pipe.transform(null)).toBe('—');
  });

  it('renders a placeholder for undefined', () => {
    expect(pipe.transform(undefined)).toBe('—');
  });

  it('never emits a raw ISO 8601 string', () => {
    const result = pipe.transform('2026-07-17T00:00:00Z');
    expect(result).not.toMatch(/\d{4}-\d{2}-\d{2}T/);
  });
});
