import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { vi } from 'vitest';
import { routes } from './app.routes';
import { AuthService } from './auth/auth.service';
import { RegisterComponent } from './auth/register/register.component';

describe('App', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter(routes), { provide: AuthService, useValue: { register: vi.fn() } }],
    });
  });

  it('should create the app', async () => {
    const harness = await RouterTestingHarness.create();
    const registerComponent = await harness.navigateByUrl('/register', RegisterComponent);
    expect(registerComponent).toBeTruthy();
  });

  it('redirects the empty path to the register page', async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/');
    expect(harness.routeNativeElement?.querySelector('form')).toBeTruthy();
  });
});
