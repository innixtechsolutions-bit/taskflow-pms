import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormField, email, maxLength, minLength, pattern, required, form } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Router } from '@angular/router';
import { AuthService } from '../auth.service';

interface RegisterFormModel {
  fullName: string;
  email: string;
  password: string;
}

// Same rule as the backend's RegisterRequest/AuthService (FR-003): kept in sync by
// convention since the two run in different languages, not by a shared constant.
const PASSWORD_PATTERN = /^(?=.*[A-Za-z])(?=.*\d).{8,}$/;

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [FormField, MatButtonModule, MatCardModule, MatFormFieldModule, MatInputModule],
  templateUrl: './register.component.html',
})
export class RegisterComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly model = signal<RegisterFormModel>({ fullName: '', email: '', password: '' });

  // Signal Forms: `form()` wires validators to a writable signal, and the [formField]
  // directive below binds each native <input> to a leaf of the resulting field tree.
  protected readonly registerForm = form(this.model, (path) => {
    required(path.fullName, { message: 'Full name is required.' });
    minLength(path.fullName, 2, { message: 'Full name must be at least 2 characters.' });
    maxLength(path.fullName, 100, { message: 'Full name must be at most 100 characters.' });

    required(path.email, { message: 'Email is required.' });
    email(path.email, { message: 'Enter a valid email address.' });

    required(path.password, { message: 'Password is required.' });
    pattern(path.password, PASSWORD_PATTERN, {
      message: 'Password must be at least 8 characters and include at least one letter and one number.',
    });
  });

  protected readonly submitting = signal(false);
  protected readonly serverError = signal<string | null>(null);

  protected async onSubmit(event: Event): Promise<void> {
    event.preventDefault();

    this.registerForm().markAsTouched();
    if (!this.registerForm().valid()) {
      return;
    }

    this.serverError.set(null);
    this.submitting.set(true);
    try {
      await this.authService.register(this.model());
      await this.router.navigateByUrl('/');
    } catch (error) {
      this.serverError.set(
        error instanceof HttpErrorResponse && error.status === 409
          ? 'An account with this email already exists.'
          : 'Something went wrong. Please try again.'
      );
    } finally {
      this.submitting.set(false);
    }
  }
}
