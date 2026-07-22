import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { MatSelectHarness } from '@angular/material/select/testing';
import { MatDialog } from '@angular/material/dialog';
import { vi } from 'vitest';
import { ProjectDetailComponent } from './project-detail.component';
import { WorkItemModalComponent } from '../work-item-modal/work-item-modal.component';
import { ProjectsService } from '../projects.service';
import { ProjectStatus, WorkItem, WorkItemsService } from '../work-items.service';
import { SprintsService } from '../sprints.service';
import { AuthService } from '../../auth/auth.service';
import { NotificationService } from '../../shared/notification.service';

async function chooseFilterOption(
  fixture: ComponentFixture<unknown>,
  label: string,
  optionText: string
): Promise<void> {
  const loader = TestbedHarnessEnvironment.loader(fixture);
  const select = await loader.getHarness(MatSelectHarness.with({ label }));
  await select.open();
  await select.clickOptions({ text: optionText });
}

const sampleProject = {
  id: 1,
  name: 'Website Redesign',
  description: 'Rebuild the marketing site',
  createdByName: 'Ada Lovelace',
  createdAt: '2026-01-01T00:00:00Z',
  totalWorkItemCount: 0,
};

const sampleProjectWithItems = { ...sampleProject, totalWorkItemCount: 12 };

function emptyPage() {
  return { items: [] as WorkItem[], page: 1, pageSize: 20, totalCount: 0 };
}

function pageOf(items: WorkItem[]) {
  return { items, page: 1, pageSize: 20, totalCount: items.length };
}

// Feature 006 — the standard four, matching what ProjectService.CreateAsync seeds
// in production; ids are stable so tests can reference "Done"'s id (4) directly.
function sampleStatuses(): ProjectStatus[] {
  return [
    { id: 1, name: 'To Do', category: 'Open', colorKey: 'Slate', position: 0, itemCount: 0 },
    { id: 2, name: 'In Progress', category: 'Open', colorKey: 'Blue', position: 1, itemCount: 0 },
    { id: 3, name: 'In Review', category: 'Open', colorKey: 'Violet', position: 2, itemCount: 0 },
    { id: 4, name: 'Done', category: 'Done', colorKey: 'Green', position: 3, itemCount: 0 },
  ];
}

function sampleItem(overrides: Partial<WorkItem> = {}): WorkItem {
  return {
    id: 1,
    projectId: 1,
    type: 'Task',
    title: 'Fix the login bug',
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
    createdByUserId: 10,
    createdByName: 'Creator',
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    parentWorkItemId: null,
    labels: [],
    sprintId: null,
    sprintName: null,
    ...overrides,
  };
}

function configure(
  getProject = vi.fn().mockResolvedValue(sampleProject),
  getWorkItems = vi.fn().mockResolvedValue(emptyPage()),
  authState: { id: number; role: string } | null = { id: 1, role: 'Developer' },
  deleteWorkItem = vi.fn().mockResolvedValue(undefined),
  deleteProject = vi.fn().mockResolvedValue(undefined),
  getAssignableUsers = vi.fn().mockResolvedValue([]),
  getWorkItemsTree = vi.fn().mockResolvedValue([]),
  getWorkItemDetail = vi.fn().mockResolvedValue({ totalDescendantCount: 0 }),
  // Most tests in this file exercise Flat-list-specific behavior (filters, edit/delete
  // controls, pagination) that predates Feature 005 Polish's board-as-default change —
  // they set up the route as if Flat were already active rather than asserting on
  // whichever view happens to be the app-wide default.
  view = 'flat',
  getStatuses = vi.fn().mockResolvedValue(sampleStatuses()),
  getProjectLabels = vi.fn().mockResolvedValue([])
) {
  const notificationService = { success: vi.fn(), error: vi.fn() };
  const dialogOpen = vi.fn().mockReturnValue({});
  TestBed.configureTestingModule({
    imports: [ProjectDetailComponent],
    providers: [
      provideRouter([]),
      { provide: ProjectsService, useValue: { getProject, deleteProject } },
      {
        provide: WorkItemsService,
        useValue: {
          getWorkItems,
          deleteWorkItem,
          getAssignableUsers,
          getWorkItemsTree,
          getWorkItemDetail,
          getStatuses,
          getProjectLabels,
          getBoard: vi.fn().mockResolvedValue({ columns: [], items: [] }),
        },
      },
      { provide: SprintsService, useValue: { getSprints: vi.fn().mockResolvedValue([]) } },
      { provide: AuthService, useValue: { currentUser: () => authState, currentRole: () => authState?.role ?? null } },
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { paramMap: convertToParamMap({ id: '1' }), queryParamMap: convertToParamMap({ view }) } },
      },
      { provide: NotificationService, useValue: notificationService },
      { provide: MatDialog, useValue: { open: dialogOpen } },
    ],
  });
  return {
    getProject,
    getWorkItems,
    deleteWorkItem,
    deleteProject,
    getAssignableUsers,
    getWorkItemsTree,
    getWorkItemDetail,
    notificationService,
    dialogOpen,
  };
}

