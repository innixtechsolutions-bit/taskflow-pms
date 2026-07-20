# Feature 06.5 — Work Item Modal & Quick Creation (paste into /speckit-specify)

Replace the full-page work item create/edit forms with a fast, Jira-style
modal dialog, and add the small fields that make daily task entry quick:
"Assign to me", start date, labels, and "create another".

## Why (context)

Creating a work item today navigates away to a full page and back —
fine for occasional use, slow for daily office use where someone logs a
dozen tasks in a planning session. The visual reference (saved Jira
"New task" modal screenshots) shows the target: a dialog that opens
over whatever you're looking at (board, tree, list, detail), lets you
create without losing context, and offers small accelerators. This
feature also prepares the ground for sprints: the start-date field
ships now, and the modal's layout leaves room for the sprint selector
a later feature will add.

## User stories

1. As any signed-in user, clicking any "new work item" / per-column
   "+" / "Add child" affordance opens a modal dialog over my current
   view; on create, the dialog closes, a toast confirms, and the view
   behind refreshes in place — I never lose my spot (board scroll
   position, expanded tree nodes, active filters all preserved).
2. As a user in the modal, all existing fields work as before (type,
   parent with hierarchy-filtered candidates, title, description,
   priority, status from the project's own workflow columns, assignee,
   due date) with the same validation and the same pre-selection
   behavior (column "+" pre-selects that status; "Add child"
   pre-selects the parent).
3. As a user assigning work to myself constantly, an "Assign to me"
   link next to the assignee field sets me as assignee in one click.
4. As a planner, I can set an optional Start date (any date, may be
   after the due date is invalid — start must be on or before due when
   both are set), shown on the work item detail alongside the due date.
5. As a user logging many items in a row, a "Create another" checkbox
   keeps the modal open after a successful create, clearing title/
   description but keeping type, status, priority, assignee, parent,
   and dates — so batch entry is fast.
6. As a team member organizing work, I can attach Labels to a work
   item — free-form short tags created on first use within a project,
   offered as suggestions afterwards — see them as small chips on
   cards, lists, tree rows, and detail, and filter the List view by
   label.
7. As a user editing an existing item, the same modal opens
   pre-populated from wherever an edit affordance exists today, with
   identical rules; deleting stays where it is (detail view) and is
   not part of the modal.

## Acceptance criteria

### Modal behavior
- Opens as a centered dialog (wide enough for comfortable two-column
  field layout on desktop; full-width on tablet), with a scrollable
  body and a fixed footer holding "Create another" + the primary
  button.
- Escape or an explicit close control dismisses it; if the form has
  unsaved changes, a confirm-discard prompt appears first.
- Server or validation errors display inside the modal without closing
  it; the same messages/rules as the current forms.
- The full-page create/edit routes are removed; any old links/routes
  redirect sensibly (e.g., to the project with the modal open or to
  the detail view). No dead routes remain.
- Background view refresh is targeted, not a full page reload.

### Assign to me
- Visible next to the assignee control; one click sets the current
  user; works in create and edit.

### Start date
- Optional; date-only; validation: when both dates are set,
  start ≤ due, enforced client- and server-side with a clear message.
- Shown on detail; included in DTOs; no board-card change in this
  feature.

### Labels
- A label is project-scoped: short text 1–30 chars, case-insensitively
  unique within the project; created inline in the modal by typing a
  new value; existing labels suggested as you type.
- A work item may have 0–5 labels.
- Displayed as small neutral chips (design-system styled, one shared
  look — not per-label colors in v1) on: board cards, list rows, tree
  rows, and detail.
- List view gains a label filter (single-select in v1, combinable with
  the existing filters).
- Any authenticated user may create/attach labels (same permission as
  editing the item's other fields — the existing edit rules apply);
  labels have no separate management screen in v1 (an unused label
  simply stops appearing in suggestions when no items reference it —
  exact cleanup semantics may be decided in planning).

### Non-functional
- All existing tests keep passing; modal logic that is pure
  (validation, create-another field retention, label normalization)
  is test-first.
- Server enforces start≤due and label constraints; ProblemDetails
  errors as established.
- The modal honors the design system (tokens, chips, datepickers,
  toasts) and the visual reference's layout spirit without copying
  Jira's palette.

## Out of scope (do NOT include in this feature)

- Sprint selector (next feature — leave layout room only)
- Rich-text/markdown description (stays a plain multiline field)
- Attachments, linked work items, teams, watchers
- Per-label colors, label management/rename/merge screens
- Board-card display of start date; label filter on Board view
- Multi-select label filtering

## Success check

Feature is complete when: from the Board, a column "+" opens the modal
with that status pre-selected, "Assign to me" sets me, a new label
"backend" is created inline, and after Create with "Create another"
checked the modal stays open with type/status/assignee/dates retained
and title cleared; the created card shows the label chip and appears
in the right column without losing my board position; setting a start
date after the due date is rejected with a clear message in the modal;
editing an item from detail opens the same modal pre-populated; the
List view filters by the "backend" label; old create/edit routes no
longer exist as pages; and all prior suites remain green.
