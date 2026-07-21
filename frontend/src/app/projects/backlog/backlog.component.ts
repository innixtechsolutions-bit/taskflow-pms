import { Component, OnInit, inject, input, signal } from '@angular/core';
import { CdkDragDrop, DragDropModule } from '@angular/cdk/drag-drop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ProjectStatus, UserLookupItem, WorkItem, WorkItemBacklog, WorkItemsFilter, WorkItemsService } from '../work-items.service';
import { SprintStatus } from '../sprints.service';
import { AuthService } from '../../auth/auth.service';
import { NotificationService } from '../../shared/notification.service';
import { SprintFormComponent } from '../sprint-form/sprint-form.component';
import { openWorkItemModal } from '../work-item-modal/open-work-item-modal';
import { EmptyStateComponent } from '../../shared/empty-state/empty-state.component';
import { FriendlyDatePipe } from '../../shared/friendly-date.pipe';
import { BacklogItemRowComponent } from './backlog-item-row.component';
import { canEditWorkItem } from '../work-item-permissions';

const TYPES = ['Epic', 'Story', 'Task', 'SubTask'];
const PRIORITIES = ['Low', 'Medium', 'High', 'Critical'];

/**
 * The Backlog view (Feature 008) — US1's minimal sprint list, extended here
 * (US2) into the full view: each sprint's own item section
 * (soonest-start-first, per GetBacklogAsync), an unscheduled Backlog section
 * beneath them (FR-013, includes Epics for context — FR-014), the same
 * status/type/priority/assignee/search filters List view already has
 * (FR-013), a per-section "+ Create" (FR-024), and an empty-state hint on a
 * zero-item sprint section (FR-025). Drag-and-drop (US3) and lifecycle
 * actions (US4) extend this component in place next.
 */
@Component({
  selector: 'app-backlog',
  standalone: true,
  imports: [
    DragDropModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    EmptyStateComponent,
    FriendlyDatePipe,
    BacklogItemRowComponent,
  ],
  templateUrl: './backlog.component.html',
  styleUrl: './backlog.component.css',
})
export class BacklogComponent implements OnInit {
  private readonly workItemsService = inject(WorkItemsService);
  private readonly authService = inject(AuthService);
  private readonly notificationService = inject(NotificationService);
  private readonly dialog = inject(MatDialog);

  readonly projectId = input.required<number>();

  protected readonly backlog = signal<WorkItemBacklog>({ sprints: [], backlogItems: [] });
  protected readonly statuses = signal<ProjectStatus[]>([]);
  protected readonly assignableUsers = signal<UserLookupItem[]>([]);
  protected readonly types = TYPES;
  protected readonly priorities = PRIORITIES;

  protected readonly statusFilter = signal('');
  protected readonly typeFilter = signal('');
  protected readonly priorityFilter = signal('');
  protected readonly assigneeFilter = signal('');
  protected readonly searchFilter = signal('');
  protected readonly searchInput = signal('');

  ngOnInit(): void {
    void this.load();
    void this.loadStatuses();
    void this.loadAssignableUsers();
  }

  private async loadStatuses(): Promise<void> {
    this.statuses.set(await this.workItemsService.getStatuses(this.projectId()));
  }

  private async loadAssignableUsers(): Promise<void> {
    this.assignableUsers.set(await this.workItemsService.getAssignableUsers());
  }

  private currentFilter(): WorkItemsFilter {
    return {
      statusId: this.statusFilter() ? Number(this.statusFilter()) : undefined,
      type: this.typeFilter() || undefined,
      priority: this.priorityFilter() || undefined,
      assigneeUserId: this.assigneeFilter() ? Number(this.assigneeFilter()) : undefined,
      search: this.searchFilter() || undefined,
    };
  }

  private async load(): Promise<void> {
    this.backlog.set(await this.workItemsService.getBacklog(this.projectId(), this.currentFilter()));
  }

