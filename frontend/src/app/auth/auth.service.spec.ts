import { TestBed } from '@angular/core/testing';
import { AuthService, AuthState, AUTH_STORAGE_KEY } from './auth.service';

const sampleState: AuthState = {
  id: 1,
  token: 'sample-token',
  expiresAt: '2026-07-15T20:00:00.000Z',
  fullName: 'Ada Lovelace',
  role: 'Developer',
};

describe('AuthService', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('initializes its signal from an existing localStorage value', () => {
    localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(sampleState));

    const service = TestBed.inject(AuthService);

    expect(service.currentUser()).toEqual(sampleState);
  });

  it('initializes to null when localStorage has no auth state', () => {
    const service = TestBed.inject(AuthService);

    expect(service.currentUser()).toBeNull();
    expect(service.isAuthenticated()).toBe(false);
    expect(service.currentRole()).toBeNull();
  });

  it('computed signals reflect the current state', () => {
    const service = TestBed.inject(AuthService);

    service.setAuth(sampleState);

    expect(service.isAuthenticated()).toBe(true);
    expect(service.currentRole()).toBe('Developer');
  });

  it('setAuth persists the state to localStorage', () => {
    const service = TestBed.inject(AuthService);

    service.setAuth(sampleState);

    expect(JSON.parse(localStorage.getItem(AUTH_STORAGE_KEY)!)).toEqual(sampleState);
  });

  it('clearAuth resets the signal and removes the localStorage entry', () => {
    const service = TestBed.inject(AuthService);
    service.setAuth(sampleState);

    service.clearAuth();

    expect(service.currentUser()).toBeNull();
    expect(service.isAuthenticated()).toBe(false);
    expect(localStorage.getItem(AUTH_STORAGE_KEY)).toBeNull();
  });
});
