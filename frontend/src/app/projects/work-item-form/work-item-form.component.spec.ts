import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { WorkItemFormComponent } from './work-item-form.component';
import { WorkItemsService } from '../work-items.service';

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
    ],
  });
  return { createWorkItem, getAssignableUsers, getParentCandidates };
}

describe('WorkItemFormComponent (create mode)', () => {
  it('submits with the title and the shown defaults', async () => {
    const { createWorkItem } = configure(vi.fn().mockResolvedValue({ id: 5 }));
    const fixture = TestBed.createComponent(WorkItemFormComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    setInputValue(root.querySelector<HTMLInputElement>('#title')!, 'Fix the login bug');
    root.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
    await fixture.whenStable();

    expect(createWorkItem).toHaveBeenCalledWith(
      1,
      expect.objectContaining({ title: 'Fix the login bug', type: 'Task', priority: 'Medium', status: 'ToDo' })
    );
  });

  it("correctly pre-selects each dropdown's bound value on initial render, not just the first option", async () => {
    configure();
    const fixture = TestBed.createComponent(WorkItemFormComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    // 'Task' and 'Medium' are each the 3rd/2nd <option>, not the 1st — this only
    // passes if the dropdown is genuinely reading the bound value (research.md §6),
    // not silently defaulting to whichever <option> happens to come first.
    expect((root.querySelector('#type') as HTMLSelectElement).value).toBe('Task');
    expect((root.querySelector('#priority') as HTMLSelectElement).value).toBe('Medium');
    expect((root.querySelector('#status') as HTMLSelectElement).value).toBe('ToDo');
  });

  it('lists assignable users fetched from the server', async () => {
    configure();
    const fixture = TestBed.createComponent(WorkItemFormComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Ada Lovelace');
    expect(fixture.nativeElement.textContent).toContain('Grace Hopper');
  });

  it('refetches parent candidates when Type changes', async () => {
    const { getParentCandidates } = configure();
    const fixture = TestBed.createComponent(WorkItemFormComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(getParentCandidates).toHaveBeenCalledWith(1, 'Task');

    const root = fixture.nativeElement as HTMLElement;
    const typeSelect = root.querySelector<HTMLSelectElement>('#type')!;
    typeSelect.value = 'SubTask';
    typeSelect.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(getParentCandidates).toHaveBeenCalledWith(1, 'SubTask');
  });

  it('marks the parent picker required for SubTask', async () => {
    configure();
    const fixture = TestBed.createComponent(WorkItemFormComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    const typeSelect = root.querySelector<HTMLSelectElement>('#type')!;
    typeSelect.value = 'SubTask';
    typeSelect.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(root.querySelector<HTMLSelectElement>('#parentWorkItemId')!.required).toBe(true);
  });

  it('hides the parent picker entirely for Epic', async () => {
    configure();
    const fixture = TestBed.createComponent(WorkItemFormComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    const typeSelect = root.querySelector<HTMLSelectElement>('#type')!;
    typeSelect.value = 'Epic';
    typeSelect.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(root.querySelector('#parentWorkItemId')).toBeNull();
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
  parentWorkItemId: null,
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
    ],
  });
  return { getWorkItem, updateWorkItem, getParentCandidates };
}

describe('WorkItemFormComponent (edit mode)', () => {
  it("pre-fills existing values, including each <select> correctly pre-selecting its current value", async () => {
    configureEdit();
    const fixture = TestBed.createComponent(WorkItemFormComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    expect((root.querySelector('#title') as HTMLInputElement).value).toBe('Existing item');
    expect((root.querySelector('#type') as HTMLSelectElement).value).toBe('Story');
    expect((root.querySelector('#priority') as HTMLSelectElement).value).toBe('High');
    expect((root.querySelector('#status') as HTMLSelectElement).value).toBe('InProgress');
    expect((root.querySelector('#assigneeUserId') as HTMLSelectElement).value).toBe('2');
  });

  it('submits changes via updateWorkItem rather than createWorkItem', async () => {
    const { updateWorkItem } = configureEdit();
    const fixture = TestBed.createComponent(WorkItemFormComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    setInputValue(root.querySelector<HTMLInputElement>('#title')!, 'Updated title');
    root.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
    await fixture.whenStable();

    expect(updateWorkItem).toHaveBeenCalledWith(7, expect.objectContaining({ title: 'Updated title' }));
  });
});
