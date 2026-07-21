import { Component, input } from '@angular/core';

@Component({
  selector: 'app-label-chip',
  standalone: true,
  templateUrl: './label-chip.component.html',
  styleUrl: '../chip.css',
})
export class LabelChipComponent {
  readonly name = input.required<string>();
}
