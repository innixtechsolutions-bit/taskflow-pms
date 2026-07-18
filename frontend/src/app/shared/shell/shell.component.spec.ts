import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AppShellComponent } from './shell.component';

describe('AppShellComponent', () => {
  it('renders a sidenav region and a content region', () => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(AppShellComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('mat-sidenav')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('mat-sidenav-content')).toBeTruthy();
  });

  it('applies the content max-width token to the content region', () => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(AppShellComponent);
    fixture.detectChanges();

    const inner: HTMLElement = fixture.nativeElement.querySelector('.shell-content-inner');
    expect(inner).toBeTruthy();
    expect(inner.style.maxWidth).toBe('var(--content-max-width)');
  });

  it('projects routed page content into the content region', () => {
    TestBed.configureTestingModule({
      imports: [HostWithProjectedContent],
      providers: [provideRouter([])],
    });
    const fixture = TestBed.createComponent(HostWithProjectedContent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Routed page content');
  });
});

@Component({
  standalone: true,
  imports: [AppShellComponent],
  template: `<app-shell><p>Routed page content</p></app-shell>`,
})
class HostWithProjectedContent {}
