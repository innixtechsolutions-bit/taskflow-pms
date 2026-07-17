import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { HeaderComponent } from './header.component';
import { AuthService, AuthState } from '../../auth/auth.service';

const signedInState: AuthState = {
  token: 'a-token',
  expiresAt: new Date(Date.now() + 60_000).toISOString(),
  fullName: 'Ada Lovelace',
  role: 'Developer',
};

function configure(logout = vi.fn()) {
  TestBed.configureTestingModule({
    imports: [HeaderComponent],
    providers: [provideRouter([]), { provide: AuthService, useValue: { currentUser: () => signedInState, logout } }],
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
});
