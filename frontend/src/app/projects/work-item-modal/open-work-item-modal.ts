import { MatDialog } from '@angular/material/dialog';
import type { WorkItemModalData } from './work-item-modal.component';

// A dynamic import, not a static one, so WorkItemModalComponent (and the
// dependencies only it needs, e.g. MatDatepickerModule and Signal Forms)
// ships in its own chunk, fetched only when a user actually opens the modal
// — not merely when they navigate to a page that happens to be able to open
// it (fix: restore production build). Shared by every entry point (board,
// work item detail, project detail) so the dynamic import and dialog config
// exist in exactly one place, not three.
export async function openWorkItemModal(dialog: MatDialog, data: WorkItemModalData): Promise<void> {
  const { WorkItemModalComponent } = await import('./work-item-modal.component');
  dialog.open(WorkItemModalComponent, { width: '720px', maxWidth: '95vw', data });
}
