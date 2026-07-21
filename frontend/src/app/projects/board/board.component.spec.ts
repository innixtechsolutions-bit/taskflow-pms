import { TestBed } from '@angular/core/testing';
import { CdkDragDrop } from '@angular/cdk/drag-drop';
import { HttpErrorResponse } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { vi } from 'vitest';
import { BoardComponent } from './board.component';
import { WorkItemModalComponent } from '../work-item-modal/work-item-modal.component';
import { WorkItemBoard, WorkItemBoardCard, WorkItemsService } from '../work-items.service';
import { Sprint, SprintsService } from '../sprints.service';
import { AuthService } from '../../auth/auth.service';
import { NotificationService } from '../../shared/notification.service';

function dropEvent(item: WorkItemBoardCard): CdkDragDrop<WorkItemBoardCard[]> {
  return { item: { data: item } } as CdkDragDrop<WorkItemBoardCard[]>;
}

function sampleBoard(): WorkItemBoard {
  return {
    columns: [
      { statusId: 1, name: 'To Do', category: 'Open', colorKey: 'Slate' },
      { statusId: 2, name: 'In Progress', category: 'Open', colorKey: 'Blue' },
      { statusId: 3, name: 'In Review', category: 'Open', colorKey: 'Violet' },
      { statusId: 4, name: 'Done', category: 'Done', colorKey: 'Green' },
    ],
    items: [
      {
        id: 1,
        type: 'Task',
        title: 'A todo item',
        statusId: 1,
        statusName: 'To Do',
        statusCategory: 'Open',
        statusColorKey: 'Slate',
        priority: 'Medium',
        assigneeUserId: null,
        assigneeName: null,
        dueDate: null,
        updatedAt: '2026-07-18T00:00:00Z',
        createdByUserId: 1,
        directChildrenCount: 0,
        directChildrenDoneCount: 0,
        labels: [],
      },
      {
        id: 2,
        type: 'Task',
        title: 'An in-progress item',
        statusId: 2,
        statusName: 'In Progress',
        statusCategory: 'Open',
        statusColorKey: 'Blue',
        priority: 'High',
        assigneeUserId: 1,
        assigneeName: 'Ada Lovelace',
        dueDate: null,
        updatedAt: '2026-07-18T00:00:00Z',
        createdByUserId: 1,
        directChildrenCount: 0,
        directChildrenDoneCount: 0,
        labels: [],
      },
    ],
  };
}

function configure(
  getBoard = vi.fn().mockResolvedValue(sampleBoard()),
  authState = { id: 1, role: 'Developer' as const },
  updateWorkItemStatus = vi.fn().mockResolvedValue(undefined),
  getSprints = vi.fn().mockResolvedValue([] as Sprint[])
) {
  const notificationService = { success: vi.fn(), error: vi.fn() };
  const dialogOpen = vi.fn().mockReturnValue({});
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [BoardComponent],
    providers: [
      provideRouter([]),
      { provide: WorkItemsService, useValue: { getBoard, updateWorkItemStatus } },
      { provide: SprintsService, useValue: { getSprints } },
      { provide: AuthService, useValue: { currentUser: () => authState, currentRole: () => authState?.role ?? null } },
      { provide: NotificationService, useValue: notificationService },
      { provide: MatDialog, useValue: { open: dialogOpen } },
    ],
  });
  return { getBoard, updateWorkItemStatus, getSprints, notificationService, dialogOpen };
}

async function render(projectId = 1) {
  const fixture = TestBed.createComponent(BoardComponent);
  fixture.componentRef.setInput('projectId', projectId);
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  return fixture;
}

// Finds which column (by its header label) a card with the given title is
// currently rendered inside — a DOM-level way to assert "the card is back in
// its source column", not just "absent from the target".
function columnLabelContaining(fixture: { nativeElement: HTMLElement }, cardTitle: string): string | undefined {
  const columns = Array.from(fixture.nativeElement.querySelectorAll('.board-column')) as HTMLElement[];
  const column = columns.find((c) => c.textContent?.includes(cardTitle));
  return column?.querySelector('.board-column-label')?.textContent ?? undefined;
}

