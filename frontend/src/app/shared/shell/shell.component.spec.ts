import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { vi } from 'vitest';
import { AppShellComponent } from './shell.component';
import { AuthService, AuthState } from '../../auth/auth.service';

const signedInState: AuthState = {
  id: 1,
  token: 'a-token',
  expiresAt: new Date(Date.now() + 60_000).toISOString(),
  fullName: 'Ada Lovelace',
  role: 'Developer',
};

function fakeAuthService(logout = vi.fn().mockResolvedValue(undefined)) {
  return {
    currentUser: () => signedInState,
    currentRole: () => signedInState.role,
    logout,
  };
}

describe('AppShellComponent', () => {
  it('renders a sidenav region and a content region', () => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthService, useValue: fakeAuthService() }],
    });
    const fixture = TestBed.createComponent(AppShellComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('mat-sidenav')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('mat-sidenav-content')).toBeTruthy();
  });

  it('applies the content max-width token to the content region', () => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthService, useValue: fakeAuthService() }],
    });
    const fixture = TestBed.createComponent(AppShellComponent);
    fixture.detectChanges();

    const inner: HTMLElement = fixture.nativeElement.querySelector('.shell-content-inner');
    expect(inner).toBeTruthy();
    expect(inner.style.maxWidth).toBe('var(--content-max-width)');
  });

  it('projects routed page content into the content region', () => {
    TestBed.configureTestingModule({
      imports: [HostWithProjectedContent],
      providers: [provideRouter([]), { provide: AuthService, useValue: fakeAuthService() }],
    });
    const fixture = TestBed.createComponent(HostWithProjectedContent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Routed page content');
  });

  it('renders the sidebar nav inside the sidenav', () => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthService, useValue: fakeAuthService() }],
    });
    const fixture = TestBed.createComponent(AppShellComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('app-sidebar-nav')).toBeTruthy();
  });

  it("logs out and navigates to /login when the sidebar's logout output fires", async () => {
    const logout = vi.fn().mockResolvedValue(undefined);
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'login', children: [] }]),
        { provide: AuthService, useValue: fakeAuthService(logout) },
      ],
    });
    const fixture = TestBed.createComponent(AppShellComponent);
    fixture.detectChanges();
    const navigateSpy = vi.spyOn(TestBed.inject(Router), 'navigateByUrl');

    (fixture.nativeElement.querySelector('.user-menu-trigger') as HTMLButtonElement).click();
    fixture.detectChanges();
    (document.querySelector('.logout-menu-item') as HTMLButtonElement).click();
    await fixture.whenStable();

    expect(logout).toHaveBeenCalled();
    expect(navigateSpy).toHaveBeenCalledWith('/login');
  });
});

@Component({
  standalone: true,
  imports: [AppShellComponent],
  template: `<app-shell><p>Routed page content</p></app-shell>`,
})
class HostWithProjectedContent {}
