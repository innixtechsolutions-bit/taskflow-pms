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

@Injectable({ providedIn: 'root' })
export class WorkItemsService {
  private readonly http = inject(HttpClient);

  async createWorkItem(projectId: number, request: WorkItemRequest): Promise<WorkItem> {
    return firstValueFrom(this.http.post<WorkItem>(`/api/projects/${projectId}/work-items`, request));
  }

  // Bare-minimum listing (no filters yet — see US6), pulled forward because US4's
  // edit/delete controls need rows to render next to (tasks.md's discovered-
  // dependency note).
  async getWorkItems(projectId: number): Promise<PagedResult<WorkItem>> {
    return firstValueFrom(this.http.get<PagedResult<WorkItem>>(`/api/projects/${projectId}/work-items`));
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
