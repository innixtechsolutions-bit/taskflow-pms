import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { SummaryComponent } from './summary.component';
import { ProjectSummary, WorkItemsService } from '../work-items.service';

function sampleSummary(overrides: Partial<ProjectSummary> = {}): ProjectSummary {
  return {
    statCards: { total: 10, completed: 4, completedPercent: 40, inProgress: 6, dueSoon: 2 },
    statusBreakdown: [],
    priorityBreakdown: [],
    workload: [],
    ...overrides,
  };
}

function configure(getProjectSummary = vi.fn().mockResolvedValue(sampleSummary())) {
  TestBed.configureTestingModule({
    imports: [SummaryComponent],
    providers: [{ provide: WorkItemsService, useValue: { getProjectSummary } }],
  });
  return { getProjectSummary };
}

async function render(projectId = 1) {
  const fixture = TestBed.createComponent(SummaryComponent);
  fixture.componentRef.setInput('projectId', projectId);
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  return fixture;
}

describe('SummaryComponent', () => {
  it('fetches the summary for the given project on init', async () => {
    const { getProjectSummary } = configure();
    await render(42);

    expect(getProjectSummary).toHaveBeenCalledWith(42);
  });

  it('renders the four stat cards with the returned values', async () => {
    configure();
    const fixture = await render();

    expect(fixture.nativeElement.querySelector('.stat-card-total')?.textContent).toContain('10');
    expect(fixture.nativeElement.querySelector('.stat-card-completed')?.textContent).toContain('4');
    expect(fixture.nativeElement.querySelector('.stat-card-completed')?.textContent).toContain('40%');
    expect(fixture.nativeElement.querySelector('.stat-card-in-progress')?.textContent).toContain('6');
    expect(fixture.nativeElement.querySelector('.stat-card-due-soon')?.textContent).toContain('2');
  });
});
