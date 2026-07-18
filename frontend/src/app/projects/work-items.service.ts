import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface WorkItemRequest {
  type: string;
  title: string;
  description?: string;
  priority?: string;
  status?: string;
  assigneeUserId?: number;
  dueDate?: string;
}

export interface WorkItem {
  id: number;
  projectId: number;
  type: string;
  title: string;
  description: string | null;
  priority: string;
  status: string;
  assigneeUserId: number | null;
  assigneeName: string | null;
  dueDate: string | null;
  createdByUserId: number;
  createdByName: string;
  createdAt: string;
  updatedAt: string;
}

export interface UserLookupItem {
  id: number;
  fullName: string;
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

  async updateWorkItem(id: number, request: WorkItemRequest): Promise<WorkItem> {
    return firstValueFrom(this.http.put<WorkItem>(`/api/work-items/${id}`, request));
  }

  async deleteWorkItem(id: number): Promise<void> {
    await firstValueFrom(this.http.delete<void>(`/api/work-items/${id}`));
  }

  async getAssignableUsers(): Promise<UserLookupItem[]> {
    return firstValueFrom(this.http.get<UserLookupItem[]>('/api/users/lookup'));
  }
}
