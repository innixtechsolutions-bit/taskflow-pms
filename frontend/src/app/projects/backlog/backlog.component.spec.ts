import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { vi } from 'vitest';
import { BacklogComponent } from './backlog.component';
import { Sprint, SprintsService } from '../sprints.service';
import { WorkItem, WorkItemBacklog, WorkItemsService } from '../work-items.service';
import { AuthService } from '../../auth/auth.service';
import { WorkItemModalComponent } from '../work-item-modal/work-item-modal.component';
import { SprintFormComponent } from '../sprint-form/sprint-form.component';

function sampleWorkItem(overrides: Partial<WorkItem> = {}): WorkItem {
  return {
    id: 1,
    projectId: 1,
    type: 'Task',
    title: 'Some item',
    description: null,
    priority: 'Medium',
    statusId: 1,
    statusName: 'To Do',
    statusCategory: 'Open',
    statusColorKey: 'Slate',
    assigneeUserId: null,
    assigneeName: null,
    dueDate: null,
    startDate: null,
    createdByUserId: 1,
    createdByName: 'Ada Lovelace',
    createdAt: '2026-07-01T00:00:00Z',
    updatedAt: '2026-07-01T00:00:00Z',
    parentWorkItemId: null,
    labels: [],
    sprintId: null,
    sprintName: null,
    ...overrides,
  };
}

function sampleBacklog(): WorkItemBacklog {
  return {
    sprints: [
      {
        id: 1,
        name: 'Sprint 1',
        startDate: '2026-08-01',
        endDate: '2026-08-15',
        status: 'Planned',
        items: [sampleWorkItem({ id: 2, title: 'In sprint 1', sprintId: 1, sprintName: 'Sprint 1' })],
      },
      {
        id: 2,
        name: 'Sprint 2',
        startDate: '2026-08-16',
        endDate: '2026-08-30',
        status: 'Planned',
        items: [],
      },
    ],
    backlogItems: [
      sampleWorkItem({ id: 3, title: 'Unscheduled task' }),
      sampleWorkItem({ id: 4, title: 'Context epic', type: 'Epic' }),
    ],
  };
}

function sampleSprints(): Sprint[] {
  return [
    { id: 1, projectId: 1, name: 'Sprint 1', startDate: '2026-08-01', endDate: '2026-08-15', status: 'Planned', itemCount: 1 },
    { id: 2, projectId: 1, name: 'Sprint 2', startDate: '2026-08-16', endDate: '2026-08-30', status: 'Planned', itemCount: 0 },
  ];
}

function configure(
  overrides: Partial<{
    getBacklog: ReturnType<typeof vi.fn>;
    getStatuses: ReturnType<typeof vi.fn>;
    getAssignableUsers: ReturnType<typeof vi.fn>;
    getProjectLabels: ReturnType<typeof vi.fn>;
    createSprint: ReturnType<typeof vi.fn>;
  }> = {},
  role: 'Developer' | 'Manager' | 'Admin' | null = 'Developer'
) {
  const dialogOpen = vi.fn().mockReturnValue({});
  const workItemsService = {
    getBacklog: vi.fn().mockResolvedValue(sampleBacklog()),
    getStatuses: vi.fn().mockResolvedValue([]),
    getAssignableUsers: vi.fn().mockResolvedValue([]),
    getProjectLabels: vi.fn().mockResolvedValue([]),
    ...overrides,
  };
  const sprintsService = { getSprints: vi.fn().mockResolvedValue(sampleSprints()), createSprint: vi.fn() };
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [BacklogComponent],
    providers: [
      provideRouter([]),
      { provide: WorkItemsService, useValue: workItemsService },
      { provide: SprintsService, useValue: sprintsService },
      { provide: AuthService, useValue: { currentRole: () => role } },
      { provide: MatDialog, useValue: { open: dialogOpen } },
    ],
  });
  return { dialogOpen, ...workItemsService };
}

