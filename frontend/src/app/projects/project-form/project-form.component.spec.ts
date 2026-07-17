import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { vi } from 'vitest';
import { ProjectFormComponent } from './project-form.component';
import { ProjectsService } from '../projects.service';

function setInputValue(el: HTMLInputElement | HTMLTextAreaElement, value: string): void {
  el.value = value;
  el.dispatchEvent(new Event('input'));
}

function fillAndSubmit(root: HTMLElement, name = 'Website Redesign'): void {
  setInputValue(root.querySelector<HTMLInputElement>('#name')!, name);
  root.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
}

function configure(createProject = vi.fn()) {
  TestBed.configureTestingModule({
    imports: [ProjectFormComponent],
    providers: [provideRouter([]), { provide: ProjectsService, useValue: { createProject } }],
  });
  return createProject;
}

describe('ProjectFormComponent (create mode)', () => {
  it('navigates to the new project on a successful submit', async () => {
    const createProject = configure(vi.fn().mockResolvedValue({ id: 42, name: 'Website Redesign' }));
    const fixture = TestBed.createComponent(ProjectFormComponent);
    fixture.detectChanges();
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigateByUrl');

    fillAndSubmit(fixture.nativeElement);
    await fixture.whenStable();

    expect(createProject).toHaveBeenCalledWith({ name: 'Website Redesign', description: '' });
    expect(navigateSpy).toHaveBeenCalledWith('/projects/42');
  });

  it('shows the duplicate-name error returned by the server', async () => {
    const createProject = configure();
    createProject.mockRejectedValue(new HttpErrorResponse({ status: 409 }));
    const fixture = TestBed.createComponent(ProjectFormComponent);
    fixture.detectChanges();

    fillAndSubmit(fixture.nativeElement);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.server-error')?.textContent).toContain('already exists');
  });
});
