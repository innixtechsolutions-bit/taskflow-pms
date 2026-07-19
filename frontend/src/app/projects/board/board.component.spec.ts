import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { BoardComponent } from './board.component';
import { WorkItemBoard, WorkItemsService } from '../work-items.service';
import { AuthService } from '../../auth/auth.service';
import { NotificationService } from '../../shared/notification.service';

function sampleBoard(): WorkItemBoard {
  return {
    columns: [
      { status: 'ToDo', label: 'To Do' },
      { status: 'InProgress', label: 'In Progress' },
      { status: 'InReview', label: 'In Review' },
      { status: 'Done', label: 'Done' },
    ],
    items: [
      {
        id: 1,
        type: 'Task',
        title: 'A todo item',
        status: 'ToDo',
        priority: 'Medium',
        assigneeUserId: null,
        assigneeName: null,
        dueDate: null,
        updatedAt: '2026-07-18T00:00:00Z',
        createdByUserId: 1,
        directChildrenCount: 0,
        directChildrenDoneCount: 0,
      },
      {
        id: 2,
        type: 'Task',
        title: 'An in-progress item',
        status: 'InProgress',
        priority: 'High',
        assigneeUserId: 1,
        assigneeName: 'Ada Lovelace',
        dueDate: null,
        updatedAt: '2026-07-18T00:00:00Z',
        createdByUserId: 1,
        directChildrenCount: 0,
        directChildrenDoneCount: 0,
      },
    ],
  };
}

function configure(getBoard = vi.fn().mockResolvedValue(sampleBoard()), authState = { id: 1, role: 'Developer' as const }) {
  TestBed.configureTestingModule({
    imports: [BoardComponent],
    providers: [
      provideRouter([]),
      { provide: WorkItemsService, useValue: { getBoard, updateWorkItemStatus: vi.fn() } },
      { provide: AuthService, useValue: { currentUser: () => authState, currentRole: () => authState?.role ?? null } },
      { provide: NotificationService, useValue: { success: vi.fn(), error: vi.fn() } },
    ],
  });
  return { getBoard };
}

async function render(projectId = 1) {
  const fixture = TestBed.createComponent(BoardComponent);
  fixture.componentRef.setInput('projectId', projectId);
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  return fixture;
}

describe('BoardComponent', () => {
  it('renders 4 columns from the backend-supplied column list, using each label verbatim', async () => {
    configure();
    const fixture = await render();

    const headers = fixture.nativeElement.querySelectorAll('.board-column-header');
    expect(headers.length).toBe(4);
    expect(headers[0].textContent).toContain('To Do');
    expect(headers[2].textContent).toContain('In Review');
  });

  it('shows each column header with an accurate item count', async () => {
    configure();
    const fixture = await render();

    const columns = fixture.nativeElement.querySelectorAll('.board-column');
    const todoColumn = Array.from(columns).find((c) => (c as HTMLElement).textContent?.includes('To Do')) as HTMLElement;
    expect(todoColumn.querySelector('.board-column-count')?.textContent).toContain('1');
  });

  it('groups cards into the correct column', async () => {
    configure();
    const fixture = await render();

    const cards = fixture.nativeElement.querySelectorAll('app-board-card');
    expect(cards.length).toBe(2);
  });

  it('shows a per-column empty state when a column has no items', async () => {
    configure(vi.fn().mockResolvedValue({ columns: sampleBoard().columns, items: [] }));
    const fixture = await render();

    expect(fixture.nativeElement.querySelectorAll('app-empty-state').length).toBe(4);
  });

  it('fetches the board for the given projectId', async () => {
    const { getBoard } = configure();
    await render(42);

    expect(getBoard).toHaveBeenCalledWith(42);
  });
});
