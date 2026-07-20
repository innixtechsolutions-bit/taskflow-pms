import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ChipColor, ProjectStatus, WorkItemStatusCategory } from './work-items.service';

export interface CreateWorkflowStatusRequest {
  name: string;
  category: WorkItemStatusCategory;
  position?: number;
}

export interface UpdateWorkflowStatusRequest {
  name?: string;
  colorKey?: ChipColor;
}

// Feature 006 — the Workflow management screen's own service (add/rename/reorder/
// delete, US3-US6), distinct from WorkItemsService.getStatuses() which every
// read-only status consumer (board, dropdowns, filters) already uses.
@Injectable({ providedIn: 'root' })
export class ProjectStatusService {
  private readonly http = inject(HttpClient);

  async getStatuses(projectId: number): Promise<ProjectStatus[]> {
    return firstValueFrom(this.http.get<ProjectStatus[]>(`/api/projects/${projectId}/statuses`));
  }

  async createStatus(projectId: number, request: CreateWorkflowStatusRequest): Promise<ProjectStatus> {
    return firstValueFrom(this.http.post<ProjectStatus>(`/api/projects/${projectId}/statuses`, request));
  }

  async updateStatus(
    projectId: number,
    statusId: number,
    request: UpdateWorkflowStatusRequest
  ): Promise<ProjectStatus> {
    return firstValueFrom(this.http.put<ProjectStatus>(`/api/projects/${projectId}/statuses/${statusId}`, request));
  }

  async reorderStatuses(projectId: number, orderedStatusIds: number[]): Promise<ProjectStatus[]> {
    return firstValueFrom(
      this.http.put<ProjectStatus[]>(`/api/projects/${projectId}/statuses/reorder`, { orderedStatusIds })
    );
  }

  async deleteStatus(projectId: number, statusId: number, destinationStatusId?: number): Promise<void> {
    const params: Record<string, number> = {};
    if (destinationStatusId !== undefined) {
      params['destinationStatusId'] = destinationStatusId;
    }
    await firstValueFrom(this.http.delete<void>(`/api/projects/${projectId}/statuses/${statusId}`, { params }));
  }
}
