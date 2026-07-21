import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Sprint, SprintsService } from '../sprints.service';
import { NotificationService } from '../../shared/notification.service';

// Same date-only-string convention as the work item modal (toDateOnlyString) —
// built from local year/month/day, not toISOString(), which would shift the
// calendar date near midnight for timezones ahead of UTC.
function toDateOnlyString(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

export interface SprintFormData {
  projectId: number;
  onSaved: (sprint: Sprint) => void;
}

// Create-only in v1 (spec's Assumptions: editing a sprint's name/dates is out
// of scope — delete-and-recreate covers the Planned case), mirroring
// WorkItemModalComponent's MatDialog mechanism (Feature 007) as its second
// call site (research.md #10).
@Component({
  selector: 'app-sprint-form',
  standalone: true,
  imports: [MatButtonModule, MatDatepickerModule, MatDialogModule, MatFormFieldModule, MatInputModule],
  templateUrl: './sprint-form.component.html',
  styleUrl: './sprint-form.component.css',
})
export class SprintFormComponent {
  private readonly sprintsService = inject(SprintsService);
  private readonly notificationService = inject(NotificationService);
  private readonly data = inject<SprintFormData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<SprintFormComponent>);

  protected readonly name = signal('');
  protected readonly startDate = signal<Date | null>(null);
  protected readonly endDate = signal<Date | null>(null);
  protected readonly submitting = signal(false);
  protected readonly serverError = signal<string | null>(null);
  protected readonly touched = signal(false);

  // Mirrors CreateSprintRequest's server-side rule (2-50 chars) so an
  // obviously invalid name is caught before a round-trip (data-model.md).
  protected readonly nameError = computed<string | null>(() => {
    const length = this.name().trim().length;
    if (length === 0) {
      return 'Name is required.';
    }
    if (length < 2 || length > 50) {
      return 'Name must be 2–50 characters.';
    }
    return null;
  });

  protected readonly dateRangeError = computed<string | null>(() => {
    const start = this.startDate();
    const end = this.endDate();
    if (!start || !end) {
      return 'Start and end dates are required.';
    }
    return end.getTime() > start.getTime() ? null : 'End date must be after the start date.';
  });

  protected onNameChange(value: string): void {
    this.touched.set(true);
    this.name.set(value);
  }

  protected onStartDateChange(value: Date | null): void {
    this.touched.set(true);
    this.startDate.set(value);
  }

  protected onEndDateChange(value: Date | null): void {
    this.touched.set(true);
    this.endDate.set(value);
  }

  protected close(): void {
    this.dialogRef.close();
  }

  protected async onSubmit(event: Event): Promise<void> {
    event.preventDefault();
    this.touched.set(true);

    if (this.nameError() || this.dateRangeError()) {
      return;
    }

    this.serverError.set(null);
    this.submitting.set(true);
    try {
      const created = await this.sprintsService.createSprint(this.data.projectId, {
        name: this.name().trim(),
        startDate: toDateOnlyString(this.startDate()!),
        endDate: toDateOnlyString(this.endDate()!),
      });
      this.notificationService.success(`Sprint "${created.name}" created.`);
      this.data.onSaved(created);
      this.dialogRef.close(created);
    } catch {
      const message = 'Could not create the sprint — check the name is unique in this project and try again.';
      this.serverError.set(message);
      this.notificationService.error(message);
    } finally {
      this.submitting.set(false);
    }
  }
}
