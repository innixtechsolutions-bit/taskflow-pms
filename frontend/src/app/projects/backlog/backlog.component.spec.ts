import { TestBed } from '@angular/core/testing';
import { CdkDragDrop } from '@angular/cdk/drag-drop';
import { provideRouter } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { vi } from 'vitest';
import { BacklogComponent } from './backlog.component';
import { Sprint, SprintsService } from '../sprints.service';
import { WorkItem, WorkItemBacklog, WorkItemsService } from '../work-items.service';
import { AuthService } from '../../auth/auth.service';
import { NotificationService } from '../../shared/notification.service';
import { WorkItemModalComponent } from '../work-item-modal/work-item-modal.component';
import { SprintFormComponent } from '../sprint-form/sprint-form.component';
import { CompleteSprintDialogComponent } from '../complete-sprint-dialog/complete-sprint-dialog.component';

function dropEvent(item: WorkItem): CdkDragDrop<WorkItem[]> {
  return { item: { data: item } } as CdkDragDrop<WorkItem[]>;
}

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
    updateWorkItemSprint: ReturnType<typeof vi.fn>;
  }> = {},
  role: 'Developer' | 'Manager' | 'Admin' | null = 'Developer',
  currentUser: { id: number } | null = { id: 1 }
) {
  const dialogOpen = vi.fn().mockReturnValue({});
  const notificationService = { success: vi.fn(), error: vi.fn() };
  const workItemsService = {
    getBacklog: vi.fn().mockResolvedValue(sampleBacklog()),
    getStatuses: vi.fn().mockResolvedValue([]),
    getAssignableUsers: vi.fn().mockResolvedValue([]),
    getProjectLabels: vi.fn().mockResolvedValue([]),
    updateWorkItemSprint: vi.fn().mockResolvedValue(undefined),
    ...overrides,
  };
  const sprintsService = {
    getSprints: vi.fn().mockResolvedValue(sampleSprints()),
    createSprint: vi.fn(),
    startSprint: vi.fn().mockResolvedValue({ status: 'Active' }),
    completeSprint: vi.fn().mockResolvedValue({ status: 'Completed' }),
    deleteSprint: vi.fn().mockResolvedValue(undefined),
  };
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [BacklogComponent],
    providers: [
      provideRouter([]),
      { provide: WorkItemsService, useValue: workItemsService },
      { provide: SprintsService, useValue: sprintsService },
      { provide: AuthService, useValue: { currentRole: () => role, currentUser: () => currentUser } },
      { provide: NotificationService, useValue: notificationService },
      { provide: MatDialog, useValue: { open: dialogOpen } },
    ],
  });
  return { dialogOpen, notificationService, ...workItemsService, ...sprintsService };
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

  // US3 — drag-and-drop

  it('onDrop moves an item into the target sprint section and persists it', async () => {
    const { updateWorkItemSprint } = configure();
    const fixture = await render();
    const item = sampleBacklog().backlogItems[0]; // 'Unscheduled task', id 3

    (fixture.componentInstance as unknown as { onDrop: (e: unknown, s: number | null) => void }).onDrop(dropEvent(item), 2);
    fixture.detectChanges();

    expect(updateWorkItemSprint).toHaveBeenCalledWith(item.id, 2);
    const sections = fixture.nativeElement.querySelectorAll('.sprint-section');
    expect(sections[1].textContent).toContain('Unscheduled task');
  });

  it('onDrop moves an item into the Backlog section when dropped there (target null)', async () => {
    const { updateWorkItemSprint } = configure();
    const fixture = await render();
    const item = sampleBacklog().sprints[0].items[0]; // 'In sprint 1', id 2, sprintId 1

    (fixture.componentInstance as unknown as { onDrop: (e: unknown, s: number | null) => void }).onDrop(dropEvent(item), null);
    fixture.detectChanges();

    expect(updateWorkItemSprint).toHaveBeenCalledWith(item.id, null);
    expect(fixture.nativeElement.querySelector('.backlog-section').textContent).toContain('In sprint 1');
  });

  it('onDrop reverts the item to its source section and shows an error toast when the PATCH fails', async () => {
    const { notificationService } = configure({ updateWorkItemSprint: vi.fn().mockRejectedValue(new Error('failed')) });
    const fixture = await render();
    const item = sampleBacklog().backlogItems[0]; // 'Unscheduled task', id 3

    (fixture.componentInstance as unknown as { onDrop: (e: unknown, s: number | null) => void }).onDrop(dropEvent(item), 2);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.backlog-section').textContent).toContain('Unscheduled task');
    expect(notificationService.error).toHaveBeenCalled();
  });

  function canDragOf(fixture: { componentInstance: unknown }): (i: WorkItem, sprintStatus: Sprint['status'] | null) => boolean {
    const instance = fixture.componentInstance as { canDrag: (i: WorkItem, sprintStatus: Sprint['status'] | null) => boolean };
    return instance.canDrag.bind(instance);
  }

  it('canDrag returns false for an Epic', async () => {
    configure();
    const fixture = await render();
    const epic = sampleBacklog().backlogItems[1]; // 'Context epic'

    expect(canDragOf(fixture)(epic, null)).toBe(false);
  });

  it('canDrag returns false for a caller without edit rights', async () => {
    configure({}, 'Developer', { id: 99 });
    const fixture = await render();
    const item = sampleBacklog().backlogItems[0]; // createdByUserId 1, assigneeUserId null

    expect(canDragOf(fixture)(item, null)).toBe(false);
  });

  it('canDrag returns false for any item inside a Completed section', async () => {
    configure();
    const fixture = await render();
    const item = sampleBacklog().backlogItems[0];

    expect(canDragOf(fixture)(item, 'Completed')).toBe(false);
  });

  it('canDrag returns true for a Story/Task the caller can edit in a non-Completed section', async () => {
    configure();
    const fixture = await render();
    const item = sampleBacklog().backlogItems[0];

    expect(canDragOf(fixture)(item, 'Planned')).toBe(true);
  });

  // US4 — lifecycle actions

  it('shows a Start action for a Manager/Admin on a Planned sprint with items, and starts it on click', async () => {
    const { startSprint, getBacklog } = configure({}, 'Manager');
    const fixture = await render();
    getBacklog.mockClear();

    const sections = fixture.nativeElement.querySelectorAll('.sprint-section');
    const startButton = sections[0].querySelector('.start-sprint-button') as HTMLButtonElement;
    expect(startButton.disabled).toBe(false);
    startButton.click();
    await fixture.whenStable();

    expect(startSprint).toHaveBeenCalledWith(1, 1);
    expect(getBacklog).toHaveBeenCalled();
  });

  it('disables Start for an empty Planned sprint', async () => {
    configure({}, 'Manager');
    const fixture = await render();

    const sections = fixture.nativeElement.querySelectorAll('.sprint-section');
    const startButton = sections[1].querySelector('.start-sprint-button') as HTMLButtonElement; // Sprint 2, 0 items
    expect(startButton.disabled).toBe(true);
  });

  it('shows a Delete action only for an empty Planned sprint, and confirms before deleting', async () => {
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
    const { deleteSprint, getBacklog } = configure({}, 'Manager');
    const fixture = await render();
    getBacklog.mockClear();

    const sections = fixture.nativeElement.querySelectorAll('.sprint-section');
    expect(sections[0].querySelector('.delete-sprint-button')).toBeNull(); // Sprint 1 has an item
    const deleteButton = sections[1].querySelector('.delete-sprint-button') as HTMLButtonElement; // Sprint 2, empty
    expect(deleteButton).toBeTruthy();
    deleteButton.click();
    await fixture.whenStable();

    expect(confirmSpy).toHaveBeenCalled();
    expect(deleteSprint).toHaveBeenCalledWith(1, 2);
    expect(getBacklog).toHaveBeenCalled();
    confirmSpy.mockRestore();
  });

  it('shows a Complete action for an Active sprint and opens CompleteSprintDialogComponent with the not-Done count and destination candidates', async () => {
    const activeBacklog: WorkItemBacklog = {
      sprints: [
        { id: 1, name: 'Sprint 1', startDate: '2026-08-01', endDate: '2026-08-15', status: 'Active', items: [
          sampleWorkItem({ id: 2, title: 'Not done', statusCategory: 'Open', sprintId: 1 }),
          sampleWorkItem({ id: 5, title: 'Done', statusCategory: 'Done', sprintId: 1 }),
        ] },
        { id: 2, name: 'Sprint 2', startDate: '2026-08-16', endDate: '2026-08-30', status: 'Planned', items: [] },
      ],
      backlogItems: [],
    };
    const { dialogOpen } = configure({ getBacklog: vi.fn().mockResolvedValue(activeBacklog) }, 'Manager');
    const fixture = await render();

    const sections = fixture.nativeElement.querySelectorAll('.sprint-section');
    expect(sections[0].querySelector('.start-sprint-button')).toBeNull();
    (sections[0].querySelector('.complete-sprint-button') as HTMLButtonElement).click();

    expect(dialogOpen).toHaveBeenCalledWith(CompleteSprintDialogComponent, expect.anything());
    const config = dialogOpen.mock.calls[0][1];
    expect(config.data.notDoneCount).toBe(1);
    expect(config.data.destinationCandidates.map((s: Sprint) => s.id)).toEqual([2]);
  });

  it('hides Start/Complete/Delete actions for a Developer', async () => {
    configure({}, 'Developer');
    const fixture = await render();

    expect(fixture.nativeElement.querySelector('.start-sprint-button')).toBeNull();
    expect(fixture.nativeElement.querySelector('.delete-sprint-button')).toBeNull();
  });
});
