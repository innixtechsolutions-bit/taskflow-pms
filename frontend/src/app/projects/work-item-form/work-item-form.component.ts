import { Component, OnInit, inject, signal } from '@angular/core';
import { FormField, maxLength, minLength, required, form } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, Router } from '@angular/router';
import { UserLookupItem, WorkItemLookupItem, WorkItemsService } from '../work-items.service';

interface TitleFormModel {
  title: string;
}

const TYPES = ['Epic', 'Story', 'Task', 'SubTask'];
const PRIORITIES = ['Low', 'Medium', 'High', 'Critical'];
const STATUSES = ['ToDo', 'InProgress', 'Done'];

@Component({
  selector: 'app-work-item-form',
  standalone: true,
  imports: [FormField, MatButtonModule, MatCardModule, MatFormFieldModule, MatInputModule],
  templateUrl: './work-item-form.component.html',
})
export class WorkItemFormComponent implements OnInit {
  private readonly workItemsService = inject(WorkItemsService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly projectId = Number(this.route.snapshot.paramMap.get('projectId'));

  // Presence of the route's :id param (only on the .../work-items/:id/edit route) is
  // what distinguishes edit mode from create mode — the same component serves both.
  private readonly workItemIdParam = this.route.snapshot.paramMap.get('id');
  protected readonly isEditMode = this.workItemIdParam !== null;
  private readonly workItemId = this.workItemIdParam ? Number(this.workItemIdParam) : null;

  protected readonly types = TYPES;
  protected readonly priorities = PRIORITIES;
  protected readonly statuses = STATUSES;
  protected readonly assignableUsers = signal<UserLookupItem[]>([]);

  // A separate, minimal Signal Forms tree for just the title — the only field with
  // real validation. The other fields are plain signals driven by [selected] on each
  // <option>, not [value] on the <select> itself (research.md §6): Angular writes a
  // <select>'s own [value] binding before its @for-rendered <option> children exist in
  // the DOM, so the browser has nothing yet to match against and silently defaults to
  // the first option — the same bug Feature 001's Phase 7 found and fixed.
  protected readonly titleModel = signal<TitleFormModel>({ title: '' });
  protected readonly titleForm = form(this.titleModel, (path) => {
    required(path.title, { message: 'Title is required.' });
    minLength(path.title, 3, { message: 'Title must be at least 3 characters.' });
    maxLength(path.title, 200, { message: 'Title must be at most 200 characters.' });
  });

  protected readonly type = signal('Task');
  protected readonly description = signal('');
  protected readonly priority = signal('Medium');
  protected readonly status = signal('ToDo');
  protected readonly assigneeUserId = signal('');
  protected readonly dueDate = signal('');
  protected readonly parentWorkItemId = signal('');
  protected readonly parentCandidates = signal<WorkItemLookupItem[]>([]);

  protected readonly submitting = signal(false);
  protected readonly serverError = signal<string | null>(null);

  ngOnInit(): void {
    void this.loadAssignableUsers();
    void this.loadParentCandidates();
    if (this.isEditMode) {
      void this.loadExistingWorkItem();
    }
  }

  private async loadAssignableUsers(): Promise<void> {
    this.assignableUsers.set(await this.workItemsService.getAssignableUsers());
  }

  private async loadParentCandidates(): Promise<void> {
    this.parentCandidates.set(await this.workItemsService.getParentCandidates(this.projectId, this.type()));
  }

  // Type drives which candidates are valid (data-model.md's Hierarchy rules table),
  // so a Type change refetches candidates and clears any parent no longer valid for
  // the newly selected type rather than leaving a stale, possibly-illegal selection.
  protected onTypeChange(value: string): void {
    this.type.set(value);
    this.parentWorkItemId.set('');
    void this.loadParentCandidates();
  }

  private async loadExistingWorkItem(): Promise<void> {
    const item = await this.workItemsService.getWorkItem(this.workItemId!);
    this.titleModel.set({ title: item.title });
    this.type.set(item.type);
    this.description.set(item.description ?? '');
    this.priority.set(item.priority);
    this.status.set(item.status);
    this.assigneeUserId.set(item.assigneeUserId ? item.assigneeUserId.toString() : '');
    // The API returns a full ISO datetime; a date <input> only accepts its date portion.
    this.dueDate.set(item.dueDate ? item.dueDate.slice(0, 10) : '');
    this.parentWorkItemId.set(item.parentWorkItemId ? item.parentWorkItemId.toString() : '');
    await this.loadParentCandidates();
  }

  protected async onSubmit(event: Event): Promise<void> {
    event.preventDefault();

    this.titleForm().markAsTouched();
    if (!this.titleForm().valid()) {
      return;
    }

    this.serverError.set(null);
    this.submitting.set(true);
    try {
      const request = {
        type: this.type(),
        title: this.titleModel().title,
        description: this.description() || undefined,
        priority: this.priority(),
        status: this.status(),
        assigneeUserId: this.assigneeUserId() ? Number(this.assigneeUserId()) : undefined,
        dueDate: this.dueDate() || undefined,
        parentWorkItemId: this.parentWorkItemId() ? Number(this.parentWorkItemId()) : undefined,
      };
      if (this.isEditMode) {
        await this.workItemsService.updateWorkItem(this.workItemId!, request);
      } else {
        await this.workItemsService.createWorkItem(this.projectId, request);
      }
      await this.router.navigateByUrl(`/projects/${this.projectId}`);
    } catch {
      this.serverError.set('Something went wrong. Please try again.');
    } finally {
      this.submitting.set(false);
    }
  }
}
