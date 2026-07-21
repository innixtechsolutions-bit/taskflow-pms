import { Component, OnInit, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ProjectStatus, UserLookupItem, WorkItemBacklog, WorkItemsFilter, WorkItemsService } from '../work-items.service';
import { AuthService } from '../../auth/auth.service';
import { SprintFormComponent } from '../sprint-form/sprint-form.component';
import { openWorkItemModal } from '../work-item-modal/open-work-item-modal';
import { EmptyStateComponent } from '../../shared/empty-state/empty-state.component';
import { FriendlyDatePipe } from '../../shared/friendly-date.pipe';
import { BacklogItemRowComponent } from './backlog-item-row.component';

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
}