async function render() {
  const fixture = TestBed.createComponent(ProjectDetailComponent);
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  // load() awaits getProject() then getWorkItems() sequentially — a second round
  // ensures both resolve before assertions run, not just the first.
  await fixture.whenStable();
  fixture.detectChanges();
  return fixture;
}

describe('ProjectDetailComponent', () => {
  it("renders the project's header info", async () => {
    configure();
    const fixture = await render();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Website Redesign');
    expect(text).toContain('Rebuild the marketing site');
    expect(text).toContain('Ada Lovelace');
  });

  it('renders the creation date in friendly format, never raw ISO (SC-006)', async () => {
    configure();
    const fixture = await render();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Jan 1, 2026');
    expect(text).not.toMatch(/\d{4}-\d{2}-\d{2}T/);
  });

  it('renders status and priority as chips, not plain text', async () => {
    configure(undefined, vi.fn().mockResolvedValue(pageOf([sampleItem({ statusId: 2, statusName: 'In Progress', priority: 'High' })])));
    const fixture = await render();

    expect(fixture.nativeElement.querySelector('app-status-chip')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('app-priority-chip')).toBeTruthy();
  });

  it('shows "No work items yet" when the project has none', async () => {
    configure();
    const fixture = await render();

    expect(fixture.nativeElement.textContent).toContain('No work items yet');
  });

  it('renders each fetched work item', async () => {
    configure(undefined, vi.fn().mockResolvedValue(pageOf([sampleItem()])));
    const fixture = await render();

    expect(fixture.nativeElement.textContent).toContain('Fix the login bug');
  });

  it("links a flat-list item's title to its detail page", async () => {
    configure(undefined, vi.fn().mockResolvedValue(pageOf([sampleItem({ id: 42 })])));
    const fixture = await render();

    const link = fixture.nativeElement.querySelector('.work-item-title a') as HTMLAnchorElement;
    expect(link.getAttribute('href')).toBe('/projects/1/work-items/42');
  });

  // User Story 5 (non-regression): the Flat view renders a parented item exactly
  // like a standalone one — no indentation, no tree-only markup — regardless of
  // whether it has a parentWorkItemId, matching Feature 002's unchanged behavior.
  it('renders a parented item in Flat view exactly like a standalone one, with no indentation', async () => {
    configure(
      undefined,
      vi.fn().mockResolvedValue(pageOf([sampleItem({ id: 1, title: 'A child item', parentWorkItemId: 99 })]))
    );
    const fixture = await render();

    expect(fixture.nativeElement.textContent).toContain('A child item');
    expect(fixture.nativeElement.querySelector('.tree-row')).toBeNull();
  });
});

