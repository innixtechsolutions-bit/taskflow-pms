import { Component, input } from '@angular/core';
import { ActivityEntry } from '../work-items.service';
import { buildActivitySentence } from './build-activity-sentence';
import { RelativeTimePipe } from '../../shared/relative-time.pipe';
import { EmptyStateComponent } from '../../shared/empty-state/empty-state.component';

/**
 * One shared component for both the project Summary tab's activity feed and
 * a work item's own activity history (research.md #15) — FR-019/FR-021 both
 * require "the same rendering" between the two, so there is only one
 * template to keep in sync. Entries are rendered in whatever order the
 * caller passes (both callers already fetch newest-first).
 */
@Component({
  selector: 'app-activity-feed',
  standalone: true,
  imports: [RelativeTimePipe, EmptyStateComponent],
  templateUrl: './activity-feed.component.html',
  styleUrl: './activity-feed.component.css',
})
export class ActivityFeedComponent {
  readonly entries = input.required<ActivityEntry[]>();

  protected sentence(entry: ActivityEntry): string {
    return buildActivitySentence(entry);
  }
}
