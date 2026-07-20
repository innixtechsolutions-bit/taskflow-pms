import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { vi } from 'vitest';
import { WorkItemDetailComponent } from './work-item-detail.component';
import { WorkItemModalComponent } from '../work-item-modal/work-item-modal.component';
import { WorkItemsService } from '../work-items.service';
import { AuthService } from '../../auth/auth.service';
import { NotificationService } from '../../shared/notification.service';

const detailWithParentAndChildren = {
  id: 5,
  projectId: 1,
  type: 'Story',
  title: 'The Story',
  description: null,
  priority: 'Medium',
  statusId: 2,
  statusName: 'In Progress',
  statusCategory: 'Open',
  statusColorKey: 'Blue',
  assigneeUserId: null,
  assigneeName: null,
  dueDate: null,
  createdByUserId: 1,
  createdByName: 'Ada Lovelace',
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
  parentWorkItemId: 2,
  parentTitle: 'The Epic',
  totalDescendantCount: 3,
  children: [
    { id: 10, title: 'Child Task', type: 'Task', statusId: 1, statusName: 'To Do', statusCategory: 'Open', statusColorKey: 'Slate', assigneeName: 'Grace Hopper' },
  ],
};

const detailWithNoParentOrChildren = {
  ...detailWithParentAndChildren,
  id: 6,
  type: 'Epic',
  parentWorkItemId: null,
  parentTitle: null,
  totalDescendantCount: 0,
  children: [],
};

function configure(
  getWorkItemDetail = vi.fn().mockResolvedValue(detailWithParentAndChildren),
  authState: { id: number; role: string } | null = { id: 1, role: 'Developer' },
  deleteWorkItem = vi.fn().mockResolvedValue(undefined)
) {
  const notificationService = { success: vi.fn(), error: vi.fn() };
  const dialogOpen = vi.fn().mockReturnValue({});
  TestBed.configureTestingModule({
    imports: [WorkItemDetailComponent],
    providers: [
      provideRouter([]),
      { provide: WorkItemsService, useValue: { getWorkItemDetail, deleteWorkItem } },
      { provide: AuthService, useValue: { currentUser: () => authState, currentRole: () => authState?.role ?? null } },
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ projectId: '1', id: '5' }) } } },
      { provide: NotificationService, useValue: notificationService },
      { provide: MatDialog, useValue: { open: dialogOpen } },
    ],
  });
  return { getWorkItemDetail, deleteWorkItem, notificationService, dialogOpen };
}

async function render() {
  const fixture = TestBed.createComponent(WorkItemDetailComponent);
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  return fixture;
}

describe('WorkItemDetailComponent', () => {
  it('renders status and priority as chips, not plain text', async () => {
    configure();
    const fixture = await render();

    const statusChip = fixture.nativeElement.querySelector('app-status-chip.work-item-status');
    expect(statusChip).toBeTruthy();
    expect(statusChip.textContent).toContain('In Progress');
    expect(fixture.nativeElement.querySelector('app-priority-chip.work-item-priority')).toBeTruthy();
  });

  it('renders the parent as a link when one exists', async () => {
    configure();
    const fixture = await render();

    const link = fixture.nativeElement.querySelector('.parent-link') as HTMLAnchorElement;
    expect(link).toBeTruthy();
    expect(link.textContent).toContain('The Epic');
  });

  it('renders no parent link when the item has no parent', async () => {
    configure(vi.fn().mockResolvedValue(detailWithNoParentOrChildren));
    const fixture = await render();

    expect(fixture.nativeElement.querySelector('.parent-link')).toBeNull();
  });

  it('lists direct children with title, type, status, and assignee, each linking to its own detail page', async () => {
    configure();
    const fixture = await render();

    const childLink = fixture.nativeElement.querySelector('.child-link') as HTMLAnchorElement;
    expect(childLink.textContent).toContain('Child Task');
    expect(fixture.nativeElement.textContent).toContain('Grace Hopper');
    expect(fixture.nativeElement.textContent).toContain('To Do');
  });

  it('opens the modal pre-selecting this item as parent and the legal child type when starting a new child (FR-019)', async () => {
    const { dialogOpen } = configure();
    const fixture = await render();

    const createChildButton = fixture.nativeElement.querySelector('.create-child-link') as HTMLButtonElement;
    expect(createChildButton).toBeTruthy();
    createChildButton.click();
    // The modal is dynamically imported (fix: restore production build) — its
    // own chunk resolves asynchronously, not synchronously with the click, so
    // poll rather than assume a fixed number of microtask turns.
    await vi.waitFor(() => expect(dialogOpen).toHaveBeenCalled());

    expect(dialogOpen).toHaveBeenCalledWith(
      WorkItemModalComponent,
      expect.objectContaining({
        data: expect.objectContaining({ mode: 'create', projectId: 1, parentWorkItemId: 5, type: 'Task' }),
      })
    );
  });

  it('hides the create-child action for a SubTask (cannot legally have children)', async () => {
    configure(vi.fn().mockResolvedValue({ ...detailWithParentAndChildren, type: 'SubTask', children: [] }));
    const fixture = await render();

    expect(fixture.nativeElement.querySelector('.create-child-link')).toBeNull();
  });

  it('opens the modal in edit mode, pre-populated for this item', async () => {
    const { dialogOpen } = configure();
    const fixture = await render();

    const editButton = fixture.nativeElement.querySelector('.edit-link') as HTMLButtonElement;
    expect(editButton).toBeTruthy();
    editButton.click();
    await vi.waitFor(() => expect(dialogOpen).toHaveBeenCalled());

    expect(dialogOpen).toHaveBeenCalledWith(
      WorkItemModalComponent,
      expect.objectContaining({ data: expect.objectContaining({ mode: 'edit', workItemId: 5 }) })
    );
  });

  it('re-fetches the detail once the modal reports a save', async () => {
    const { getWorkItemDetail, dialogOpen } = configure();
    const fixture = await render();

    (fixture.nativeElement.querySelector('.edit-link') as HTMLButtonElement).click();
    await vi.waitFor(() => expect(dialogOpen).toHaveBeenCalled());
    const { onSaved } = dialogOpen.mock.calls[0][1].data;
    getWorkItemDetail.mockClear();
    onSaved();
    await fixture.whenStable();

    expect(getWorkItemDetail).toHaveBeenCalledWith(5);
  });

  it('states the total descendant count in the delete confirmation', async () => {
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(false);
    configure();
    const fixture = await render();

    (fixture.nativeElement.querySelector('.delete-button') as HTMLButtonElement).click();

    expect(confirmSpy).toHaveBeenCalledWith(expect.stringContaining('3'));
    confirmSpy.mockRestore();
  });

  it('shows a success toast and navigates after a confirmed delete', async () => {
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
    const { notificationService } = configure();
    const fixture = await render();
    const router = TestBed.inject(Router);
    vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);

    (fixture.nativeElement.querySelector('.delete-button') as HTMLButtonElement).click();
    await fixture.whenStable();

    expect(notificationService.success).toHaveBeenCalled();
    confirmSpy.mockRestore();
  });

  it('shows an error toast when the delete request fails', async () => {
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
    const { notificationService } = configure(undefined, undefined, vi.fn().mockRejectedValue(new Error('failed')));
    const fixture = await render();

    (fixture.nativeElement.querySelector('.delete-button') as HTMLButtonElement).click();
    await fixture.whenStable();

    expect(notificationService.error).toHaveBeenCalled();
    confirmSpy.mockRestore();
  });
});
