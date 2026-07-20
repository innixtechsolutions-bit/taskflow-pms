import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { CdkDragDrop } from '@angular/cdk/drag-drop';
import { vi } from 'vitest';
import { WorkflowComponent } from './workflow.component';
import { ProjectStatusService } from '../project-status.service';
import { ProjectStatus } from '../work-items.service';
import { NotificationService } from '../../shared/notification.service';

function sampleStatuses(): ProjectStatus[] {
  return [
    { id: 1, name: 'To Do', category: 'Open', colorKey: 'Slate', position: 0, itemCount: 3 },
    { id: 2, name: 'In Progress', category: 'Open', colorKey: 'Blue', position: 1, itemCount: 1 },
    { id: 4, name: 'Done', category: 'Done', colorKey: 'Green', position: 2, itemCount: 5 },
  ];
}

function configure(
  getStatuses = vi.fn().mockResolvedValue(sampleStatuses()),
  createStatus = vi.fn(),
  updateStatus = vi.fn(),
  reorderStatuses = vi.fn().mockResolvedValue(sampleStatuses())
) {
  const notificationService = { success: vi.fn(), error: vi.fn() };
  TestBed.configureTestingModule({
    imports: [WorkflowComponent],
    providers: [
      provideRouter([]),
      { provide: ProjectStatusService, useValue: { getStatuses, createStatus, updateStatus, reorderStatuses } },
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '1' }) } } },
      { provide: NotificationService, useValue: notificationService },
    ],
  });
  return { getStatuses, createStatus, updateStatus, reorderStatuses, notificationService };
}

function setInputValue(el: HTMLInputElement, value: string): void {
  el.value = value;
  el.dispatchEvent(new Event('input'));
}

async function render() {
  const fixture = TestBed.createComponent(WorkflowComponent);
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  return fixture;
}

describe('WorkflowComponent', () => {
  it('lists statuses in position order with name, category, and item count', async () => {
    configure();
    const fixture = await render();

    const rows = Array.from(fixture.nativeElement.querySelectorAll('.workflow-row')) as HTMLElement[];
    expect(rows.length).toBe(3);
    expect(rows[0].textContent).toContain('To Do');
    expect(rows[0].textContent).toContain('Open');
    expect(rows[0].textContent).toContain('3');
    expect(rows[2].textContent).toContain('Done');
    expect(rows[2].textContent).toContain('5');
  });

  it('fetches statuses for the project id taken from the route', async () => {
    const { getStatuses } = configure();
    await render();

    expect(getStatuses).toHaveBeenCalledWith(1);
  });
});

describe('WorkflowComponent add-form (US3)', () => {
  it('submits name + category and appends the new status to the list', async () => {
    const createStatus = vi
      .fn()
      .mockResolvedValue({ id: 5, name: 'QA', category: 'Open', colorKey: 'Teal', position: 2, itemCount: 0 });
    configure(vi.fn().mockResolvedValue(sampleStatuses()), createStatus);
    const fixture = await render();

    const root = fixture.nativeElement as HTMLElement;
    setInputValue(root.querySelector<HTMLInputElement>('.add-status-name')!, 'QA');
    const categorySelect = root.querySelector<HTMLSelectElement>('.add-status-category')!;
    categorySelect.value = 'Open';
    categorySelect.dispatchEvent(new Event('change'));
    root.querySelector('.add-status-form')!.dispatchEvent(new Event('submit', { cancelable: true }));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(createStatus).toHaveBeenCalledWith(1, { name: 'QA', category: 'Open' });
    expect(fixture.nativeElement.textContent).toContain('QA');
  });
});

describe('WorkflowComponent inline rename (US4)', () => {
  function rowFor(fixture: { nativeElement: HTMLElement }, name: string): HTMLElement {
    return Array.from(fixture.nativeElement.querySelectorAll<HTMLElement>('.workflow-row')).find((row) =>
      row.textContent?.includes(name)
    )!;
  }

  it('updates the list after a successful rename', async () => {
    const updateStatus = vi
      .fn()
      .mockResolvedValue({ id: 1, name: 'Doing', category: 'Open', colorKey: 'Slate', position: 0, itemCount: 3 });
    configure(vi.fn().mockResolvedValue(sampleStatuses()), undefined, updateStatus);
    const fixture = await render();

    (rowFor(fixture, 'To Do').querySelector('.rename-button') as HTMLButtonElement).click();
    fixture.detectChanges();
    setInputValue(fixture.nativeElement.querySelector('.edit-status-name')!, 'Doing');
    (fixture.nativeElement.querySelector('.save-edit-button') as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(updateStatus).toHaveBeenCalledWith(1, 1, { name: 'Doing' });
    expect(fixture.nativeElement.textContent).toContain('Doing');
    expect(fixture.nativeElement.textContent).not.toContain('To Do');
  });

  it('surfaces a duplicate-name error without losing the edit', async () => {
    const updateStatus = vi.fn().mockRejectedValue(new Error('Conflict'));
    configure(vi.fn().mockResolvedValue(sampleStatuses()), undefined, updateStatus);
    const fixture = await render();

    (rowFor(fixture, 'To Do').querySelector('.rename-button') as HTMLButtonElement).click();
    fixture.detectChanges();
    setInputValue(fixture.nativeElement.querySelector('.edit-status-name')!, 'Done');
    (fixture.nativeElement.querySelector('.save-edit-button') as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.edit-error')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('.edit-status-name')).toBeTruthy();
  });
});

describe('WorkflowComponent drag reorder (US5)', () => {
  it('calls the reorder endpoint with the new id sequence on drop', async () => {
    const reorderStatuses = vi.fn().mockResolvedValue(sampleStatuses());
    configure(vi.fn().mockResolvedValue(sampleStatuses()), undefined, undefined, reorderStatuses);
    const fixture = await render();

    // Dragging the first row (id 1, "To Do") to the last position -- CDK's drop event
    // carries indices, not ids; exercising the handler directly (rather than
    // simulating real pointer events, which CDK's drag-drop doesn't support well in
    // jsdom) is this codebase's established way to test drop logic.
    (fixture.componentInstance as unknown as { onReorderDrop(event: CdkDragDrop<ProjectStatus[]>): void }).onReorderDrop({
      previousIndex: 0,
      currentIndex: 2,
    } as CdkDragDrop<ProjectStatus[]>);
    await fixture.whenStable();

    expect(reorderStatuses).toHaveBeenCalledWith(1, [2, 4, 1]);
  });
});
