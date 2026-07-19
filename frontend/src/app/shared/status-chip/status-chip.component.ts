import { Component, computed, input } from '@angular/core';
import { WorkItemStatus } from '../../projects/work-items.service';

const STATUS_LABELS: Record<WorkItemStatus, string> = {
  ToDo: 'To Do',
  InProgress: 'In Progress',
  InReview: 'In Review',
  Done: 'Done',
};

// Exhaustive switch, not a lookup object, so a new WorkItemStatus value
// without a matching case is a compile error rather than an uncolored chip
// (spec.md edge case: "status value with no defined chip color").
function classFor(status: WorkItemStatus): string {
  switch (status) {
    case 'ToDo':
      return 'chip--status-todo';
    case 'InProgress':
      return 'chip--status-inprogress';
    case 'InReview':
      return 'chip--status-inreview';
    case 'Done':
      return 'chip--status-done';
  }
}

@Component({
  selector: 'app-status-chip',
  standalone: true,
  templateUrl: './status-chip.component.html',
  styleUrl: '../chip.css',
})
export class StatusChipComponent {
  readonly status = input.required<WorkItemStatus>();

  protected readonly label = computed(() => STATUS_LABELS[this.status()]);
  protected readonly colorClass = computed(() => classFor(this.status()));
}
