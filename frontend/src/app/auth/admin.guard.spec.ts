import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  provideRouter,
  RouterStateSnapshot,
  UrlTree,
} from '@angular/router';
import { vi } from 'vitest';
import { adminGuard } from './admin.guard';
import { AuthService, AuthState } from './auth.service';

const validAdmin: AuthState = {
  token: 'a-token',
  expiresAt: new Date(Date.now() + 60_000).toISOString(),
  fullName: 'Ada Lovelace',
  role: 'Admin',
};

const validDeveloper: AuthState = { ...validAdmin, role: 'Developer' };

function configure(currentUser: AuthState | null) {
  const clearAuth = vi.fn();
  TestBed.configureTestingModule({
    providers: [provideRouter([]), { provide: AuthService, useValue: { currentUser: () => currentUser, currentRole: () => currentUser?.role ?? null, clearAuth } }],
  });
  return { clearAuth };
}

function runGuard(url: string) {
  return TestBed.runInInjectionContext(() =>
    adminGuard({} as ActivatedRouteSnapshot, { url } as RouterStateSnapshot)
  );
}

describe('adminGuard', () => {
  it('allows activation for a signed-in Admin', () => {
    configure(validAdmin);

    expect(runGuard('/users')).toBe(true);
  });

  it('redirects a signed-in non-Admin to the home page, not to login', () => {
    configure(validDeveloper);

    const result = runGuard('/users') as UrlTree;

    expect(result).toBeInstanceOf(UrlTree);
    expect(result.toString()).toBe('/');
  });

  it('redirects an unauthenticated visitor to /login with a returnUrl (delegates to authGuard)', () => {
    configure(null);

    const result = runGuard('/users') as UrlTree;

    expect(result).toBeInstanceOf(UrlTree);
    expect(result.queryParams).toEqual({ returnUrl: '/users' });
  });
});
