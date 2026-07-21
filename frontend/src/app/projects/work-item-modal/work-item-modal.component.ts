import { Component, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { FormField, maxLength, minLength, required, form } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ProjectStatus, UserLookupItem, WorkItemLookupItem, WorkItemsService } from '../work-items.service';
import { AuthService } from '../../auth/auth.service';
import { NotificationService } from '../../shared/notification.service';
import { LabelChipComponent } from '../../shared/label-chip/label-chip.component';

interface TitleFormModel {
  title: string;
}

const TYPES = ['Epic', 'Story', 'Task', 'SubTask'];
const PRIORITIES = ['Low', 'Medium', 'High', 'Critical'];

// A date <input>/the API's dueDate field both want 'YYYY-MM-DD'; MatDatepicker
// wants a Date. Built from local year/month/day rather than toISOString() (which
// converts to UTC first and can shift the date by a day near midnight in
// timezones ahead of UTC) — same helpers as the removed WorkItemFormComponent.
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

// The sole way this modal learns which project/item it's working with and
// what to pre-select -- replaces the route/query params WorkItemFormComponent
// (removed, US1) used to read. onSaved is invoked after every successful
// create/update, before the dialog closes, so the view that opened this modal
// can refresh itself in place (research.md #9) -- including every individual
// save during a future "Create another" batch, not only once at the end.
export interface WorkItemModalData {
  mode: 'create' | 'edit';
  projectId: number;
  workItemId?: number;
  statusId?: number;
  parentWorkItemId?: number;
  type?: string;
  // Feature 008 (US2, FR-024) — create mode only. An invisible pass-through:
  // the created item is pre-assigned to this sprint, but the modal itself
  // gains no visible Sprint field (out of scope for this feature's UI).
  sprintId?: number;
  onSaved: () => void;
}

