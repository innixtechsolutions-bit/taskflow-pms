import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { vi } from 'vitest';
import { ProjectFormComponent } from './project-form.component';
import { ProjectsService } from '../projects.service';
import { NotificationService } from '../../shared/notification.service';

function setInputValue(el: HTMLInputElement | HTMLTextAreaElement, value: string): void {
  el.value = value;
  el.dispatchEvent(new Event('input'));
}

function fillAndSubmit(root: HTMLElement, name = 'Website Redesign'): void {
  setInputValue(root.querySelector<HTMLInputElement>('#name')!, name);
  root.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
}

function configure(createProject = vi.fn()) {
  const notificationService = { success: vi.fn(), error: vi.fn() };
  TestBed.configureTestingModule({
    imports: [ProjectFormComponent],
    providers: [
      provideRouter([]),
      { provide: ProjectsService, useValue: { createProject } },
      { provide: NotificationService, useValue: notificationService },
    ],
  });
  return createProject;
}

describe('ProjectFormComponent (create mode)', () => {
  it('navigates to the new project on a successful submit and shows a success toast', async () => {
    const createProject = configure(vi.fn().mockResolvedValue({ id: 42, name: 'Website Redesign' }));
    const fixture = TestBed.createComponent(ProjectFormComponent);
    fixture.detectChanges();
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigateByUrl');
    const notificationService = TestBed.inject(NotificationService);

    fillAndSubmit(fixture.nativeElement);
    await fixture.whenStable();

    expect(createProject).toHaveBeenCalledWith({ name: 'Website Redesign', description: '' });
    expect(navigateSpy).toHaveBeenCalledWith('/projects/42');
    expect(notificationService.success).toHaveBeenCalled();
  });

  it('shows the duplicate-name error returned by the server, and an error toast', async () => {
    const createProject = configure();
    createProject.mockRejectedValue(new HttpErrorResponse({ status: 409 }));
    const fixture = TestBed.createComponent(ProjectFormComponent);
    fixture.detectChanges();
    const notificationService = TestBed.inject(NotificationService);

    fillAndSubmit(fixture.nativeElement);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.server-error')?.textContent).toContain('already exists');
    expect(notificationService.error).toHaveBeenCalled();
  });
});

const existingProject = {
  id: 7,
  name: 'Existing Project',
  description: 'Existing description',
  createdByName: 'Ada Lovelace',
  createdAt: '2026-01-01T00:00:00Z',
  totalWorkItemCount: 0,
};

function configureEdit(
  getProject = vi.fn().mockResolvedValue(existingProject),
  updateProject = vi.fn().mockResolvedValue(existingProject)
) {
  TestBed.configureTestingModule({
    imports: [ProjectFormComponent],
    providers: [
      provideRouter([]),
      { provide: ProjectsService, useValue: { getProject, updateProject } },
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '7' }) } } },
      { provide: NotificationService, useValue: { success: vi.fn(), error: vi.fn() } },
    ],
  });
  return { getProject, updateProject };
}

describe('ProjectFormComponent (edit mode)', () => {
  it('pre-fills the existing name and description', async () => {
    configureEdit();
    const fixture = TestBed.createComponent(ProjectFormComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    expect((root.querySelector('#name') as HTMLInputElement).value).toBe('Existing Project');
    expect((root.querySelector('#description') as HTMLTextAreaElement).value).toBe('Existing description');
  });

  it('submits changes via updateProject rather than createProject', async () => {
    const { updateProject } = configureEdit();
    const fixture = TestBed.createComponent(ProjectFormComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    setInputValue(fixture.nativeElement.querySelector('#name')!, 'Renamed Project');
    fixture.nativeElement.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
    await fixture.whenStable();

    expect(updateProject).toHaveBeenCalledWith(7, expect.objectContaining({ name: 'Renamed Project' }));
  });
});
