import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { vi } from 'vitest';
import { LoginComponent } from './login.component';
import { AuthService } from '../auth.service';

function setInputValue(el: HTMLInputElement, value: string): void {
  el.value = value;
  el.dispatchEvent(new Event('input'));
}

function fillAndSubmit(root: HTMLElement): void {
  setInputValue(root.querySelector<HTMLInputElement>('#email')!, 'ada@example.com');
  setInputValue(root.querySelector<HTMLInputElement>('#password')!, 'Password1');
  root.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
}

function configure(queryParams: Record<string, string> = {}) {
  const loginSpy = vi.fn();
  TestBed.configureTestingModule({
    imports: [LoginComponent],
    providers: [
      provideRouter([]),
      { provide: AuthService, useValue: { login: loginSpy } },
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(queryParams) } } },
    ],
  });
  return loginSpy;
}

describe('LoginComponent', () => {
  it('shows the generic invalid-credentials message on a 401, without saying which field was wrong', async () => {
    const loginSpy = configure();
    loginSpy.mockRejectedValue(new HttpErrorResponse({ status: 401 }));
    const fixture = TestBed.createComponent(LoginComponent);
    fixture.detectChanges();

    fillAndSubmit(fixture.nativeElement);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.server-error')?.textContent).toContain('Invalid email or password.');
  });

  it('shows the rate-limit message on a 429', async () => {
    const loginSpy = configure();
    loginSpy.mockRejectedValue(new HttpErrorResponse({ status: 429 }));
    const fixture = TestBed.createComponent(LoginComponent);
    fixture.detectChanges();

    fillAndSubmit(fixture.nativeElement);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.server-error')?.textContent).toContain('Too many attempts, try again later.');
  });

  it('shows a session-expired notice when routed here with ?expired=true', () => {
    configure({ expired: 'true' });
    const fixture = TestBed.createComponent(LoginComponent);

    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.session-expired')?.textContent).toContain('Your session has expired.');
  });

  it('navigates to the returnUrl after a successful login', async () => {
    const loginSpy = configure({ returnUrl: '/users' });
    loginSpy.mockResolvedValue(undefined);
    const fixture = TestBed.createComponent(LoginComponent);
    fixture.detectChanges();
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigateByUrl');

    fillAndSubmit(fixture.nativeElement);
    await fixture.whenStable();

    expect(navigateSpy).toHaveBeenCalledWith('/users');
  });

  it('navigates to the home page after a successful login with no returnUrl', async () => {
    const loginSpy = configure();
    loginSpy.mockResolvedValue(undefined);
    const fixture = TestBed.createComponent(LoginComponent);
    fixture.detectChanges();
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigateByUrl');

    fillAndSubmit(fixture.nativeElement);
    await fixture.whenStable();

    expect(navigateSpy).toHaveBeenCalledWith('/');
  });
});
