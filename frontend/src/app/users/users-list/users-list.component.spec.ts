import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { UsersListComponent } from './users-list.component';
import { UserListItem, UsersService } from '../users.service';

const sampleUsers: UserListItem[] = [
  { id: 1, fullName: 'Ada Lovelace', email: 'ada@example.com', role: 'Developer', createdAt: '2026-01-01T00:00:00Z' },
  { id: 2, fullName: 'Grace Hopper', email: 'grace@example.com', role: 'Manager', createdAt: '2026-01-02T00:00:00Z' },
];

function configure(
  getUsers = vi.fn().mockResolvedValue({ items: sampleUsers, page: 1, pageSize: 20, totalCount: 2 }),
  changeRole = vi.fn()
) {
  TestBed.configureTestingModule({
    imports: [UsersListComponent],
    providers: [{ provide: UsersService, useValue: { getUsers, changeRole } }],
  });
  return { getUsers, changeRole };
}

describe('UsersListComponent', () => {
  it('renders the paginated list of users', async () => {
    configure();
    const fixture = TestBed.createComponent(UsersListComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Ada Lovelace');
    expect(text).toContain('grace@example.com');
  });

  it('triggers a role change when a new role is selected', async () => {
    const updated: UserListItem = { ...sampleUsers[0], role: 'Manager' };
    const { changeRole } = configure(undefined, vi.fn().mockResolvedValue(updated));
    const fixture = TestBed.createComponent(UsersListComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const select = fixture.nativeElement.querySelector('select') as HTMLSelectElement;
    select.value = 'Manager';
    select.dispatchEvent(new Event('change'));
    await fixture.whenStable();

    expect(changeRole).toHaveBeenCalledWith(1, 'Manager');
  });
});
