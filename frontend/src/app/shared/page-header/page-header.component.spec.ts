import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { PageHeaderComponent } from './page-header.component';

describe('PageHeaderComponent', () => {
  it('renders the title and subtitle', () => {
    const fixture = TestBed.createComponent(PageHeaderComponent);
    fixture.componentRef.setInput('title', 'Projects');
    fixture.componentRef.setInput('subtitle', 'Track and manage all your projects in one place.');
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Projects');
    expect(text).toContain('Track and manage all your projects in one place.');
  });

  it('renders without a subtitle when none is provided', () => {
    const fixture = TestBed.createComponent(PageHeaderComponent);
    fixture.componentRef.setInput('title', 'Dashboard');
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Dashboard');
  });

  it('projects action content when provided', () => {
    TestBed.configureTestingModule({ imports: [HostWithActions] });
    const fixture = TestBed.createComponent(HostWithActions);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('button')?.textContent).toContain('New Project');
  });

  it('renders no action markup when none is projected', () => {
    const fixture = TestBed.createComponent(PageHeaderComponent);
    fixture.componentRef.setInput('title', 'Users');
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('button')).toBeNull();
  });
});

@Component({
  standalone: true,
  imports: [PageHeaderComponent],
  template: `
    <app-page-header title="Projects">
      <button page-header-actions type="button">New Project</button>
    </app-page-header>
  `,
})
class HostWithActions {}
