import { Component, OnInit, inject, signal } from '@angular/core';
import { FormField, maxLength, minLength, required, form } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute, Router } from '@angular/router';
import { UserLookupItem, WorkItemLookupItem, WorkItemsService } from '../work-items.service';
import { NotificationService } from '../../shared/notification.service';
import { PageHeaderComponent } from '../../shared/page-header/page-header.component';

interface TitleFormModel {
  title: string;
}

const TYPES = ['Epic', 'Story', 'Task', 'SubTask'];
const PRIORITIES = ['Low', 'Medium', 'High', 'Critical'];
const STATUSES = ['ToDo', 'InProgress', 'Done'];

// A date <input>/the API's dueDate field both want 'YYYY-MM-DD'; MatDatepicker
// wants a Date. Built from local year/month/day rather than toISOString() (which
// converts to UTC first and can shift the date by a day near midnight in
// timezones ahead of UTC).
function toDateOnlyString(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

function parseDateOnlyString(value: string): Date {
  const [year, month, day] = value.slice(0, 10).split('-').map(Number);
  return new Date(year, month - 1, day);
}

@Component({
  selector: 'app-work-item-form',
  standalone: true,
  imports: [
    FormField,
    MatButtonModule,
    MatCardModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    PageHeaderComponent,
  ],
  templateUrl: './work-item-form.component.html',
})
export class WorkItemFormComponent implements OnInit {
  private readonly workItemsService = inject(WorkItemsService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly notificationService = inject(NotificationService);
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
  // real validation. The other fields are plain signals, each driven by mat-select's
  // own [value]/(selectionChange) (or MatDatepicker for the date) rather than a
  // native <select>'s [value]/(change) — mat-select doesn't have the native-<select>
  // "[value] written before @for-rendered <option>s exist" bug research.md §6
  // originally worked around, so that workaround no longer applies to these fields.
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
  protected readonly dueDate = signal<Date | null>(null);
  protected readonly parentWorkItemId = signal('');
  protected readonly parentCandidates = signal<WorkItemLookupItem[]>([]);

  protected readonly submitting = signal(false);
  protected readonly serverError = signal<string | null>(null);

  ngOnInit(): void {
    if (!this.isEditMode) {
      // Set when arriving via a work item detail view's "Add child" action (FR-019) —
      // pre-selects both the legal child Type and this parent, rather than leaving
      // the user to rediscover which type/parent combination they came here to create.
      const typeParam = this.route.snapshot.queryParamMap.get('type');
      const parentWorkItemIdParam = this.route.snapshot.queryParamMap.get('parentWorkItemId');
      if (typeParam) {
        this.type.set(typeParam);
      }
      if (parentWorkItemIdParam) {
        this.parentWorkItemId.set(parentWorkItemIdParam);
      }
    }
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
    this.dueDate.set(item.dueDate ? parseDateOnlyString(item.dueDate) : null);
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
      const dueDate = this.dueDate();
      const request = {
        type: this.type(),
        title: this.titleModel().title,
        description: this.description() || undefined,
        priority: this.priority(),
        status: this.status(),
        assigneeUserId: this.assigneeUserId() ? Number(this.assigneeUserId()) : undefined,
        dueDate: dueDate ? toDateOnlyString(dueDate) : undefined,
        parentWorkItemId: this.parentWorkItemId() ? Number(this.parentWorkItemId()) : undefined,
      };
      if (this.isEditMode) {
        await this.workItemsService.updateWorkItem(this.workItemId!, request);
      } else {
        await this.workItemsService.createWorkItem(this.projectId, request);
      }
      this.notificationService.success(this.isEditMode ? 'Work item updated.' : 'Work item created.');
      await this.router.navigateByUrl(`/projects/${this.projectId}`);
    } catch {
      const message = 'Something went wrong. Please try again.';
      this.serverError.set(message);
      this.notificationService.error(message);
    } finally {
      this.submitting.set(false);
    }
  }
}
