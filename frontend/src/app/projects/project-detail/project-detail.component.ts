import { Component, OnInit, inject, signal } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ProjectDetail, ProjectsService } from '../projects.service';
import {
  UserLookupItem,
  WorkItem,
  WorkItemsFilter,
  WorkItemsService,
  WorkItemTreeNode,
} from '../work-items.service';
import { AuthService } from '../../auth/auth.service';
import { PageHeaderComponent } from '../../shared/page-header/page-header.component';
import { StatusChipComponent } from '../../shared/status-chip/status-chip.component';
import { PriorityChipComponent } from '../../shared/priority-chip/priority-chip.component';
import { UserAvatarComponent } from '../../shared/user-avatar/user-avatar.component';
import { FriendlyDatePipe } from '../../shared/friendly-date.pipe';
import { NotificationService } from '../../shared/notification.service';
import { EmptyStateComponent } from '../../shared/empty-state/empty-state.component';

const STATUSES = ['ToDo', 'InProgress', 'Done'];
const TYPES = ['Epic', 'Story', 'Task', 'SubTask'];
const PRIORITIES = ['Low', 'Medium', 'High', 'Critical'];

@Component({
  selector: 'app-project-detail',
  standalone: true,
  imports: [
    RouterLink,
    NgTemplateOutlet,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatTableModule,
    PageHeaderComponent,
    StatusChipComponent,
    PriorityChipComponent,
    UserAvatarComponent,
    FriendlyDatePipe,
    EmptyStateComponent,
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
  private readonly projectId = Number(this.route.snapshot.paramMap.get('id'));

  // Column order for mat-table's structural directives.
  protected readonly displayedColumns = ['title', 'status', 'priority', 'actions'];

  protected readonly project = signal<ProjectDetail | null>(null);
  protected readonly workItems = signal<WorkItem[]>([]);
  protected readonly assignableUsers = signal<UserLookupItem[]>([]);

  protected readonly statuses = STATUSES;
  protected readonly types = TYPES;
  protected readonly priorities = PRIORITIES;

  protected readonly statusFilter = signal('');
  protected readonly typeFilter = signal('');
  protected readonly priorityFilter = signal('');
  protected readonly assigneeFilter = signal('');
  protected readonly searchFilter = signal('');
  protected readonly searchInput = signal('');

  protected readonly page = signal(1);
  protected readonly pageSize = 20;
  protected readonly totalCount = signal(0);

  // Flat is the default (Feature 002's unchanged behavior, FR-023); Tree is opt-in
  // via the toggle, per User Story 5's non-regression guarantee.
  protected readonly viewMode = signal<'flat' | 'tree'>('flat');
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
  }

  private async loadProject(): Promise<void> {
    this.project.set(await this.projectsService.getProject(this.projectId));
  }

  private async loadTree(): Promise<void> {
    this.treeNodes.set(await this.workItemsService.getWorkItemsTree(this.projectId));
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
      status: this.statusFilter() || undefined,
      type: this.typeFilter() || undefined,
      priority: this.priorityFilter() || undefined,
      assigneeUserId: this.assigneeFilter() ? Number(this.assigneeFilter()) : undefined,
      search: this.searchFilter() || undefined,
    };
  }

  private async loadWorkItems(): Promise<void> {
    const result = await this.workItemsService.getWorkItems(this.projectId, this.currentFilter());
    this.workItems.set(result.items);
    this.totalCount.set(result.totalCount);
  }

  // Distinguishes a genuinely empty project (FR-023) from filters that simply matched
  // nothing — the two need different messages.
  protected hasActiveFilters(): boolean {
    return !!(
      this.statusFilter() ||
      this.typeFilter() ||
      this.priorityFilter() ||
      this.assigneeFilter() ||
      this.searchFilter()
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
  protected canEdit(item: WorkItem): boolean {
    const userId = this.authService.currentUser()?.id;
    if (userId === undefined) {
      return false;
    }
    return item.createdByUserId === userId || item.assigneeUserId === userId || this.isManagerOrAdmin();
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
