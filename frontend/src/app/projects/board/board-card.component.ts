import { Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CdkDrag } from '@angular/cdk/drag-drop';
import { PriorityChipComponent } from '../../shared/priority-chip/priority-chip.component';
import { UserAvatarComponent } from '../../shared/user-avatar/user-avatar.component';
import { FriendlyDatePipe } from '../../shared/friendly-date.pipe';
import { WorkItemBoardCard } from '../work-items.service';
import { isOverdue } from './overdue';

/**
 * A single board card: title, type, priority chip, assignee avatar (or an
 * "Unassigned" indicator), friendly + overdue-flagged due date, and "n/m
 * done" child progress (FR-009/FR-011) — entirely built from the existing
 * design system, no new ad-hoc styling (FR-023). Clicking the card (not
 * dragging it) navigates to its detail route (FR-018/US5) — cdkDrag stays
 * on the outer element since CDK itself distinguishes a click from a drag
 * by movement threshold, so the nested routerLink still receives clicks.
 */
@Component({
  selector: 'app-board-card',
  standalone: true,
  imports: [PriorityChipComponent, UserAvatarComponent, FriendlyDatePipe, CdkDrag, RouterLink],
  templateUrl: './board-card.component.html',
  styleUrl: './board-card.component.css',
})
export class BoardCardComponent {
  readonly card = input.required<WorkItemBoardCard>();
  readonly projectId = input.required<number>();
  // Drag is disabled per-card by the board (canEditWorkItem), not decided here.
  readonly dragDisabled = input(false);

  protected readonly isOverdue = computed(() => isOverdue(this.card().dueDate, this.card().status));
  protected readonly hasChildren = computed(() => this.card().directChildrenCount > 0);
}
