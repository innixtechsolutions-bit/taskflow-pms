import { Component, computed, input } from '@angular/core';
import { PriorityBreakdownItem, WorkItemPriority } from '../work-items.service';

// Same solid `-text` token every priority chip already uses (research.md #14).
function colorVarFor(priority: WorkItemPriority): string {
  return `var(--color-priority-${priority.toLowerCase()}-text)`;
}

@Component({
  selector: 'app-priority-bar-chart',
  standalone: true,
  templateUrl: './priority-bar-chart.component.html',
  styleUrl: './priority-bar-chart.component.css',
})
export class PriorityBarChartComponent {
  readonly breakdown = input.required<PriorityBreakdownItem[]>();

  private readonly maxCount = computed(() => Math.max(1, ...this.breakdown().map((item) => item.count)));

  protected percent(count: number): number {
    return (count / this.maxCount()) * 100;
  }

  protected colorVar(priority: WorkItemPriority): string {
    return colorVarFor(priority);
  }
}
