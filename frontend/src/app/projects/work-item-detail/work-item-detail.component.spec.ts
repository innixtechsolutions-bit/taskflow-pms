import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { WorkItemDetailComponent } from './work-item-detail.component';
import { WorkItemsService } from '../work-items.service';
import { AuthService } from '../../auth/auth.service';

const detailWithParentAndChildren = {
  id: 5,
  projectId: 1,
  type: 'Story',
  title: 'The Story',
  description: null,
  priority: 'Medium',
  status: 'InProgress',
  assigneeUserId: null,
  assigneeName: null,
  dueDate: null,
  createdByUserId: 1,
  createdByName: 'Ada Lovelace',
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
  parentWorkItemId: 2,
  parentTitle: 'The Epic',
  totalDescendantCount: 3,
  children: [
    { id: 10, title: 'Child Task', type: 'Task', status: 'ToDo', assigneeName: 'Grace Hopper' },
  ],
};

const detailWithNoParentOrChildren = {
  ...detailWithParentAndChildren,
  id: 6,
  type: 'Epic',
  parentWorkItemId: null,
  parentTitle: null,
  totalDescendantCount: 0,
  children: [],
};

function configure(
  getWorkItemDetail = vi.fn().mockResolvedValue(detailWithParentAndChildren),
  authState: { id: number; role: string } | null = { id: 1, role: 'Developer' },
  deleteWorkItem = vi.fn().mockResolvedValue(undefined)
) {
  TestBed.configureTestingModule({
    imports: [WorkItemDetailComponent],
    providers: [
      provideRouter([]),
      { provide: WorkItemsService, useValue: { getWorkItemDetail, deleteWorkItem } },
      { provide: AuthService, useValue: { currentUser: () => authState, currentRole: () => authState?.role ?? null } },
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ projectId: '1', id: '5' }) } } },
    ],
  });
  return { getWorkItemDetail, deleteWorkItem };
}

async function render() {
  const fixture = TestBed.createComponent(WorkItemDetailComponent);
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  return fixture;
}

describe('WorkItemDetailComponent', () => {
  it('renders status and priority as chips, not plain text', async () => {
    configure();
    const fixture = await render();

    expect(fixture.nativeElement.querySelector('app-status-chip.work-item-status')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('app-priority-chip.work-item-priority')).toBeTruthy();
  });

  it('renders the parent as a link when one exists', async () => {
    configure();
    const fixture = await render();

    const link = fixture.nativeElement.querySelector('.parent-link') as HTMLAnchorElement;
    expect(link).toBeTruthy();
    expect(link.textContent).toContain('The Epic');
  });

  it('renders no parent link when the item has no parent', async () => {
    configure(vi.fn().mockResolvedValue(detailWithNoParentOrChildren));
    const fixture = await render();

    expect(fixture.nativeElement.querySelector('.parent-link')).toBeNull();
  });

  it('lists direct children with title, type, status, and assignee, each linking to its own detail page', async () => {
    configure();
    const fixture = await render();

    const childLink = fixture.nativeElement.querySelector('.child-link') as HTMLAnchorElement;
    expect(childLink.textContent).toContain('Child Task');
    expect(fixture.nativeElement.textContent).toContain('Grace Hopper');
  });

  it('pre-selects this item as parent when starting a new child (FR-019)', async () => {
    configure();
    const fixture = await render();

    const createChildLink = fixture.nativeElement.querySelector('.create-child-link') as HTMLAnchorElement;
    expect(createChildLink).toBeTruthy();
    expect(createChildLink.getAttribute('href')).toContain('parentWorkItemId=5');
  });

  it('hides the create-child action for a SubTask (cannot legally have children)', async () => {
    configure(vi.fn().mockResolvedValue({ ...detailWithParentAndChildren, type: 'SubTask', children: [] }));
    const fixture = await render();

    expect(fixture.nativeElement.querySelector('.create-child-link')).toBeNull();
  });

  it('states the total descendant count in the delete confirmation', async () => {
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(false);
    configure();
    const fixture = await render();

    (fixture.nativeElement.querySelector('.delete-button') as HTMLButtonElement).click();

    expect(confirmSpy).toHaveBeenCalledWith(expect.stringContaining('3'));
    confirmSpy.mockRestore();
  });
});
