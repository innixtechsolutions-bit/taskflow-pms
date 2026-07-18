import { DatePipe } from '@angular/common';
import { Pipe, PipeTransform } from '@angular/core';

const PLACEHOLDER = '—';

/**
 * Wraps Angular's built-in DatePipe with a fixed short format
 * ("Jul 17, 2026") and a null placeholder, so every date in the app goes
 * through one place instead of repeating a format string / `?? '—'`
 * everywhere (FR-011).
 */
@Pipe({ name: 'friendlyDate', standalone: true })
export class FriendlyDatePipe implements PipeTransform {
  private readonly datePipe = new DatePipe('en-US');

  transform(value: string | Date | null | undefined): string {
    if (value === null || value === undefined) {
      return PLACEHOLDER;
    }
    return this.datePipe.transform(value, 'MMM d, y') ?? PLACEHOLDER;
  }
}
