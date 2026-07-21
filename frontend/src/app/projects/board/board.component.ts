import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CdkDragDrop, DragDropModule } from '@angular/cdk/drag-drop';
import { MatDialog } from '@angular/material/dialog';
import { BoardColumn, WorkItemBoard, WorkItemBoardCard, WorkItemsService } from '../work-items.service';
import { Sprint, SprintsService } from '../sprints.service';
import { EmptyStateComponent } from '../../shared/empty-state/empty-state.component';
import { BoardCardComponent } from './board-card.component';
import { openWorkItemModal } from '../work-item-modal/open-work-item-modal';
import { AuthService } from '../../auth/auth.service';
import { NotificationService } from '../../shared/notification.service';
import { canEditWorkItem } from '../work-item-permissions';

interface BoardColumnView extends BoardColumn {
  items: WorkItemBoardCard[];
}

type BoardMode = 'all' | 'active';

/**
 * The Kanban board: columns rendered purely from the backend's ordered
 * column list (a project's own WorkflowStatus rows, Feature 006 — no
 * client-side status->name lookup anywhere in this component), each with a
 * header (name + count), its cards, and a per-column empty state
 * (FR-005-FR-008, FR-021). Feature 008 (US5) adds an "All items"/"Active
 * sprint" toggle — this component stays self-contained for it, fetching the
 * project's own sprints itself (research.md #6), the same way it already
 * owns its own board data and `refresh()`.
 */
@Component({
  selector: 'app-board',
  standalone: true,
  imports: [EmptyStateComponent, BoardCardComponent, DragDropModule, RouterLink],
  templateUrl: './board.component.html',
  styleUrl: './board.component.css',
})
export class BoardComponent implements OnInit {
  private readonly workItemsService = inject(WorkItemsService);
  private readonly sprintsService = inject(SprintsService);
  private readonly authService = inject(AuthService);
  private readonly notificationService = inject(NotificationService);
  private readonly dialog = inject(MatDialog);

  readonly projectId = input.required<number>();

  private readonly board = signal<WorkItemBoard | null>(null);
  protected readonly mode = signal<BoardMode>('all');
  private readonly sprints = signal<Sprint[]>([]);

  protected readonly activeSprint = computed<Sprint | undefined>(() => this.sprints().find((s) => s.status === 'Active'));

  protected readonly columns = computed<BoardColumnView[]>(() => {
    const board = this.board();
    if (!board) {
      return [];
    }
    return board.columns.map((column) => ({
      ...column,
      items: board.items.filter((item) => item.statusId === column.statusId),
    }));
  });

  ngOnInit(): void {
    void this.load();
    void this.loadSprints();
  }

  // Public so ProjectDetailComponent (this component's host when viewMode is
  // 'board') can refresh it too, after a create/edit made via its own
  // toolbar/empty-state modal entry points rather than a column's own "+".
  refresh(): void {
    void this.load();
  }

  private async loadSprints(): Promise<void> {
    this.sprints.set(await this.sprintsService.getSprints(this.projectId()));
  }

  protected setMode(mode: BoardMode): void {
    this.mode.set(mode);
    void this.load();
  }

  private async load(): Promise<void> {
    if (this.mode() === 'active') {
      const sprintId = this.activeSprint()?.id;
      if (sprintId === undefined) {
        // No Active sprint to scope to — the empty state covers this; skip
        // the request entirely rather than firing one with a nonsensical filter.
        this.board.set({ columns: [], items: [] });
        return;
      }
      this.board.set(await this.workItemsService.getBoard(this.projectId(), sprintId));
      return;
    }
    this.board.set(await this.workItemsService.getBoard(this.projectId()));
  }

  // Replaces the removed .../work-items/new?statusId= routerLink (US1) — opens
  // the modal with this column's status pre-selected and refreshes the board
  // once the modal reports a save, rather than navigating away and back
  // (research.md #9).
  protected openCreateModal(statusId: number): void {
    void openWorkItemModal(this.dialog, {
      mode: 'create',
      projectId: this.projectId(),
      statusId,
      onSaved: () => void this.load(),
    });
  }

  // Only the current user's own permission to edit that card is checked here
  // (research.md #3/#5's rule, via the shared canEditWorkItem) — the board
  // never re-derives status-transition rules of its own.
  protected canDrag(item: WorkItemBoardCard): boolean {
    const userId = this.authService.currentUser()?.id;
    if (userId === undefined) {
      return false;
    }
    return canEditWorkItem(item, userId, this.authService.currentRole());
  }

  // Optimistic: moves the card into the target column immediately (by
  // replacing the whole `items` array within the single `board` signal —
  // `columns` is a derived view, not an independently CDK-managed array, so
  // CDK's transferArrayItem/moveItemInArray helpers don't apply here), then
  // persists via PATCH; reverts to the original board state and shows an
  // error toast if the PATCH fails (M3).
  protected onDrop(event: CdkDragDrop<WorkItemBoardCard[]>, targetStatusId: number): void {
    const item = event.item.data as WorkItemBoardCard;
    const previousBoard = this.board();
    if (!previousBoard || item.statusId === targetStatusId) {
      return;
    }

    const targetColumn = previousBoard.columns.find((c) => c.statusId === targetStatusId)!;
    const optimisticBoard: WorkItemBoard = {
      ...previousBoard,
      items: previousBoard.items.map((i) =>
        i.id === item.id
          ? { ...i, statusId: targetColumn.statusId, statusName: targetColumn.name, statusCategory: targetColumn.category, statusColorKey: targetColumn.colorKey }
          : i
      ),
    };
    this.board.set(optimisticBoard);

    this.workItemsService.updateWorkItemStatus(item.id, targetStatusId).catch(() => {
      this.board.set(previousBoard);
      this.notificationService.error(`Could not move "${item.title}". Please try again.`);
    });
  }
}
