import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatRadioModule } from '@angular/material/radio';
import { MatSelectModule } from '@angular/material/select';
import { Sprint, SprintsService } from '../sprints.service';
import { NotificationService } from '../../shared/notification.service';

export interface CompleteSprintDialogData {
  projectId: number;
  sprintId: number;
  sprintName: string;
  // Not-Done item count, derived by the caller from the Backlog view's
  // already-loaded section data (research.md #8) — no extra network call.
  notDoneCount: number;
  // Other Planned/Active sprints in the project, excluding this one.
  destinationCandidates: Sprint[];
  onCompleted: () => void;
}

/**
 * The "Complete sprint" resolution picker (Feature 008 US4). When
 * notDoneCount is 0, completion needs no resolution at all (FR-007) and
 * submits immediately. Otherwise a choice is required: move the not-Done
 * items to the Backlog, or to another Planned/Active sprint — mirrors
 * WorkItemModalComponent's MatDialog mechanism, this feature's third call
 * site after SprintFormComponent.
 */
@Component({
  selector: 'app-complete-sprint-dialog',
  standalone: true,
  imports: [MatButtonModule, MatDialogModule, MatFormFieldModule, MatRadioModule, MatSelectModule],
  templateUrl: './complete-sprint-dialog.component.html',
  styleUrl: './complete-sprint-dialog.component.css',
})
export class CompleteSprintDialogComponent {
  private readonly sprintsService = inject(SprintsService);
  private readonly notificationService = inject(NotificationService);
  protected readonly data = inject<CompleteSprintDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<CompleteSprintDialogComponent>);

  protected readonly needsResolution = this.data.notDoneCount > 0;

  protected readonly resolution = signal<'Backlog' | 'Sprint' | null>(null);
  protected readonly destinationSprintId = signal<number | null>(null);
  protected readonly submitting = signal(false);
  protected readonly serverError = signal<string | null>(null);

  protected readonly canSubmit = computed(() => {
    if (!this.needsResolution) {
      return true;
    }
    const resolution = this.resolution();
    if (resolution === 'Backlog') {
      return true;
    }
    if (resolution === 'Sprint') {
      return this.destinationSprintId() !== null;
    }
    return false;
  });

  protected close(): void {
    this.dialogRef.close();
  }

  protected async onSubmit(event: Event): Promise<void> {
    event.preventDefault();
    if (!this.canSubmit()) {
      return;
    }

    this.serverError.set(null);
    this.submitting.set(true);
    try {
      const request = this.needsResolution
        ? this.resolution() === 'Backlog'
          ? { resolution: 'Backlog' as const }
          : { resolution: 'Sprint' as const, destinationSprintId: this.destinationSprintId()! }
        : {};
      await this.sprintsService.completeSprint(this.data.projectId, this.data.sprintId, request);
      this.notificationService.success(`Sprint "${this.data.sprintName}" completed.`);
      this.data.onCompleted();
      this.dialogRef.close();
    } catch {
      const message = 'Could not complete the sprint. Please try again.';
      this.serverError.set(message);
      this.notificationService.error(message);
    } finally {
      this.submitting.set(false);
    }
  }
}
