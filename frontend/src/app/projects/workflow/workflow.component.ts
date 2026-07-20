import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import { ProjectStatus, WorkItemStatusCategory } from '../work-items.service';
import { ProjectStatusService } from '../project-status.service';
import { NotificationService } from '../../shared/notification.service';

// The Workflow management screen (Feature 006, US2) — created here read-only and
// extended in place by US3-US6 (add/rename/reorder/delete), rather than rebuilt
// per story (plan.md). Column reordering (US5) is this dependency's second,
// independent consumer after Feature 005's board card dragging.
@Component({
  selector: 'app-workflow',
  standalone: true,
  imports: [RouterLink, DragDropModule],
  templateUrl: './workflow.component.html',
  styleUrl: './workflow.component.css',
})
export class WorkflowComponent implements OnInit {
  private readonly projectStatusService = inject(ProjectStatusService);
  private readonly route = inject(ActivatedRoute);
  private readonly notificationService = inject(NotificationService);
  protected readonly projectId = Number(this.route.snapshot.paramMap.get('id'));

  protected readonly statuses = signal<ProjectStatus[]>([]);

  protected readonly newStatusName = signal('');
  protected readonly newStatusCategory = signal<WorkItemStatusCategory>('Open');

  protected readonly editingId = signal<number | null>(null);
  protected readonly editName = signal('');
  protected readonly editError = signal<string | null>(null);

  // The status currently showing its destination-picker (non-empty column delete,
  // US6 scenario 2) -- null when no delete is in progress.
  protected readonly pendingDeleteId = signal<number | null>(null);
  protected readonly destinationStatusId = signal<number | null>(null);

  ngOnInit(): void {
    void this.loadStatuses();
  }

  private async loadStatuses(): Promise<void> {
    this.statuses.set(await this.projectStatusService.getStatuses(this.projectId));
  }

  protected async onAddStatus(): Promise<void> {
    const name = this.newStatusName().trim();
    if (!name) {
      return;
    }
    try {
      const created = await this.projectStatusService.createStatus(this.projectId, {
        name,
        category: this.newStatusCategory(),
      });
      this.statuses.update((statuses) => [...statuses, created].sort((a, b) => a.position - b.position));
      this.newStatusName.set('');
    } catch {
      this.notificationService.error(`Could not add "${name}". Please try again.`);
    }
  }

  protected onStartEdit(status: ProjectStatus): void {
    this.editingId.set(status.id);
    this.editName.set(status.name);
    this.editError.set(null);
  }

  protected onCancelEdit(): void {
    this.editingId.set(null);
    this.editError.set(null);
  }

  protected async onSaveEdit(status: ProjectStatus): Promise<void> {
    const name = this.editName().trim();
    try {
      const updated = await this.projectStatusService.updateStatus(this.projectId, status.id, { name });
      this.statuses.update((statuses) => statuses.map((s) => (s.id === updated.id ? updated : s)));
      this.editingId.set(null);
      this.editError.set(null);
    } catch {
      this.editError.set('A status with this name already exists in this project.');
    }
  }

  // Optimistic, same style as the board's card drag (FR-012): reorders locally first,
  // then persists; reverts by re-fetching if the request fails.
  protected onReorderDrop(event: CdkDragDrop<ProjectStatus[]>): void {
    if (event.previousIndex === event.currentIndex) {
      return;
    }
    const reordered = [...this.statuses()];
    moveItemInArray(reordered, event.previousIndex, event.currentIndex);
    this.statuses.set(reordered);

    this.projectStatusService
      .reorderStatuses(
        this.projectId,
        reordered.map((s) => s.id)
      )
      .then((updated) => this.statuses.set(updated))
      .catch(() => {
        this.notificationService.error('Could not reorder statuses. Please try again.');
        void this.loadStatuses();
      });
  }

  // Empty columns delete directly (US6 scenario 1); a column with items must first
  // have a destination chosen (scenario 2), handled by onConfirmDeleteWithMove below.
  protected onClickDelete(status: ProjectStatus): void {
    if (status.itemCount === 0) {
      if (!confirm(`Delete "${status.name}"? This cannot be undone.`)) {
        return;
      }
      void this.performDelete(status.id);
      return;
    }
    this.pendingDeleteId.set(status.id);
    this.destinationStatusId.set(null);
  }

  protected onCancelDelete(): void {
    this.pendingDeleteId.set(null);
    this.destinationStatusId.set(null);
  }

  // Wording matches spec.md US6 acceptance scenario 2 exactly: "Move N items to 'X'
  // and delete 'Y'?".
  protected onConfirmDeleteWithMove(status: ProjectStatus): void {
    const destinationId = this.destinationStatusId();
    const destination = this.statuses().find((s) => s.id === destinationId);
    if (!destination) {
      return;
    }
    const message = `Move ${status.itemCount} items to '${destination.name}' and delete '${status.name}'?`;
    if (!confirm(message)) {
      return;
    }
    void this.performDelete(status.id, destination.id);
  }

  private async performDelete(statusId: number, destinationStatusId?: number): Promise<void> {
    try {
      await this.projectStatusService.deleteStatus(this.projectId, statusId, destinationStatusId);
      this.statuses.update((statuses) => statuses.filter((s) => s.id !== statusId));
      this.pendingDeleteId.set(null);
      this.destinationStatusId.set(null);
    } catch {
      this.notificationService.error('Could not delete this status. Please try again.');
    }
  }
}
