import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

// Mirrors backend/TaskFlow.Api/Data/Entities/WorkItem.cs's WorkItemStatus/
// WorkItemPriority enums by value name (Feature 004, FR-009/data-model.md) —
// narrowing these from `string` lets StatusChipComponent/PriorityChipComponent
// switch exhaustively, so a new enum value without a matching chip case is a
// compile error instead of a silently uncolored chip.
export type WorkItemStatus = 'ToDo' | 'InProgress' | 'Done';
export type WorkItemPriority = 'Low' | 'Medium' | 'High' | 'Critical';

export interface WorkItemRequest {
  type: string;
  title: string;
  description?: string;
  priority?: string;
  status?: string;
  assigneeUserId?: number;
  dueDate?: string;
  parentWorkItemId?: number;
}

export interface WorkItem {
  id: number;
  projectId: number;
  type: string;
  title: string;
  description: string | null;
  priority: WorkItemPriority;
  status: WorkItemStatus;
  assigneeUserId: number | null;
  assigneeName: string | null;
  dueDate: string | null;
  createdByUserId: number;
  createdByName: string;
  createdAt: string;
  updatedAt: string;
  parentWorkItemId: number | null;
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
  status: WorkItemStatus;
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
  status: WorkItemStatus;
  priority: WorkItemPriority;
  assigneeName: string | null;
  directChildrenCount: number;
  directChildrenDoneCount: number;
  children: WorkItemTreeNode[];
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
  status?: string;
  type?: string;
  priority?: string;
  assigneeUserId?: number;
  search?: string;
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
    if (filter.status) params['status'] = filter.status;
    if (filter.type) params['type'] = filter.type;
    if (filter.priority) params['priority'] = filter.priority;
    if (filter.assigneeUserId) params['assigneeUserId'] = filter.assigneeUserId;
    if (filter.search) params['search'] = filter.search;
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
}