describe('ProjectDetailComponent edit control visibility', () => {
  function editLink(fixture: { nativeElement: HTMLElement }, itemId: number): Element | null {
    return fixture.nativeElement.querySelector(`#work-item-${itemId} .edit-link`);
  }

  it('shows the edit control for the item creator', async () => {
    configure(undefined, vi.fn().mockResolvedValue(pageOf([sampleItem({ id: 1, createdByUserId: 1 })])), { id: 1, role: 'Developer' });
    const fixture = await render();

    expect(editLink(fixture, 1)).toBeTruthy();
  });

  it('shows the edit control for the current assignee', async () => {
    configure(undefined, vi.fn().mockResolvedValue(pageOf([sampleItem({ id: 1, createdByUserId: 99, assigneeUserId: 1 })])), { id: 1, role: 'Developer' });
    const fixture = await render();

    expect(editLink(fixture, 1)).toBeTruthy();
  });

  it('shows the edit control for a Manager who is neither creator nor assignee', async () => {
    configure(undefined, vi.fn().mockResolvedValue(pageOf([sampleItem({ id: 1, createdByUserId: 99, assigneeUserId: 98 })])), { id: 1, role: 'Manager' });
    const fixture = await render();

    expect(editLink(fixture, 1)).toBeTruthy();
  });

  it('shows the edit control for an Admin who is neither creator nor assignee', async () => {
    configure(undefined, vi.fn().mockResolvedValue(pageOf([sampleItem({ id: 1, createdByUserId: 99, assigneeUserId: 98 })])), { id: 1, role: 'Admin' });
    const fixture = await render();

    expect(editLink(fixture, 1)).toBeTruthy();
  });

  it('hides the edit control for an unrelated viewer', async () => {
    configure(undefined, vi.fn().mockResolvedValue(pageOf([sampleItem({ id: 1, createdByUserId: 99, assigneeUserId: 98 })])), { id: 1, role: 'Developer' });
    const fixture = await render();

    expect(editLink(fixture, 1)).toBeNull();
  });
});

