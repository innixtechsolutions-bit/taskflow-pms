import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { ProjectDetailComponent } from './project-detail.component';
import { ProjectsService } from '../projects.service';
import { WorkItem, WorkItemsService } from '../work-items.service';
import { AuthService } from '../../auth/auth.service';

const sampleProject = {
  id: 1,
  name: 'Website Redesign',
  description: 'Rebuild the marketing site',
  createdByName: 'Ada Lovelace',
  createdAt: '2026-01-01T00:00:00Z',
  totalWorkItemCount: 0,
};

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
    ...overrides,
  };
}

function configure(
  getProject = vi.fn().mockResolvedValue(sampleProject),
  getWorkItems = vi.fn().mockResolvedValue(emptyPage()),
  authState: { id: number; role: string } | null = { id: 1, role: 'Developer' },
  deleteWorkItem = vi.fn().mockResolvedValue(undefined)
) {
  TestBed.configureTestingModule({
    imports: [ProjectDetailComponent],
    providers: [
      provideRouter([]),
      { provide: ProjectsService, useValue: { getProject } },
      { provide: WorkItemsService, useValue: { getWorkItems, deleteWorkItem } },
      { provide: AuthService, useValue: { currentUser: () => authState, currentRole: () => authState?.role ?? null } },
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '1' }) } } },
    ],
  });
  return { getProject, getWorkItems, deleteWorkItem };
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

  it('calls deleteWorkItem and refreshes the list after a confirmed delete', async () => {
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
    const getWorkItems = vi
      .fn()
      .mockResolvedValueOnce(pageOf([sampleItem({ id: 1, createdByUserId: 1 })]))
      .mockResolvedValueOnce(emptyPage());
    const { deleteWorkItem } = configure(undefined, getWorkItems, { id: 1, role: 'Developer' });
    const fixture = await render();

    (deleteButton(fixture, 1) as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(deleteWorkItem).toHaveBeenCalledWith(1);
    expect(getWorkItems).toHaveBeenCalledTimes(2);
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
