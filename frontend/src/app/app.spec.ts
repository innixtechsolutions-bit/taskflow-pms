import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { vi } from 'vitest';
import { App } from './app';
import { routes } from './app.routes';
import { AuthService, AuthState } from './auth/auth.service';
import { RegisterComponent } from './auth/register/register.component';

const signedInState: AuthState = {
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

  it('does not show the header when not signed in', () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes),
        { provide: AuthService, useValue: { currentUser: () => null, isAuthenticated: () => false, clearAuth: vi.fn() } },
      ],
    });
    const fixture = TestBed.createComponent(App);

    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('header')).toBeNull();
  });

  it('shows the header with the signed-in name on every authenticated page', () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes),
        { provide: AuthService, useValue: { currentUser: () => signedInState, isAuthenticated: () => true, logout: vi.fn() } },
      ],
    });
    const fixture = TestBed.createComponent(App);

    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('header')?.textContent).toContain('Ada Lovelace');
  });
});