  protected onStatusFilterChange(value: string): void {
    this.statusFilter.set(value);
    void this.load();
  }

  protected onTypeFilterChange(value: string): void {
    this.typeFilter.set(value);
    void this.load();
  }

  protected onPriorityFilterChange(value: string): void {
    this.priorityFilter.set(value);
    void this.load();
  }

  protected onAssigneeFilterChange(value: string): void {
    this.assigneeFilter.set(value);
    void this.load();
  }

  protected onSearch(): void {
    this.searchFilter.set(this.searchInput());
    void this.load();
  }

  protected canManageSprints(): boolean {
    const role = this.authService.currentRole();
    return role === 'Manager' || role === 'Admin';
  }

  protected openCreateSprintDialog(): void {
    this.dialog.open(SprintFormComponent, {
      data: {
        projectId: this.projectId(),
        onSaved: () => void this.load(),
      },
    });
  }

  // FR-024 — the created item is pre-assigned to this section: a real sprint id
  // for a sprint section's own "+ Create", or undefined (no sprint) for the
  // Backlog section's. sprintId travels as an invisible pass-through on the
  // modal's create request — the modal itself gains no visible Sprint field.
  protected openCreateItemDialog(sprintId?: number): void {
    void openWorkItemModal(this.dialog, {
      mode: 'create',
      projectId: this.projectId(),
      sprintId,
      onSaved: () => void this.load(),
    });
  }

  // US3 (FR-014/FR-015) — an Epic is never draggable (it can never have a
  // sprint); a caller without edit rights on the item can't drag it (reuses
  // the same canEditWorkItem rule as the Board's own drag check); a
  // Completed sprint's section is read-only (FR-009) so nothing inside it —
  // dragging in or out — is draggable either.
  protected canDrag(item: WorkItem, sprintStatus: SprintStatus | null): boolean {
    if (item.type === 'Epic' || sprintStatus === 'Completed') {
      return false;
    }
    const userId = this.authService.currentUser()?.id;
    if (userId === undefined) {
      return false;
    }
    return canEditWorkItem(item, userId, this.authService.currentRole());
  }

  // Optimistic: moves the item into the target section immediately (by
  // rebuilding the whole `backlog` signal — sections are plain arrays, not
  // independently CDK-managed lists, so CDK's transferArrayItem doesn't apply
  // here, same reasoning as BoardComponent.onDrop), then persists via PATCH;
  // reverts and shows an error toast if the PATCH fails (mirrors
  // BoardComponent.onDrop exactly, one field over: SprintId instead of StatusId).
  protected onDrop(event: CdkDragDrop<WorkItem[]>, targetSprintId: number | null): void {
    const item = event.item.data as WorkItem;
    const previousBacklog = this.backlog();
    if (item.sprintId === targetSprintId) {
      return;
    }

    const withoutItem: WorkItemBacklog = {
      sprints: previousBacklog.sprints.map((s) => ({ ...s, items: s.items.filter((i) => i.id !== item.id) })),
      backlogItems: previousBacklog.backlogItems.filter((i) => i.id !== item.id),
    };
    const destinationSprint = previousBacklog.sprints.find((s) => s.id === targetSprintId);
    const movedItem: WorkItem = { ...item, sprintId: targetSprintId, sprintName: destinationSprint?.name ?? null };

    const optimisticBacklog: WorkItemBacklog =
      targetSprintId === null
        ? { ...withoutItem, backlogItems: [...withoutItem.backlogItems, movedItem] }
        : { ...withoutItem, sprints: withoutItem.sprints.map((s) => (s.id === targetSprintId ? { ...s, items: [...s.items, movedItem] } : s)) };
    this.backlog.set(optimisticBacklog);

    this.workItemsService.updateWorkItemSprint(item.id, targetSprintId).catch(() => {
      this.backlog.set(previousBacklog);
      this.notificationService.error(`Could not move "${item.title}". Please try again.`);
    });
  }
}
