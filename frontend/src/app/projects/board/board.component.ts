import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { WorkItemBoard, WorkItemBoardCard, WorkItemsService } from '../work-items.service';
import { EmptyStateComponent } from '../../shared/empty-state/empty-state.component';
import { BoardCardComponent } from './board-card.component';

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
  imports: [EmptyStateComponent, BoardCardComponent],
  templateUrl: './board.component.html',
  styleUrl: './board.component.css',
})
export class BoardComponent implements OnInit {
  private readonly workItemsService = inject(WorkItemsService);

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
}
