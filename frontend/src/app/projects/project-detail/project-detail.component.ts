import { Component, OnInit, ViewChild, inject, signal } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ProjectDetail, ProjectsService } from '../projects.service';
import {
  ProjectStatus,
  UserLookupItem,
  WorkItem,
  WorkItemsFilter,
  WorkItemsService,
  WorkItemTreeNode,
} from '../work-items.service';
import { AuthService } from '../../auth/auth.service';
import { StatusChipComponent } from '../../shared/status-chip/status-chip.component';
import { PriorityChipComponent } from '../../shared/priority-chip/priority-chip.component';
import { LabelChipComponent } from '../../shared/label-chip/label-chip.component';
import { UserAvatarComponent } from '../../shared/user-avatar/user-avatar.component';
import { FriendlyDatePipe } from '../../shared/friendly-date.pipe';
import { NotificationService } from '../../shared/notification.service';
import { EmptyStateComponent } from '../../shared/empty-state/empty-state.component';
import { BoardComponent } from '../board/board.component';
import { openWorkItemModal } from '../work-item-modal/open-work-item-modal';
import { canEditWorkItem } from '../work-item-permissions';

const TYPES = ['Epic', 'Story', 'Task', 'SubTask'];
const PRIORITIES = ['Low', 'Medium', 'High', 'Critical'];

type ViewMode = 'flat' | 'tree' | 'board';
const VIEW_MODES: ViewMode[] = ['flat', 'tree', 'board'];

function parseViewMode(value: string | null): ViewMode {
  return VIEW_MODES.includes(value as ViewMode) ? (value as ViewMode) : 'board';
}

@Component({
  selector: 'app-project-detail',
  standalone: true,
  imports: [
    RouterLink,
    NgTemplateOutlet,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatTableModule,
    MatTabsModule,
    StatusChipComponent,
    PriorityChipComponent,
    LabelChipComponent,
    UserAvatarComponent,
    FriendlyDatePipe,
    EmptyStateComponent,
    BoardComponent,
  ],
  templateUrl: './project-detail.component.html',
})
export class ProjectDetailComponent implements OnInit {
  private readonly projectsService = inject(ProjectsService);
  private readonly workItemsService = inject(WorkItemsService);
  private readonly authService = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly notificationService = inject(NotificationService);
  private readonly dialog = inject(MatDialog);
  private readonly projectId = Number(this.route.snapshot.paramMap.get('id'));

  // Present only while viewMode() === 'board' (the @if in the template
  // destroys/recreates it) — refreshed alongside the flat list/tree below
  // whenever a create/edit modal reports a save, so switching straight to
  // Board afterward doesn't show stale data.
  @ViewChild(BoardComponent) private boardComponent?: BoardComponent;

  // Column order for mat-table's structural directives.
  protected readonly displayedColumns = ['title', 'status', 'priority', 'labels', 'actions'];

  protected readonly project = signal<ProjectDetail | null>(null);
  // Feature 005 Polish: the description renders as a single truncated line by
  // default (point 1 of the page-composition pass) — this just toggles the
  // CSS class that lets it wrap to full height.
  protected readonly descriptionExpanded = signal(false);
  protected readonly workItems = signal<WorkItem[]>([]);
  protected readonly assignableUsers = signal<UserLookupItem[]>([]);

  // Feature 006 — sourced from the project's own workflow columns, not a fixed list.
  protected readonly statuses = signal<ProjectStatus[]>([]);
  protected readonly types = TYPES;
  protected readonly priorities = PRIORITIES;

  protected readonly statusFilter = signal('');
  protected readonly typeFilter = signal('');
  protected readonly priorityFilter = signal('');
  protected readonly assigneeFilter = signal('');
  protected readonly searchFilter = signal('');
  protected readonly searchInput = signal('');
  // Feature 007 US5 — single-select, sourced from every label referenced by
  // ≥1 work item in the project (research.md #6, data-model.md).
  protected readonly labelFilter = signal('');
  protected readonly projectLabels = signal<string[]>([]);

  protected readonly page = signal(1);
  protected readonly pageSize = 20;
  protected readonly totalCount = signal(0);

