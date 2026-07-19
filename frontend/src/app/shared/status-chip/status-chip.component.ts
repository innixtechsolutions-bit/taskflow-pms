import { Component, computed, input } from '@angular/core';
import { ChipColor } from '../../projects/work-items.service';

// Exhaustive switch, not a lookup object, so a new ChipColor member without a
// matching case is a compile error rather than an uncolored chip (Feature 006 —
// re-keyed from status name to ColorKey, since status names are no longer a closed
// set the compiler can reason about, but colors still are).
function classFor(colorKey: ChipColor): string {
  switch (colorKey) {
    case 'Slate':
      return 'chip--color-slate';
    case 'Blue':
      return 'chip--color-blue';
    case 'Violet':
      return 'chip--color-violet';
    case 'Amber':
      return 'chip--color-amber';
    case 'Teal':
      return 'chip--color-teal';
    case 'Rose':
      return 'chip--color-rose';
    case 'Indigo':
      return 'chip--color-indigo';
    case 'Cyan':
      return 'chip--color-cyan';
    case 'Green':
      return 'chip--color-green';
    case 'Emerald':
      return 'chip--color-emerald';
  }
}

@Component({
  selector: 'app-status-chip',
  standalone: true,
  templateUrl: './status-chip.component.html',
  styleUrl: '../chip.css',
})
export class StatusChipComponent {
  readonly name = input.required<string>();
  readonly colorKey = input.required<ChipColor>();

  protected readonly colorClass = computed(() => classFor(this.colorKey()));
}
