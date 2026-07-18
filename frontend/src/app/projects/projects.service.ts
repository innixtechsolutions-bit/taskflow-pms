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

export interface ProjectListItem {
  id: number;
  name: string;
  createdByName: string;
  createdAt: string;
  openWorkItemCount: number;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

@Injectable({ providedIn: 'root' })
export class ProjectsService {
  private readonly http = inject(HttpClient);

  async createProject(request: ProjectRequest): Promise<ProjectDetail> {
    return firstValueFrom(this.http.post<ProjectDetail>('/api/projects', request));
  }

  async getProjects(page = 1, pageSize = 20): Promise<PagedResult<ProjectListItem>> {
    return firstValueFrom(
      this.http.get<PagedResult<ProjectListItem>>('/api/projects', { params: { page, pageSize } })
    );
  }

  async getProject(id: number): Promise<ProjectDetail> {
    return firstValueFrom(this.http.get<ProjectDetail>(`/api/projects/${id}`));
  }

  async updateProject(id: number, request: ProjectRequest): Promise<ProjectDetail> {
    return firstValueFrom(this.http.put<ProjectDetail>(`/api/projects/${id}`, request));
  }

  async deleteProject(id: number): Promise<void> {
    await firstValueFrom(this.http.delete<void>(`/api/projects/${id}`));
  }
}
