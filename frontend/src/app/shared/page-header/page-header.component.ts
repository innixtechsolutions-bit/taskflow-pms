import { Component, input } from '@angular/core';

/**
 * Shared page header: title, optional subtitle, and an optional
 * right-aligned primary action projected via the `page-header-actions`
 * attribute selector. Used by every retrofitted authenticated page
 * (FR-004) so title/subtitle/action styling lives in one place.
 */
@Component({
  selector: 'app-page-header',
  standalone: true,
  templateUrl: './page-header.component.html',
  styleUrl: './page-header.component.css',
})
export class PageHeaderComponent {
  readonly title = input.required<string>();
  readonly subtitle = input<string>();
}
