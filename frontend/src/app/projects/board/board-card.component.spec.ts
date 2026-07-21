import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { BoardCardComponent } from './board-card.component';
import { WorkItemBoardCard } from '../work-items.service';

function baseCard(overrides: Partial<WorkItemBoardCard> = {}): WorkItemBoardCard {
  return {
    id: 1,
    type: 'Task',
    title: 'Fix the login bug',
    statusId: 1,
    statusName: 'To Do',
    statusCategory: 'Open',
    statusColorKey: 'Slate',
    priority: 'High',
    assigneeUserId: null,
    assigneeName: null,
    dueDate: null,
    updatedAt: '2026-07-18T00:00:00Z',
    createdByUserId: 10,
    directChildrenCount: 0,
    directChildrenDoneCount: 0,
    labels: [],
    ...overrides,
  };
}

function render(card: WorkItemBoardCard, projectId = 1) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [provideRouter([])] });
  const fixture = TestBed.createComponent(BoardCardComponent);
  fixture.componentRef.setInput('card', card);
  fixture.componentRef.setInput('projectId', projectId);
  fixture.detectChanges();
  return fixture;
}

describe('BoardCardComponent', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date(2026, 6, 18));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('renders title, type, and priority chip', () => {
    const fixture = render(baseCard({ title: 'Fix the login bug', type: 'Task', priority: 'Critical' }));

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Fix the login bug');
    expect(text).toContain('Task');
    expect(fixture.nativeElement.querySelector('app-priority-chip')).toBeTruthy();
  });

  it('renders an assignee avatar when assigned', () => {
    const fixture = render(baseCard({ assigneeUserId: 3, assigneeName: 'Grace Hopper' }));

    expect(fixture.nativeElement.querySelector('app-user-avatar')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('.unassigned-indicator')).toBeNull();
  });

  it('renders an "Unassigned" placeholder avatar when there is no assignee', () => {
    const fixture = render(baseCard({ assigneeUserId: null, assigneeName: null }));

    expect(fixture.nativeElement.querySelector('app-user-avatar')).toBeNull();
    const indicator = fixture.nativeElement.querySelector('.unassigned-indicator');
    expect(indicator).toBeTruthy();
    expect(indicator.getAttribute('title')).toBe('Unassigned');
  });

  it('renders the due date in friendly format, never raw ISO', () => {
    const fixture = render(baseCard({ dueDate: '2026-07-20T00:00:00Z' }));

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Jul 20, 2026');
    expect(text).not.toMatch(/\d{4}-\d{2}-\d{2}T/);
  });

  it('flags an overdue due date visually', () => {
    const overdue = render(baseCard({ dueDate: '2026-07-01T00:00:00Z', statusCategory: 'Open' }));
    const notOverdue = render(baseCard({ dueDate: '2026-07-01T00:00:00Z', statusCategory: 'Done' }));

    expect(overdue.nativeElement.querySelector('.due-date-overdue')).toBeTruthy();
    expect(notOverdue.nativeElement.querySelector('.due-date-overdue')).toBeNull();
  });

  it('shows no due date element when there is none', () => {
    const fixture = render(baseCard({ dueDate: null }));

    expect(fixture.nativeElement.querySelector('.card-due-date')).toBeNull();
  });

  it('shows "n/m done" only when the item has children', () => {
    const withChildren = render(baseCard({ directChildrenCount: 3, directChildrenDoneCount: 1 }));
    const noChildren = render(baseCard({ directChildrenCount: 0, directChildrenDoneCount: 0 }));

    expect(withChildren.nativeElement.textContent).toContain('1/3 done');
    expect(noChildren.nativeElement.querySelector('.card-progress')).toBeNull();
  });

  it('links to the correct work item detail route (US5)', () => {
    const fixture = render(baseCard({ id: 42 }), 7);

    const link = fixture.nativeElement.querySelector('.card-link') as HTMLAnchorElement;
    expect(link.getAttribute('href')).toBe('/projects/7/work-items/42');
  });
});
