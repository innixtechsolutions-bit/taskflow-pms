import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

// Feature 006 — status is no longer a fixed system-wide set, so it can no longer be a
// TypeScript string-literal union (that only works for closed sets). Category stays a
// closed 2-value set — it's what the system reasons about (open-item counts, tree
// "n/m done", overdue highlighting); ColorKey is the design system's fixed, curated
// chip palette (research.md #3) — both remain narrow unions.
export type WorkItemStatusCategory = 'Open' | 'Done';
export type ChipColor = 'Slate' | 'Blue' | 'Violet' | 'Amber' | 'Teal' | 'Rose' | 'Indigo' | 'Cyan' | 'Green' | 'Emerald';
export type WorkItemPriority = 'Low' | 'Medium' | 'High' | 'Critical';

// A project's own workflow column (Feature 006) — replaces the fixed WorkItemStatus
// union everywhere a project's status list is needed (dropdowns, filters, the
// Workflow management screen, board column headers).
export interface ProjectStatus {
  id: number;
  name: string;
  category: WorkItemStatusCategory;
  colorKey: ChipColor;
  position: number;
  itemCount: number;
}

export interface WorkItemRequest {
  type: string;
  title: string;
  description?: string;
  priority?: string;
  statusId?: number;
  assigneeUserId?: number;
  dueDate?: string;
  startDate?: string;
  parentWorkItemId?: number;
  // Always sent (even empty) once populated by the modal — the backend treats
  // an omitted/null Labels as "no labels" on PUT's replace-the-whole-set
  // semantics (data-model.md), so a real edit must include the full current set.
  labels?: string[];
  // Feature 008 — optional; omitted/undefined means "no sprint" (the backlog).
  sprintId?: number;
}

// Status fields are flattened (statusId/statusName/statusCategory/statusColorKey),
// not a nested object — mirrors the backend DTO shape 1:1, no client-side mapping.
export interface WorkItem {
  id: number;
  projectId: number;
  type: string;
  title: string;
  description: string | null;
  priority: WorkItemPriority;
  statusId: number;
  statusName: string;
  statusCategory: WorkItemStatusCategory;
  statusColorKey: ChipColor;
  assigneeUserId: number | null;
  assigneeName: string | null;
  dueDate: string | null;
  startDate: string | null;
  createdByUserId: number;
  createdByName: string;
  createdAt: string;
  updatedAt: string;
  parentWorkItemId: number | null;
  labels: string[];
  // Feature 008 — null means "no sprint" (the backlog).
  sprintId: number | null;
  sprintName: string | null;
}

export interface UserLookupItem {
  id: number;
  fullName: string;
}

export interface WorkItemLookupItem {
  id: number;
  title: string;
}

export interface WorkItemChild {
  id: number;
  title: string;
  type: string;
  statusId: number;
  statusName: string;
  statusCategory: WorkItemStatusCategory;
  statusColorKey: ChipColor;
  assigneeName: string | null;
}

export interface WorkItemDetail extends WorkItem {
  parentTitle: string | null;
  totalDescendantCount: number;
  children: WorkItemChild[];
}

export interface WorkItemTreeNode {
  id: number;
  type: string;
  title: string;
  statusId: number;
  statusName: string;
  statusCategory: WorkItemStatusCategory;
  statusColorKey: ChipColor;
  priority: WorkItemPriority;
  assigneeName: string | null;
  directChildrenCount: number;
  directChildrenDoneCount: number;
  children: WorkItemTreeNode[];
  labels: string[];
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface WorkItemsFilter {
  page?: number;
  pageSize?: number;
  statusId?: number;
  type?: string;
  priority?: string;
  assigneeUserId?: number;
  search?: string;
  label?: string;
}

// Feature 005 (Kanban Board). Columns come from the project's own ordered
// WorkflowStatus list (Feature 006) — the board never derives a column's
// name/color itself.
export interface BoardColumn {
  statusId: number;
  name: string;
  category: WorkItemStatusCategory;
  colorKey: ChipColor;
}

export interface WorkItemBoardCard {
  id: number;
  type: string;
  title: string;
  statusId: number;
  statusName: string;
  statusCategory: WorkItemStatusCategory;
  statusColorKey: ChipColor;
  priority: WorkItemPriority;
  assigneeUserId: number | null;
  assigneeName: string | null;
  dueDate: string | null;
  updatedAt: string;
  createdByUserId: number;
  directChildrenCount: number;
  directChildrenDoneCount: number;
  labels: string[];
}

export interface WorkItemBoard {
  columns: BoardColumn[];
  items: WorkItemBoardCard[];
}

// Feature 008 (US2) — the Backlog view's response shape. Sprints arrive
// pre-ordered soonest-start-first; BacklogItems are every filtered item with
// no sprint assignment (including every Epic, which can never have one).
export interface BacklogSprintSection {
  id: number;
  name: string;
  startDate: string;
  endDate: string;
  status: 'Planned' | 'Active' | 'Completed';
  items: WorkItem[];
}

export interface WorkItemBacklog {
  sprints: BacklogSprintSection[];
  backlogItems: WorkItem[];
}

@Injectable({ providedIn: 'root' })
export class WorkItemsService {
  private readonly http = inject(HttpClient);

  async createWorkItem(projectId: number, request: WorkItemRequest): Promise<WorkItem> {
    return firstValueFrom(this.http.post<WorkItem>(`/api/projects/${projectId}/work-items`, request));
  }

