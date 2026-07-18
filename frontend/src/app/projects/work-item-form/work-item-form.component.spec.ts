import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { HarnessLoader } from '@angular/cdk/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { MatSelectHarness } from '@angular/material/select/testing';
import { provideNativeDateAdapter } from '@angular/material/core';
import { vi } from 'vitest';
import { WorkItemFormComponent } from './work-item-form.component';
import { WorkItemsService } from '../work-items.service';
import { NotificationService } from '../../shared/notification.service';

const sampleUsers = [
  { id: 1, fullName: 'Ada Lovelace' },
  { id: 2, fullName: 'Grace Hopper' },
];

function setInputValue(el: HTMLInputElement | HTMLTextAreaElement, value: string): void {
  el.value = value;
  el.dispatchEvent(new Event('input'));
}

const sampleCandidates = [
  { id: 10, title: 'Epic One' },
  { id: 11, title: 'Epic Two' },
];

async function selectByLabel(loader: HarnessLoader, label: string): Promise<MatSelectHarness> {
  return loader.getHarness(MatSelectHarness.with({ label }));
}

async function chooseOption(loader: HarnessLoader, label: string, optionText: string): Promise<void> {
  const select = await selectByLabel(loader, label);
  await select.open();
  await select.clickOptions({ text: optionText });
}

function configure(
  createWorkItem = vi.fn(),
  getAssignableUsers = vi.fn().mockResolvedValue(sampleUsers),
  getParentCandidates = vi.fn().mockResolvedValue(sampleCandidates)
) {
  TestBed.configureTestingModule({
    imports: [WorkItemFormComponent],
    providers: [
      provideRouter([]),
      { provide: WorkItemsService, useValue: { createWorkItem, getAssignableUsers, getParentCandidates } },
      {
        provide: ActivatedRoute,
        useValue: {
          snapshot: { paramMap: convertToParamMap({ projectId: '1' }), queryParamMap: convertToParamMap({}) },
        },
      },
      { provide: NotificationService, useValue: { success: vi.fn(), error: vi.fn() } },
      provideNativeDateAdapter(),
    ],
  });
  return { createWorkItem, getAssignableUsers, getParentCandidates };
}

async function render(): Promise<{ fixture: ReturnType<typeof TestBed.createComponent>; loader: HarnessLoader }> {
  const fixture = TestBed.createComponent(WorkItemFormComponent);
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  const loader = TestbedHarnessEnvironment.loader(fixture);
  return { fixture, loader };
}

describe('WorkItemFormComponent (create mode)', () => {
  it('submits with the title and the shown defaults, and shows a success toast', async () => {
    const { createWorkItem } = configure(vi.fn().mockResolvedValue({ id: 5 }));
    const { fixture } = await render();
    const notificationService = TestBed.inject(NotificationService);

    const root = fixture.nativeElement as HTMLElement;
    setInputValue(root.querySelector<HTMLInputElement>('#title')!, 'Fix the login bug');
    root.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
    await fixture.whenStable();

    expect(createWorkItem).toHaveBeenCalledWith(
      1,
      expect.objectContaining({ title: 'Fix the login bug', type: 'Task', priority: 'Medium', status: 'ToDo' })
    );
    expect(notificationService.success).toHaveBeenCalled();
  });

  it("correctly pre-selects each dropdown's bound value on initial render, not just the first option", async () => {
    configure();
    const { loader } = await render();

    // 'Task' and 'Medium' are each the 3rd/2nd option, not the 1st — this only
    // passes if mat-select is genuinely reading the bound value (research.md §6's
    // underlying concern, now via mat-select instead of a native <select>).
    expect(await (await selectByLabel(loader, 'Type')).getValueText()).toBe('Task');
    expect(await (await selectByLabel(loader, 'Priority')).getValueText()).toBe('Medium');
    expect(await (await selectByLabel(loader, 'Status')).getValueText()).toBe('ToDo');
  });

  it('lists assignable users fetched from the server', async () => {
    configure();
    const { loader } = await render();

    const assigneeSelect = await selectByLabel(loader, 'Assignee');
    await assigneeSelect.open();
    const optionTexts = await Promise.all((await assigneeSelect.getOptions()).map((o) => o.getText()));

    expect(optionTexts).toContain('Ada Lovelace');
    expect(optionTexts).toContain('Grace Hopper');
  });

  it('refetches parent candidates when Type changes', async () => {
    const { getParentCandidates } = configure();
    const { fixture, loader } = await render();

    expect(getParentCandidates).toHaveBeenCalledWith(1, 'Task');

    await chooseOption(loader, 'Type', 'SubTask');
    await fixture.whenStable();

    expect(getParentCandidates).toHaveBeenCalledWith(1, 'SubTask');
  });

  it('marks the parent picker required for SubTask', async () => {
    configure();
    const { fixture, loader } = await render();

    await chooseOption(loader, 'Type', 'SubTask');
    await fixture.whenStable();

    const parentSelect = await selectByLabel(loader, 'Parent (required)');
    expect(await parentSelect.isRequired()).toBe(true);
  });

  it('hides the parent picker entirely for Epic', async () => {
    configure();
    const { fixture, loader } = await render();

    await chooseOption(loader, 'Type', 'Epic');
    await fixture.whenStable();

    const parentSelects = await loader.getAllHarnesses(MatSelectHarness.with({ label: /Parent/ }));
    expect(parentSelects.length).toBe(0);
  });
});

