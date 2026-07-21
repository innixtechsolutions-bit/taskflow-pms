import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export type SprintStatus = 'Planned' | 'Active' | 'Completed';

// A project's own sprint (Feature 008). Mirrors the backend SprintDto shape
// 1:1, no client-side mapping -- same convention as every other DTO here.
export interface Sprint {
  id: number;
  projectId: number;
  name: string;
  startDate: string;
  endDate: string;
  status: SprintStatus;
  itemCount: number;
}

export interface CreateSprintRequest {
  name: string;
  startDate: string;
  endDate: string;
}

// Feature 008 — sprint CRUD/lifecycle, distinct from WorkItemsService the same
// way ProjectStatusService (Feature 006) is distinct from it: Sprint has its
// own independent lifecycle, not folded into work-item concerns.
@Injectable({ providedIn: 'root' })
export class SprintsService {
  private readonly http = inject(HttpClient);

  async getSprints(projectId: number): Promise<Sprint[]> {
    return firstValueFrom(this.http.get<Sprint[]>(`/api/projects/${projectId}/sprints`));
  }

  async createSprint(projectId: number, request: CreateSprintRequest): Promise<Sprint> {
    return firstValueFrom(this.http.post<Sprint>(`/api/projects/${projectId}/sprints`, request));
  }
}
