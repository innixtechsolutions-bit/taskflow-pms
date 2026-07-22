import { Pipe, PipeTransform } from '@angular/core';
import { FriendlyDatePipe } from './friendly-date.pipe';

const PLACEHOLDER = '—';
const MINUTE_MS = 60_000;
const HOUR_MS = 60 * MINUTE_MS;
const DAY_MS = 24 * HOUR_MS;
const WEEK_MS = 7 * DAY_MS;

/**
 * Renders a timestamp as a short relative phrase ("just now" / "6 minutes
 * ago" / "3 hours ago" / "2 days ago"), falling back to FriendlyDatePipe's
 * absolute format beyond ~7 days — mirrors friendly-date.pipe.ts's own
 * null-placeholder structure (FR-019/FR-021's "relative timestamp").
 */
@Pipe({ name: 'relativeTime', standalone: true })
export class RelativeTimePipe implements PipeTransform {
  private readonly friendlyDate = new FriendlyDatePipe();

  transform(value: string | Date | null | undefined): string {
    if (value === null || value === undefined) {
      return PLACEHOLDER;
    }

    const date = typeof value === 'string' ? new Date(value) : value;
    const diffMs = Date.now() - date.getTime();

    if (diffMs < MINUTE_MS) {
      return 'just now';
    }
    if (diffMs < HOUR_MS) {
      const minutes = Math.floor(diffMs / MINUTE_MS);
      return `${minutes} minute${minutes === 1 ? '' : 's'} ago`;
    }
    if (diffMs < DAY_MS) {
      const hours = Math.floor(diffMs / HOUR_MS);
      return `${hours} hour${hours === 1 ? '' : 's'} ago`;
    }
    if (diffMs < WEEK_MS) {
      const days = Math.floor(diffMs / DAY_MS);
      return `${days} day${days === 1 ? '' : 's'} ago`;
    }
    return this.friendlyDate.transform(value);
  }
}
