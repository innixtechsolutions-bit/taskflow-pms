import { Component, OnInit, inject, signal } from '@angular/core';
import { UserRole } from '../../auth/auth.service';
import { UserListItem, UsersService } from '../users.service';

const ROLES: UserRole[] = ['Developer', 'Manager', 'Admin'];

@Component({
  selector: 'app-users-list',
  standalone: true,
  templateUrl: './users-list.component.html',
})
export class UsersListComponent implements OnInit {
  private readonly usersService = inject(UsersService);

  protected readonly roles = ROLES;
  protected readonly items = signal<UserListItem[]>([]);
  protected readonly page = signal(1);
  protected readonly pageSize = 20;
  protected readonly totalCount = signal(0);
  protected readonly errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    void this.load();
  }

  protected async onRoleChange(user: UserListItem, role: string): Promise<void> {
    this.errorMessage.set(null);
    try {
      const updated = await this.usersService.changeRole(user.id, role as UserRole);
      this.items.update((items) => items.map((u) => (u.id === updated.id ? updated : u)));
    } catch {
      this.errorMessage.set('Could not change role. Please try again.');
    }
  }

  protected nextPage(): void {
    this.page.update((p) => p + 1);
    void this.load();
  }

  protected prevPage(): void {
    this.page.update((p) => Math.max(1, p - 1));
    void this.load();
  }

  private async load(): Promise<void> {
    const result = await this.usersService.getUsers(this.page(), this.pageSize);
    this.items.set(result.items);
    this.totalCount.set(result.totalCount);
  }
}
