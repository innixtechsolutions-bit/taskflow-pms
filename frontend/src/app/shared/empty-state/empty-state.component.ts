import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/**
 * Friendly icon + message + optional primary action, replacing bare text
 * like "No work items yet" (FR-013).
 */
@Component({
  selector: 'app-empty-state',
  standalone: true,
  imports: [MatIconModule],
  templateUrl: './empty-state.component.html',
  styleUrl: './empty-state.component.css',
})
export class EmptyStateComponent {
  readonly icon = input.required<string>();
  readonly message = input.required<string>();
}