  // Board is the default view (Feature 005 Polish: promoted from opt-in to
  // primary); List/Tree remain available via the tab row. Initialized from the
  // `view` query param (not just a plain signal default) and kept in sync with it
  // on every change (setViewMode below) so the selection survives navigating to a
  // card's detail page and back (FR-019/US5, Feature 005) — a plain
  // component-instance signal would reset to 'board' when the router recreates
  // this component on return navigation.
  protected readonly viewMode = signal<ViewMode>(parseViewMode(this.route.snapshot.queryParamMap.get('view')));
  protected readonly treeNodes = signal<WorkItemTreeNode[]>([]);
  // Tracks which parent rows are collapsed rather than which are expanded, so a
  // freshly-loaded tree starts fully expanded without needing to pre-populate a set
  // of every node's id (FR-013's collapse state need not persist across reloads).
  protected readonly collapsedIds = signal<Set<number>>(new Set());

  ngOnInit(): void {
    void this.loadProject();
    void this.loadWorkItems();
    void this.loadAssignableUsers();
    void this.loadTree();
    void this.loadStatuses();
    void this.loadProjectLabels();
  }

  private async loadProjectLabels(): Promise<void> {
    this.projectLabels.set(await this.workItemsService.getProjectLabels(this.projectId));
  }

  private async loadProject(): Promise<void> {
    this.project.set(await this.projectsService.getProject(this.projectId));
  }

  private async loadStatuses(): Promise<void> {
    this.statuses.set(await this.workItemsService.getStatuses(this.projectId));
  }

  private async loadTree(): Promise<void> {
    this.treeNodes.set(await this.workItemsService.getWorkItemsTree(this.projectId));
  }

