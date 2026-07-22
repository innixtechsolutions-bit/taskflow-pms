import { Component, computed, input } from '@angular/core';
import { ChipColor, StatusBreakdownItem } from '../work-items.service';
import { donutSegments, DONUT_RADIUS } from './donut-segments';

// Same solid `-text` token (not the pastel `-bg` half) every chip already
// uses, so the donut's colors visually match the same status's chip
// elsewhere on the page (research.md #14) — one palette, one source of truth.
function colorVarFor(colorKey: ChipColor): string {
  return `var(--color-chip-${colorKey.toLowerCase()}-text)`;
}

@Component({
  selector: 'app-status-donut-chart',
  standalone: true,
  templateUrl: './status-donut-chart.component.html',
  styleUrl: './status-donut-chart.component.css',
})
export class StatusDonutChartComponent {
  readonly breakdown = input.required<StatusBreakdownItem[]>();

  protected readonly radius = DONUT_RADIUS;

  protected readonly segments = computed(() =>
    donutSegments(this.breakdown().map((item) => ({ count: item.count, colorVar: colorVarFor(item.colorKey) })))
  );

  protected colorVar(colorKey: ChipColor): string {
    return colorVarFor(colorKey);
  }
}
