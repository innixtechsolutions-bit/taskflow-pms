import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { SummaryComponent } from './summary.component';
import { ActivityEntry, PagedResult, ProjectSummary, WorkItemsService } from '../work-items.service';

function sampleSummary(overrides: Partial<ProjectSummary> = {}): ProjectSummary {
  return {
    statCards: { total: 10, completed: 4, completedPercent: 40, inProgress: 6, dueSoon: 2 },
    statusBreakdown: [],
    priorityBreakdown: [],
    workload: [],
    ...overrides,
  };
}

function entry(overrides: Partial<ActivityEntry> = {}): ActivityEntry {
  return {
    id: 1,
    workItemId: 10,
    workItemTitle: 'Fix login',
    workItemType: 'Task',
    eventType: 'Created',
    field: null,
    oldValue: null,
    newValue: null,
    actorUserId: 1,
    actorName: 'Jane',
    createdAt: new Date().toISOString(),
    ...overrides,
  };
}

function emptyActivityPage(): PagedResult<ActivityEntry> {
  return { items: [], page: 1, pageSize: 20, totalCount: 0 };
}

function configure(
  getProjectSummary = vi.fn().mockResolvedValue(sampleSummary()),
  getProjectActivity = vi.fn().mockResolvedValue(emptyActivityPage())
) {
  TestBed.configureTestingModule({
    imports: [SummaryComponent],
    providers: [{ provide: WorkItemsService, useValue: { getProjectSummary, getProjectActivity } }],
  });
  return { getProjectSummary, getProjectActivity };
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

  it('fetches the first page of project activity on init and renders it via the activity feed', async () => {
    const { getProjectActivity } = configure(
      undefined,
      vi.fn().mockResolvedValue({ items: [entry()], page: 1, pageSize: 20, totalCount: 1 })
    );
    const fixture = await render(42);

    expect(getProjectActivity).toHaveBeenCalledWith(42, 1, 20);
    expect(fixture.nativeElement.querySelector('app-activity-feed')).toBeTruthy();
    expect(fixture.nativeElement.textContent).toContain("Jane created Task 'Fix login'");
  });

  it('loads and appends the next page when "Load more" is clicked', async () => {
    const getProjectActivity = vi
      .fn()
      .mockResolvedValueOnce({ items: [entry({ id: 1, workItemTitle: 'First' })], page: 1, pageSize: 20, totalCount: 2 })
      .mockResolvedValueOnce({ items: [entry({ id: 2, workItemTitle: 'Second' })], page: 2, pageSize: 20, totalCount: 2 });
    configure(undefined, getProjectActivity);
    const fixture = await render(42);

    (fixture.nativeElement.querySelector('.load-more-activity') as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(getProjectActivity).toHaveBeenCalledWith(42, 2, 20);
    const rows = fixture.nativeElement.querySelectorAll('.activity-entry');
    expect(rows.length).toBe(2);
  });

  it('hides "Load more" once every entry has been loaded', async () => {
    configure(
      undefined,
      vi.fn().mockResolvedValue({ items: [entry()], page: 1, pageSize: 20, totalCount: 1 })
    );
    const fixture = await render();

    expect(fixture.nativeElement.querySelector('.load-more-activity')).toBeNull();
  });
});
