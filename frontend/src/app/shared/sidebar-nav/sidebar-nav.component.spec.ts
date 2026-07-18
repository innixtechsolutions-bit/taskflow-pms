import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { vi } from 'vitest';
import { SidebarNavComponent } from './sidebar-nav.component';
import { AuthService, AuthState, UserRole } from '../../auth/auth.service';

function stateFor(role: UserRole): AuthState {
  return {
    id: 1,
    token: 'a-token',
    expiresAt: new Date(Date.now() + 60_000).toISOString(),
    fullName: 'Ada Lovelace',
    role,
  };
}

async function render(role: UserRole = 'Developer', initialUrl = '/projects') {
  TestBed.resetTestingModule();
  const state = stateFor(role);
  TestBed.configureTestingModule({
    imports: [SidebarNavComponent],
    providers: [
      provideRouter([
        { path: '', children: [] },
        { path: 'projects', children: [] },
        { path: 'users', children: [] },
      ]),
      { provide: AuthService, useValue: { currentUser: () => state, currentRole: () => state.role } },
    ],
  });
  const router = TestBed.inject(Router);
  await router.navigateByUrl(initialUrl);
  const fixture = TestBed.createComponent(SidebarNavComponent);
  fixture.detectChanges();
  return fixture;
}

describe('SidebarNavComponent', () => {
  it("shows the signed-in user's name and role in the user block", async () => {
    const fixture = await render('Developer');

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Ada Lovelace');
    expect(text).toContain('Developer');
  });

  it('marks the nav link matching the current route as active, and no other', async () => {
    const fixture = await render('Developer', '/projects');

    const projectsLink = fixture.nativeElement.querySelector('a[href="/projects"]');
    const dashboardLink = fixture.nativeElement.querySelector('a[href="/"]');
    expect(projectsLink.classList.contains('active-nav-item')).toBe(true);
    expect(dashboardLink.classList.contains('active-nav-item')).toBe(false);
  });

  it('shows the Users link for an Admin', async () => {
    const fixture = await render('Admin');
    expect(fixture.nativeElement.querySelector('a[href="/users"]')).toBeTruthy();
  });

  it('does not show the Users link for a non-Admin', async () => {
    const developerFixture = await render('Developer');
    expect(developerFixture.nativeElement.querySelector('a[href="/users"]')).toBeNull();

    const managerFixture = await render('Manager');
    expect(managerFixture.nativeElement.querySelector('a[href="/users"]')).toBeNull();
  });

  it('shows the Projects link for every role', async () => {
    for (const role of ['Developer', 'Manager', 'Admin'] as const) {
      const fixture = await render(role);
      expect(fixture.nativeElement.querySelector('a[href="/projects"]')).toBeTruthy();
    }
  });

  it('emits logout when "Log out" is chosen from the user menu', async () => {
    const fixture = await render('Developer');
    const logoutSpy = vi.fn();
    fixture.componentInstance.logout.subscribe(logoutSpy);

    (fixture.nativeElement.querySelector('.user-menu-trigger') as HTMLButtonElement).click();
    fixture.detectChanges();
    const menuButton = document.querySelector('.logout-menu-item') as HTMLButtonElement;
    menuButton.click();

    expect(logoutSpy).toHaveBeenCalled();
  });
});
