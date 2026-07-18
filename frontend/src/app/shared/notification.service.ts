import { Injectable, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';

const SUCCESS_DURATION_MS = 4000;
const ERROR_DURATION_MS = 6000;

/**
 * Thin wrapper around Angular Material's MatSnackBar (research.md #6) —
 * reuses the queued, accessible, auto-dismissing toast behavior it already
 * provides instead of building a custom one. Replaces silent navigation
 * after create/edit/delete actions (FR-012).
 */
@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly snackBar = inject(MatSnackBar);

  success(message: string): void {
    this.snackBar.open(message, 'Dismiss', {
      duration: SUCCESS_DURATION_MS,
      panelClass: ['notification-success'],
    });
  }

  error(message: string): void {
    this.snackBar.open(message, 'Dismiss', {
      duration: ERROR_DURATION_MS,
      panelClass: ['notification-error'],
    });
  }
}
