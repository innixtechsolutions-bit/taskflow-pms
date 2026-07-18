# Feature Specification: Kanban Board

**Feature Branch**: `005-kanban-board`

**Created**: 2026-07-18

**Status**: Draft

**Input**: User description: "Add a drag-and-drop Kanban board to each project so teams can see and move work visually — the daily driver view for office use. Adds a new 'In Review' workflow status between In Progress and Done (fixed set for now; a later feature makes columns per-project configurable, so the board must render its columns from a backend-supplied list rather than hard-coding them). Board is a third view toggle alongside Tree and Flat. Cards show title, type, priority chip, assignee avatar, friendly/overdue due date, and child progress (n/m done). Dragging a card between columns changes its status with optimistic UI and revert-on-failure; the same edit permission rules as today apply (creator, assignee, Manager, Admin), enforced server-side independent of the UI. Each column has a '+ Add' affordance that pre-selects that column's status in the create form. Clicking a card opens its detail page; returning goes back to the board. Existing Tree/Flat views, filters, and all current tests must keep working with In Review integrated throughout (dropdowns, chips, filters, open-item counts)."

**Visual Reference**: [`visual-reference.png`](./visual-reference.png) (in this
directory) — a reference board screenshot (from a well-known project
management tool) showing column header treatment (status name + count +
overflow menu), card anatomy (title, key/type line, priority indicator,
subtask progress count with an expand affordance, assignee avatar
bottom-right), and overall card density. This feature's functional
requirements describe *what* the board must do; the image is a styling
reference only — card colors, chip colors, and avatars MUST come from
TaskFlow's own design system (Feature 004: `<app-status-chip>`,
`<app-priority-chip>`, `<app-user-avatar>`, `design-tokens.scss`), not the
reference image's own color scheme.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - In Review status integrated everywhere (Priority: P1)

As any signed-in user, I see a new **In Review** status — between In
Progress and Done — everywhere statuses already appear: work item
create/edit forms, status chips, project-detail filters, and any
open-item counts. Existing work items keep whatever status they already
have; nothing is remapped.

**Why this priority**: Every other story in this feature depends on In
Review existing as a real, selectable status first — a four-column board
is meaningless if the fourth status doesn't exist anywhere else in the
app yet. It is also independently valuable and testable on its own,
before any board UI exists.

**Independent Test**: Open a work item's edit form and confirm In Review
is a selectable status option between In Progress and Done; set an item
to In Review, confirm its chip renders with a distinct color from To
Do/In Progress/Done; filter a project's flat list by In Review and
confirm it returns the right items; confirm a project's "open work
items" count includes In Review items (open = not Done).

**Acceptance Scenarios**:

1. **Given** the work item create or edit form, **When** the Status
   field is opened, **Then** the options appear in order To Do, In
   Progress, In Review, Done.
2. **Given** a work item set to In Review, **When** its status chip
   renders anywhere in the app (tree, flat list, detail, board), **Then**
   it uses one consistent color, visually distinct from the other three
   status colors.
3. **Given** work items that existed before this feature shipped,
   **When** the feature is deployed, **Then** every existing item keeps
   its current status exactly as it was — none become In Review
   automatically.
4. **Given** a project's work item count that considers "open" items,
   **When** an item is In Review, **Then** it is counted as open (the
   existing open-item definition — anything not Done — already covers
   this without needing to treat In Review as a special case).

---

### User Story 2 - View the board (Priority: P1)

As any signed-in user, I can open a project's Board view (alongside the
existing Tree and Flat views) and see one column per status, each with a
header showing the status name and item count, and each work item
rendered as a card showing enough information (title, type, priority,
assignee, due date, child progress) to be useful without opening it.

**Why this priority**: This is the core deliverable — the "daily driver
view" the feature exists to provide. Without it, nothing else (drag,
per-column create) has anywhere to live.

**Independent Test**: Open a project with a mix of work item types,
priorities, assignees, and due dates (including overdue and unassigned
items, and at least one item with children) in Board view. Confirm four
columns appear in the correct order with accurate counts, and every
card shows title, type, priority chip, assignee avatar (or an
unassigned placeholder), a friendly-formatted due date visually flagged
when overdue, and an "n/m done" indicator only on items that have
children.

**Acceptance Scenarios**:

1. **Given** a project's detail page, **When** a user selects the Board
   view toggle, **Then** the Tree/Flat content is replaced by columns —
   one per status, in the fixed order To Do, In Progress, In Review,
   Done — each rendered from a status list the backend supplies (the
   frontend does not hard-code the column set).
