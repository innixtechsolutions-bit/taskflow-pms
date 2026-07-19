import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CdkDragDrop, DragDropModule } from '@angular/cdk/drag-drop';
import { WorkItemBoard, WorkItemBoardCard, WorkItemsService } from '../work-items.service';
import { EmptyStateComponent } from '../../shared/empty-state/empty-state.component';
import { BoardCardComponent } from './board-card.component';
import { AuthService } from '../../auth/auth.service';
import { NotificationService } from '../../shared/notification.service';
import { canEditWorkItem } from '../work-item-permissions';

interface BoardColumnView {
  status: WorkItemBoardCard['status'];
  label: string;
  items: WorkItemBoardCard[];
}

/**
 * The Kanban board: columns rendered purely from the backend's ordered
 * column list (M1 — no client-side status->label lookup anywhere in this
 * component), each with a header (label + count), its cards, and a
 * per-column empty state (FR-005-FR-008, FR-021).
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
  private readonly authService = inject(AuthService);
  private readonly notificationService = inject(NotificationService);

  readonly projectId = input.required<number>();

  private readonly board = signal<WorkItemBoard | null>(null);

  protected readonly columns = computed<BoardColumnView[]>(() => {
    const board = this.board();
    if (!board) {
      return [];
    }
    return board.columns.map((column) => ({
      status: column.status,
      label: column.label,
      items: board.items.filter((item) => item.status === column.status),
    }));
  });

  ngOnInit(): void {
    void this.load();
  }

  private async load(): Promise<void> {
    this.board.set(await this.workItemsService.getBoard(this.projectId()));
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
  protected onDrop(event: CdkDragDrop<WorkItemBoardCard[]>, targetStatus: WorkItemBoardCard['status']): void {
    const item = event.item.data as WorkItemBoardCard;
    const previousBoard = this.board();
    if (!previousBoard || item.status === targetStatus) {
      return;
    }

    const optimisticBoard: WorkItemBoard = {
      ...previousBoard,
      items: previousBoard.items.map((i) => (i.id === item.id ? { ...i, status: targetStatus } : i)),
    };
    this.board.set(optimisticBoard);

    this.workItemsService.updateWorkItemStatus(item.id, targetStatus).catch(() => {
      this.board.set(previousBoard);
      this.notificationService.error(`Could not move "${item.title}". Please try again.`);
    });
  }
}
