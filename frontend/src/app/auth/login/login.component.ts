import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormField, email, required, form } from '@angular/forms/signals';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../auth.service';

interface LoginFormModel {
  email: string;
  password: string;
}

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormField],
  templateUrl: './login.component.html',
})
export class LoginComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  // Set once, from the query params authGuard/the interceptor attach on redirect —
  // not tied to submit errors, since it should show up even before a submit attempt.
  protected readonly sessionExpired = this.route.snapshot.queryParamMap.get('expired') === 'true';

  protected readonly model = signal<LoginFormModel>({ email: '', password: '' });

  protected readonly loginForm = form(this.model, (path) => {
    required(path.email, { message: 'Email is required.' });
    email(path.email, { message: 'Enter a valid email address.' });

    required(path.password, { message: 'Password is required.' });
  });

  protected readonly submitting = signal(false);
  protected readonly serverError = signal<string | null>(null);

  protected async onSubmit(event: Event): Promise<void> {
    event.preventDefault();

    this.loginForm().markAsTouched();
    if (!this.loginForm().valid()) {
      return;
    }

    this.serverError.set(null);
    this.submitting.set(true);
    try {
      await this.authService.login(this.model());
      const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
      await this.router.navigateByUrl(returnUrl ?? '/');
    } catch (error) {
      this.serverError.set(this.messageFor(error));
    } finally {
      this.submitting.set(false);
    }
  }

  // Same generic message for a wrong email or wrong password (FR-008) — the backend
  // already collapses both into one 401, so there's nothing more specific to show.
  private messageFor(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 401) {
        return 'Invalid email or password.';
      }
      if (error.status === 429) {
        return 'Too many attempts, try again later.';
      }
    }
    return 'Something went wrong. Please try again.';
  }
}
