import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ProjectStatus } from '../work-items.service';
import { ProjectStatusService } from '../project-status.service';

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
  protected readonly projectId = Number(this.route.snapshot.paramMap.get('id'));

  protected readonly statuses = signal<ProjectStatus[]>([]);

  ngOnInit(): void {
    void this.loadStatuses();
  }

  private async loadStatuses(): Promise<void> {
    this.statuses.set(await this.projectStatusService.getStatuses(this.projectId));
  }
}
