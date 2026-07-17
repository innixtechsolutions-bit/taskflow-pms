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

@Injectable({ providedIn: 'root' })
export class WorkItemsService {
  private readonly http = inject(HttpClient);

  async createWorkItem(projectId: number, request: WorkItemRequest): Promise<WorkItem> {
    return firstValueFrom(this.http.post<WorkItem>(`/api/projects/${projectId}/work-items`, request));
  }

  async getAssignableUsers(): Promise<UserLookupItem[]> {
    return firstValueFrom(this.http.get<UserLookupItem[]>('/api/users/lookup'));
  }
}
