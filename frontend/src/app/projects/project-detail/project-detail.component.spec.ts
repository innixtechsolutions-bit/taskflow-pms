import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { MatSelectHarness } from '@angular/material/select/testing';
import { vi } from 'vitest';
import { ProjectDetailComponent } from './project-detail.component';
import { ProjectsService } from '../projects.service';
import { WorkItem, WorkItemsService } from '../work-items.service';
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

function sampleItem(overrides: Partial<WorkItem> = {}): WorkItem {
  return {
    id: 1,
    projectId: 1,
    type: 'Task',
    title: 'Fix the login bug',
    description: null,
    priority: 'Medium',
    status: 'ToDo',
    assigneeUserId: null,
    assigneeName: null,
    dueDate: null,
    createdByUserId: 10,
    createdByName: 'Creator',
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    parentWorkItemId: null,
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
  getWorkItemDetail = vi.fn().mockResolvedValue({ totalDescendantCount: 0 })
) {
  const notificationService = { success: vi.fn(), error: vi.fn() };
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
          getBoard: vi.fn().mockResolvedValue({ columns: [], items: [] }),
        },
      },
      { provide: AuthService, useValue: { currentUser: () => authState, currentRole: () => authState?.role ?? null } },
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { paramMap: convertToParamMap({ id: '1' }), queryParamMap: convertToParamMap({}) } },
      },
      { provide: NotificationService, useValue: notificationService },
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
    configure(undefined, vi.fn().mockResolvedValue(pageOf([sampleItem({ status: 'InProgress', priority: 'High' })])));
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

describe('ProjectDetailComponent filter, search, and pagination', () => {
  it('applies a status filter and re-requests with it', async () => {
    const getWorkItems = vi
      .fn()
      .mockResolvedValueOnce(pageOf([sampleItem({ id: 1 })]))
      .mockResolvedValueOnce(pageOf([sampleItem({ id: 2, status: 'Done' })]));
    configure(undefined, getWorkItems);
    const fixture = await render();

    await chooseFilterOption(fixture, 'Status', 'Done');
    await fixture.whenStable();
    fixture.detectChanges();

    expect(getWorkItems).toHaveBeenLastCalledWith(1, expect.objectContaining({ status: 'Done', page: 1 }));
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

const treeData = [
  {
    id: 1,
    type: 'Epic',
    title: 'Epic One',
    status: 'ToDo',
    priority: 'Medium',
    assigneeName: null,
    directChildrenCount: 1,
    directChildrenDoneCount: 0,
    children: [
      {
        id: 2,
        type: 'Story',
        title: 'Story One',
        status: 'InProgress',
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
    status: 'ToDo',
    priority: 'Low',
    assigneeName: null,
    directChildrenCount: 0,
    directChildrenDoneCount: 0,
    children: [],
  },
];

describe('ProjectDetailComponent tree view', () => {
  it('defaults to the Flat view (Feature 002 behavior unchanged)', async () => {
    configure(undefined, vi.fn().mockResolvedValue(pageOf([sampleItem()])));
    const fixture = await render();

    expect(fixture.nativeElement.querySelector('table[mat-table]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('.tree-view')).toBeNull();
  });

  it('switches to Tree view and back via the toggle', async () => {
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
            getBoard: vi.fn().mockResolvedValue({ columns: [], items: [] }),
          },
        },
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
