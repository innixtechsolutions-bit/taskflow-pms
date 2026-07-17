import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export type UserRole = 'Developer' | 'Manager' | 'Admin';

export interface AuthState {
  token: string;
  expiresAt: string;
  fullName: string;
  role: UserRole;
}

export interface RegisterRequest {
  fullName: string;
  email: string;
  password: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

// Matches the backend's AuthResponse DTO (ASP.NET Core's default JSON naming policy
// is camelCase, so C#'s Token/ExpiresAt/FullName/Role become these field names).
interface AuthApiResponse {
  token: string;
  expiresAt: string;
  fullName: string;
  role: UserRole;
}

export const AUTH_STORAGE_KEY = 'taskflow.auth';

// A root-provided signal is the single source of truth for "who is signed in" —
// the interceptor, the route guard, and the header component all read it via the
// computed signals below instead of each re-deriving it from localStorage.
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly state = signal<AuthState | null>(readFromStorage());

  readonly currentUser = computed(() => this.state());
  readonly isAuthenticated = computed(() => this.state() !== null);
  readonly currentRole = computed<UserRole | null>(() => this.state()?.role ?? null);

  setAuth(newState: AuthState): void {
    this.state.set(newState);
    localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(newState));
  }

  clearAuth(): void {
    this.state.set(null);
    localStorage.removeItem(AUTH_STORAGE_KEY);
  }

  async register(request: RegisterRequest): Promise<void> {
    const response = await firstValueFrom(
      this.http.post<AuthApiResponse>('/api/auth/register', request)
    );
    this.setAuth(response);
  }

  async login(request: LoginRequest): Promise<void> {
    const response = await firstValueFrom(
      this.http.post<AuthApiResponse>('/api/auth/login', request)
    );
    this.setAuth(response);
  }

  // The server has no session to end (see AuthController.Logout's comment on stateless
  // JWTs) — this call exists for symmetry/future-proofing, not because skipping it and
  // just clearing local state would behave any differently today.
  async logout(): Promise<void> {
    try {
      await firstValueFrom(this.http.post('/api/auth/logout', null));
    } finally {
      this.clearAuth();
    }
  }
}

function readFromStorage(): AuthState | null {
  const raw = localStorage.getItem(AUTH_STORAGE_KEY);
  if (!raw) {
    return null;
  }
  try {
    return JSON.parse(raw) as AuthState;
  } catch {
    return null;
  }
}
