import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormField, maxLength, minLength, required, form } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, Router } from '@angular/router';
import { ProjectsService } from '../projects.service';
import { NotificationService } from '../../shared/notification.service';

interface ProjectFormModel {
  name: string;
  description: string;
}

@Component({
  selector: 'app-project-form',
  standalone: true,
  imports: [FormField, MatButtonModule, MatCardModule, MatFormFieldModule, MatInputModule],
  templateUrl: './project-form.component.html',
})
export class ProjectFormComponent implements OnInit {
  private readonly projectsService = inject(ProjectsService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly notificationService = inject(NotificationService);

  // Presence of the route's :id param (only on the .../:id/edit route) distinguishes
  // edit mode from create mode — the same component serves both.
  private readonly projectIdParam = this.route.snapshot.paramMap.get('id');
  protected readonly isEditMode = this.projectIdParam !== null;
  private readonly projectId = this.projectIdParam ? Number(this.projectIdParam) : null;

  protected readonly model = signal<ProjectFormModel>({ name: '', description: '' });

  protected readonly projectForm = form(this.model, (path) => {
    required(path.name, { message: 'Name is required.' });
    minLength(path.name, 3, { message: 'Name must be at least 3 characters.' });
    maxLength(path.name, 100, { message: 'Name must be at most 100 characters.' });

    maxLength(path.description, 2000, { message: 'Description must be at most 2000 characters.' });
  });

  protected readonly submitting = signal(false);
  protected readonly serverError = signal<string | null>(null);

  ngOnInit(): void {
    if (this.isEditMode) {
      void this.loadExisting();
    }
  }

  private async loadExisting(): Promise<void> {
    const project = await this.projectsService.getProject(this.projectId!);
    this.model.set({ name: project.name, description: project.description ?? '' });
  }

  protected async onSubmit(event: Event): Promise<void> {
    event.preventDefault();

    this.projectForm().markAsTouched();
    if (!this.projectForm().valid()) {
      return;
    }

    this.serverError.set(null);
    this.submitting.set(true);
    try {
      const result = this.isEditMode
        ? await this.projectsService.updateProject(this.projectId!, this.model())
        : await this.projectsService.createProject(this.model());
      this.notificationService.success(this.isEditMode ? 'Project updated.' : 'Project created.');
      await this.router.navigateByUrl(`/projects/${result.id}`);
    } catch (error) {
      const message =
        error instanceof HttpErrorResponse && error.status === 409
          ? 'A project with this name already exists.'
          : 'Something went wrong. Please try again.';
      this.serverError.set(message);
      this.notificationService.error(message);
    } finally {
      this.submitting.set(false);
    }
  }
}
