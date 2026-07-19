import { TestBed } from '@angular/core/testing';
import { StatusChipComponent } from './status-chip.component';
import { ChipColor } from '../../projects/work-items.service';

function render(name: string, colorKey: ChipColor) {
  const fixture = TestBed.createComponent(StatusChipComponent);
  fixture.componentRef.setInput('name', name);
  fixture.componentRef.setInput('colorKey', colorKey);
  fixture.detectChanges();
  return fixture;
}

describe('StatusChipComponent', () => {
  it('renders the given name as its label, not a fixed lookup', () => {
    expect(render('To Do', 'Slate').nativeElement.textContent).toContain('To Do');
    expect(render('QA', 'Amber').nativeElement.textContent).toContain('QA');
    expect(render('Doing', 'Blue').nativeElement.textContent).toContain('Doing');
  });

  it('applies a distinct color class per colorKey value', () => {
    const colors: ChipColor[] = ['Slate', 'Blue', 'Violet', 'Amber', 'Teal', 'Rose', 'Indigo', 'Cyan', 'Green', 'Emerald'];
    const classes = colors.map((colorKey) => {
      const chip = render('Some Status', colorKey).nativeElement.querySelector('.chip');
      return Array.from(chip.classList as DOMTokenList).find((c) => c.startsWith('chip--'));
    });

    expect(new Set(classes).size).toBe(colors.length);
    expect(classes.every(Boolean)).toBe(true);
  });

  it('renders the same color class for the same colorKey every time, regardless of name', () => {
    const first = render('In Progress', 'Blue').nativeElement.querySelector('.chip').className;
    const second = render('Doing', 'Blue').nativeElement.querySelector('.chip').className;
    expect(first).toBe(second);
  });
});