describe('BoardComponent', () => {
  it('renders 4 columns from the backend-supplied column list, using each name verbatim', async () => {
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

  it('moves a dragged card to the target column and persists the new status', async () => {
    const { updateWorkItemStatus } = configure();
    const fixture = await render();
    const item = sampleBoard().items[0]; // 'A todo item', statusId: 1

    (fixture.componentInstance as unknown as { onDrop: (e: unknown, s: number) => void }).onDrop(dropEvent(item), 3);
    fixture.detectChanges();

    expect(updateWorkItemStatus).toHaveBeenCalledWith(item.id, 3);
    expect(columnLabelContaining(fixture, item.title)).toBe('In Review');
  });

  // M3 (strengthened): asserts the card is found specifically back in its
  // original/source column (not merely "absent from the target"), for both a
  // generic failed PATCH and a 403-style rejection, and that the toast fires
  // in both cases.
  it.each([
    ['a generic failed PATCH', new Error('network error')],
    ['a 403 rejection', new HttpErrorResponse({ status: 403 })],
  ])('reverts the card to its source column and shows an error toast on %s', async (_label, rejection) => {
    const { notificationService } = configure(undefined, undefined, vi.fn().mockRejectedValue(rejection));
    const fixture = await render();
    const item = sampleBoard().items[0]; // 'A todo item', statusId: 1

    (fixture.componentInstance as unknown as { onDrop: (e: unknown, s: number) => void }).onDrop(dropEvent(item), 3);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(columnLabelContaining(fixture, item.title)).toBe('To Do');
    expect(notificationService.error).toHaveBeenCalled();
  });

  it('disables dragging for a card the current user cannot edit', async () => {
    const board: WorkItemBoard = {
      columns: sampleBoard().columns,
      items: [{ ...sampleBoard().items[0], createdByUserId: 99, assigneeUserId: 99 }],
    };
    configure(vi.fn().mockResolvedValue(board), { id: 1, role: 'Developer' });
    const fixture = await render();

    expect(fixture.nativeElement.querySelector('.board-card.drag-disabled')).toBeTruthy();
  });

  it('leaves dragging enabled for a card the current user can edit', async () => {
    configure();
    const fixture = await render();

    expect(fixture.nativeElement.querySelector('.board-card.drag-disabled')).toBeNull();
  });

  it("each column's + affordance opens the work item modal with the project id and that column's statusId pre-selected", async () => {
    const { dialogOpen } = configure();
    const fixture = await render(42);

    const buttons = Array.from(
      fixture.nativeElement.querySelectorAll('.board-column-add')
    ) as HTMLButtonElement[];
    expect(buttons.length).toBe(4);
    // Third column is In Review (statusId 3) per sampleBoard()'s column order.
    buttons[2].click();
    // The modal is dynamically imported (fix: restore production build) — its
    // own chunk resolves asynchronously, not synchronously with the click, so
    // poll rather than assume a fixed number of microtask turns.
    await vi.waitFor(() => expect(dialogOpen).toHaveBeenCalled());

    expect(dialogOpen).toHaveBeenCalledWith(
      WorkItemModalComponent,
      expect.objectContaining({
        data: expect.objectContaining({ mode: 'create', projectId: 42, statusId: 3 }),
      })
    );
  });

  it('refreshes the board once the modal reports a save (research.md #9)', async () => {
    const { getBoard, dialogOpen } = configure();
    const fixture = await render(42);

    const buttons = Array.from(fixture.nativeElement.querySelectorAll('.board-column-add')) as HTMLButtonElement[];
    buttons[0].click();
    await vi.waitFor(() => expect(dialogOpen).toHaveBeenCalled());

    const { onSaved } = dialogOpen.mock.calls[0][1].data;
    getBoard.mockClear();
    onSaved();
    await fixture.whenStable();

    expect(getBoard).toHaveBeenCalled();
  });

  // US5 — sprint-scoped Board

  function sampleSprints(): Sprint[] {
    return [
      { id: 1, projectId: 1, name: 'Sprint 1', startDate: '2026-08-01', endDate: '2026-08-15', status: 'Completed', itemCount: 0 },
      { id: 2, projectId: 1, name: 'Sprint 2', startDate: '2026-08-16', endDate: '2026-08-30', status: 'Active', itemCount: 3 },
    ];
  }

  it('"All items" mode is unaffected: calls getBoard with no sprintId', async () => {
    const { getBoard } = configure(undefined, undefined, undefined, vi.fn().mockResolvedValue(sampleSprints()));
    await render(1);

    expect(getBoard).toHaveBeenCalledWith(1);
  });

  it('"Active sprint" mode calls getBoard with the resolved Active sprint\'s id', async () => {
    const { getBoard } = configure(undefined, undefined, undefined, vi.fn().mockResolvedValue(sampleSprints()));
    const fixture = await render(1);
    getBoard.mockClear();

    (fixture.nativeElement.querySelector('.active-sprint-toggle') as HTMLButtonElement).click();
    await fixture.whenStable();

    expect(getBoard).toHaveBeenCalledWith(1, 2);
  });

  it('shows an empty state with a Backlog link when no sprint is Active', async () => {
    const noActive = sampleSprints().map((s) => ({ ...s, status: 'Completed' as const }));
    configure(undefined, undefined, undefined, vi.fn().mockResolvedValue(noActive));
    const fixture = await render(1);

    (fixture.nativeElement.querySelector('.active-sprint-toggle') as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('app-empty-state')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('.no-active-sprint-backlog-link')).toBeTruthy();
  });
});