2. **Given** the Board view, **When** it renders, **Then** each column
   header shows the status name and the count of items currently in
   that column.
3. **Given** a work item with a due date in the past that is not Done,
   **When** its card renders, **Then** the due date is visually flagged
   as overdue; a Done item with a past due date is not flagged.
4. **Given** a work item with no assignee, **When** its card renders,
   **Then** an "unassigned" placeholder appears instead of an avatar.
5. **Given** a work item with child items, **When** its card renders,
   **Then** it shows an "n/m done" count of completed vs. total
   children; an item with no children shows no such indicator.
6. **Given** a project with up to 200 work items, **When** Board view
   loads, **Then** all items render without pagination (the board is
   not paginated the way the flat list is).
7. **Given** columns with more cards than fit the viewport height,
   **When** a user scrolls within a column, **Then** only that column's
   cards scroll; if columns collectively exceed the viewport width, the
   board scrolls horizontally without breaking the surrounding app
   shell (sidebar stays put).
8. **Given** a user switches between Tree, Flat, and Board during a
   session, **When** they navigate elsewhere and come back within the
   same session, **Then** their last-chosen view is still selected (no
   requirement that this survives a full page reload).

---

### User Story 3 - Drag a card to change its status (Priority: P1)

As any signed-in user with permission to edit a work item, I can drag
its card from one column to another and see its status change — the
move happens immediately on screen, and if saving fails, the card
returns to its original column with an explanatory error.

**Why this priority**: This is the feature's defining interaction —
"see and move work visually." Without it, the board is just a
differently-shaped read-only list, which the existing Tree/Flat views
already provide.

**Independent Test**: As a user permitted to edit a given item, drag its
card to a different column and confirm the status updates (verified by
reopening the item or checking the flat list). Simulate a save failure
and confirm the card reverts to its original column with an error
toast. As a user NOT permitted to edit a given item (e.g. a Developer
who is neither its creator nor assignee), attempt to drag its card and
confirm the move is rejected with a clear message and the card stays
put; confirm a direct API status-change attempt by that same user is
independently refused by the server regardless of what the UI does.

**Acceptance Scenarios**:

1. **Given** a work item the current user may edit, **When** they drag
   its card to a different column, **Then** the card moves to that
   column immediately (optimistic UI) and the new status is persisted.
2. **Given** a card move that fails to save, **When** the failure
   occurs, **Then** the card returns to its original column and an
   error toast explains that the change wasn't saved.
3. **Given** a work item the current user may NOT edit (per the same
   rule already used for edit/status changes elsewhere: creator,
   current assignee, Manager, or Admin), **When** they attempt to drag
   its card to another column, **Then** the move is rejected — the card
   does not move (or reverts immediately) and a clear message explains
   why.
4. **Given** any status change attempted directly against the API by a
   user without permission, **When** the request is made outside the
   board UI entirely, **Then** the server refuses it — the board's
   permission check is a UX convenience, not the enforcement boundary.
5. **Given** a user who cannot or does not want to use drag-and-drop,
   **When** they open a card's detail/edit page instead, **Then** they
   can change its status there exactly as today — drag is not the only
   way to change status.

---

### User Story 4 - Add a work item from a column (Priority: P2)

As any signed-in user, I can start creating a work item directly from a
column's "+ Add" affordance, and the create form opens with that
column's status already selected.

**Why this priority**: A meaningful convenience once the board exists,
but the board is fully useful for viewing and moving existing work
without it — creation already works via the existing "New work item"
entry point.

**Independent Test**: Click "+ Add" in the In Review column and confirm
the create form opens with Status pre-selected to In Review and the
correct project context already set; submit it and confirm the new item
appears in the In Review column.

**Acceptance Scenarios**:

1. **Given** the Board view, **When** a user clicks a column's "+ Add"
   affordance, **Then** the standard work item create form opens with
   that column's status pre-selected and the current project already
   set as context.
2. **Given** a work item created this way, **When** the form is
   submitted successfully, **Then** the new card appears in the column
   whose status was pre-selected.

---

### User Story 5 - Open a card's detail and return to the board (Priority: P2)

As any signed-in user, I can click a card to open that work item's
detail page, and navigate back to find the board as I left it.

**Why this priority**: Useful navigational convenience — the board
already surfaces the key information; opening detail is for the rest
(description, full child list, etc.). Lower priority than viewing/moving
cards themselves.

**Independent Test**: From the Board view, click a card, confirm the
work item detail page opens for the correct item, then navigate back and
confirm the Board view (not Tree or Flat) is shown again with the same
project.

**Acceptance Scenarios**:

1. **Given** the Board view, **When** a user clicks a card (not the
   drag handle), **Then** that work item's detail page opens.
