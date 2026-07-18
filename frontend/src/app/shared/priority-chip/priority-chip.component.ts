import { Component, computed, input } from '@angular/core';
import { WorkItemPriority } from '../../projects/work-items.service';

const PRIORITY_LABELS: Record<WorkItemPriority, string> = {
  Low: 'Low',
  Medium: 'Medium',
  High: 'High',
  Critical: 'Critical',
};

// Exhaustive switch, not a lookup object, so a new WorkItemPriority value
// without a matching case is a compile error rather than an uncolored chip.
function classFor(priority: WorkItemPriority): string {
  switch (priority) {
    case 'Low':
      return 'chip--priority-low';
    case 'Medium':
      return 'chip--priority-medium';
    case 'High':
      return 'chip--priority-high';
    case 'Critical':
      return 'chip--priority-critical';
  }
}

@Component({
  selector: 'app-priority-chip',
  standalone: true,
  templateUrl: './priority-chip.component.html',
  styleUrl: '../chip.css',
})
export class PriorityChipComponent {
  readonly priority = input.required<WorkItemPriority>();

  protected readonly label = computed(() => PRIORITY_LABELS[this.priority()]);
  protected readonly colorClass = computed(() => classFor(this.priority()));
}
