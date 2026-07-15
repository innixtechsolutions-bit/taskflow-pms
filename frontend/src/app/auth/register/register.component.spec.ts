import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { vi } from 'vitest';
import { RegisterComponent } from './register.component';
import { AuthService } from '../auth.service';

function setInputValue(el: HTMLInputElement, value: string): void {
  el.value = value;
  el.dispatchEvent(new Event('input'));
}

function fillAndSubmit(root: HTMLElement): void {
  setInputValue(root.querySelector<HTMLInputElement>('#fullName')!, 'Ada Lovelace');
  setInputValue(root.querySelector<HTMLInputElement>('#email')!, 'ada@example.com');
  setInputValue(root.querySelector<HTMLInputElement>('#password')!, 'Password1');
  root.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
}

describe('RegisterComponent', () => {
  let registerSpy: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    registerSpy = vi.fn();
    TestBed.configureTestingModule({
      imports: [RegisterComponent],
      providers: [provideRouter([]), { provide: AuthService, useValue: { register: registerSpy } }],
    });
  });

  it('shows the password rules before the form is submitted', () => {
    const fixture = TestBed.createComponent(RegisterComponent);
    fixture.detectChanges();

    const hint = fixture.nativeElement.querySelector('.password-hint');
    expect(hint?.textContent).toContain('at least 8 characters');
    expect(registerSpy).not.toHaveBeenCalled();
  });

  it('shows a duplicate-email error returned by the server', async () => {
    registerSpy.mockRejectedValue(new HttpErrorResponse({ status: 409 }));
    const fixture = TestBed.createComponent(RegisterComponent);
    fixture.detectChanges();

    fillAndSubmit(fixture.nativeElement);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.server-error')?.textContent).toContain('already exists');
  });

  it('navigates to the home page after a successful submit', async () => {
    registerSpy.mockResolvedValue(undefined);
    const fixture = TestBed.createComponent(RegisterComponent);
    fixture.detectChanges();
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigateByUrl');

    fillAndSubmit(fixture.nativeElement);
    await fixture.whenStable();

    expect(navigateSpy).toHaveBeenCalledWith('/');
  });
});
