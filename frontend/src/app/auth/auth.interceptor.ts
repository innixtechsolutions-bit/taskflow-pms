import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

// A functional interceptor (Angular's standalone-era style, matching the constitution's
// "no NgModules" rule): attaches the bearer token to every outgoing request, and clears
// auth state on a 401 so a stale/expired token doesn't linger in the signal or storage.
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const token = authService.currentUser()?.token;

  const authorizedReq = token
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;

  return next(authorizedReq).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        authService.clearAuth();
      }
      return throwError(() => error);
    })
  );
};