  // Bare-minimum listing was pulled forward into US4 (edit/delete controls need rows to
  // render next to — tasks.md's discovered-dependency note); this extends it with the
  // full filter/search/pagination set.
  async getWorkItems(projectId: number, filter: WorkItemsFilter = {}): Promise<PagedResult<WorkItem>> {
    const params: Record<string, string | number> = {};
    if (filter.page) params['page'] = filter.page;
    if (filter.pageSize) params['pageSize'] = filter.pageSize;
    if (filter.statusId) params['statusId'] = filter.statusId;
    if (filter.type) params['type'] = filter.type;
    if (filter.priority) params['priority'] = filter.priority;
    if (filter.assigneeUserId) params['assigneeUserId'] = filter.assigneeUserId;
    if (filter.search) params['search'] = filter.search;
    if (filter.label) params['label'] = filter.label;
    return firstValueFrom(
      this.http.get<PagedResult<WorkItem>>(`/api/projects/${projectId}/work-items`, { params })
    );
  }

  async getWorkItem(id: number): Promise<WorkItem> {
    return firstValueFrom(this.http.get<WorkItem>(`/api/work-items/${id}`));
  }

  // Same endpoint as getWorkItem() above — the response is a superset (parent link,
  // direct children, descendant count) that only the detail page needs.
  async getWorkItemDetail(id: number): Promise<WorkItemDetail> {
    return firstValueFrom(this.http.get<WorkItemDetail>(`/api/work-items/${id}`));
  }

  async updateWorkItem(id: number, request: WorkItemRequest): Promise<WorkItem> {
    return firstValueFrom(this.http.put<WorkItem>(`/api/work-items/${id}`, request));
  }

  async deleteWorkItem(id: number): Promise<void> {
    await firstValueFrom(this.http.delete<void>(`/api/work-items/${id}`));
  }

  async getAssignableUsers(): Promise<UserLookupItem[]> {
    return firstValueFrom(this.http.get<UserLookupItem[]>('/api/users/lookup'));
  }

  // Called whenever the form's Type field changes — the candidate list depends on
  // Type (data-model.md's Hierarchy rules table), so a fresh list is fetched each time
  // rather than filtering one big list client-side.
  async getParentCandidates(projectId: number, type: string): Promise<WorkItemLookupItem[]> {
    const result = await firstValueFrom(
      this.http.get<{ candidates: WorkItemLookupItem[] }>(
        `/api/projects/${projectId}/work-items/parent-candidates`,
        { params: { type } }
      )
    );
    return result.candidates;
  }

  // Unpaginated by design — a tree's shape doesn't compose with pagination, and at
  // this feature's scale returning the whole project's hierarchy in one response is
  // simpler than tree-aware paging (research.md §4).
  async getWorkItemsTree(projectId: number): Promise<WorkItemTreeNode[]> {
    return firstValueFrom(this.http.get<WorkItemTreeNode[]>(`/api/projects/${projectId}/work-items/tree`));
  }

  // Also unpaginated (FR-020) — every item in the project, flat, grouped by
  // status client-side using the returned columns' order (research.md #2).
  // Feature 008 (US5) — sprintId is optional; omitted means "All items"
  // (unchanged, FR-020), present means "Active sprint" mode (FR-017).
  async getBoard(projectId: number, sprintId?: number): Promise<WorkItemBoard> {
    const params: Record<string, number> = {};
    if (sprintId !== undefined) params['sprintId'] = sprintId;
    return firstValueFrom(this.http.get<WorkItemBoard>(`/api/projects/${projectId}/work-items/board`, { params }));
  }

  // Field-scoped, not the full updateWorkItem() PUT — the board's drag interaction
  // only ever changes status, and never carries fields (description,
  // parentWorkItemId) it would otherwise risk clobbering (research.md #3).
  async updateWorkItemStatus(id: number, statusId: number): Promise<WorkItem> {
    return firstValueFrom(this.http.patch<WorkItem>(`/api/work-items/${id}/status`, { statusId }));
  }

  // Feature 006 — a project's own workflow columns, in position order. Used by the
  // work-item form's Status dropdown, project-detail's status filter, the board's
  // column list, and the Workflow management screen.
  async getStatuses(projectId: number): Promise<ProjectStatus[]> {
    return firstValueFrom(this.http.get<ProjectStatus[]>(`/api/projects/${projectId}/statuses`));
  }

  // Feature 007 US5 — labels referenced by at least one work item in the project,
  // alphabetically ordered. Powers both the modal's create/suggest input and
  // the List view's label filter dropdown.
  async getProjectLabels(projectId: number): Promise<string[]> {
    return firstValueFrom(this.http.get<string[]>(`/api/projects/${projectId}/labels`));
  }

  // Feature 008 (US3) — field-scoped, mirrors updateWorkItemStatus above: the
  // Backlog's drag interaction only ever changes SprintId, never any field it
  // would otherwise risk clobbering. null clears the sprint (moves to backlog).
  async updateWorkItemSprint(id: number, sprintId: number | null): Promise<WorkItem> {
    return firstValueFrom(this.http.patch<WorkItem>(`/api/work-items/${id}/sprint`, { sprintId }));
  }

  // Feature 008 (US2) — same filter set as getWorkItems (page/pageSize don't
  // apply; the Backlog view is unpaginated, matching getBoard's precedent).
  async getBacklog(projectId: number, filter: WorkItemsFilter = {}): Promise<WorkItemBacklog> {
    const params: Record<string, string | number> = {};
    if (filter.statusId) params['statusId'] = filter.statusId;
    if (filter.type) params['type'] = filter.type;
    if (filter.priority) params['priority'] = filter.priority;
    if (filter.assigneeUserId) params['assigneeUserId'] = filter.assigneeUserId;
    if (filter.search) params['search'] = filter.search;
    if (filter.label) params['label'] = filter.label;
    return firstValueFrom(this.http.get<WorkItemBacklog>(`/api/projects/${projectId}/backlog`, { params }));
  }
}
