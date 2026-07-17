import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface ProjectRequest {
  name: string;
  description?: string;
}

export interface ProjectDetail {
  id: number;
  name: string;
  description: string | null;
  createdByName: string;
  createdAt: string;
  totalWorkItemCount: number;
}

@Injectable({ providedIn: 'root' })
export class ProjectsService {
  private readonly http = inject(HttpClient);

  async createProject(request: ProjectRequest): Promise<ProjectDetail> {
    return firstValueFrom(this.http.post<ProjectDetail>('/api/projects', request));
  }
}
