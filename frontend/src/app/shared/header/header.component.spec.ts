import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { HeaderComponent } from './header.component';
import { AuthService, AuthState, UserRole } from '../../auth/auth.service';

function stateFor(role: UserRole): AuthState {
  return {
    id: 1,
    token: 'a-token',
    expiresAt: new Date(Date.now() + 60_000).toISOString(),
    fullName: 'Ada Lovelace',
    role,
  };
}

function configure(role: UserRole = 'Developer', logout = vi.fn()) {
  const state = stateFor(role);
  TestBed.configureTestingModule({
    imports: [HeaderComponent],
    providers: [
      provideRouter([]),
      { provide: AuthService, useValue: { currentUser: () => state, currentRole: () => state.role, logout } },
    ],
  });
  return logout;
}

describe('HeaderComponent', () => {
  it("displays the signed-in person's name and role", () => {
    configure();
    const fixture = TestBed.createComponent(HeaderComponent);

    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Ada Lovelace');
    expect(text).toContain('Developer');
  });

  it('ends the session when the logout button is clicked', () => {
    const logout = configure();
    const fixture = TestBed.createComponent(HeaderComponent);
    fixture.detectChanges();

    fixture.nativeElement.querySelector('button')!.click();

    expect(logout).toHaveBeenCalled();
  });

  it('shows a Users navigation link for an Admin', () => {
    configure('Admin');
    const fixture = TestBed.createComponent(HeaderComponent);

    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('a[href="/users"]')).toBeTruthy();
  });

  it('does not show a Users navigation link for a non-Admin', () => {
    configure('Developer');
    const fixture = TestBed.createComponent(HeaderComponent);

    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('a[href="/users"]')).toBeNull();
  });
});
