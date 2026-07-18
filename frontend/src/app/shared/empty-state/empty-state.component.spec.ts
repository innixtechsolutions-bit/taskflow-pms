import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { EmptyStateComponent } from './empty-state.component';

describe('EmptyStateComponent', () => {
  it('renders the icon and message', () => {
    const fixture = TestBed.createComponent(EmptyStateComponent);
    fixture.componentRef.setInput('icon', 'inbox');
    fixture.componentRef.setInput('message', 'No work items yet');
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No work items yet');
    expect(fixture.nativeElement.querySelector('mat-icon').textContent).toContain('inbox');
  });

  it('renders no action markup when none is projected', () => {
    const fixture = TestBed.createComponent(EmptyStateComponent);
    fixture.componentRef.setInput('icon', 'inbox');
    fixture.componentRef.setInput('message', 'No items match your filters.');
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('button')).toBeNull();
  });

  it('projects an action when provided', () => {
    TestBed.configureTestingModule({ imports: [HostWithAction] });
    const fixture = TestBed.createComponent(HostWithAction);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('button')?.textContent).toContain('Add work item');
  });
});

@Component({
  standalone: true,
  imports: [EmptyStateComponent],
  template: `
    <app-empty-state icon="inbox" message="No work items yet">
      <button empty-state-action type="button">Add work item</button>
    </app-empty-state>
  `,
})
class HostWithAction {}
