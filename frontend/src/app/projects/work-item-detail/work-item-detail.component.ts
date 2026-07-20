import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { WorkItemDetail, WorkItemsService } from '../work-items.service';
import { WorkItemModalComponent } from '../work-item-modal/work-item-modal.component';
import { AuthService } from '../../auth/auth.service';
import { StatusChipComponent } from '../../shared/status-chip/status-chip.component';
import { PriorityChipComponent } from '../../shared/priority-chip/priority-chip.component';
import { UserAvatarComponent } from '../../shared/user-avatar/user-avatar.component';
import { NotificationService } from '../../shared/notification.service';
import { EmptyStateComponent } from '../../shared/empty-state/empty-state.component';
import { canEditWorkItem } from '../work-item-permissions';

// Mirrors the backend's RequiredParentType mapping in reverse (data-model.md's
// Hierarchy rules table) — the type a new child would need, given this item's type.
// SubTask has no entry: it can never have children (FR-019 only applies to
// Epic/Story/Task), so canCreateChild() below never looks it up.
const CHILD_TYPE: Record<string, string> = { Epic: 'Story', Story: 'Task', Task: 'SubTask' };

@Component({
  selector: 'app-work-item-detail',
  standalone: true,
  imports: [
    RouterLink,
    MatButtonModule,
    MatCardModule,
    StatusChipComponent,
    PriorityChipComponent,
    UserAvatarComponent,
    EmptyStateComponent,
  ],
  templateUrl: './work-item-detail.component.html',
})
export class WorkItemDetailComponent implements OnInit {
  private readonly workItemsService = inject(WorkItemsService);
  private readonly authService = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly notificationService = inject(NotificationService);
  private readonly dialog = inject(MatDialog);
  protected readonly projectId = Number(this.route.snapshot.paramMap.get('projectId'));
  private readonly workItemId = Number(this.route.snapshot.paramMap.get('id'));

  protected readonly item = signal<WorkItemDetail | null>(null);

  ngOnInit(): void {
    void this.load();
  }

  private async load(): Promise<void> {
    this.item.set(await this.workItemsService.getWorkItemDetail(this.workItemId));
  }

  // Replaces the removed .../work-items/new?parentWorkItemId=&type= routerLink
  // (US1) — pre-selects this item as parent and the legal child type, and
  // re-fetches the detail once the modal reports a save.
  protected openCreateChildModal(): void {
    this.dialog.open(WorkItemModalComponent, {
      width: '720px',
      maxWidth: '95vw',
      data: {
        mode: 'create',
        projectId: this.projectId,
        parentWorkItemId: this.workItemId,
        type: this.childType(),
        onSaved: () => void this.load(),
      },
    });
  }

  // Replaces the removed .../work-items/:id/edit routerLink (US1).
  protected openEditModal(): void {
    this.dialog.open(WorkItemModalComponent, {
      width: '720px',
      maxWidth: '95vw',
      data: {
        mode: 'edit',
        projectId: this.projectId,
        workItemId: this.workItemId,
        onSaved: () => void this.load(),
      },
    });
  }

  protected canCreateChild(): boolean {
    const item = this.item();
    return !!item && item.type in CHILD_TYPE;
  }

  protected childType(): string {
    return CHILD_TYPE[this.item()?.type ?? ''] ?? '';
  }

  // Broader than canDelete: also allows the item's current assignee (FR-016) — same
  // rule project-detail's canEdit already applies to the flat/tree lists. Delegates
  // to the shared canEditWorkItem() (Feature 005).
  protected canEdit(): boolean {
    const item = this.item();
    const userId = this.authService.currentUser()?.id;
    if (!item || userId === undefined) {
      return false;
    }
    return canEditWorkItem(item, userId, this.authService.currentRole());
  }

  // Narrower than canEdit: the current assignee alone cannot delete (FR-017/FR-018).
  protected canDelete(): boolean {
    const item = this.item();
    const userId = this.authService.currentUser()?.id;
    if (!item || userId === undefined) {
      return false;
    }
    return item.createdByUserId === userId || this.isManagerOrAdmin();
  }

  private isManagerOrAdmin(): boolean {
    const role = this.authService.currentRole();
    return role === 'Manager' || role === 'Admin';
  }

  protected async onDelete(): Promise<void> {
    const item = this.item();
    if (!item) {
      return;
    }
    // States the exact descendant count before deleting (FR-020) — already loaded
    // with the rest of the detail view, no extra round trip needed (research.md §6).
    const message =
      item.totalDescendantCount > 0
        ? `Delete "${item.title}"? This will also delete ${item.totalDescendantCount} nested item(s). This cannot be undone.`
        : `Delete "${item.title}"? This cannot be undone.`;
    if (!confirm(message)) {
      return;
    }
    try {
      await this.workItemsService.deleteWorkItem(item.id);
    } catch {
      this.notificationService.error(`Could not delete "${item.title}". Please try again.`);
      return;
    }
    this.notificationService.success(`"${item.title}" deleted.`);
    await this.router.navigateByUrl(`/projects/${this.projectId}`);
  }
}
