import { TestBed } from '@angular/core/testing';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { vi } from 'vitest';
import { SprintFormComponent, SprintFormData } from './sprint-form.component';
import { SprintsService } from '../sprints.service';
import { NotificationService } from '../../shared/notification.service';

function configure(
  data: Partial<SprintFormData> = {},
  serviceOverrides: Partial<{ createSprint: ReturnType<typeof vi.fn> }> = {}
) {
  const close = vi.fn();
  const onSaved = vi.fn();
  const dialogRef = { close, disableClose: false } as unknown as MatDialogRef<SprintFormComponent>;

  const services = {
    createSprint: vi.fn().mockResolvedValue({
      id: 1, projectId: 1, name: 'Sprint 1', startDate: '2026-08-01', endDate: '2026-08-15', status: 'Planned', itemCount: 0,
    }),
    ...serviceOverrides,
  };

  const notificationService = { success: vi.fn(), error: vi.fn() };

  TestBed.configureTestingModule({
    imports: [SprintFormComponent],
    providers: [
      { provide: SprintsService, useValue: services },
      { provide: NotificationService, useValue: notificationService },
      { provide: MAT_DIALOG_DATA, useValue: { projectId: 1, onSaved, ...data } },
      { provide: MatDialogRef, useValue: dialogRef },
      provideNativeDateAdapter(),
    ],
  });

  const fixture = TestBed.createComponent(SprintFormComponent);
  fixture.detectChanges();
  return { fixture, ...services, close, onSaved, dialogRef, notificationService };
}

function submit(fixture: ReturnType<typeof TestBed.createComponent<SprintFormComponent>>): void {
  const form = fixture.nativeElement.querySelector('form') as HTMLFormElement;
  form.dispatchEvent(new Event('submit'));
}

describe('SprintFormComponent', () => {
  it('blocks submit when the name is too short', async () => {
    const { fixture, createSprint } = configure();
    const component = fixture.componentInstance;

    component['name'].set('A');
    component['startDate'].set(new Date(2026, 7, 1));
    component['endDate'].set(new Date(2026, 7, 15));
    fixture.detectChanges();
    submit(fixture);
    await fixture.whenStable();

    expect(createSprint).not.toHaveBeenCalled();
  });

  it('blocks submit when the end date is not after the start date', async () => {
    const { fixture, createSprint } = configure();
    const component = fixture.componentInstance;

    component['name'].set('Sprint 1');
    component['startDate'].set(new Date(2026, 7, 15));
    component['endDate'].set(new Date(2026, 7, 15));
    fixture.detectChanges();
    submit(fixture);
    await fixture.whenStable();

    expect(createSprint).not.toHaveBeenCalled();
  });

  it('creates the sprint and closes on valid submit', async () => {
    const { fixture, createSprint, onSaved, close } = configure();
    const component = fixture.componentInstance;

    component['name'].set('Sprint 1');
    component['startDate'].set(new Date(2026, 7, 1));
    component['endDate'].set(new Date(2026, 7, 15));
    fixture.detectChanges();
    submit(fixture);
    await fixture.whenStable();

    expect(createSprint).toHaveBeenCalledWith(1, { name: 'Sprint 1', startDate: '2026-08-01', endDate: '2026-08-15' });
    expect(onSaved).toHaveBeenCalled();
    expect(close).toHaveBeenCalled();
  });

  it('shows a server error inline without closing when the name is a duplicate', async () => {
    const createSprint = vi.fn().mockRejectedValue(new Error('Conflict'));
    const { fixture, close } = configure({}, { createSprint });
    const component = fixture.componentInstance;

    component['name'].set('Sprint 1');
    component['startDate'].set(new Date(2026, 7, 1));
    component['endDate'].set(new Date(2026, 7, 15));
    fixture.detectChanges();
    submit(fixture);
    await fixture.whenStable();

    expect(component['serverError']()).toBeTruthy();
    expect(close).not.toHaveBeenCalled();
  });
});
