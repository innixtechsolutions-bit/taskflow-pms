import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormField, maxLength, minLength, required, form } from '@angular/forms/signals';
import { Router } from '@angular/router';
import { ProjectsService } from '../projects.service';

interface ProjectFormModel {
  name: string;
  description: string;
}

@Component({
  selector: 'app-project-form',
  standalone: true,
  imports: [FormField],
  templateUrl: './project-form.component.html',
})
export class ProjectFormComponent {
  private readonly projectsService = inject(ProjectsService);
  private readonly router = inject(Router);

  protected readonly model = signal<ProjectFormModel>({ name: '', description: '' });

  protected readonly projectForm = form(this.model, (path) => {
    required(path.name, { message: 'Name is required.' });
    minLength(path.name, 3, { message: 'Name must be at least 3 characters.' });
    maxLength(path.name, 100, { message: 'Name must be at most 100 characters.' });

    maxLength(path.description, 2000, { message: 'Description must be at most 2000 characters.' });
  });

  protected readonly submitting = signal(false);
  protected readonly serverError = signal<string | null>(null);

  protected async onSubmit(event: Event): Promise<void> {
    event.preventDefault();

    this.projectForm().markAsTouched();
    if (!this.projectForm().valid()) {
      return;
    }

    this.serverError.set(null);
    this.submitting.set(true);
    try {
      const created = await this.projectsService.createProject(this.model());
      await this.router.navigateByUrl(`/projects/${created.id}`);
    } catch (error) {
      this.serverError.set(
        error instanceof HttpErrorResponse && error.status === 409
          ? 'A project with this name already exists.'
          : 'Something went wrong. Please try again.'
      );
    } finally {
      this.submitting.set(false);
    }
  }
}
