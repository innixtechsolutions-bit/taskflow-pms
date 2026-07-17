import { TestBed } from '@angular/core/testing';
import { HomeComponent } from './home.component';
import { AuthService, AuthState } from '../auth/auth.service';

const signedInState: AuthState = {
  id: 1,
  token: 'a-token',
  expiresAt: new Date(Date.now() + 60_000).toISOString(),
  fullName: 'Ada Lovelace',
  role: 'Developer',
};

describe('HomeComponent', () => {
  it('shows the signed-in person\'s name and role', () => {
    TestBed.configureTestingModule({
      imports: [HomeComponent],
      providers: [{ provide: AuthService, useValue: { currentUser: () => signedInState } }],
    });
    const fixture = TestBed.createComponent(HomeComponent);

    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Ada Lovelace');
    expect(text).toContain('Developer');
  });
});
