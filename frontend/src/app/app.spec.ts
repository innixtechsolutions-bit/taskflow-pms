import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { vi } from 'vitest';
import { App } from './app';
import { routes } from './app.routes';
import { AuthService, AuthState } from './auth/auth.service';
import { RegisterComponent } from './auth/register/register.component';
import { ProjectsListComponent } from './projects/projects-list/projects-list.component';
import { ProjectsService } from './projects/projects.service';

const signedInState: AuthState = {
  id: 1,
  token: 'a-token',
  expiresAt: new Date(Date.now() + 60_000).toISOString(),
  fullName: 'Ada Lovelace',
  role: 'Developer',
};

describe('App', () => {
  it('renders the register page', async () => {
    TestBed.configureTestingModule({
      providers: [provideRouter(routes), { provide: AuthService, useValue: { register: vi.fn() } }],
    });
    const harness = await RouterTestingHarness.create();

    const registerComponent = await harness.navigateByUrl('/register', RegisterComponent);

    expect(registerComponent).toBeTruthy();
  });

  it('redirects the empty path to /login when not signed in', async () => {
    TestBed.configureTestingModule({
      providers: [provideRouter(routes), { provide: AuthService, useValue: { currentUser: () => null, clearAuth: vi.fn() } }],
    });
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/');

    expect(TestBed.inject(Router).url).toBe('/login?returnUrl=%2F');
  });

  it('renders the home page for a signed-in visitor', async () => {
    TestBed.configureTestingModule({
      providers: [provideRouter(routes), { provide: AuthService, useValue: { currentUser: () => signedInState } }],
    });
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/');

    expect(harness.routeNativeElement?.textContent).toContain('Ada Lovelace');
  });

  // Bug report: after a successful login (home renders fine), clicking "Projects"
  // redirects to /login?returnUrl=%2Fprojects even though the visitor is signed in.
  // Reproduces the exact reported sequence — navigate to '/' first, then '/projects',
  // against the real routes/guard with one persistent signed-in AuthService.
  it('navigates to /projects after already having visited the home page while signed in', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes),
        { provide: AuthService, useValue: { currentUser: () => signedInState, currentRole: () => signedInState.role } },
        { provide: ProjectsService, useValue: { getProjects: vi.fn().mockResolvedValue({ items: [], totalCount: 0 }) } },
      ],
    });
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/');

    await harness.navigateByUrl('/projects', ProjectsListComponent);

    expect(TestBed.inject(Router).url).toBe('/projects');
  });

  // Same reproduction, but against the REAL AuthService (not a mock) — seeded via
  // setAuth() exactly the way a real login() call leaves it, to catch a bug in
  // AuthService's own signal/storage state rather than assuming it's sound.
  it('navigates to /projects using the real AuthService after visiting home', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes),
        { provide: ProjectsService, useValue: { getProjects: vi.fn().mockResolvedValue({ items: [], totalCount: 0 }) } },
      ],
    });
    TestBed.inject(AuthService).setAuth(signedInState);
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/');

    await harness.navigateByUrl('/projects', ProjectsListComponent);

    expect(TestBed.inject(Router).url).toBe('/projects');
  });

  it('does not wrap the page in the app shell when not signed in', () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes),
        { provide: AuthService, useValue: { currentUser: () => null, isAuthenticated: () => false, clearAuth: vi.fn() } },
      ],
    });
    const fixture = TestBed.createComponent(App);

    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('app-shell')).toBeNull();
  });

  it('wraps every authenticated page in the app shell', () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes),
        {
          provide: AuthService,
          useValue: { currentUser: () => signedInState, currentRole: () => signedInState.role, isAuthenticated: () => true, logout: vi.fn() },
        },
      ],
    });
    const fixture = TestBed.createComponent(App);

    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('app-shell')).toBeTruthy();
  });
});
