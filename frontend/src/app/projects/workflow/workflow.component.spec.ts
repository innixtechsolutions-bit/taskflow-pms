import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
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
  createStatus = vi.fn()
) {
  const notificationService = { success: vi.fn(), error: vi.fn() };
  TestBed.configureTestingModule({
    imports: [WorkflowComponent],
    providers: [
      provideRouter([]),
      { provide: ProjectStatusService, useValue: { getStatuses, createStatus } },
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '1' }) } } },
      { provide: NotificationService, useValue: notificationService },
    ],
  });
  return { getStatuses, createStatus, notificationService };
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