2. **Given** a user who navigated from the board to a card's detail page,
   **When** they return (e.g. via back navigation), **Then** the
   project detail page shows the Board view again, not Tree or Flat.

---

### Edge Cases

- What happens when two users change the same item's status at nearly
  the same time (one via drag, one via the edit form)? No real-time
  sync exists yet (explicitly out of scope); the last write wins at the
  server, and a user's board may show a stale column placement until
  they reload or the project data is refetched — this is an accepted
  limitation, not a defect, for this feature.
- What happens to a work item with an invalid/unrecognized status value
  (should not occur given the fixed enum, but the board must not
  silently drop a card that doesn't match any known column)?
- What happens when a project has zero work items? Each column renders
  empty (with the design system's empty-state treatment) with a count
  of 0, not an error.
- What happens when a card's title is very long? It wraps to a maximum
  of two lines and truncates with an ellipsis beyond that, consistent
  with the requirement on card title display.
- What happens when a due date is exactly today (not strictly in the
  past)? Not treated as overdue — only past-dated, non-Done items are
  flagged.
- What happens when an Epic (which can have many descendants) appears
  on the board? It renders like any other card, including its own
  direct-children progress count if it has direct children; the type
  label makes it identifiable at a glance.
- Does which column a card is dragged *into* affect whether the drag is
  allowed? No — the permission rule (FR-014) is about whether the user
  may edit that *item* at all (creator, assignee, Manager, Admin); there
  is no separate concept of a status a user "isn't allowed to set." An
  unauthorized user's drag is rejected the same way regardless of which
  column it's dropped on. *(Merged during triage — L1: this previously
  posed a confusing hypothetical about destination-specific permissions,
  which don't exist in this app; the real answer is fully covered by
  FR-014/US3's own acceptance scenarios.)*

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST support a fourth work item status, **In
  Review**, ordered between In Progress and Done, everywhere statuses
  are presented or selected (create/edit forms, chips, filters, board
  columns).
- **FR-002**: Adding the In Review status MUST NOT change the status of
  any existing work item — no automatic remapping.
- **FR-003**: The In Review status chip MUST render with a color
  distinct from To Do, In Progress, and Done, following the design
  system's existing token-based approach (no ad-hoc/hard-coded colors).
- **FR-004**: Every existing feature that already handles status
  (dropdowns, chips, filters, "open work item" counts, the Tree/Flat
  views) MUST correctly include and display In Review without requiring
  separate special-casing beyond adding it to the fixed status list.
- **FR-005**: The project detail page MUST offer a third view mode,
  Board, alongside the existing Tree and Flat views, selectable the same
  way the existing views are (a view toggle).
- **FR-006**: The Board view's columns MUST be rendered from an ordered
  list of statuses the system supplies, not a column set hard-coded in
  the display layer — this feature ships one fixed, system-wide list (To
  Do, In Progress, In Review, Done), but the rendering approach MUST NOT
  assume that list is permanently fixed, since a future feature makes it
  configurable per project.
- **FR-007**: Each column MUST display the status name and the current
  count of work items in that status.
- **FR-008**: Each work item, regardless of type (including Epics), MUST
  appear on the board as a card in the column matching its current
  status.
- **FR-009**: Each card MUST show: title (wrapped to a maximum of two
  lines, truncated with ellipsis beyond that), type, a priority chip, an
  assignee avatar (or an unassigned indicator when there is none), and
  its due date in the system's friendly date format.
- **FR-010**: A card's due date MUST be visually flagged when it is in
  the past AND the item's status is not Done; due dates that are today,
  in the future, or belong to a Done item MUST NOT be flagged.
- **FR-011**: A card for a work item with one or more direct children
  MUST show an "n/m done" count of completed vs. total direct children;
  a card for a work item with no children MUST NOT show this indicator.
- **FR-012**: Within a column, cards MUST be ordered by most recently
  updated first; manual reordering within a column is not required.
- **FR-013**: Dragging a card to a different column MUST update that
  work item's status to match the destination column, using an
  optimistic UI: the card MUST move immediately, and if the underlying
  save fails, the card MUST return to its original column and the user
  MUST see an error explaining the change wasn't saved.
- **FR-014**: The system MUST apply the same permission rule to a
  drag-triggered status change as already applies to editing a work item
  (its creator, its current assignee, or a Manager/Admin); an
  unauthorized user's drag attempt MUST be rejected in the UI with a
  clear explanation.
- **FR-015**: The server MUST independently enforce the same
  authorization rule for any status-changing request regardless of
  whether it originated from the board's drag interaction — the board's
  client-side permission check is a UX convenience only, not the
  enforcement boundary.
- **FR-016**: Changing a work item's status MUST remain possible without
  drag-and-drop, via the existing edit form, as an accessible fallback
  path that requires no new keyboard-specific drag interaction to be
  built.
- **FR-017**: Each column MUST offer an affordance to create a new work
  item with that column's status pre-selected and the current project
  already set as context, using the existing create form.
- **FR-018**: Clicking a card (outside of initiating a drag) MUST open
  that work item's detail page; returning from the detail page MUST
  show the project detail page in Board view again if that is the view
  the user was on.
- **FR-019**: The Board view's selection MUST persist while navigating
  within the app during a session; it is not required to persist across
  a full page reload.
- **FR-020**: Board view MUST render all of a project's work items (up
  to at least 200) without pagination, distinct from the Flat view,
  which remains paginated.
- **FR-021**: Columns MUST scroll independently when their cards exceed
  the visible column height; the board MUST scroll horizontally, not
  break the surrounding app shell layout, when columns collectively
  exceed the viewport width.
- **FR-022**: This feature MUST NOT introduce manual card reordering
  within a column, work-in-progress limits, swimlanes, board-specific
  filters, collapsible columns, real-time multi-user board updates, or
  a cross-project board — all explicitly out of scope.
- **FR-023**: All work item statuses, including In Review, MUST use
  cards, chips, and avatars from the existing design system (Feature
  004); this feature MUST NOT introduce new ad-hoc presentation
  components or colors.
- **FR-024**: All existing automated tests (backend and frontend) MUST
  continue to pass; pure board logic (grouping items into columns,
  overdue detection, and whether the current user may drag a given
  item) MUST have tests written before its implementation.

### Key Entities

- **Work Item** (existing entity, extended): gains In Review as a valid
  status value; all other attributes (type, priority, assignee, due
  date, parent/children) are unchanged by this feature and are exactly
  what a card displays.
- **Board Column** (new, derived — not a persisted entity by itself in
  this feature): a status name, its position in the ordered column
  list, and the work items currently in that status; this feature's
  column list is a fixed, system-wide sequence, but the concept is
  modeled so a future feature can make it a per-project, persisted list
  without reshaping the board's rendering approach.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can see all of a project's work items organized by
  status in a single board view, with every column's displayed count
  matching the actual number of items in that status, for projects with
  up to 200 items.
- **SC-002**: A user can change a work item's status by dragging its
  card, with the visual move happening immediately and, on a failed
  save, 100% of the time the card returns to its original column with
  an explanatory message.
- **SC-003**: 100% of drag attempts by a user without edit permission on
  the dragged item are rejected in the UI, and 100% of direct
  status-change requests by unauthorized users are refused by the
  server, regardless of the board's own checks.
- **SC-004**: A user can identify a work item's type, priority,
  assignee (or lack of one), and due-date urgency from its card alone,
  without opening it, for every item on the board.
- **SC-005**: Starting a new work item from a column takes one action
  (clicking that column's add affordance) to reach a create form with
  the correct status already selected.
- **SC-006**: 100% of pre-existing automated tests (backend and
  frontend) continue to pass after In Review and the board ship.
- **SC-007**: In Review appears correctly (correct order, correct
  distinct color, correctly counted as "open") in 100% of existing
  surfaces that already handle status — forms, chips, filters, and
  counts — with no surface missed.

## Assumptions

- The four-status list (To Do, In Progress, In Review, Done) is
  system-wide and fixed for this feature; per-project customization is
  explicitly the next feature (006) and is not built here, though the
  rendering approach must not preclude it.
- "Open" work items already means "status is not Done" in the existing
  open-item count; In Review is automatically included under that
  existing definition with no special-case logic required.
- A card's revert-on-failure returns it to its prior column using
  already-known client state (no server refetch is required to know
  where it came from).
- The exact chip color assigned to In Review is a design-token detail
  (a distinct hue, e.g. from the purple/violet family already used for
  primary accents) decided concretely during planning, not fixed by
  this spec beyond "distinct from the other three statuses."
- No new keyboard-specific drag-and-drop interaction pattern is
  required; the existing edit form is the accessible way to change
  status without a mouse, per the feature's explicit scope boundary.
- The visual reference image is a layout/anatomy reference only, from a
  different product with its own color scheme; actual colors, chips,
  and avatars come from TaskFlow's existing design system, not the
  reference image.
- Dark mode, board filters, swimlanes, WIP limits, manual reordering,
  real-time updates, and cross-project boards are explicitly out of
  scope, per the feature description.
