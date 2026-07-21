import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { BacklogItemRowComponent } from './backlog-item-row.component';
import { WorkItem } from '../work-items.service';

function baseItem(overrides: Partial<WorkItem> = {}): WorkItem {
  return {
    id: 1,
    projectId: 1,
    type: 'Task',
    title: 'Fix the login bug',
    description: null,
    priority: 'High',
    statusId: 1,
    statusName: 'To Do',
    statusCategory: 'Open',
    statusColorKey: 'Slate',
    assigneeUserId: null,
    assigneeName: null,
    dueDate: null,
    startDate: null,
    createdByUserId: 10,
    createdByName: 'Ada Lovelace',
    createdAt: '2026-07-18T00:00:00Z',
    updatedAt: '2026-07-18T00:00:00Z',
    parentWorkItemId: null,
    labels: [],
    sprintId: null,
    sprintName: null,
    ...overrides,
  };
}

function render(item: WorkItem, projectId = 1) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ imports: [BacklogItemRowComponent], providers: [provideRouter([])] });
  const fixture = TestBed.createComponent(BacklogItemRowComponent);
  fixture.componentRef.setInput('item', item);
  fixture.componentRef.setInput('projectId', projectId);
  fixture.detectChanges();
  return fixture;
}

describe('BacklogItemRowComponent', () => {
  it('renders the title, type, and status chip inline (FR-026)', () => {
    const fixture = render(baseItem({ title: 'Fix the login bug', type: 'Task', statusName: 'In Progress' }));

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Fix the login bug');
    expect(text).toContain('Task');
    expect(fixture.nativeElement.querySelector('app-status-chip')).toBeTruthy();
  });

  it('renders the due date inline in friendly format when set', () => {
    const fixture = render(baseItem({ dueDate: '2026-07-20T00:00:00Z' }));

    expect(fixture.nativeElement.textContent).toContain('Jul 20, 2026');
  });

  it('renders the assignee avatar inline when assigned, otherwise an unassigned indicator', () => {
    const assigned = render(baseItem({ assigneeUserId: 3, assigneeName: 'Grace Hopper' }));
    const unassigned = render(baseItem({ assigneeUserId: null, assigneeName: null }));

    expect(assigned.nativeElement.querySelector('app-user-avatar')).toBeTruthy();
    expect(unassigned.nativeElement.querySelector('.unassigned-indicator')).toBeTruthy();
  });

  it('links to the item detail route', () => {
    const fixture = render(baseItem({ id: 42 }), 7);

    const link = fixture.nativeElement.querySelector('.item-row-link') as HTMLAnchorElement;
    expect(link.getAttribute('href')).toBe('/projects/7/work-items/42');
  });
});
