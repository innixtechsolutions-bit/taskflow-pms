import { Component, OnInit, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { Sprint, SprintsService } from '../sprints.service';
import { AuthService } from '../../auth/auth.service';
import { SprintFormComponent } from '../sprint-form/sprint-form.component';
import { EmptyStateComponent } from '../../shared/empty-state/empty-state.component';
import { FriendlyDatePipe } from '../../shared/friendly-date.pipe';

/**
 * The Backlog view (Feature 008) — created here minimal and read-only-first
 * (US1: just the project's sprint list + "Create sprint"), extended in place
 * by US2 (sections/items/filters), US3 (drag-and-drop), US4 (lifecycle
 * actions), and US6 (days-remaining indicator) — the same "build read-only,
 * extend per story" approach WorkflowComponent used in Feature 006.
 */
@Component({
  selector: 'app-backlog',
  standalone: true,
  imports: [MatButtonModule, EmptyStateComponent, FriendlyDatePipe],
  templateUrl: './backlog.component.html',
  styleUrl: './backlog.component.css',
})
export class BacklogComponent implements OnInit {
  private readonly sprintsService = inject(SprintsService);
  private readonly authService = inject(AuthService);
  private readonly dialog = inject(MatDialog);

  readonly projectId = input.required<number>();

  protected readonly sprints = signal<Sprint[]>([]);

  ngOnInit(): void {
    void this.load();
  }

  private async load(): Promise<void> {
    this.sprints.set(await this.sprintsService.getSprints(this.projectId()));
  }

  protected canManageSprints(): boolean {
    const role = this.authService.currentRole();
    return role === 'Manager' || role === 'Admin';
  }

  protected openCreateSprintDialog(): void {
    this.dialog.open(SprintFormComponent, {
      data: {
        projectId: this.projectId(),
        onSaved: () => void this.load(),
      },
    });
  }
}