describe('ProjectDetailComponent delete control visibility (narrower than edit)', () => {
  function deleteButton(fixture: { nativeElement: HTMLElement }, itemId: number): Element | null {
    return fixture.nativeElement.querySelector(`#work-item-${itemId} .delete-button`);
  }

  it('shows the delete control for the item creator', async () => {
    configure(undefined, vi.fn().mockResolvedValue(pageOf([sampleItem({ id: 1, createdByUserId: 1 })])), { id: 1, role: 'Developer' });
    const fixture = await render();

    expect(deleteButton(fixture, 1)).toBeTruthy();
  });

  it('shows the delete control for a Manager', async () => {
    configure(undefined, vi.fn().mockResolvedValue(pageOf([sampleItem({ id: 1, createdByUserId: 99 })])), { id: 1, role: 'Manager' });
    const fixture = await render();

    expect(deleteButton(fixture, 1)).toBeTruthy();
  });

  it('shows the delete control for an Admin', async () => {
    configure(undefined, vi.fn().mockResolvedValue(pageOf([sampleItem({ id: 1, createdByUserId: 99 })])), { id: 1, role: 'Admin' });
    const fixture = await render();

    expect(deleteButton(fixture, 1)).toBeTruthy();
  });

  it('hides the delete control for the current assignee who is not also creator/Manager/Admin', async () => {
    configure(undefined, vi.fn().mockResolvedValue(pageOf([sampleItem({ id: 1, createdByUserId: 99, assigneeUserId: 1 })])), { id: 1, role: 'Developer' });
    const fixture = await render();

    expect(deleteButton(fixture, 1)).toBeNull();
  });

  it('hides the delete control for an unrelated viewer', async () => {
    configure(undefined, vi.fn().mockResolvedValue(pageOf([sampleItem({ id: 1, createdByUserId: 99 })])), { id: 1, role: 'Developer' });
    const fixture = await render();

    expect(deleteButton(fixture, 1)).toBeNull();
  });

  it('calls deleteWorkItem and refreshes the list after a confirmed delete, showing a success toast', async () => {
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
    const getWorkItems = vi
      .fn()
      .mockResolvedValueOnce(pageOf([sampleItem({ id: 1, createdByUserId: 1 })]))
      .mockResolvedValueOnce(emptyPage());
    const { deleteWorkItem, notificationService } = configure(undefined, getWorkItems, { id: 1, role: 'Developer' });
    const fixture = await render();

    (deleteButton(fixture, 1) as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(deleteWorkItem).toHaveBeenCalledWith(1);
    expect(getWorkItems).toHaveBeenCalledTimes(2);
    expect(notificationService.success).toHaveBeenCalled();
    confirmSpy.mockRestore();
  });

  it('shows an error toast when deleting a work item fails', async () => {
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
    const { notificationService } = configure(
      undefined,
      vi.fn().mockResolvedValue(pageOf([sampleItem({ id: 1, createdByUserId: 1 })])),
      { id: 1, role: 'Developer' },
      vi.fn().mockRejectedValue(new Error('failed'))
    );
    const fixture = await render();

    (deleteButton(fixture, 1) as HTMLButtonElement).click();
    await fixture.whenStable();

    expect(notificationService.error).toHaveBeenCalled();
    confirmSpy.mockRestore();
  });

  it("states the total descendant count in the flat list's delete confirmation", async () => {
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(false);
    const getWorkItemDetail = vi.fn().mockResolvedValue({ totalDescendantCount: 3 });
    configure(
      undefined,
      vi.fn().mockResolvedValue(pageOf([sampleItem({ id: 1, createdByUserId: 1 })])),
      { id: 1, role: 'Developer' },
      undefined,
      undefined,
      undefined,
      undefined,
      getWorkItemDetail
    );
    const fixture = await render();

    (deleteButton(fixture, 1) as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(confirmSpy).toHaveBeenCalledWith(expect.stringContaining('3'));
    confirmSpy.mockRestore();
  });

  it('does not call deleteWorkItem when the confirmation is declined', async () => {
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(false);
    const { deleteWorkItem } = configure(undefined, vi.fn().mockResolvedValue(pageOf([sampleItem({ id: 1, createdByUserId: 1 })])), { id: 1, role: 'Developer' });
    const fixture = await render();

    (deleteButton(fixture, 1) as HTMLButtonElement).click();
    await fixture.whenStable();

    expect(deleteWorkItem).not.toHaveBeenCalled();
    confirmSpy.mockRestore();
  });
});

describe('ProjectDetailComponent project-level edit/delete controls', () => {
  it('shows the project edit/delete controls for a Manager', async () => {
    configure(undefined, undefined, { id: 1, role: 'Manager' });
    const fixture = await render();

    expect(fixture.nativeElement.querySelector('.project-edit-link')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('.project-delete-button')).toBeTruthy();
  });

  it('shows the project edit/delete controls for an Admin', async () => {
    configure(undefined, undefined, { id: 1, role: 'Admin' });
    const fixture = await render();

    expect(fixture.nativeElement.querySelector('.project-edit-link')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('.project-delete-button')).toBeTruthy();
  });

  it('hides the project edit/delete controls for a Developer', async () => {
    configure(undefined, undefined, { id: 1, role: 'Developer' });
    const fixture = await render();

    expect(fixture.nativeElement.querySelector('.project-edit-link')).toBeNull();
    expect(fixture.nativeElement.querySelector('.project-delete-button')).toBeNull();
  });

  it('states the exact work-item count (from totalWorkItemCount) in the delete confirmation', async () => {
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(false);
    configure(vi.fn().mockResolvedValue(sampleProjectWithItems), undefined, { id: 1, role: 'Manager' });
    const fixture = await render();

    fixture.nativeElement.querySelector('.project-delete-button').click();

    expect(confirmSpy).toHaveBeenCalledWith(expect.stringContaining('12'));
    confirmSpy.mockRestore();
  });

  it('calls deleteProject when the confirmation is accepted, showing a success toast', async () => {
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
    const { deleteProject, notificationService } = configure(
      vi.fn().mockResolvedValue(sampleProjectWithItems),
      undefined,
      { id: 1, role: 'Manager' }
    );
    const fixture = await render();
    // provideRouter([]) has no registered routes, so the real navigateByUrl('/projects')
    // this triggers would reject with "cannot match any routes" — irrelevant to what
    // this test checks, so it's stubbed out rather than exercised.
    vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);

    fixture.nativeElement.querySelector('.project-delete-button').click();
    await fixture.whenStable();

    expect(deleteProject).toHaveBeenCalledWith(1);
    expect(notificationService.success).toHaveBeenCalled();
    confirmSpy.mockRestore();
  });

  it('does not call deleteProject when the confirmation is declined', async () => {
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(false);
    const { deleteProject } = configure(vi.fn().mockResolvedValue(sampleProjectWithItems), undefined, { id: 1, role: 'Manager' });
    const fixture = await render();

    fixture.nativeElement.querySelector('.project-delete-button').click();
    await fixture.whenStable();

    expect(deleteProject).not.toHaveBeenCalled();
    confirmSpy.mockRestore();
  });
});

describe('ProjectDetailComponent workflow entry point (US2)', () => {
  it('shows a Workflow link for a Manager', async () => {
    configure(undefined, undefined, { id: 1, role: 'Manager' });
    const fixture = await render();

    expect(fixture.nativeElement.querySelector('.workflow-link')).toBeTruthy();
  });

  it('shows a Workflow link for an Admin', async () => {
    configure(undefined, undefined, { id: 1, role: 'Admin' });
    const fixture = await render();

    expect(fixture.nativeElement.querySelector('.workflow-link')).toBeTruthy();
  });

  it('hides the Workflow link for a Developer (US2 scenario 2)', async () => {
    configure(undefined, undefined, { id: 1, role: 'Developer' });
    const fixture = await render();

    expect(fixture.nativeElement.querySelector('.workflow-link')).toBeNull();
  });
});

describe('ProjectDetailComponent work item modal (US1)', () => {
  it('opens the create modal with no prefill from the toolbar "New work item" button', async () => {
    const { dialogOpen } = configure();
    const fixture = await render();

    (fixture.nativeElement.querySelector('.new-work-item-button') as HTMLButtonElement).click();
    // The modal is dynamically imported (fix: restore production build) — its
    // own chunk resolves via a microtask, not synchronously with the click.
    await vi.waitFor(() => expect(dialogOpen).toHaveBeenCalled());

    expect(dialogOpen).toHaveBeenCalledWith(
      WorkItemModalComponent,
      expect.objectContaining({ data: expect.objectContaining({ mode: 'create', projectId: 1 }) })
    );
  });

  it('opens the create modal from an empty state\'s "Add work item" action', async () => {
    const { dialogOpen } = configure(undefined, vi.fn().mockResolvedValue(emptyPage()));
    const fixture = await render();

    (fixture.nativeElement.querySelector('.empty-state-add-work-item') as HTMLButtonElement).click();
    await vi.waitFor(() => expect(dialogOpen).toHaveBeenCalled());

    expect(dialogOpen).toHaveBeenCalledWith(
      WorkItemModalComponent,
      expect.objectContaining({ data: expect.objectContaining({ mode: 'create', projectId: 1 }) })
    );
  });

  it('opens the edit modal for a flat-list row', async () => {
    const { dialogOpen } = configure(
      undefined,
      vi.fn().mockResolvedValue(pageOf([sampleItem({ id: 1, createdByUserId: 1 })])),
      { id: 1, role: 'Developer' }
    );
    const fixture = await render();

    (fixture.nativeElement.querySelector('#work-item-1 .edit-link') as HTMLButtonElement).click();
    await vi.waitFor(() => expect(dialogOpen).toHaveBeenCalled());

    expect(dialogOpen).toHaveBeenCalledWith(
      WorkItemModalComponent,
      expect.objectContaining({ data: expect.objectContaining({ mode: 'edit', workItemId: 1 }) })
    );
  });

  it('re-fetches the flat list and tree once the modal reports a save', async () => {
    const getWorkItems = vi.fn().mockResolvedValue(pageOf([sampleItem({ id: 1, createdByUserId: 1 })]));
    const getWorkItemsTree = vi.fn().mockResolvedValue([]);
    const { dialogOpen } = configure(
      undefined,
      getWorkItems,
      { id: 1, role: 'Developer' },
      undefined,
      undefined,
      undefined,
      getWorkItemsTree
    );
    const fixture = await render();

    (fixture.nativeElement.querySelector('#work-item-1 .edit-link') as HTMLButtonElement).click();
    await vi.waitFor(() => expect(dialogOpen).toHaveBeenCalled());
    const { onSaved } = dialogOpen.mock.calls[0][1].data;
    getWorkItems.mockClear();
    getWorkItemsTree.mockClear();
    onSaved();
    await fixture.whenStable();

    expect(getWorkItems).toHaveBeenCalled();
    expect(getWorkItemsTree).toHaveBeenCalled();
  });
});

describe('ProjectDetailComponent filter, search, and pagination', () => {
  it('applies a status filter and re-requests with it', async () => {
    const getWorkItems = vi
      .fn()
      .mockResolvedValueOnce(pageOf([sampleItem({ id: 1 })]))
      .mockResolvedValueOnce(pageOf([sampleItem({ id: 2, statusId: 4, statusName: 'Done', statusCategory: 'Done' })]));
    configure(undefined, getWorkItems);
    const fixture = await render();

    await chooseFilterOption(fixture, 'Status', 'Done');
    await fixture.whenStable();
    fixture.detectChanges();

    expect(getWorkItems).toHaveBeenLastCalledWith(1, expect.objectContaining({ statusId: 4, page: 1 }));
  });

  it('searches by title when the search button is clicked', async () => {
    const getWorkItems = vi
      .fn()
      .mockResolvedValueOnce(pageOf([sampleItem()]))
      .mockResolvedValueOnce(pageOf([sampleItem({ title: 'Fix the login bug' })]));
    configure(undefined, getWorkItems);
    const fixture = await render();

    const input = fixture.nativeElement.querySelector('#searchFilter') as HTMLInputElement;
    input.value = 'login';
    input.dispatchEvent(new Event('input'));
    (fixture.nativeElement.querySelector('.search-button') as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(getWorkItems).toHaveBeenLastCalledWith(1, expect.objectContaining({ search: 'login', page: 1 }));
  });

  it('shows "No work items yet" when the project genuinely has none and no filters are applied', async () => {
    configure(undefined, vi.fn().mockResolvedValue(emptyPage()));
    const fixture = await render();

    expect(fixture.nativeElement.textContent).toContain('No work items yet');
  });

  it('shows "No items match your filters." (not "No work items yet") when filters are applied and nothing matches', async () => {
    const getWorkItems = vi
      .fn()
      .mockResolvedValueOnce(pageOf([sampleItem()]))
      .mockResolvedValueOnce(emptyPage());
    configure(undefined, getWorkItems);
    const fixture = await render();

    await chooseFilterOption(fixture, 'Status', 'Done');
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No items match your filters.');
    expect(fixture.nativeElement.textContent).not.toContain('No work items yet');
  });

  it('pages forward and backward, disabling the buttons at the boundaries', async () => {
    const getWorkItems = vi.fn().mockResolvedValue({ items: [sampleItem()], page: 1, pageSize: 20, totalCount: 25 });
    configure(undefined, getWorkItems);
    const fixture = await render();

    const prevButton = fixture.nativeElement.querySelector('.prev-page') as HTMLButtonElement;
    const nextButton = fixture.nativeElement.querySelector('.next-page') as HTMLButtonElement;
    expect(prevButton.disabled).toBe(true);
    expect(nextButton.disabled).toBe(false);

    nextButton.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(getWorkItems).toHaveBeenLastCalledWith(1, expect.objectContaining({ page: 2 }));
  });
});

describe('ProjectDetailComponent label filter (US5)', () => {
  it('selects a label and includes it in the list query, combinable with existing filters', async () => {
    const getWorkItems = vi
      .fn()
      .mockResolvedValueOnce(pageOf([sampleItem({ id: 1 })]))
      .mockResolvedValueOnce(pageOf([sampleItem({ id: 2 })]))
      .mockResolvedValueOnce(pageOf([sampleItem({ id: 2 })]));
    configure(
      undefined,
      getWorkItems,
      undefined,
      undefined,
      undefined,
      undefined,
      undefined,
      undefined,
      'flat',
      undefined,
      vi.fn().mockResolvedValue(['backend', 'urgent'])
    );
    const fixture = await render();

    await chooseFilterOption(fixture, 'Label', 'backend');
    await fixture.whenStable();
    fixture.detectChanges();

    expect(getWorkItems).toHaveBeenLastCalledWith(1, expect.objectContaining({ label: 'backend', page: 1 }));

    await chooseFilterOption(fixture, 'Status', 'Done');
    await fixture.whenStable();
    fixture.detectChanges();

    expect(getWorkItems).toHaveBeenLastCalledWith(1, expect.objectContaining({ label: 'backend', statusId: 4 }));
  });
});

const treeData = [
  {
    id: 1,
    type: 'Epic',
    title: 'Epic One',
    statusId: 1,
    statusName: 'To Do',
    statusCategory: 'Open',
    statusColorKey: 'Slate',
    priority: 'Medium',
    assigneeName: null,
    directChildrenCount: 1,
    directChildrenDoneCount: 0,
    children: [
      {
        id: 2,
        type: 'Story',
        title: 'Story One',
        statusId: 2,
        statusName: 'In Progress',
        statusCategory: 'Open',
        statusColorKey: 'Blue',
        priority: 'High',
        assigneeName: null,
        directChildrenCount: 0,
        directChildrenDoneCount: 0,
        children: [],
      },
    ],
  },
  {
    id: 3,
    type: 'Task',
    title: 'Standalone Task',
    statusId: 1,
    statusName: 'To Do',
    statusCategory: 'Open',
    statusColorKey: 'Slate',
    priority: 'Low',
    assigneeName: null,
    directChildrenCount: 0,
    directChildrenDoneCount: 0,
    children: [],
  },
];

describe('ProjectDetailComponent tree view', () => {
  it('switches to Tree view and back to List via the toggles', async () => {
    configure(undefined, vi.fn().mockResolvedValue(pageOf([sampleItem()])), undefined, undefined, undefined, undefined, vi.fn().mockResolvedValue(treeData));
    const fixture = await render();

    (fixture.nativeElement.querySelector('.tree-view-toggle') as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.tree-view')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('table[mat-table]')).toBeNull();

    (fixture.nativeElement.querySelector('.flat-view-toggle') as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('table[mat-table]')).toBeTruthy();
  });

  it('renders children indented beneath their parent, and standalone items at the top level', async () => {
    configure(undefined, undefined, undefined, undefined, undefined, undefined, vi.fn().mockResolvedValue(treeData));
    const fixture = await render();
    (fixture.nativeElement.querySelector('.tree-view-toggle') as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Epic One');
    expect(text).toContain('Story One');
    expect(text).toContain('Standalone Task');
    const parentRow = fixture.nativeElement.querySelector('#tree-work-item-1') as HTMLElement;
    const childRow = fixture.nativeElement.querySelector('#tree-work-item-2') as HTMLElement;
    expect(Number(childRow.dataset['level'])).toBeGreaterThan(Number(parentRow.dataset['level']));
  });

  it('renders the tree inside a card, with status/priority chips per row (FR-014)', async () => {
    configure(undefined, undefined, undefined, undefined, undefined, undefined, vi.fn().mockResolvedValue(treeData));
    const fixture = await render();
    (fixture.nativeElement.querySelector('.tree-view-toggle') as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.tree-view-card')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#tree-work-item-1 app-status-chip')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#tree-work-item-1 app-priority-chip')).toBeTruthy();
  });

  it("links a tree row's title to its detail page", async () => {
    configure(undefined, undefined, undefined, undefined, undefined, undefined, vi.fn().mockResolvedValue(treeData));
    const fixture = await render();
    (fixture.nativeElement.querySelector('.tree-view-toggle') as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();

    const link = fixture.nativeElement.querySelector('#tree-work-item-1 .tree-item-title') as HTMLAnchorElement;
    expect(link.getAttribute('href')).toBe('/projects/1/work-items/1');
  });

  it("shows a parent row's direct-children done count", async () => {
    configure(undefined, undefined, undefined, undefined, undefined, undefined, vi.fn().mockResolvedValue(treeData));
    const fixture = await render();
    (fixture.nativeElement.querySelector('.tree-view-toggle') as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect((fixture.nativeElement.querySelector('#tree-work-item-1') as HTMLElement).textContent).toContain('0/1 done');
  });

  it("collapses and re-expands a parent's children", async () => {
    configure(undefined, undefined, undefined, undefined, undefined, undefined, vi.fn().mockResolvedValue(treeData));
    const fixture = await render();
    (fixture.nativeElement.querySelector('.tree-view-toggle') as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();

    const toggle = fixture.nativeElement.querySelector('#tree-work-item-1 .expand-toggle') as HTMLButtonElement;
    toggle.click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#tree-work-item-2')).toBeNull();

    toggle.click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#tree-work-item-2')).toBeTruthy();
  });
});

describe('ProjectDetailComponent board view', () => {
  it('shows the board when the Board view toggle is clicked', async () => {
    configure();
    const fixture = await render();

    (fixture.nativeElement.querySelector('.board-view-toggle') as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('app-board')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('table[mat-table]')).toBeNull();
  });

  // Feature 009 US1: Summary is now the default view — a project opened without
  // an explicit `view` query param (e.g. a link that predates this change, or one
  // typed by hand) should land on Summary, not Board (FR-002).
  it('defaults to the Summary view when no `view` query param is present', async () => {
    const notificationService = { success: vi.fn(), error: vi.fn() };
    TestBed.configureTestingModule({
      imports: [ProjectDetailComponent],
      providers: [
        provideRouter([]),
        { provide: ProjectsService, useValue: { getProject: vi.fn().mockResolvedValue(sampleProject) } },
        {
          provide: WorkItemsService,
          useValue: {
            getWorkItems: vi.fn().mockResolvedValue(emptyPage()),
            getAssignableUsers: vi.fn().mockResolvedValue([]),
            getWorkItemsTree: vi.fn().mockResolvedValue([]),
            getStatuses: vi.fn().mockResolvedValue(sampleStatuses()),
            getProjectLabels: vi.fn().mockResolvedValue([]),
            getBoard: vi.fn().mockResolvedValue({ columns: [], items: [] }),
            getProjectSummary: vi.fn().mockResolvedValue({
              statCards: { total: 0, completed: 0, completedPercent: 0, inProgress: 0, dueSoon: 0 },
              statusBreakdown: [],
              priorityBreakdown: [],
              workload: [],
            }),
            getProjectActivity: vi.fn().mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 }),
          },
        },
        { provide: SprintsService, useValue: { getSprints: vi.fn().mockResolvedValue([]) } },
        { provide: AuthService, useValue: { currentUser: () => ({ id: 1, role: 'Developer' }), currentRole: () => 'Developer' } },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: '1' }), queryParamMap: convertToParamMap({}) } },
        },
        { provide: NotificationService, useValue: notificationService },
      ],
    });
    const fixture = await render();

    expect(fixture.nativeElement.querySelector('app-summary')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('.summary-view-toggle')?.classList).toContain('active');
    expect(fixture.nativeElement.querySelector('app-board')).toBeNull();
    expect(fixture.nativeElement.querySelector('table[mat-table]')).toBeNull();
    expect(fixture.nativeElement.querySelector('.tree-view')).toBeNull();
  });

  it('orders the view tabs Summary, Board, Backlog, List, Tree, with Summary first (FR-001)', async () => {
    configure();
    const fixture = await render();

    const tabs = Array.from(fixture.nativeElement.querySelectorAll('.view-tab-nav a')) as HTMLAnchorElement[];
    expect(tabs.map((t) => t.textContent?.trim())).toEqual(['Summary', 'Board', 'Backlog', 'List', 'Tree']);
  });

  it('labels the former "Flat" tab as "List"', async () => {
    configure();
    const fixture = await render();

    expect((fixture.nativeElement.querySelector('.flat-view-toggle') as HTMLElement).textContent?.trim()).toBe('List');
  });

  it('starts on the Board view when the `view` query param is `board` (FR-019/US5)', async () => {
    const notificationService = { success: vi.fn(), error: vi.fn() };
    TestBed.configureTestingModule({
      imports: [ProjectDetailComponent],
      providers: [
        provideRouter([]),
        { provide: ProjectsService, useValue: { getProject: vi.fn().mockResolvedValue(sampleProject) } },
        {
          provide: WorkItemsService,
          useValue: {
            getWorkItems: vi.fn().mockResolvedValue(emptyPage()),
            getAssignableUsers: vi.fn().mockResolvedValue([]),
            getWorkItemsTree: vi.fn().mockResolvedValue([]),
            getStatuses: vi.fn().mockResolvedValue(sampleStatuses()),
            getProjectLabels: vi.fn().mockResolvedValue([]),
            getBoard: vi.fn().mockResolvedValue({ columns: [], items: [] }),
          },
        },
        { provide: SprintsService, useValue: { getSprints: vi.fn().mockResolvedValue([]) } },
        { provide: AuthService, useValue: { currentUser: () => ({ id: 1, role: 'Developer' }), currentRole: () => 'Developer' } },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: convertToParamMap({ id: '1' }), queryParamMap: convertToParamMap({ view: 'board' }) },
          },
        },
        { provide: NotificationService, useValue: notificationService },
      ],
    });
    const fixture = await render();

    expect(fixture.nativeElement.querySelector('app-board')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('.board-view-toggle')?.classList).toContain('active');
  });
});
