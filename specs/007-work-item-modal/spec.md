# Feature Specification: Work Item Modal & Quick Creation

**Feature Branch**: `007-work-item-modal`

**Created**: 2026-07-20

**Status**: Draft

**Input**: User description: "Replace the full-page work item create/edit forms with a fast, Jira-style modal dialog, and add the small fields that make daily task entry quick: 'Assign to me', start date, labels, and 'create another'." (Full description sourced from `feature-06-5-work-item-modal.md` in the project root.)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create and edit work items without leaving the current view (Priority: P1)

Any signed-in user clicks a "new work item" affordance (global button, per-column "+" on the board, "Add child" on a parent item) or an "edit" affordance on an existing item, and a modal dialog opens over whatever they were looking at — board, tree, list, or detail. All the fields that exist in today's full-page forms (type, parent with hierarchy-filtered candidates, title, description, priority, status drawn from the project's own workflow columns, assignee, due date) are present in the modal with the same validation rules and the same pre-selection behavior (a column "+" pre-selects that column's status; "Add child" pre-selects the parent). On successful create or save, the modal closes, a toast confirms the result, and the view behind it refreshes in place — board scroll position, expanded tree nodes, and active filters are all preserved. Escape or an explicit close control dismisses the modal; if there are unsaved changes, a confirm-discard prompt appears first. Server and validation errors display inside the modal without closing it. The old full-page create/edit routes are removed and no longer exist as pages; any old links redirect sensibly instead of dead-ending.

**Why this priority**: This is the entire premise of the feature — without it, none of the smaller accelerators (assign to me, start date, create another, labels) have anywhere to live. It is also the only story that removes the old full-page forms, so it must ship as one complete, working replacement.

**Independent Test**: From the Board, click a column's "+" affordance, fill in a title, submit, and confirm: the modal closes, a toast appears, the new card shows up in the correct column, and the board's prior scroll position and filters are unchanged. Separately, open an existing item's edit affordance from its detail view, change a field, save, and confirm the same item reflects the change without a full page navigation having occurred.

**Acceptance Scenarios**:

1. **Given** the Board view with an existing scroll position and active filters, **When** a user clicks a column's "+" affordance, **Then** a modal opens with that column's status pre-selected and the board behind it is unchanged.
2. **Given** an item's detail view, **When** a user clicks "Add child", **Then** the modal opens with that item pre-selected as parent and the parent candidate list filtered to items valid under the hierarchy rules.
3. **Given** the modal is open with valid required fields filled in, **When** the user submits, **Then** the item is created (or saved, for edit), the modal closes, a success toast appears, and the underlying view refreshes in place without a full page reload.
4. **Given** the modal is open with unsaved changes, **When** the user presses Escape or clicks the close control, **Then** a confirm-discard prompt appears before the modal closes.
5. **Given** the modal is open with no unsaved changes, **When** the user presses Escape, **Then** the modal closes immediately with no prompt.
6. **Given** the modal is open, **When** the server rejects the submission (validation or server error), **Then** the error is shown inside the modal, the modal stays open, and entered values are retained.
7. **Given** an existing item, **When** a user opens its edit affordance from any surface where one exists today (board card, list row, tree row, detail view), **Then** the same modal opens pre-populated with that item's current field values.
8. **Given** a user navigates to a bookmarked old create/edit page URL, **When** the page loads, **Then** they are redirected to a working view (the project with the modal open, or the item's detail view) rather than seeing a broken or dead page.

---

### User Story 2 - Assign a new item to myself in one click (Priority: P2)

While the modal is open (create or edit), a user sees an "Assign to me" link next to the assignee field. Clicking it sets the current user as the assignee immediately, without opening the assignee dropdown.

**Why this priority**: A small but high-frequency accelerator for the most common assignment case (self-assignment); depends on User Story 1's modal existing but is independently valuable and testable on top of it.

**Independent Test**: Open the create modal, click "Assign to me", and confirm the assignee field shows the current user without any further interaction. Repeat in the edit modal on an item currently assigned to someone else and confirm it switches to the current user.

**Acceptance Scenarios**:

1. **Given** the create modal is open with no assignee selected, **When** the user clicks "Assign to me", **Then** the assignee field is set to the current user.
2. **Given** the edit modal is open on an item assigned to another user, **When** the user clicks "Assign to me", **Then** the assignee field updates to the current user (not yet saved until the user submits).

---

### User Story 3 - Set an optional start date (Priority: P2)

While the modal is open, a user can set an optional Start date (date-only) alongside the existing due date. If both a start date and a due date are set, the start date must be on or before the due date; otherwise the modal shows a clear validation message and blocks submission. The start date is shown on the work item's detail view alongside the due date.

**Why this priority**: Prepares the data model for a future sprint feature and is useful standalone for planning, but is lower-frequency than the core create/edit flow and the assign-to-me shortcut.

**Independent Test**: Open the modal, set a start date earlier than or equal to an existing due date, submit, and confirm the item saves with both dates visible on its detail view. Separately, set a start date after the due date and confirm submission is blocked with a clear inline message.

**Acceptance Scenarios**:

1. **Given** the modal is open with no due date set, **When** the user sets a start date, **Then** the field accepts the value with no error.
2. **Given** the modal is open with a due date already set, **When** the user sets a start date on or before that due date, **Then** the field accepts the value with no error.
3. **Given** the modal is open with a due date already set, **When** the user sets a start date after that due date, **Then** the modal shows a validation message and blocks submission until corrected.
4. **Given** an item with both a start date and due date, **When** its detail view is opened, **Then** both dates are visible.

---

### User Story 4 - Log several items in a row without re-opening the modal (Priority: P2)

While the create modal is open, a user checks a "Create another" checkbox before submitting. On successful create, instead of closing, the modal stays open with title and description cleared but type, status, priority, assignee, parent, and both dates retained, ready for the next item.

**Why this priority**: Directly targets the "daily office use, a dozen tasks in a planning session" scenario named as the core motivation for the feature; meaningful time savings for the primary use case, but only matters once the base create flow (User Story 1) exists.

**Independent Test**: Open the create modal, check "Create another", fill in a title and other fields, submit, and confirm the modal remains open with title/description blank and all other fields (type, status, priority, assignee, parent, start date, due date) unchanged from before submission.

**Acceptance Scenarios**:

1. **Given** the create modal is open with "Create another" checked and all required fields filled in, **When** the user submits, **Then** the item is created, a success toast appears, and the modal remains open.
2. **Given** the modal remains open after a "Create another" submission, **When** the user inspects the form, **Then** title and description are empty while type, status, priority, assignee, parent, start date, and due date retain their prior values.
3. **Given** "Create another" is unchecked, **When** the user submits successfully, **Then** the modal closes as in User Story 1.
4. **Given** "Create another" is checked, **When** the user is editing an existing item (not creating), **Then** the checkbox has no effect (edit always closes the modal on save, or the checkbox is not shown in edit mode).

---

### User Story 5 - Tag work items with labels and filter by them (Priority: P3)

While the modal is open, a user can attach up to 5 short free-form labels to a work item. Typing a new value and confirming it creates that label within the current project on first use; typing a partial value that matches existing project labels offers them as suggestions. Attached labels appear as small neutral chips on board cards, list rows, tree rows, and the detail view. The List view gains a single-select label filter that combines with existing filters.

**Why this priority**: Adds real organizational value but is the most self-contained addition — it does not block or get blocked by the other stories, and is reasonable to ship last.

**Independent Test**: Open the modal, type a new label name "backend" and confirm it, save the item, and verify: the label now appears as a chip on the item's board card, list row, tree row, and detail view; opening the modal on a different item and typing "back" offers "backend" as a suggestion; and the List view can be filtered to show only items labeled "backend".

**Acceptance Scenarios**:

1. **Given** the modal is open, **When** a user types a label name that does not yet exist in the project and confirms it, **Then** the label is created within that project and attached to the item.
2. **Given** the modal is open and the project already has a label "backend", **When** a user types "back", **Then** "backend" appears as a suggestion.
3. **Given** an item already has 5 labels attached, **When** the user tries to add a 6th, **Then** the modal prevents it with a clear message.
4. **Given** a user types a label name that matches an existing project label case-insensitively but with different casing, **When** they confirm it, **Then** the existing label is reused rather than a duplicate being created.
5. **Given** an item has one or more labels, **When** it is viewed on the board, in a list, in the tree, or on its detail view, **Then** each label appears as a small neutral chip.
6. **Given** the List view, **When** a user selects a label in the label filter, **Then** only items carrying that label are shown, combinable with any other active filters.

---

### Edge Cases

- What happens if a user opens the create modal, does not touch any field, and closes it? No confirm-discard prompt should appear since nothing changed, and no item should be created.
- What happens if two users are viewing the same board and one creates an item while the other's modal is open? Each user's own modal state is unaffected; the other user's view refreshes independently when they next interact with or reopen it.
- What happens when a user submits a label name that is only whitespace or exceeds 30 characters? The modal rejects it with a clear inline message before submission.
- What happens when a user tries to attach a label that already exists on the item? The modal ignores the duplicate attempt rather than attaching it twice.
- What happens if a project ends up with zero workflow columns (edge state from feature 006)? The status field in the modal has no valid option, and the modal communicates that a workflow column must exist before an item can be created.
- What happens when a user sets a start date but no due date, then later someone sets a due date before that start date on the same item? The same start ≤ due validation applies at that later edit.
- What happens when the parent field's hierarchy-filtered candidate list is empty (e.g., creating at the top level with no eligible parents)? The parent field is optional and simply shows no candidates, not an error.
- What happens when a user's session expires while the modal is open? Submission fails with the existing authentication error handling; the modal stays open with entered values retained so the user does not lose their input after re-authenticating.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a modal dialog for creating a work item, reachable from every surface that currently offers item creation (a global "new work item" affordance, each board column's "+" affordance, and "Add child" on an existing item).
- **FR-002**: The system MUST provide the same modal dialog, pre-populated with the item's current values, for editing an existing work item, reachable from every surface that currently offers an edit affordance (board card, list row, tree row, detail view).
- **FR-003**: The modal MUST include all fields present in the current full-page forms — type, parent (hierarchy-filtered candidates), title, description, priority, status (drawn from the project's own workflow columns), assignee, and due date — with the same validation rules as today.
- **FR-004**: The modal MUST preserve existing pre-selection behavior: a board column's "+" affordance pre-selects that column's status; "Add child" pre-selects the triggering item as parent.
- **FR-005**: On successful create or save, the system MUST close the modal (except when "Create another" is checked during create — see FR-011), show a confirmation toast, and refresh the underlying view in place without a full page reload, preserving board scroll position, expanded tree nodes, and active filters.
- **FR-006**: The system MUST dismiss the modal when the user presses Escape or activates an explicit close control, showing a confirm-discard prompt first only if the form has unsaved changes.
- **FR-007**: The system MUST display server-side and validation errors inside the modal without closing it, retaining the user's entered values.
- **FR-008**: The system MUST remove the full-page work item create and edit routes; any request to a former create/edit URL MUST redirect to a working view (e.g., the project with the modal open, or the item's detail view) rather than a dead page.
- **FR-009**: The modal MUST include an "Assign to me" control next to the assignee field, in both create and edit modes, that sets the current authenticated user as assignee in a single interaction.
- **FR-010**: The modal MUST include an optional, date-only Start date field. When both start date and due date are set, the system MUST enforce start ≤ due on both client and server, rejecting violations with a clear message.
- **FR-011**: The start date MUST be persisted, included in work item data returned by the API, and displayed on the work item detail view alongside the due date.
- **FR-012**: The create modal MUST include a "Create another" checkbox. When checked, a successful create MUST leave the modal open, clear title and description, and retain type, status, priority, assignee, parent, start date, and due date for the next entry.
- **FR-013**: The system MUST support attaching 0–5 labels to a work item. Each label is scoped to a single project, is 1–30 characters, and is unique within that project case-insensitively.
- **FR-014**: The modal MUST allow creating a new label inline by typing a value not already present in the project, and MUST suggest existing project labels matching what the user is typing.
- **FR-015**: The system MUST render attached labels as small, neutral, uniformly styled chips (not per-label colors) on board cards, list rows, tree rows, and the work item detail view.
- **FR-016**: The List view MUST provide a single-select label filter that combines with the view's existing filters.
- **FR-017**: The system MUST allow any user permitted to edit a work item's other fields to create and attach labels to it — no separate label-management permission or screen exists in this feature.
- **FR-018**: The system MUST reject label attach/create attempts that would exceed 5 labels on an item, are empty/whitespace-only, or exceed 30 characters, with a clear inline message.
- **FR-019**: The system MUST NOT attach the same label to an item more than once.
- **FR-020**: All existing automated test suites MUST continue to pass, and new pure logic introduced by this feature (start≤due validation, "Create another" field retention, label name normalization/uniqueness) MUST have tests written before the corresponding implementation.

### Key Entities

- **Work Item** *(existing entity, extended)*: Gains an optional Start date (date-only) alongside its existing due date, and a collection of 0–5 attached Labels.
- **Label**: A short (1–30 character) free-form tag scoped to a single project; unique within that project case-insensitively; created inline the first time a user types a new value in the modal; referenced by zero or more work items within its project.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can create a new work item from the Board, List, or Tree view without the browser navigating away from that view, 100% of the time.
- **SC-002**: A user logging five work items in a row using "Create another" completes all five without the modal ever closing between entries.
- **SC-003**: Every field and validation rule available in the removed full-page forms is available and behaves identically in the modal, with zero regressions across existing automated test suites.
- **SC-004**: After creating a label, it appears as a filter option and as a typing suggestion for the next item created in the same project without a page reload.
- **SC-005**: No former create/edit page URL renders a dead or broken page; each redirects to a working, functionally equivalent view.
- **SC-006**: A user can filter the List view down to items carrying a single chosen label and see only matching items.
- **SC-007**: Setting a start date after an already-set due date is rejected with a clear, in-modal message in 100% of attempts, both when entered client-side and if it somehow reaches the server unvalidated.

## Assumptions

- The current full-page create/edit forms' exact validation rules (field requiredness, length limits, permission checks) carry over unchanged into the modal; this feature changes presentation and adds new fields, not existing validation semantics.
- "Every surface that currently offers item creation/edit" refers to affordances that exist in the shipped product as of feature 006 (board column "+", "Add child", board card/list row/tree row/detail edit actions); no new entry points are introduced.
- Redirecting old create/edit URLs targets either the item's project (with the modal open) for create routes, or the item's detail view for edit routes — the exact choice per route is a planning-level decision, not a product-level one.
- Unused labels (referenced by no work items) remain stored but simply stop appearing in suggestions; no label deletion, rename, or merge capability is introduced, consistent with the "no management screen in v1" constraint. Exact storage/cleanup mechanics are deferred to planning.
- "Any authenticated user" who may create/attach labels means any user who already has permission to edit the work item's other fields under existing project-role rules — this feature does not introduce a new permission tier.
- The Sprint selector referenced as future work is out of scope; the modal's layout should not actively block adding it later, but no placeholder field or UI is built now.
- Rich-text/markdown editing for description is out of scope; description remains a plain multiline text field, same as today's full-page form.
- Board-card display does not change to show the new start date, and the Board view does not gain a label filter in this feature — both are explicitly deferred per the feature's stated scope.
