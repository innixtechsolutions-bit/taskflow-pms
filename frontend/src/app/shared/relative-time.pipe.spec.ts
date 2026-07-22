import { RelativeTimePipe } from './relative-time.pipe';

function isoMinutesAgo(minutes: number): string {
  return new Date(Date.now() - minutes * 60_000).toISOString();
}

describe('RelativeTimePipe', () => {
  const pipe = new RelativeTimePipe();

  it('renders "just now" for a timestamp under a minute old', () => {
    expect(pipe.transform(isoMinutesAgo(0))).toBe('just now');
  });

  it('renders "N minutes ago" for a timestamp under an hour old', () => {
    expect(pipe.transform(isoMinutesAgo(6))).toBe('6 minutes ago');
    expect(pipe.transform(isoMinutesAgo(1))).toBe('1 minute ago');
  });

  it('renders "N hours ago" for a timestamp under a day old', () => {
    expect(pipe.transform(isoMinutesAgo(3 * 60))).toBe('3 hours ago');
    expect(pipe.transform(isoMinutesAgo(60))).toBe('1 hour ago');
  });

  it('renders "N days ago" for a timestamp under 7 days old', () => {
    expect(pipe.transform(isoMinutesAgo(2 * 24 * 60))).toBe('2 days ago');
    expect(pipe.transform(isoMinutesAgo(24 * 60))).toBe('1 day ago');
  });

  it('falls back to friendlyDate beyond ~7 days', () => {
    const eightDaysAgo = isoMinutesAgo(8 * 24 * 60);
    const result = pipe.transform(eightDaysAgo);
    expect(result).not.toContain('ago');
    expect(result).not.toBe('—');
  });

  it('renders the placeholder for a null/undefined value', () => {
    expect(pipe.transform(null)).toBe('—');
    expect(pipe.transform(undefined)).toBe('—');
  });
});
