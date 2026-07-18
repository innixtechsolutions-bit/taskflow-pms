import { TestBed } from '@angular/core/testing';
import { UserAvatarComponent } from './user-avatar.component';

function render(fullName: string, showName = false) {
  const fixture = TestBed.createComponent(UserAvatarComponent);
  fixture.componentRef.setInput('fullName', fullName);
  fixture.componentRef.setInput('showName', showName);
  fixture.detectChanges();
  return fixture;
}

describe('UserAvatarComponent', () => {
  it('renders initials from the first two words of the full name', () => {
    const fixture = render('Uma Kannan');
    expect(fixture.nativeElement.querySelector('.avatar-badge').textContent.trim()).toBe('UK');
  });

  it('renders initials from a single-word name using just that letter', () => {
    const fixture = render('Cher');
    expect(fixture.nativeElement.querySelector('.avatar-badge').textContent.trim()).toBe('C');
  });

  it('renders the same background color for the same full name every time', () => {
    const first = render('Ada Lovelace').nativeElement.querySelector('.avatar-badge').style.backgroundColor;
    const second = render('Ada Lovelace').nativeElement.querySelector('.avatar-badge').style.backgroundColor;
    expect(first).toBe(second);
    expect(first).not.toBe('');
  });

  it('renders (likely) different colors for different names', () => {
    const a = render('Ada Lovelace').nativeElement.querySelector('.avatar-badge').style.backgroundColor;
    const b = render('Grace Hopper').nativeElement.querySelector('.avatar-badge').style.backgroundColor;
    expect(a).not.toBe(b);
  });

  it('shows the name text only when showName is true', () => {
    expect(render('Ada Lovelace', false).nativeElement.textContent).not.toContain('Ada Lovelace');
    expect(render('Ada Lovelace', true).nativeElement.textContent).toContain('Ada Lovelace');
  });
});
