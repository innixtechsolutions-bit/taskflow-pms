# Feature 05 — Kanban Board (paste this into /speckit-specify)

Add a drag-and-drop Kanban board to each project so teams can see and
move work visually — the daily driver view for office use.

## Why (context)

TaskFlow has projects, hierarchical work items, filtering, and a design
system (Features 001–004). What's missing is the view teams actually
live in: a board of columns where a card is a work item and dragging it
between columns changes its status. This feature ships that board with
a fixed set of workflow statuses; a later feature (custom workflow
columns) will make the columns per-project configurable, so the board
here must render its columns from a list rather than hard-coding them.

## New workflow status

The current statuses are To Do, In Progress, Done. This feature adds
**In Review** between In Progress and Done, because real office
workflows have a review/QA stage. All existing items keep their current
status; nothing is remapped.

Status order everywhere (board columns, dropdowns, chips):
To Do → In Progress → In Review → Done. In Review gets its own chip
color consistent with the design system's color language (a distinct
hue — e.g. the purple accent family — not reusing blue/green).

## User stories

1. As any signed-in user, I can open a project's Board view (alongside
   the existing Tree and Flat views) and see one column per status,
   each with a header, item count, and the work items as cards.
2. As any signed-in user, I can drag a card from one column to another
   and the item's status changes — the card moves immediately, and if
   saving fails the card returns to its original column with an error
   toast.
3. As any signed-in user, I can read a card at a glance: title, type,
   priority chip, assignee avatar, due date (highlighted when overdue),
   and — when the item has children — a "n/m done" progress indicator.
4. As any signed-in user, I can add a work item directly from a
   column's "+ Add" affordance, with that column's status pre-selected
   in the create form.
5. As any signed-in user, I can click a card to open that work item's
   detail page, and return to the board where I left it.
6. As a user of the existing views, nothing regresses: Tree and Flat
   views, filters, and hierarchy behavior keep working, and the new
   In Review status appears correctly in every existing status
   dropdown, chip, and filter.

## Acceptance criteria

### Board layout & columns
- Board is a third view toggle on the project detail page
  (Tree / Flat / Board); the chosen view persists while navigating
  within the app session (no requirement to persist across reloads).
- Columns render from an ordered status list supplied by the backend —
  the frontend must not hard-code column names or order (future
  feature will make this list per-project).
- Each column header shows the status name and current item count.
- Columns scroll independently when cards overflow vertically; the
  board scrolls horizontally if columns exceed the viewport, without
  breaking the app shell.
- All work item types appear on the board (Epics included); type is
  visible on the card so the mix is scannable.

### Cards
- Card shows: title (wraps to max 2 lines with ellipsis), type label,
  priority chip, assignee avatar (or an "unassigned" placeholder),
  due date in friendly format — visually flagged when in the past and
  the item is not Done — and "n/m done" for items with children.
- Cards use the design system (tokens, chips, avatars) — no new ad-hoc
  colors.
- Within a column, cards are ordered by most recently updated first
  (manual reordering within a column is out of scope).

### Drag and drop
- Dragging a card to another column updates the item's status
  (optimistic move, then save; on failure revert with an error toast).
- The same status-change permission rules as editing apply (creator,
  assignee, Manager, Admin per Feature 002); a user who cannot edit an
  item cannot drop it into another column — the attempt reverts with a
  clear message.
- Server-side enforcement unchanged: a direct API status change by an
  unauthorized user is still refused (board is UI on top of existing
  rules).
- Keyboard alternative exists: the card's detail/edit path still
  allows changing status without drag (no new keyboard-DnD framework
  required — the form is the accessible fallback).

### In Review status (backend)
- Added between In Progress and Done in every ordered list.
- Existing items and their statuses are untouched by the migration.
- All status dropdowns, chips, filters, and "open items" counts
  (open = anything not Done) treat In Review correctly.

### Per-column create
- Each column has an add affordance; it opens the standard create form
  with Status pre-selected to that column (and project context set).

### Non-functional
- Board initial load for a project with up to 200 items renders
  without pagination (boards show everything; the flat list remains
  the paginated view).
- All existing tests keep passing; board logic that is pure
  (column grouping, overdue detection, permission-to-drag) is
  test-first.

## Out of scope (do NOT include in this feature)

- Custom / per-project columns and column management (next feature —
  but render-from-a-list must be honored now)
- Manual card ordering within a column; WIP limits
- Swimlanes, board filters, collapsed columns
- Real-time multi-user board updates (SignalR phase later)
- Board for cross-project views ("all my tasks" board)

## Success check

Feature is complete when: a project shows a Board view with four
columns (To Do, In Progress, In Review, Done) rendered from
backend-supplied data with correct counts; dragging a card between
columns changes its status with optimistic UI and error revert; a
card shows title, type, priority chip, avatar, friendly/overdue due
date, and child progress; "+ Add" in the In Review column opens the
create form with In Review pre-selected; a Developer who is neither
creator nor assignee of an item cannot move it (UI reverts, direct
API refused); and Tree/Flat views, filters, and all prior tests
continue to pass with In Review integrated throughout.
