import { TestBed } from '@angular/core/testing';
import { StatusChipComponent } from './status-chip.component';
import { WorkItemStatus } from '../../projects/work-items.service';

function render(status: WorkItemStatus) {
  const fixture = TestBed.createComponent(StatusChipComponent);
  fixture.componentRef.setInput('status', status);
  fixture.detectChanges();
  return fixture;
}

describe('StatusChipComponent', () => {
  it('renders the correct label for each status value', () => {
    expect(render('ToDo').nativeElement.textContent).toContain('To Do');
    expect(render('InProgress').nativeElement.textContent).toContain('In Progress');
    expect(render('Done').nativeElement.textContent).toContain('Done');
  });

  it('applies a distinct color class per status value', () => {
    const classes = (['ToDo', 'InProgress', 'Done'] as const).map((status) => {
      const chip = render(status).nativeElement.querySelector('.chip');
      return Array.from(chip.classList as DOMTokenList).find((c) => c.startsWith('chip--'));
    });

    expect(new Set(classes).size).toBe(3);
    expect(classes.every(Boolean)).toBe(true);
  });

  it('renders the same color class for the same status value every time', () => {
    const first = render('InProgress').nativeElement.querySelector('.chip').className;
    const second = render('InProgress').nativeElement.querySelector('.chip').className;
    expect(first).toBe(second);
  });
});
