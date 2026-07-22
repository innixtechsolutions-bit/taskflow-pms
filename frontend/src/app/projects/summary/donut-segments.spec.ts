import { donutSegments, DONUT_CIRCUMFERENCE } from './donut-segments';

describe('donutSegments', () => {
  it('always sums segment lengths to the full circumference', () => {
    const segments = donutSegments([
      { count: 3, colorVar: 'var(--a)' },
      { count: 1, colorVar: 'var(--b)' },
      { count: 4, colorVar: 'var(--c)' },
    ]);

    const total = segments.reduce((sum, s) => sum + Number(s.dashArray.split(' ')[0]), 0);
    expect(total).toBeCloseTo(DONUT_CIRCUMFERENCE, 5);
  });

  it('produces an empty list for a zero-item project', () => {
    const segments = donutSegments([
      { count: 0, colorVar: 'var(--a)' },
      { count: 0, colorVar: 'var(--b)' },
    ]);

    expect(segments).toEqual([]);
  });

  it('produces one full segment for a single-status project', () => {
    const segments = donutSegments([
      { count: 5, colorVar: 'var(--a)' },
      { count: 0, colorVar: 'var(--b)' },
    ]);

    expect(segments.length).toBe(1);
    expect(Number(segments[0].dashArray.split(' ')[0])).toBeCloseTo(DONUT_CIRCUMFERENCE, 5);
    expect(segments[0].colorVar).toBe('var(--a)');
  });

  it('skips zero-count entries entirely', () => {
    const segments = donutSegments([
      { count: 2, colorVar: 'var(--a)' },
      { count: 0, colorVar: 'var(--b)' },
      { count: 2, colorVar: 'var(--c)' },
    ]);

    expect(segments.map((s) => s.colorVar)).toEqual(['var(--a)', 'var(--c)']);
  });
});