const existingItem = {
  id: 7,
  projectId: 1,
  type: 'Story',
  title: 'Existing item',
  description: 'Some description',
  priority: 'High',
  status: 'InProgress',
  assigneeUserId: 2,
  assigneeName: 'Grace Hopper',
  dueDate: '2026-08-01T00:00:00.000Z',
  createdByUserId: 1,
  createdByName: 'Ada Lovelace',
  createdAt: '2026-07-01T00:00:00.000Z',
  updatedAt: '2026-07-01T00:00:00.000Z',
  parentWorkItemId: 10,
};

function configureEdit(
  getWorkItem = vi.fn().mockResolvedValue(existingItem),
  updateWorkItem = vi.fn().mockResolvedValue(existingItem),
  getParentCandidates = vi.fn().mockResolvedValue(sampleCandidates)
) {
  TestBed.configureTestingModule({
    imports: [WorkItemFormComponent],
    providers: [
      provideRouter([]),
      {
        provide: WorkItemsService,
        useValue: {
          getWorkItem,
          updateWorkItem,
          getAssignableUsers: vi.fn().mockResolvedValue(sampleUsers),
          getParentCandidates,
        },
      },
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ projectId: '1', id: '7' }) } } },
      { provide: NotificationService, useValue: { success: vi.fn(), error: vi.fn() } },
      provideNativeDateAdapter(),
    ],
  });
  return { getWorkItem, updateWorkItem, getParentCandidates };
}

describe('WorkItemFormComponent (edit mode)', () => {
  it('pre-fills existing values, including each mat-select correctly pre-selecting its current value', async () => {
    configureEdit();
    const { fixture, loader } = await render();

    const root = fixture.nativeElement as HTMLElement;
    expect((root.querySelector('#title') as HTMLInputElement).value).toBe('Existing item');
    expect(await (await selectByLabel(loader, 'Type')).getValueText()).toBe('Story');
    expect(await (await selectByLabel(loader, 'Priority')).getValueText()).toBe('High');
    expect(await (await selectByLabel(loader, 'Status')).getValueText()).toBe('InProgress');
    expect(await (await selectByLabel(loader, 'Assignee')).getValueText()).toBe('Grace Hopper');
  });

  it('submits changes via updateWorkItem rather than createWorkItem', async () => {
    const { updateWorkItem } = configureEdit();
    const { fixture } = await render();

    const root = fixture.nativeElement as HTMLElement;
    setInputValue(root.querySelector<HTMLInputElement>('#title')!, 'Updated title');
    root.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
    await fixture.whenStable();

    expect(updateWorkItem).toHaveBeenCalledWith(7, expect.objectContaining({ title: 'Updated title' }));
  });

  it("pre-fills the item's current parent", async () => {
    configureEdit();
    const { loader } = await render();

    const parentSelect = await selectByLabel(loader, 'Parent');
    expect(await parentSelect.getValueText()).toBe('Epic One');
  });

  it('submits a changed parent', async () => {
    const { updateWorkItem } = configureEdit();
    const { fixture, loader } = await render();

    await chooseOption(loader, 'Parent', 'Epic Two');
    fixture.nativeElement.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
    await fixture.whenStable();

    expect(updateWorkItem).toHaveBeenCalledWith(7, expect.objectContaining({ parentWorkItemId: 11 }));
  });

  it('submits a cleared parent as undefined', async () => {
    const { updateWorkItem } = configureEdit(
      vi.fn().mockResolvedValue({ ...existingItem, type: 'Task', parentWorkItemId: null })
    );
    const { fixture } = await render();

    fixture.nativeElement.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
    await fixture.whenStable();

    expect(updateWorkItem).toHaveBeenCalledWith(7, expect.objectContaining({ parentWorkItemId: undefined }));
  });
});
