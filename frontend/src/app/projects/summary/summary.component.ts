import { Component, OnInit, inject, input, signal } from '@angular/core';
import { ProjectSummary, WorkItemsService } from '../work-items.service';

/**
 * The Summary tab's own data-fetching component (Feature 009), self-contained
 * the same way BoardComponent/BacklogComponent already are. US1 renders just
 * the four stat cards; the status donut/priority bar/team workload sections
 * are wired into this same template by US2/US3.
 */
@Component({
  selector: 'app-summary',
  standalone: true,
  templateUrl: './summary.component.html',
  styleUrl: './summary.component.css',
})
export class SummaryComponent implements OnInit {
  private readonly workItemsService = inject(WorkItemsService);

  readonly projectId = input.required<number>();

  protected readonly summary = signal<ProjectSummary | null>(null);

  ngOnInit(): void {
    void this.load();
  }

  private async load(): Promise<void> {
    this.summary.set(await this.workItemsService.getProjectSummary(this.projectId()));
  }
}
