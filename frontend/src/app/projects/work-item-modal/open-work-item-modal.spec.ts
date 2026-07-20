import { MatDialog } from '@angular/material/dialog';
import { vi } from 'vitest';
import { openWorkItemModal } from './open-work-item-modal';
import { WorkItemModalComponent } from './work-item-modal.component';

describe('openWorkItemModal', () => {
  it('dynamically loads WorkItemModalComponent and opens it with the given data', async () => {
    const open = vi.fn().mockReturnValue({});
    const dialog = { open } as unknown as MatDialog;
    const onSaved = vi.fn();

    await openWorkItemModal(dialog, { mode: 'create', projectId: 1, statusId: 3, onSaved });

    expect(open).toHaveBeenCalledWith(
      WorkItemModalComponent,
      expect.objectContaining({ data: expect.objectContaining({ mode: 'create', projectId: 1, statusId: 3, onSaved }) })
    );
  });
});
