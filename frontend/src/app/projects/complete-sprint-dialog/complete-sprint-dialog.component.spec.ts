import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { vi } from 'vitest';
import { CompleteSprintDialogComponent, CompleteSprintDialogData } from './complete-sprint-dialog.component';
import { SprintsService } from '../sprints.service';
import { NotificationService } from '../../shared/notification.service';

function configure(
  data: Partial<CompleteSprintDialogData> = {},
  serviceOverrides: Partial<{ completeSprint: ReturnType<typeof vi.fn> }> = {}
) {
  const close = vi.fn();
  const onCompleted = vi.fn();
  const dialogRef = { close } as unknown as MatDialogRef<CompleteSprintDialogComponent>;
  const services = { completeSprint: vi.fn().mockResolvedValue({ status: 'Completed' }), ...serviceOverrides };
  const notificationService = { success: vi.fn(), error: vi.fn() };

  TestBed.configureTestingModule({
    imports: [CompleteSprintDialogComponent],
    providers: [
      { provide: SprintsService, useValue: services },
      { provide: NotificationService, useValue: notificationService },
      {
        provide: MAT_DIALOG_DATA,
        useValue: {
          projectId: 1,
          sprintId: 5,
          sprintName: 'Sprint 1',
          notDoneCount: 0,
          destinationCandidates: [],
          onCompleted,
          ...data,
        },
      },
      { provide: MatDialogRef, useValue: dialogRef },
    ],
  });

  const fixture = TestBed.createComponent(CompleteSprintDialogComponent);
  fixture.detectChanges();
  return { fixture, close, onCompleted, notificationService, ...services };
}

function submit(fixture: ReturnType<typeof TestBed.createComponent<CompleteSprintDialogComponent>>): void {
  const form = fixture.nativeElement.querySelector('form') as HTMLFormElement;
  form.dispatchEvent(new Event('submit'));
}

describe('CompleteSprintDialogComponent', () => {
  it('completes immediately with no resolution when notDoneCount is 0', async () => {
    const { fixture, completeSprint, onCompleted, close } = configure({ notDoneCount: 0 });

    submit(fixture);
    await fixture.whenStable();

    expect(completeSprint).toHaveBeenCalledWith(1, 5, {});
    expect(onCompleted).toHaveBeenCalled();
    expect(close).toHaveBeenCalled();
  });

  it('requires a resolution choice when notDoneCount is greater than 0', async () => {
    const { fixture, completeSprint } = configure({ notDoneCount: 2 });

    submit(fixture);
    await fixture.whenStable();

    expect(completeSprint).not.toHaveBeenCalled();
  });

  it('completes with "Backlog" resolution when chosen', async () => {
    const { fixture, completeSprint } = configure({ notDoneCount: 2 });
    fixture.componentInstance['resolution'].set('Backlog');

    submit(fixture);
    await fixture.whenStable();

    expect(completeSprint).toHaveBeenCalledWith(1, 5, { resolution: 'Backlog' });
  });

  it('requires a destination sprint when "Sprint" resolution is chosen', async () => {
    const { fixture, completeSprint } = configure({
      notDoneCount: 2,
      destinationCandidates: [{ id: 6, projectId: 1, name: 'Sprint 2', startDate: '2026-08-16', endDate: '2026-08-30', status: 'Planned', itemCount: 0 }],
    });
    fixture.componentInstance['resolution'].set('Sprint');

    submit(fixture);
    await fixture.whenStable();
    expect(completeSprint).not.toHaveBeenCalled();

    fixture.componentInstance['destinationSprintId'].set(6);
    submit(fixture);
    await fixture.whenStable();

    expect(completeSprint).toHaveBeenCalledWith(1, 5, { resolution: 'Sprint', destinationSprintId: 6 });
  });
});
