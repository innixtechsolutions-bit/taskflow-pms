import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

// Functional guard (Angular's standalone-era style, matching the interceptor/AuthService):
// this is a UX nicety only — the server enforces identity/role on every request
// regardless (FR-021), so there's no security consequence to a client bypassing this.
export const authGuard: CanActivateFn = (_route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  const user = authService.currentUser();
  const hasValidSession = user !== null && new Date(user.expiresAt).getTime() > Date.now();

  if (hasValidSession) {
    return true;
  }

  // Distinguishes "was signed in, but the token has since expired" from "never signed
  // in" (FR-010 vs FR-012) — only the former clears state and shows the expired-session
  // message; a plain unauthenticated visit gets just the returnUrl.
  const wasSignedIn = user !== null;
  if (wasSignedIn) {
    authService.clearAuth();
  }

  return router.createUrlTree(['/login'], {
    queryParams: wasSignedIn ? { returnUrl: state.url, expired: 'true' } : { returnUrl: state.url },
  });
};
