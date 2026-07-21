import { TestBed } from '@angular/core/testing';
import { LabelChipComponent } from './label-chip.component';

function render(name: string) {
  const fixture = TestBed.createComponent(LabelChipComponent);
  fixture.componentRef.setInput('name', name);
  fixture.detectChanges();
  return fixture;
}

describe('LabelChipComponent', () => {
  it("renders the label's name", () => {
    expect(render('backend').nativeElement.textContent).toContain('backend');
  });

  it('applies the shared neutral .chip--label class, not a ColorKey-keyed one', () => {
    const chip = render('backend').nativeElement.querySelector('.chip');
    expect(chip.classList).toContain('chip--label');
  });
});
