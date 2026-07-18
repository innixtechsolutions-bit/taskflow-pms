import { TestBed } from '@angular/core/testing';
import { PriorityChipComponent } from './priority-chip.component';
import { WorkItemPriority } from '../../projects/work-items.service';

function render(priority: WorkItemPriority) {
  const fixture = TestBed.createComponent(PriorityChipComponent);
  fixture.componentRef.setInput('priority', priority);
  fixture.detectChanges();
  return fixture;
}

describe('PriorityChipComponent', () => {
  it('renders the correct label for each priority value', () => {
    expect(render('Low').nativeElement.textContent).toContain('Low');
    expect(render('Medium').nativeElement.textContent).toContain('Medium');
    expect(render('High').nativeElement.textContent).toContain('High');
    expect(render('Critical').nativeElement.textContent).toContain('Critical');
  });

  it('applies a distinct color class per priority value', () => {
    const classes = (['Low', 'Medium', 'High', 'Critical'] as const).map((priority) => {
      const chip = render(priority).nativeElement.querySelector('.chip');
      return Array.from(chip.classList as DOMTokenList).find((c) => c.startsWith('chip--'));
    });

    expect(new Set(classes).size).toBe(4);
    expect(classes.every(Boolean)).toBe(true);
  });
});
