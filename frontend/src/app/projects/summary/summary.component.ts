import { Component, OnInit, inject, input, signal } from '@angular/core';
import { ActivityEntry, ProjectSummary, WorkItemsService } from '../work-items.service';
import { ActivityFeedComponent } from '../activity-feed/activity-feed.component';
import { StatusDonutChartComponent } from './status-donut-chart.component';
import { PriorityBarChartComponent } from './priority-bar-chart.component';

const ACTIVITY_PAGE_SIZE = 20;

/**
 * The Summary tab's own data-fetching component (Feature 009), self-contained
 * the same way BoardComponent/BacklogComponent already are. US1 renders the
 * four stat cards; US4 adds the project's activity feed (paginated, "load
 * more"); the status donut/priority bar/team workload sections are wired
 * into this same template by US2/US3.
 */
@Component({
  selector: 'app-summary',
  standalone: true,
  imports: [ActivityFeedComponent, StatusDonutChartComponent, PriorityBarChartComponent],
  templateUrl: './summary.component.html',
  styleUrl: './summary.component.css',
})
export class SummaryComponent implements OnInit {
  private readonly workItemsService = inject(WorkItemsService);

  readonly projectId = input.required<number>();

  protected readonly summary = signal<ProjectSummary | null>(null);
  protected readonly activityEntries = signal<ActivityEntry[]>([]);
  private readonly activityPage = signal(1);
  private readonly activityTotalCount = signal(0);

  protected readonly hasMoreActivity = () => this.activityEntries().length < this.activityTotalCount();

  ngOnInit(): void {
    void this.load();
    void this.loadActivity(1);
  }

  private async load(): Promise<void> {
    this.summary.set(await this.workItemsService.getProjectSummary(this.projectId()));
  }

  private async loadActivity(page: number): Promise<void> {
    const result = await this.workItemsService.getProjectActivity(this.projectId(), page, ACTIVITY_PAGE_SIZE);
    this.activityEntries.update((existing) => (page === 1 ? result.items : [...existing, ...result.items]));
    this.activityPage.set(result.page);
    this.activityTotalCount.set(result.totalCount);
  }

  protected loadMoreActivity(): void {
    void this.loadActivity(this.activityPage() + 1);
  }
}