async function render(projectId = 1) {
  const fixture = TestBed.createComponent(BacklogComponent);
  fixture.componentRef.setInput('projectId', projectId);
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  return fixture;
}

describe('BacklogComponent', () => {
  it('renders each sprint section (name/dates/count) with its items, plus the Backlog section, soonest-first order preserved', async () => {
    configure();
    const fixture = await render();

    const sections = fixture.nativeElement.querySelectorAll('.sprint-section');
    expect(sections.length).toBe(2);
    expect(sections[0].textContent).toContain('Sprint 1');
    expect(sections[0].textContent).toContain('In sprint 1');
    expect(sections[1].textContent).toContain('Sprint 2');

    const backlogSection = fixture.nativeElement.querySelector('.backlog-section');
    expect(backlogSection.textContent).toContain('Unscheduled task');
    expect(backlogSection.textContent).toContain('Context epic');
  });

  it('shows an empty-state hint inside a sprint section with zero items (FR-025)', async () => {
    configure();
    const fixture = await render();

    const sections = fixture.nativeElement.querySelectorAll('.sprint-section');
    expect(sections[1].textContent).toContain('Sprint 2');
    expect(sections[1].querySelector('.sprint-empty-hint')).toBeTruthy();
    expect(sections[0].querySelector('.sprint-empty-hint')).toBeNull();
  });

  it('re-fetches getBacklog with the selected filters when a filter changes', async () => {
    const { getBacklog } = configure();
    const fixture = await render();
    getBacklog.mockClear();

    const component = fixture.componentInstance;
    component['onTypeFilterChange']('Epic');
    await fixture.whenStable();

    expect(getBacklog).toHaveBeenCalledWith(1, expect.objectContaining({ type: 'Epic' }));
  });

  it('shows "Create sprint" for a Manager/Admin and hides it for a Developer', async () => {
    configure({}, 'Manager');
    const manager = await render();
    expect(manager.nativeElement.querySelector('.create-sprint-button')).toBeTruthy();

    configure({}, 'Developer');
    const developer = await render();
    expect(developer.nativeElement.querySelector('.create-sprint-button')).toBeNull();
  });

  it('opens SprintFormComponent when "Create sprint" is clicked and refreshes the backlog on save', async () => {
    const { dialogOpen, getBacklog } = configure({}, 'Manager');
    const fixture = await render();
    getBacklog.mockClear();

    (fixture.nativeElement.querySelector('.create-sprint-button') as HTMLButtonElement).click();

    expect(dialogOpen).toHaveBeenCalledWith(SprintFormComponent, expect.anything());
    const config = dialogOpen.mock.calls[0][1];
    config.data.onSaved();
    await fixture.whenStable();

    expect(getBacklog).toHaveBeenCalled();
  });

  it('opens WorkItemModalComponent pre-assigned to a sprint section when its "+ Create" is used', async () => {
    const { dialogOpen } = configure();
    const fixture = await render();

    const sections = fixture.nativeElement.querySelectorAll('.sprint-section');
    (sections[0].querySelector('.section-create-button') as HTMLButtonElement).click();
    await vi.waitFor(() => expect(dialogOpen).toHaveBeenCalled());

    expect(dialogOpen).toHaveBeenCalledWith(WorkItemModalComponent, expect.anything());
    const config = dialogOpen.mock.calls[0][1];
    expect(config.data.sprintId).toBe(1);
    expect(config.data.mode).toBe('create');
  });

  it('opens WorkItemModalComponent with no sprintId when the Backlog section\'s "+ Create" is used', async () => {
    const { dialogOpen } = configure();
    const fixture = await render();

    (fixture.nativeElement.querySelector('.backlog-section .section-create-button') as HTMLButtonElement).click();
    await vi.waitFor(() => expect(dialogOpen).toHaveBeenCalled());

    const config = dialogOpen.mock.calls[0][1];
    expect(config.data.sprintId).toBeUndefined();
  });
});
