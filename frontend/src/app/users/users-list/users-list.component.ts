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

  protected async onRoleChange(user: UserListItem, select: HTMLSelectElement): Promise<void> {
    const requestedRole = select.value as UserRole;
    this.errorMessage.set(null);
    try {
      const updated = await this.usersService.changeRole(user.id, requestedRole);
      this.items.update((items) => items.map((u) => (u.id === updated.id ? updated : u)));
    } catch {
      this.errorMessage.set('Could not change role. Please try again.');
      // A binding alone won't undo this: a native <select>'s DOM state changes the
      // instant the person picks an option, but Angular only re-pushes a binding when
      // the *bound expression* changes — and since the request failed, user.role never
      // did. Left alone, the dropdown would show the rejected role forever, even though
      // the database (and every other view of this user) still has the real one.
      select.value = user.role;
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
