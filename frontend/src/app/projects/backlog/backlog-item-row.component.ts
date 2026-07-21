import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CdkDrag } from '@angular/cdk/drag-drop';
import { StatusChipComponent } from '../../shared/status-chip/status-chip.component';
import { UserAvatarComponent } from '../../shared/user-avatar/user-avatar.component';
import { FriendlyDatePipe } from '../../shared/friendly-date.pipe';
import { WorkItem } from '../work-items.service';

/**
 * A single Backlog row: title, type, and — per FR-026 — status/due date/
 * assignee shown inline, using the same chip/avatar conventions as the List
 * view (StatusChipComponent, UserAvatarComponent, FriendlyDatePipe). `cdkDrag`
 * lives here (US3); dragDisabled is decided by the parent BacklogComponent,
 * the same split BoardCardComponent already uses.
 */
@Component({
  selector: 'app-backlog-item-row',
  standalone: true,
  imports: [StatusChipComponent, UserAvatarComponent, FriendlyDatePipe, CdkDrag, RouterLink],
  templateUrl: './backlog-item-row.component.html',
  styleUrl: './backlog-item-row.component.css',
})
export class BacklogItemRowComponent {
  readonly item = input.required<WorkItem>();
  readonly projectId = input.required<number>();
  readonly dragDisabled = input(false);
}
