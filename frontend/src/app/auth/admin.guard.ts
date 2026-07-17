import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { authGuard } from './auth.guard';
import { AuthService } from './auth.service';

// Composes with authGuard rather than duplicating its session/expiry logic: first the
// same "signed in with a valid session" check (returnUrl/expired handling), then an
// additional Admin-only check. This exists purely as a UX nicety (FR-021) — the actual
// enforcement is UsersController's [Authorize(Roles = "Admin")], which refuses a
// non-Admin regardless of how the request is made (FR-017).
export const adminGuard: CanActivateFn = (route, state) => {
  const authResult = authGuard(route, state);
  if (authResult !== true) {
    return authResult;
  }

  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.currentRole() === 'Admin') {
    return true;
  }

  return router.createUrlTree(['/']);
};
