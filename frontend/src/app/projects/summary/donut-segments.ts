export const DONUT_RADIUS = 40;
export const DONUT_CIRCUMFERENCE = 2 * Math.PI * DONUT_RADIUS;

export interface DonutSegmentInput {
  count: number;
  colorVar: string;
}

export interface DonutSegment {
  colorVar: string;
  dashArray: string;
  dashOffset: number;
}

/**
 * Pure arc-math for a dependency-free SVG donut (research.md #14): turns
 * per-status counts into stacked `<circle>` arc specs via
 * stroke-dasharray/stroke-dashoffset. Zero-count entries are skipped (no
 * zero-length arcs); an all-zero input (a brand-new project) returns an
 * empty list so the component can render its own neutral/empty state.
 */
export function donutSegments(input: DonutSegmentInput[]): DonutSegment[] {
  const total = input.reduce((sum, item) => sum + item.count, 0);
  if (total === 0) {
    return [];
  }

  const segments: DonutSegment[] = [];
  let cumulative = 0;
  for (const item of input) {
    if (item.count === 0) {
      continue;
    }
    const length = (item.count / total) * DONUT_CIRCUMFERENCE;
    segments.push({
      colorVar: item.colorVar,
      dashArray: `${length} ${DONUT_CIRCUMFERENCE - length}`,
      dashOffset: -cumulative,
    });
    cumulative += length;
  }
  return segments;
}
