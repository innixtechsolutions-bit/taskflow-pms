import { TestBed } from '@angular/core/testing';
import { HarnessLoader } from '@angular/cdk/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { MatSelectHarness } from '@angular/material/select/testing';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { vi } from 'vitest';
import { WorkItemModalComponent, WorkItemModalData } from './work-item-modal.component';
import { ProjectStatus, WorkItemsService } from '../work-items.service';
import { AuthService } from '../../auth/auth.service';
import { NotificationService } from '../../shared/notification.service';

const sampleUsers = [
  { id: 1, fullName: 'Ada Lovelace' },
  { id: 2, fullName: 'Grace Hopper' },
];

// Feature 006 — the standard four, matching what ProjectService.CreateAsync seeds.
function sampleStatuses(): ProjectStatus[] {
  return [
    { id: 1, name: 'To Do', category: 'Open', colorKey: 'Slate', position: 0, itemCount: 0 },
    { id: 2, name: 'In Progress', category: 'Open', colorKey: 'Blue', position: 1, itemCount: 0 },
    { id: 3, name: 'In Review', category: 'Open', colorKey: 'Violet', position: 2, itemCount: 0 },
    { id: 4, name: 'Done', category: 'Done', colorKey: 'Green', position: 3, itemCount: 0 },
  ];
}

const sampleCandidates = [
  { id: 10, title: 'Epic One' },
  { id: 11, title: 'Epic Two' },
];

const existingItem = {
  id: 7,
  projectId: 1,
  type: 'Story',
  title: 'Existing item',
  description: 'Some description',
  priority: 'High',
  statusId: 2,
  statusName: 'In Progress',
  statusCategory: 'Open',
  statusColorKey: 'Blue',
  assigneeUserId: 2,
  assigneeName: 'Grace Hopper',
  dueDate: '2026-08-01T00:00:00.000Z',
  createdByUserId: 1,
  createdByName: 'Ada Lovelace',
  createdAt: '2026-07-01T00:00:00.000Z',
  updatedAt: '2026-07-01T00:00:00.000Z',
  parentWorkItemId: 10,
};

function setInputValue(el: HTMLInputElement | HTMLTextAreaElement, value: string): void {
  el.value = value;
  el.dispatchEvent(new Event('input'));
}

async function selectByLabel(loader: HarnessLoader, label: string): Promise<MatSelectHarness> {
  return loader.getHarness(MatSelectHarness.with({ label }));
}

function configure(
  data: Partial<WorkItemModalData>,
  serviceOverrides: Partial<{
    createWorkItem: ReturnType<typeof vi.fn>;
    updateWorkItem: ReturnType<typeof vi.fn>;
    getWorkItem: ReturnType<typeof vi.fn>;
    getAssignableUsers: ReturnType<typeof vi.fn>;
    getParentCandidates: ReturnType<typeof vi.fn>;
    getStatuses: ReturnType<typeof vi.fn>;
  }> = {},
  currentUser: { id: number } | null = { id: 1 }
) {
  const close = vi.fn();
  const onSaved = vi.fn();
  const dialogRef = { close, disableClose: false } as unknown as MatDialogRef<WorkItemModalComponent>;

  const services = {
    createWorkItem: vi.fn().mockResolvedValue({ id: 5 }),
    updateWorkItem: vi.fn().mockResolvedValue(existingItem),
    getWorkItem: vi.fn().mockResolvedValue(existingItem),
    getAssignableUsers: vi.fn().mockResolvedValue(sampleUsers),
    getParentCandidates: vi.fn().mockResolvedValue(sampleCandidates),
    getStatuses: vi.fn().mockResolvedValue(sampleStatuses()),
    ...serviceOverrides,
  };

  const notificationService = { success: vi.fn(), error: vi.fn() };

  TestBed.configureTestingModule({
    imports: [WorkItemModalComponent],
    providers: [
      { provide: WorkItemsService, useValue: services },
      { provide: NotificationService, useValue: notificationService },
      { provide: AuthService, useValue: { currentUser: () => currentUser } },
      { provide: MAT_DIALOG_DATA, useValue: { mode: 'create', projectId: 1, onSaved, ...data } },
      { provide: MatDialogRef, useValue: dialogRef },
      provideNativeDateAdapter(),
    ],
  });

  return { ...services, close, onSaved, dialogRef, notificationService };
}

async function render(): Promise<{ fixture: ReturnType<typeof TestBed.createComponent>; loader: HarnessLoader }> {
  const fixture = TestBed.createComponent(WorkItemModalComponent);
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  const loader = TestbedHarnessEnvironment.loader(fixture);
  return { fixture, loader };
}

describe('WorkItemModalComponent (create mode, pre-selection)', () => {
  it("pre-selects the Status field from dialog data (board's '+' affordance)", async () => {
    configure({ mode: 'create', statusId: 3 });
    const { loader } = await render();

    expect(await (await selectByLabel(loader, 'Status')).getValueText()).toBe('In Review');
  });

  it("pre-selects Parent and Type from dialog data ('Add child' affordance)", async () => {
    configure({ mode: 'create', parentWorkItemId: 10, type: 'Story' });
    const { loader } = await render();

    expect(await (await selectByLabel(loader, 'Type')).getValueText()).toBe('Story');
    expect(await (await selectByLabel(loader, 'Parent')).getValueText()).toBe('Epic One');
  });
});

