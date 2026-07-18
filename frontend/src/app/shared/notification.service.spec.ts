import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { vi } from 'vitest';
import { NotificationService } from './notification.service';

function configure() {
  const open = vi.fn();
  TestBed.configureTestingModule({
    providers: [{ provide: MatSnackBar, useValue: { open } }],
  });
  return { open, service: TestBed.inject(NotificationService) };
}

describe('NotificationService', () => {
  it('opens a snack bar with the success panel class', () => {
    const { open, service } = configure();

    service.success('Project created');

    expect(open).toHaveBeenCalledWith(
      'Project created',
      expect.any(String),
      expect.objectContaining({ panelClass: expect.arrayContaining(['notification-success']) })
    );
  });

  it('opens a snack bar with the error panel class', () => {
    const { open, service } = configure();

    service.error('Could not delete project');

    expect(open).toHaveBeenCalledWith(
      'Could not delete project',
      expect.any(String),
      expect.objectContaining({ panelClass: expect.arrayContaining(['notification-error']) })
    );
  });
});
