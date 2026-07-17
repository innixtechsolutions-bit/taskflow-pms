import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { UserRole } from '../auth/auth.service';

export interface UserListItem {
  id: number;
  fullName: string;
  email: string;
  role: UserRole;
  createdAt: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

@Injectable({ providedIn: 'root' })
export class UsersService {
  private readonly http = inject(HttpClient);

  async getUsers(page = 1, pageSize = 20): Promise<PagedResult<UserListItem>> {
    return firstValueFrom(
      this.http.get<PagedResult<UserListItem>>('/api/users', { params: { page, pageSize } })
    );
  }

  async changeRole(id: number, role: UserRole): Promise<UserListItem> {
    return firstValueFrom(this.http.put<UserListItem>(`/api/users/${id}/role`, { role }));
  }
}
