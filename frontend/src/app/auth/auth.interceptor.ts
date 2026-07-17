import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

// A functional interceptor (Angular's standalone-era style, matching the constitution's
// "no NgModules" rule): attaches the bearer token to every outgoing request, and reacts
// to a 401 by clearing auth state so a stale/expired token doesn't linger in the signal
// or storage.
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const token = authService.currentUser()?.token;

  const authorizedReq = token
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;

  return next(authorizedReq).pipe(
    catchError((error: unknown) => {
      // Only redirect with "session expired" when we thought we were signed in —
      // otherwise this would hijack a plain wrong-password 401 from the login form
      // itself (fired while isAuthenticated() is still false) and yank the visitor
      // away before LoginComponent ever gets to show its own error message.
      if (error instanceof HttpErrorResponse && error.status === 401 && authService.isAuthenticated()) {
        authService.clearAuth();
        router.navigate(['/login'], { queryParams: { expired: 'true' } });
      }
      return throwError(() => error);
    })
  );
};
