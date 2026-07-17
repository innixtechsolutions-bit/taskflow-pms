import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { vi } from 'vitest';
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
});
