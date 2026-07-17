import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { vi } from 'vitest';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

function configure(isAuthenticated: boolean) {
  const clearAuth = vi.fn();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(withInterceptors([authInterceptor])),
      provideHttpClientTesting(),
      provideRouter([]),
      { provide: AuthService, useValue: { currentUser: () => null, isAuthenticated: () => isAuthenticated, clearAuth } },
    ],
  });
  return { clearAuth };
}

describe('authInterceptor', () => {
  it('clears state and redirects to /login?expired=true on a 401 while previously signed in', async () => {
    const { clearAuth } = configure(true);
    const http = TestBed.inject(HttpClient);
    const httpMock = TestBed.inject(HttpTestingController);
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigate');

    http.get('/api/auth/me').subscribe({ error: () => {} });
    httpMock.expectOne('/api/auth/me').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(clearAuth).toHaveBeenCalled();
    expect(navigateSpy).toHaveBeenCalledWith(['/login'], { queryParams: { expired: 'true' } });
  });

  it('does not redirect on a 401 while not signed in (e.g. a failed login attempt)', async () => {
    const { clearAuth } = configure(false);
    const http = TestBed.inject(HttpClient);
    const httpMock = TestBed.inject(HttpTestingController);
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigate');

    http.post('/api/auth/login', {}).subscribe({ error: () => {} });
    httpMock.expectOne('/api/auth/login').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(clearAuth).not.toHaveBeenCalled();
    expect(navigateSpy).not.toHaveBeenCalled();
  });
});
