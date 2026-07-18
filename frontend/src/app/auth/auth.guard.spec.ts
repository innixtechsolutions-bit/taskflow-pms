import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  provideRouter,
  RouterStateSnapshot,
  UrlTree,
} from '@angular/router';
import { vi } from 'vitest';
import { authGuard } from './auth.guard';
import { AuthService, AuthState } from './auth.service';

const validState: AuthState = {
  id: 1,
  token: 'a-token',
  expiresAt: new Date(Date.now() + 60_000).toISOString(),
  fullName: 'Ada Lovelace',
  role: 'Developer',
};

const expiredState: AuthState = { ...validState, expiresAt: new Date(Date.now() - 60_000).toISOString() };

function configure(currentUser: AuthState | null) {
  const clearAuth = vi.fn();
  TestBed.configureTestingModule({
    providers: [provideRouter([]), { provide: AuthService, useValue: { currentUser: () => currentUser, clearAuth } }],
  });
  return { clearAuth };
}

function runGuard(url: string) {
  return TestBed.runInInjectionContext(() =>
    authGuard({} as ActivatedRouteSnapshot, { url } as RouterStateSnapshot)
  );
}

describe('authGuard', () => {
  it('allows activation when signed in with a valid session', () => {
    configure(validState);

    expect(runGuard('/users')).toBe(true);
  });

  it('redirects to /login with only a returnUrl when never signed in', () => {
    const { clearAuth } = configure(null);

    const result = runGuard('/users') as UrlTree;

    expect(result).toBeInstanceOf(UrlTree);
    expect(result.toString()).toContain('/login');
    expect(result.queryParams).toEqual({ returnUrl: '/users' });
    expect(clearAuth).not.toHaveBeenCalled();
  });

  it('clears state and redirects with an expired flag when the session has expired', () => {
    const { clearAuth } = configure(expiredState);

    const result = runGuard('/users') as UrlTree;

    expect(clearAuth).toHaveBeenCalled();
    expect(result.queryParams).toEqual({ returnUrl: '/users', expired: 'true' });
  });
});