@Component({
  selector: 'app-work-item-modal',
  standalone: true,
  imports: [
    FormField,
    LabelChipComponent,
    MatButtonModule,
    MatCheckboxModule,
    MatDatepickerModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  templateUrl: './work-item-modal.component.html',
  styleUrl: './work-item-modal.component.css',
})
export class WorkItemModalComponent implements OnInit {
  private readonly workItemsService = inject(WorkItemsService);
  private readonly notificationService = inject(NotificationService);
  private readonly authService = inject(AuthService);
  private readonly data = inject<WorkItemModalData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<WorkItemModalComponent>);

  protected readonly isEditMode = this.data.mode === 'edit';
  private readonly projectId = this.data.projectId;
  private readonly workItemId = this.data.workItemId ?? null;

  protected readonly types = TYPES;
  protected readonly priorities = PRIORITIES;
  // Feature 006 — sourced from the project's own workflow columns, not a fixed list.
  protected readonly statuses = signal<ProjectStatus[]>([]);
  protected readonly assignableUsers = signal<UserLookupItem[]>([]);

  // Set true on the first change to any field after the modal opens (or after
  // an edit-mode load finishes) — research.md #2's dirty-flag, deliberately a
  // single boolean rather than a full value diff.
  protected readonly dirty = signal(false);

  // A separate, minimal Signal Forms tree for just the title — the only field
  // with real validation, same split as the removed WorkItemFormComponent.
  protected readonly titleModel = signal<TitleFormModel>({ title: '' });
  protected readonly titleForm = form(this.titleModel, (path) => {
    required(path.title, { message: 'Title is required.' });
    minLength(path.title, 3, { message: 'Title must be at least 3 characters.' });
    maxLength(path.title, 200, { message: 'Title must be at most 200 characters.' });
  });

  protected readonly type = signal('Task');
  protected readonly description = signal('');
  protected readonly priority = signal('Medium');
  // null until either dialog data or the loaded status list picks a default.
  protected readonly statusId = signal<number | null>(null);
  protected readonly assigneeUserId = signal('');
  protected readonly dueDate = signal<Date | null>(null);
  protected readonly startDate = signal<Date | null>(null);
  protected readonly parentWorkItemId = signal('');
  protected readonly parentCandidates = signal<WorkItemLookupItem[]>([]);

  // Labels (US5) — attached names, the in-progress input value, project-wide
  // suggestions, and an inline validation message. Attach/dedupe/cap rules
  // mirror the backend's NormalizeAndAttachLabelsAsync (data-model.md) so
  // the same mistake is caught here rather than round-tripping to the server.
  protected readonly labels = signal<string[]>([]);
  protected readonly labelInput = signal('');
  protected readonly labelError = signal<string | null>(null);
  protected readonly projectLabels = signal<string[]>([]);

  protected readonly labelSuggestions = computed<string[]>(() => {
    const query = this.labelInput().trim().toLowerCase();
    if (!query) {
      return [];
    }
    const attached = new Set(this.labels().map((l) => l.toLowerCase()));
    return this.projectLabels().filter((l) => l.toLowerCase().startsWith(query) && !attached.has(l.toLowerCase()));
  });

  protected readonly submitting = signal(false);
  protected readonly serverError = signal<string | null>(null);

  // Create mode only (US4) — keeps the dialog open after a successful create
  // instead of closing it, so several items can be logged in a row.
  protected readonly createAnother = signal(false);

  // Client-side mirror of the server's start<=due enforcement (US3) — only
  // constrained when both dates are set, matching InvalidDateRangeException.
  protected readonly dateRangeError = computed<string | null>(() => {
    const start = this.startDate();
    const due = this.dueDate();
    return start && due && start.getTime() > due.getTime()
      ? 'Start date must be on or before the due date.'
      : null;
  });

  constructor() {
    // Our own Escape/close-button handling (attemptClose, below) replaces
    // MatDialog's default immediate-close so an unsaved change can be
    // confirmed first (FR-006, research.md #2). disableClose only affects
    // MatDialog's own backdrop/Escape wiring — dialogRef.close() below still
    // always works.
    this.dialogRef.disableClose = true;
  }

  ngOnInit(): void {
    if (!this.isEditMode) {
      // Set when arriving via a board column's "+" affordance (statusId) or a
      // work item detail's "Add child" action (parentWorkItemId/type) — the
      // modal's equivalent of the removed route's query params.
      if (this.data.type) {
        this.type.set(this.data.type);
      }
      if (this.data.parentWorkItemId !== undefined) {
        this.parentWorkItemId.set(this.data.parentWorkItemId.toString());
      }
      if (this.data.statusId !== undefined) {
        this.statusId.set(this.data.statusId);
      }
    }
    void this.loadAssignableUsers();
    void this.loadParentCandidates();
    void this.loadStatuses();
    void this.loadProjectLabels();
    if (this.isEditMode) {
      void this.loadExistingWorkItem();
    }
  }

  private async loadProjectLabels(): Promise<void> {
    this.projectLabels.set(await this.workItemsService.getProjectLabels(this.projectId));
  }

  // Feature 006 — if nothing else has already picked a status (dialog data, or
  // the item being edited), default the dropdown to the project's first status
  // once loaded, matching the old fixed-list's "defaults to the first entry" UX.
  private async loadStatuses(): Promise<void> {
    const statuses = await this.workItemsService.getStatuses(this.projectId);
    this.statuses.set(statuses);
    if (this.statusId() === null && statuses.length > 0) {
      this.statusId.set(statuses[0].id);
    }
  }

  private async loadAssignableUsers(): Promise<void> {
    this.assignableUsers.set(await this.workItemsService.getAssignableUsers());
  }

  private async loadParentCandidates(): Promise<void> {
    this.parentCandidates.set(await this.workItemsService.getParentCandidates(this.projectId, this.type()));
  }

  // Every field-changing handler below calls markDirty() in addition to
  // updating its own signal — the load paths (loadExistingWorkItem,
  // ngOnInit's dialog-data prefill) call .set() directly instead, so loading
  // an existing item or applying a pre-selection never marks the form dirty.
  protected markDirty(): void {
    this.dirty.set(true);
  }

  // Type drives which candidates are valid (data-model.md's Hierarchy rules
  // table), so a Type change refetches candidates and clears any parent no
  // longer valid for the newly selected type.
  protected onTypeChange(value: string): void {
    this.markDirty();
    this.type.set(value);
    this.parentWorkItemId.set('');
    void this.loadParentCandidates();
  }

  protected onParentChange(value: string): void {
    this.markDirty();
    this.parentWorkItemId.set(value);
  }

  protected onDescriptionChange(value: string): void {
    this.markDirty();
    this.description.set(value);
  }

  protected onPriorityChange(value: string): void {
    this.markDirty();
    this.priority.set(value);
  }

  protected onStatusChange(value: number): void {
    this.markDirty();
    this.statusId.set(value);
  }

  protected onAssigneeChange(value: string): void {
    this.markDirty();
    this.assigneeUserId.set(value);
  }

  // "Assign to me" (US2) — one click, both modes; works the same as any other
  // assignee change (goes through the same setter, so it also marks dirty and
  // isn't persisted until Submit in edit mode).
  protected assignToMe(): void {
    const userId = this.authService.currentUser()?.id;
    if (userId !== undefined) {
      this.onAssigneeChange(userId.toString());
    }
  }

  protected onDueDateChange(value: Date | null): void {
    this.markDirty();
    this.dueDate.set(value);
  }

  protected onStartDateChange(value: Date | null): void {
    this.markDirty();
    this.startDate.set(value);
  }

  protected onCreateAnotherChange(checked: boolean): void {
    this.createAnother.set(checked);
  }

  protected onLabelInputChange(value: string): void {
    this.labelInput.set(value);
  }

  protected onLabelInputKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter') {
      event.preventDefault();
      this.addLabel(this.labelInput());
    }
  }

  protected selectLabelSuggestion(name: string): void {
    this.addLabel(name);
  }

  protected removeLabel(name: string): void {
    this.markDirty();
    this.labels.update((current) => current.filter((l) => l !== name));
  }

  // Order matters: an over-length name is always an error; a duplicate of an
  // already-attached label is always silently ignored (spec Edge Cases), even
  // when the item is already at the 5-label cap; only a genuinely new name
  // can trigger the cap error.
  private addLabel(rawName: string): void {
    const name = rawName.trim();
    if (!name) {
      return;
    }
    if (name.length > 30) {
      this.labelError.set('Labels must be at most 30 characters.');
      return;
    }
    if (this.labels().some((l) => l.toLowerCase() === name.toLowerCase())) {
      this.labelInput.set('');
      return;
    }
    if (this.labels().length >= 5) {
      this.labelError.set('A work item can have at most 5 labels.');
      return;
    }
    this.labelError.set(null);
    this.markDirty();
    this.labels.update((current) => [...current, name]);
    this.labelInput.set('');
  }

  private async loadExistingWorkItem(): Promise<void> {
    const item = await this.workItemsService.getWorkItem(this.workItemId!);
    this.titleModel.set({ title: item.title });
    this.type.set(item.type);
    this.description.set(item.description ?? '');
    this.priority.set(item.priority);
    this.statusId.set(item.statusId);
    this.assigneeUserId.set(item.assigneeUserId ? item.assigneeUserId.toString() : '');
    this.dueDate.set(item.dueDate ? parseDateOnlyString(item.dueDate) : null);
    this.startDate.set(item.startDate ? parseDateOnlyString(item.startDate) : null);
    this.parentWorkItemId.set(item.parentWorkItemId ? item.parentWorkItemId.toString() : '');
    this.labels.set(item.labels ?? []);
    await this.loadParentCandidates();
  }

  // Bound to the host element's keydown.escape and to the template's explicit
  // close (Cancel) control — FR-006's two documented ways to dismiss the
  // modal, both funneled through the same confirm-discard check.
  @HostListener('keydown.escape')
  protected attemptClose(): void {
    if (!this.dirty() || confirm('Discard unsaved changes?')) {
      this.dialogRef.close();
    }
  }

  protected async onSubmit(event: Event): Promise<void> {
    event.preventDefault();

    this.titleForm().markAsTouched();
    if (!this.titleForm().valid()) {
      return;
    }
    if (this.dateRangeError()) {
      return;
    }

    this.serverError.set(null);
    this.submitting.set(true);
    try {
      const dueDate = this.dueDate();
      const startDate = this.startDate();
      const request = {
        type: this.type(),
        title: this.titleModel().title,
        description: this.description() || undefined,
        priority: this.priority(),
        statusId: this.statusId() ?? undefined,
        assigneeUserId: this.assigneeUserId() ? Number(this.assigneeUserId()) : undefined,
        dueDate: dueDate ? toDateOnlyString(dueDate) : undefined,
        startDate: startDate ? toDateOnlyString(startDate) : undefined,
        parentWorkItemId: this.parentWorkItemId() ? Number(this.parentWorkItemId()) : undefined,
        // Always sent, even empty — PUT replaces the whole label set (data-model.md),
        // so omitting it on edit would silently wipe existing labels.
        labels: this.labels(),
      };
      if (this.isEditMode) {
        await this.workItemsService.updateWorkItem(this.workItemId!, request);
      } else {
        // Feature 008 — create-mode-only, invisible pre-assignment (FR-024); never
        // sent on edit, since an edit's SprintId is never touched by this modal.
        await this.workItemsService.createWorkItem(this.projectId, { ...request, sprintId: this.data.sprintId });
      }
      this.notificationService.success(this.isEditMode ? 'Work item updated.' : 'Work item created.');
      this.data.onSaved();
      if (!this.isEditMode && this.createAnother()) {
        // Title/description/labels reset for the next entry; every other field
        // (type, status, priority, assignee, parent, dates) is retained
        // (spec.md User Story 4).
        this.titleModel.set({ title: '' });
        this.description.set('');
        this.labels.set([]);
        this.labelInput.set('');
        this.labelError.set(null);
        // Picks up any label just created via inline entry (SC-004: it must
        // suggest on the very next item, same session, no page reload).
        void this.loadProjectLabels();
      } else {
        this.dialogRef.close();
      }
    } catch {
      const message = 'Something went wrong. Please try again.';
      this.serverError.set(message);
      this.notificationService.error(message);
    } finally {
      this.submitting.set(false);
    }
  }
}
