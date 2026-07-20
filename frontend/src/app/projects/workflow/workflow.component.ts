import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ProjectStatus, WorkItemStatusCategory } from '../work-items.service';
import { ProjectStatusService } from '../project-status.service';
import { NotificationService } from '../../shared/notification.service';

// The Workflow management screen (Feature 006, US2) — created here read-only and
// extended in place by US3-US6 (add/rename/reorder/delete), rather than rebuilt
// per story (plan.md).
@Component({
  selector: 'app-workflow',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './workflow.component.html',
  styleUrl: './workflow.component.css',
})
export class WorkflowComponent implements OnInit {
  private readonly projectStatusService = inject(ProjectStatusService);
  private readonly route = inject(ActivatedRoute);
  private readonly notificationService = inject(NotificationService);
  protected readonly projectId = Number(this.route.snapshot.paramMap.get('id'));

  protected readonly statuses = signal<ProjectStatus[]>([]);

  protected readonly newStatusName = signal('');
  protected readonly newStatusCategory = signal<WorkItemStatusCategory>('Open');

  ngOnInit(): void {
    void this.loadStatuses();
  }

  private async loadStatuses(): Promise<void> {
    this.statuses.set(await this.projectStatusService.getStatuses(this.projectId));
  }

  protected async onAddStatus(): Promise<void> {
    const name = this.newStatusName().trim();
    if (!name) {
      return;
    }
    try {
      const created = await this.projectStatusService.createStatus(this.projectId, {
        name,
        category: this.newStatusCategory(),
      });
      this.statuses.update((statuses) => [...statuses, created].sort((a, b) => a.position - b.position));
      this.newStatusName.set('');
    } catch {
      this.notificationService.error(`Could not add "${name}". Please try again.`);
    }
  }
}