  // Writes `view` into the URL's query params (not just the signal) so returning
  // to this page — e.g. via back-navigation from a board card's detail page —
  // restores the same view even though the router recreates this component
  // (FR-019/US5).
  protected setViewMode(mode: ViewMode): void {
    this.viewMode.set(mode);
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { view: mode },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  protected isCollapsed(id: number): boolean {
    return this.collapsedIds().has(id);
  }

  protected toggleCollapsed(id: number): void {
    const next = new Set(this.collapsedIds());
    if (next.has(id)) {
      next.delete(id);
    } else {
      next.add(id);
    }
    this.collapsedIds.set(next);
  }

  private async loadAssignableUsers(): Promise<void> {
    this.assignableUsers.set(await this.workItemsService.getAssignableUsers());
  }

  private currentFilter(): WorkItemsFilter {
    return {
      page: this.page(),
      pageSize: this.pageSize,
      statusId: this.statusFilter() ? Number(this.statusFilter()) : undefined,
      type: this.typeFilter() || undefined,
      priority: this.priorityFilter() || undefined,
      assigneeUserId: this.assigneeFilter() ? Number(this.assigneeFilter()) : undefined,
      search: this.searchFilter() || undefined,
      label: this.labelFilter() || undefined,
    };
  }

  private async loadWorkItems(): Promise<void> {
    const result = await this.workItemsService.getWorkItems(this.projectId, this.currentFilter());
    this.workItems.set(result.items);
    this.totalCount.set(result.totalCount);
  }

  // Replaces the removed .../work-items/new routerLink (US1) — used by the
  // toolbar "New work item" button and both empty states' "Add work item"
  // actions, none of which pre-select anything.
  protected openCreateModal(): void {
    void openWorkItemModal(this.dialog, {
      mode: 'create',
      projectId: this.projectId,
      onSaved: () => this.onWorkItemSaved(),
    });
  }

  // Replaces the removed .../work-items/:id/edit routerLink (US1) — used by a
  // flat-list row's "Edit" action.
  protected openEditModal(item: WorkItem): void {
    void openWorkItemModal(this.dialog, {
      mode: 'edit',
      projectId: this.projectId,
      workItemId: item.id,
      onSaved: () => this.onWorkItemSaved(),
    });
  }

  // Every view this page can show (flat list, tree, and — if currently
  // mounted — the embedded board) is refreshed in place, since a save from
  // either modal entry point could affect any of them (research.md #9).
  private onWorkItemSaved(): void {
    void this.loadWorkItems();
    void this.loadTree();
    void this.loadProjectLabels();
    this.boardComponent?.refresh();
  }

  // Distinguishes a genuinely empty project (FR-023) from filters that simply matched
  // nothing — the two need different messages.
  protected hasActiveFilters(): boolean {
    return !!(
      this.statusFilter() ||
      this.typeFilter() ||
      this.priorityFilter() ||
      this.assigneeFilter() ||
      this.searchFilter() ||
      this.labelFilter()
    );
  }

  protected onStatusFilterChange(value: string): void {
    this.statusFilter.set(value);
    this.applyFilters();
  }

  protected onTypeFilterChange(value: string): void {
    this.typeFilter.set(value);
    this.applyFilters();
  }

  protected onPriorityFilterChange(value: string): void {
    this.priorityFilter.set(value);
    this.applyFilters();
  }

  protected onAssigneeFilterChange(value: string): void {
    this.assigneeFilter.set(value);
    this.applyFilters();
  }

  protected onLabelFilterChange(value: string): void {
    this.labelFilter.set(value);
    this.applyFilters();
  }

  protected onSearch(): void {
    this.searchFilter.set(this.searchInput());
    this.applyFilters();
  }

  private applyFilters(): void {
    this.page.set(1);
    void this.loadWorkItems();
  }

  protected nextPage(): void {
    this.page.update((p) => p + 1);
    void this.loadWorkItems();
  }

  protected prevPage(): void {
    this.page.update((p) => Math.max(1, p - 1));
    void this.loadWorkItems();
  }

  // Broader than canDelete: also allows the item's current assignee (FR-016).
  // Delegates to the shared canEditWorkItem() (Feature 005) — this is its
  // second call site, alongside work-item-detail and the board's drag check.
  protected canEdit(item: WorkItem): boolean {
    const userId = this.authService.currentUser()?.id;
    if (userId === undefined) {
      return false;
    }
    return canEditWorkItem(item, userId, this.authService.currentRole());
  }

  // Narrower than canEdit: the current assignee alone cannot delete (FR-017/FR-018).
  protected canDelete(item: WorkItem): boolean {
    const userId = this.authService.currentUser()?.id;
    if (userId === undefined) {
      return false;
    }
    return item.createdByUserId === userId || this.isManagerOrAdmin();
  }

  private isManagerOrAdmin(): boolean {
    const role = this.authService.currentRole();
    return role === 'Manager' || role === 'Admin';
  }

  protected async onDelete(item: WorkItem): Promise<void> {
    // Fetched fresh, right before confirming, rather than carried on every flat-list
    // row — the row itself doesn't need a descendant count until the moment a delete
    // is attempted (FR-020, research.md §6).
    const detail = await this.workItemsService.getWorkItemDetail(item.id);
    const message =
      detail.totalDescendantCount > 0
        ? `Delete "${item.title}"? This will also delete ${detail.totalDescendantCount} nested item(s). This cannot be undone.`
        : `Delete "${item.title}"? This cannot be undone.`;
    if (!confirm(message)) {
      return;
    }
    try {
      await this.workItemsService.deleteWorkItem(item.id);
      this.notificationService.success(`"${item.title}" deleted.`);
    } catch {
      this.notificationService.error(`Could not delete "${item.title}". Please try again.`);
      return;
    }
    // Deleting a work item changes the project's totalWorkItemCount (used by the
    // project-level delete confirmation) and the tree's shape, so all three need
    // refreshing, not just the flat list.
    await this.loadProject();
    await this.loadWorkItems();
    await this.loadTree();
  }

  // Project edit/delete is a simple role check — no ownership dimension, unlike
  // work-item edit/delete (research.md §1).
  protected canManageProject(): boolean {
    return this.isManagerOrAdmin();
  }

  protected async onDeleteProject(): Promise<void> {
    const project = this.project();
    if (!project) {
      return;
    }
    // Uses totalWorkItemCount already fetched to render this page, rather than a
    // second dedicated endpoint (research.md §5).
    const confirmed = confirm(
      `Delete "${project.name}"? This will also delete ${project.totalWorkItemCount} work item(s). This cannot be undone.`
    );
    if (!confirmed) {
      return;
    }
    try {
      await this.projectsService.deleteProject(project.id);
    } catch {
      this.notificationService.error(`Could not delete "${project.name}". Please try again.`);
      return;
    }
    this.notificationService.success(`"${project.name}" deleted.`);
    await this.router.navigateByUrl('/projects');
  }
}
