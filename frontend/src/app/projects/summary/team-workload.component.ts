import { Component, computed, input } from '@angular/core';
import { WorkloadRow } from '../work-items.service';
import { UserAvatarComponent } from '../../shared/user-avatar/user-avatar.component';
import { EmptyStateComponent } from '../../shared/empty-state/empty-state.component';

/**
 * The Summary tab's team workload panel (US3) — rows arrive already sorted
 * by open-item count descending from GetSummaryAsync (US1); this component
 * renders them as-is with a proportion bar per row, reusing
 * UserAvatarComponent (including for the synthetic "Unassigned" row — no
 * special-casing needed, same avatar treatment as any other display name).
 */
@Component({
  selector: 'app-team-workload',
  standalone: true,
  imports: [UserAvatarComponent, EmptyStateComponent],
  templateUrl: './team-workload.component.html',
  styleUrl: './team-workload.component.css',
})
export class TeamWorkloadComponent {
  readonly workload = input.required<WorkloadRow[]>();

  private readonly maxCount = computed(() => Math.max(1, ...this.workload().map((row) => row.openItemCount)));

  protected percent(count: number): number {
    return (count / this.maxCount()) * 100;
  }
}