describe('WorkItemModalComponent (edit mode, pre-population)', () => {
  it('pre-populates every field from the existing work item', async () => {
    const { getWorkItem } = configure({ mode: 'edit', workItemId: 7 });
    const { fixture, loader } = await render();

    expect(getWorkItem).toHaveBeenCalledWith(7);
    const root = fixture.nativeElement as HTMLElement;
    expect((root.querySelector('#title') as HTMLInputElement).value).toBe('Existing item');
    expect(await (await selectByLabel(loader, 'Type')).getValueText()).toBe('Story');
    expect(await (await selectByLabel(loader, 'Priority')).getValueText()).toBe('High');
    expect(await (await selectByLabel(loader, 'Status')).getValueText()).toBe('In Progress');
    expect(await (await selectByLabel(loader, 'Assignee')).getValueText()).toBe('Grace Hopper');
    expect(await (await selectByLabel(loader, 'Parent')).getValueText()).toBe('Epic One');
  });
});

describe('WorkItemModalComponent (Assign to me, US2)', () => {
  it('sets the assignee to the current user in create mode', async () => {
    configure({ mode: 'create' }, {}, { id: 1 });
    const { fixture, loader } = await render();

    (fixture.nativeElement.querySelector('.assign-to-me-button') as HTMLButtonElement).click();
    await fixture.whenStable();

    expect(await (await selectByLabel(loader, 'Assignee')).getValueText()).toBe('Ada Lovelace');
  });

  it('switches the assignee to the current user in edit mode on an item assigned to someone else', async () => {
    configure({ mode: 'edit', workItemId: 7 }, {}, { id: 1 });
    const { fixture, loader } = await render();

    expect(await (await selectByLabel(loader, 'Assignee')).getValueText()).toBe('Grace Hopper');

    (fixture.nativeElement.querySelector('.assign-to-me-button') as HTMLButtonElement).click();
    await fixture.whenStable();

    expect(await (await selectByLabel(loader, 'Assignee')).getValueText()).toBe('Ada Lovelace');
  });
});

describe('WorkItemModalComponent (dirty-flag / confirm-discard)', () => {
  it('closes immediately on Escape when nothing has changed', async () => {
    const { close } = configure({});
    const { fixture } = await render();

    const confirmSpy = vi.spyOn(window, 'confirm');
    fixture.nativeElement.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));

    expect(confirmSpy).not.toHaveBeenCalled();
    expect(close).toHaveBeenCalled();
  });

  it('prompts via confirm() before closing on Escape once a field has changed, and respects "Cancel"', async () => {
    const { close } = configure({});
    const { fixture } = await render();

    const root = fixture.nativeElement as HTMLElement;
    setInputValue(root.querySelector<HTMLInputElement>('#title')!, 'Something typed');

    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(false);
    fixture.nativeElement.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));

    expect(confirmSpy).toHaveBeenCalled();
    expect(close).not.toHaveBeenCalled();
  });

  it('closes on Escape after a change when the user confirms discarding it', async () => {
    const { close } = configure({});
    const { fixture } = await render();

    const root = fixture.nativeElement as HTMLElement;
    setInputValue(root.querySelector<HTMLInputElement>('#title')!, 'Something typed');

    vi.spyOn(window, 'confirm').mockReturnValue(true);
    fixture.nativeElement.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));

    expect(close).toHaveBeenCalled();
  });

  it('sets disableClose on the injected MatDialogRef so MatDialog never bypasses the confirm check', async () => {
    const { dialogRef } = configure({});
    await render();

    expect(dialogRef.disableClose).toBe(true);
  });
});

describe('WorkItemModalComponent (error display)', () => {
  it('shows a server error inside the dialog without closing it, retaining entered values', async () => {
    const { close, notificationService } = configure(
      {},
      { createWorkItem: vi.fn().mockRejectedValue(new Error('boom')) }
    );
    const { fixture } = await render();

    const root = fixture.nativeElement as HTMLElement;
    setInputValue(root.querySelector<HTMLInputElement>('#title')!, 'Fix the login bug');
    root.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(root.querySelector('.server-error')?.textContent).toContain('Something went wrong');
    expect(notificationService.error).toHaveBeenCalled();
    expect(close).not.toHaveBeenCalled();
    expect((root.querySelector('#title') as HTMLInputElement).value).toBe('Fix the login bug');
  });
});

describe('WorkItemModalComponent (success: toast + onSaved + close)', () => {
  it('shows a success toast, invokes onSaved, and closes on a successful create', async () => {
    const { createWorkItem, close, onSaved, notificationService } = configure({ mode: 'create' });
    const { fixture } = await render();

    const root = fixture.nativeElement as HTMLElement;
    setInputValue(root.querySelector<HTMLInputElement>('#title')!, 'Fix the login bug');
    root.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
    await fixture.whenStable();

    expect(createWorkItem).toHaveBeenCalledWith(1, expect.objectContaining({ title: 'Fix the login bug' }));
    expect(notificationService.success).toHaveBeenCalled();
    expect(onSaved).toHaveBeenCalled();
    expect(close).toHaveBeenCalled();
  });

  it('shows a success toast, invokes onSaved, and closes on a successful edit', async () => {
    const { updateWorkItem, close, onSaved } = configure({ mode: 'edit', workItemId: 7 });
    const { fixture } = await render();

    const root = fixture.nativeElement as HTMLElement;
    setInputValue(root.querySelector<HTMLInputElement>('#title')!, 'Updated title');
    root.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
    await fixture.whenStable();

    expect(updateWorkItem).toHaveBeenCalledWith(7, expect.objectContaining({ title: 'Updated title' }));
    expect(onSaved).toHaveBeenCalled();
    expect(close).toHaveBeenCalled();
  });
});
