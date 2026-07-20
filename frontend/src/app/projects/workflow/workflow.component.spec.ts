import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { WorkflowComponent } from './workflow.component';
import { ProjectStatusService } from '../project-status.service';
import { ProjectStatus } from '../work-items.service';

function sampleStatuses(): ProjectStatus[] {
  return [
    { id: 1, name: 'To Do', category: 'Open', colorKey: 'Slate', position: 0, itemCount: 3 },
    { id: 2, name: 'In Progress', category: 'Open', colorKey: 'Blue', position: 1, itemCount: 1 },
    { id: 4, name: 'Done', category: 'Done', colorKey: 'Green', position: 2, itemCount: 5 },
  ];
}

function configure(getStatuses = vi.fn().mockResolvedValue(sampleStatuses())) {
  TestBed.configureTestingModule({
    imports: [WorkflowComponent],
    providers: [
      provideRouter([]),
      { provide: ProjectStatusService, useValue: { getStatuses } },
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '1' }) } } },
    ],
  });
  return { getStatuses };
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
