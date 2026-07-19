# Feature Specification: Custom Workflow Columns

**Feature Branch**: `006-custom-workflow-columns`

**Created**: 2026-07-19

**Status**: Draft

**Input**: User description: "Let each project define its own workflow columns (statuses) — add, rename, reorder, and remove columns — so the Kanban board matches how each team actually works. Feature 005 shipped the Kanban board with four fixed statuses; this feature converts workflow status from a fixed system-wide list into a per-project, manageable list, built on Feature 005's deliberate constraint that the board renders columns from a backend-supplied list rather than a hard-coded set. Every project owns an ordered list of statuses, each with a name (unique per project, 2–30 chars), a position, and a category (Open or Done) — category is what the system reasons about (open-item counts, tree 'n/m done', overdue highlighting), names are for humans. Every project must always keep at least one Open and one Done-category status. New projects get the standard four (To Do, In Progress, In Review as Open; Done as Done). Existing projects are migrated automatically to per-project rows preserving every item's current status, one-way, no dual-write. A Manager or Admin can: view a project's columns in order with name/category/item-count; add a column (name + category, positioned before the first Done column by default); rename a column (reflected everywhere immediately); reorder columns by drag in a management screen; delete an empty column, or a non-empty one by choosing a destination column for its items atomically in the same confirmed action. Any user sees a project's own columns consistently across board, dropdowns, chips, and filters, independent of other projects' workflows. Server-enforced rules: Manager/Admin only; case-insensitive per-project name uniqueness; never zero Open or zero Done statuses; atomic delete-with-move; max 10 columns per project. Each status gets a color from the design system's approved chip palette at creation (Open cycles open hues, Done uses green family; rename keeps color, color is editable), consistent everywhere. The board needs minimal structural change since it already renders from a supplied list; per-column '+ Add' pre-selects that column's status; tree view done-counts and filters use category, not name. Out of scope: WIP limits, column-level permissions, workflow templates/copying, transition rules (any column to any column remains allowed), manual card ordering within a column."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Every project's workflow is its own, everywhere (Priority: P1)

As any signed-in user, everywhere a work item's status already appears —
board columns, status dropdowns, chips, and filters — I see the
project's own list of statuses, in that project's own order, and two
projects with different workflows never affect each other's display.
Every work item that existed before this feature shipped keeps exactly
the status it already had.

**Why this priority**: This is the foundation every other story in this
feature depends on. Until statuses are a per-project, migrated concept
that every existing surface reads correctly, there is nothing for a
management screen to manage, and no way to safely verify that a change
to one project's workflow doesn't leak into another's.

**Independent Test**: Before any management UI exists, confirm that
every pre-existing project's board, dropdowns, chips, and filters show
the standard four statuses in the standard order, with every work item
still showing its exact pre-migration status. Then confirm that two
different projects can be inspected side by side and each shows only
its own statuses.

**Acceptance Scenarios**:

1. **Given** a project that existed before this feature shipped, **When**
   the feature is deployed, **Then** the project shows exactly the
   standard four statuses (To Do, In Progress, In Review as Open; Done
   as Done category) in that order, and every one of its work items
   still shows the exact status it had immediately before deployment.
2. **Given** two different projects, **When** their boards, status
   dropdowns, chips, and filters are viewed, **Then** each shows only
   that project's own statuses in that project's own order, regardless
   of what the other project's statuses are.
3. **Given** a project's open-item count or a tree view's "n/m done"
   count, **When** it is computed, **Then** it reasons about each
   item's status **category** (Open vs. Done), never the status name.
4. **Given** a newly created project, **When** it is created, **Then**
   it starts with the standard four statuses (To Do, In Progress, In
   Review as Open; Done as Done category) in that order.

---

### User Story 2 - View a project's workflow (Priority: P1)

As a Manager or Admin, I can open a project's Workflow management
screen and see its columns in order, each showing its name, its
category, and how many work items currently sit in it.

**Why this priority**: A Manager needs to see the current state of a
project's workflow before changing anything; this is also the screen
every other management action (add/rename/reorder/delete) lives on, so
it must exist before those stories can be exercised.

**Independent Test**: As a Manager or Admin, open a project's Workflow
screen and confirm every column appears in position order with its
correct name, category, and current item count; open it as a
Developer and confirm the screen is not reachable.

**Acceptance Scenarios**:

1. **Given** a project's Workflow management screen, **When** a Manager
   or Admin opens it, **Then** every status appears in position order,
   each showing its name, its category (Open or Done), and the number
   of work items currently in that status.
2. **Given** a Developer, **When** they look for a way to reach a
   project's Workflow management screen, **Then** no such entry point
   is shown to them.
3. **Given** a Developer, **When** they call the workflow-management API
   directly (bypassing the UI), **Then** the server refuses the
   request.

---

### User Story 3 - Add a workflow column (Priority: P1)

As a Manager or Admin, I can add a new column to a project's workflow
by giving it a name and a category, and it appears at a sensible
position (by default, just before the first Done-category column).

**Why this priority**: Adding a column is the single most requested
customization ("we need a QA column") and the smallest end-to-end slice
that delivers the feature's core promise — a workflow that matches how
a team actually works.

**Independent Test**: As a Manager, add a column named "QA" with
category Open to a project. Confirm it appears in the workflow list and
on the board at the expected position, is selectable in every status
dropdown and filter for that project, and that a second project's
workflow is unaffected.

**Acceptance Scenarios**:

1. **Given** a project's Workflow screen, **When** a Manager or Admin
   adds a column with a name and a category, **Then** it is inserted
   just before the project's first Done-category column by default, and
   immediately appears on the board, in status dropdowns, chips, and
   filters for that project.
2. **Given** a project already at the maximum of 10 columns, **When** a
   Manager or Admin attempts to add another, **Then** the attempt is
   refused with a clear message and no column is added.
3. **Given** a name that already exists in the project (case-insensitive
   match), **When** a Manager or Admin attempts to add a column with
   that name, **Then** the attempt is refused with a clear message.
4. **Given** a name shorter than 2 characters or longer than 30,
   **When** a Manager or Admin attempts to add it, **Then** the attempt
   is refused with a clear message.

---

### User Story 4 - Rename a workflow column (Priority: P2)

As a Manager or Admin, I can rename a project's column, and every card,
chip, dropdown, and filter that shows that status immediately reflects
the new name.

**Why this priority**: High-value, low-risk customization ("call it
'Doing' instead of 'In Progress'") that requires no data migration of
work items themselves — the status identity, category, color, and
items in it are unchanged.

**Independent Test**: As a Manager, rename "In Progress" to "Doing" on
a project. Confirm the board column header, every card's status chip
formerly in that column, every status dropdown, and every filter option
for that project now show "Doing," while the color and the work items
in that column are unchanged.

**Acceptance Scenarios**:

1. **Given** an existing column, **When** a Manager or Admin renames it,
   **Then** its new name appears everywhere that status is shown for
   that project, immediately, with its category, color, position, and
   contained work items unchanged.
2. **Given** a new name that collides case-insensitively with another
   status already in the same project, **When** a rename is attempted,
   **Then** it is refused with a clear message and the original name is
   kept.

---

### User Story 5 - Reorder workflow columns (Priority: P2)

As a Manager or Admin, I can drag a project's columns into a new order
on the Workflow management screen, and the board and every other
ordered list of that project's statuses follow the new order.

**Why this priority**: Ordering matters for how a team reads their
board left-to-right, but a workflow is still fully usable in whatever
order columns were created in, so this ranks below being able to add
and rename columns at all.

**Independent Test**: As a Manager, drag a project's columns into a new
order on the Workflow screen. Confirm the board's column order, the
Workflow screen's own order, and every status dropdown's option order
for that project now match, while a second project's order is
unaffected.

**Acceptance Scenarios**:

1. **Given** a project's Workflow screen, **When** a Manager or Admin
   drags a column into a new position, **Then** that new position is
   saved and immediately reflected in the column order on the board and
   in every status dropdown and filter for that project.
2. **Given** a project whose columns have been reordered, **When** a
   different project's Workflow screen or board is viewed, **Then** its
   own column order is unaffected.

---

### User Story 6 - Delete a workflow column (Priority: P3)

As a Manager or Admin, I can delete a column that has no work items in
it directly, or delete a column that does have items by choosing a
destination column, with the items moved and the column deleted as one
atomic, confirmed action.

**Why this priority**: The riskiest of the four management actions
(irreversibly changes where existing work items sit) and the one teams
need least often — most workflow cleanup is renaming or adding, not
removing — so it ranks last.

**Independent Test**: As a Manager, delete an empty column and confirm
it disappears everywhere immediately. Then attempt to delete a column
containing 3 work items, confirm a destination column must be chosen,
confirm the confirmation message names the item count and both column
names (e.g. "Move 12 items to 'In Progress' and delete 'QA'?"), and
confirm that after confirming, all 3 items now show the destination
column's status and the deleted column is gone everywhere for that
project. Attempt to delete a project's only remaining Open (or only
remaining Done-category) column and confirm it is refused.

**Acceptance Scenarios**:

1. **Given** a column with no work items, **When** a Manager or Admin
   deletes it, **Then** it is removed immediately from the board, the
   Workflow screen, and every dropdown and filter for that project.
2. **Given** a column with one or more work items, **When** a Manager or
   Admin attempts to delete it, **Then** they must first choose a
   destination column, and the confirmation names the number of items,
   the destination column, and the column being deleted.
3. **Given** a confirmed delete-with-move, **When** it is submitted,
   **Then** every affected item's status becomes the destination column
   and the source column is deleted as a single atomic action — if any
   part fails, neither the move nor the delete happens.
4. **Given** a project with only one Open-category column, **When** a
   Manager or Admin attempts to delete it, **Then** the attempt is
   refused with a clear message and no column is deleted, regardless of
   whether it currently has items.
5. **Given** a project with only one Done-category column, **When** a
   Manager or Admin attempts to delete it, **Then** the attempt is
   refused with a clear message for the same reason.

---

### Edge Cases

- What happens when a Manager tries to delete the last Open-category or
  last Done-category column? Refused unconditionally (FR-013), even if
  the project would otherwise still have other columns left.
- What happens when the destination column chosen for a delete-with-move
  is itself deleted or renamed by someone else before the action is
  confirmed? The action re-validates the destination at submission time;
  if it no longer exists, the delete is refused and the Manager must
  choose again — no partial move occurs.
- What happens if two Managers edit the same project's workflow at
  nearly the same time (e.g., one renames while another deletes)? Each
  individual action (add/rename/reorder/delete) is applied atomically
  server-side in the order received; no real-time collaborative editing
  or conflict UI is required — this mirrors the same last-write-wins
  behavior already accepted for concurrent work-item edits.
- What happens to a status's color and category when it is renamed?
  Both are unchanged — renaming affects only the display name.
- Can a column's category (Open vs. Done) be changed after creation? No
  — category is fixed at creation; changing what "Done" means for
  existing items in a column is out of scope for this feature. A team
  that needs a different category creates a new column and uses the
  delete-with-move flow to migrate items into it.
- What happens to a project's "default completion status" when the
  column currently serving as its default is deleted? Nothing needs to
  happen — the default completion status is not stored, only computed
  (the first Done-category status in position order, per FR-024), so
  deleting it simply means a different column now computes as first;
  no reassignment step or user action exists or is required.
- What happens when a name is unique within its own project but already
  used by another project's column? Nothing — uniqueness is per project
  only.
- What happens to items already displayed on a board or filter list
  mid-session when a column they belong to is renamed or reordered by
  someone else? The next data refresh (matching the app's existing
  refresh behavior for other project data) shows the change; no
  real-time push is required, consistent with Feature 005's accepted
  staleness behavior for concurrent status changes.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Every project MUST own its own ordered list of statuses;
  no status list MUST be shared across projects.
- **FR-002**: Each status MUST have a name unique within its project
  (case-insensitive), 2–30 characters, a position defining its order
  among that project's statuses, and a category of either **Open** or
  **Done**.
- **FR-003**: A project MUST never be left with zero Open-category
  statuses or zero Done-category statuses; any add, rename, reorder, or
  delete action that would produce that state MUST be refused with a
  clear error, and no partial change MUST be applied.
- **FR-004**: A project MUST NOT have more than 10 statuses; any attempt
  to add an 11th MUST be refused with a clear error.
- **FR-005**: A newly created project MUST start with the standard four
  statuses: To Do, In Progress, and In Review (category Open), and Done
  (category Done), in that order.
- **FR-006**: Every project that existed before this feature shipped
  MUST be migrated automatically, exactly once, to the standard four
  per-project statuses (matching FR-005's names, order, and categories),
  with every existing work item's status preserved exactly as it was
  immediately before migration — no item MUST change state.
- **FR-007**: Once migrated, a work item's status MUST reference its
  project's own status list; the prior system-wide fixed status field
  MUST no longer be used (a one-way migration with no dual-write
  period).
- **FR-008**: Only a Manager or Admin MUST be able to view or use a
  project's workflow-management screen; the entry point MUST be hidden
  from other roles, and the server MUST independently refuse any
  workflow-management request (view, add, rename, reorder, delete) made
  by a user who is not a Manager or Admin, regardless of what the UI
  allows.
- **FR-009**: The workflow-management screen MUST show a project's
  statuses in position order, each with its name, category, and the
  current count of work items in that status.
- **FR-010**: A Manager or Admin MUST be able to add a status by
  supplying a name and a category; when no explicit position is chosen,
  it MUST be inserted immediately before the project's first
  Done-category status.
- **FR-011**: A Manager or Admin MUST be able to rename a status; the
  new name MUST take effect immediately everywhere that status is shown
  for that project (board, chips, dropdowns, filters), while its
  category, color, position, and the work items already in it remain
  unchanged.
- **FR-012**: A Manager or Admin MUST be able to reorder a project's
  statuses; the new order MUST take effect immediately in the board's
  column order and in every dropdown's and filter's option order for
  that project, without affecting any other project's order.
- **FR-013**: A Manager or Admin MUST be able to delete a status that
  currently has no work items directly; the column MUST be removed
  immediately from the board, dropdowns, and filters for that project.
- **FR-014**: A Manager or Admin MUST be able to delete a status that
  currently has work items only by choosing a destination status first;
  the confirmation MUST state the number of affected items, the
  destination status, and the status being deleted, and the move of all
  affected items plus the deletion MUST occur as one atomic action — if
  any part fails, neither the move nor the delete MUST take effect.
- **FR-015**: Each status MUST be assigned a color from the design
  system's approved chip palette at creation — Open-category statuses
  cycling the palette's open-family hues, Done-category statuses
  drawing from the green family — and that color MUST remain associated
  with the status (including through a rename) until explicitly changed
  by a Manager or Admin in the workflow-management screen.
- **FR-016**: A status's chip color MUST render identically (same
  color) everywhere that status appears for its project — board, chips,
  dropdowns, filters.
- **FR-017**: The Kanban board MUST continue to render its columns and
  per-column item counts from its project's backend-supplied, ordered
  status list (no hard-coded column set), now reflecting that specific
  project's own customized list rather than one fixed system-wide list.
- **FR-018**: Each board column's "+ Add" affordance MUST pre-select
  that column's specific status (by identity, not by name) in the
  work-item create form, including for statuses added after Feature
  005 shipped.
- **FR-019**: Any computation that reasons about whether a work item is
  "done" — open-item counts, the tree view's "n/m done" indicator,
  overdue-due-date highlighting, and any other place that previously
  checked for the fixed Done status — MUST reason about the item's
  status **category**, never its status name.
- **FR-020**: Every status dropdown and filter (work-item create/edit
  forms, project filters) MUST list exactly and only its project's own
  current statuses, in that project's position order.
- **FR-021**: A status's category MUST be fixed at creation; this
  feature MUST NOT provide a way to change an existing status's
  category.
- **FR-022**: This feature MUST NOT introduce per-column work-in-progress
  limits, column-level move permissions, workflow templates or
  copying a workflow between projects, transition rules restricting
  which column a work item may move to next, or manual card ordering
  within a column — all explicitly out of scope.
- **FR-023**: All existing automated tests (backend and frontend) MUST
  continue to pass; the migration MUST be verified by tests that
  snapshot representative pre- and post-migration data; new pure logic
  (category reasoning, delete-with-move, position ordering, name
  uniqueness) MUST have tests written before its implementation.
- **FR-024**: A project's **default completion status** MUST be
  defined, deterministically, as the first Done-category status in that
  project's position order — computed on demand rather than stored as
  an independently-settable flag, so it is automatically correct
  whenever columns are added, reordered, or deleted, with no
  reassignment step. This feature exposes no dedicated UI to choose it
  explicitly; it exists so future features that need "the" done column
  (e.g. sprint completion, quick-complete actions) have a deterministic
  answer.

### Key Entities

- **Status** (new, per-project; replaces the prior system-wide fixed
  status list): belongs to exactly one project; has a name (unique
  within that project, case-insensitive), a position (defines order
  among that project's statuses), a category (Open or Done), and a chip
  color. It does not store a separate "is default completion status"
  flag — that is always computed per FR-024 (the first Done-category
  status in position order).
- **Project** (existing entity, extended): now owns an ordered
  collection of Statuses instead of relying on one system-wide fixed
  list; every project must have at least one Open-category and one
  Done-category status at all times.
- **Work Item** (existing entity, changed): its status now references
  one of its own project's Status rows instead of a fixed system-wide
  status value; all other attributes (type, priority, assignee, due
  date, parent/children) are unchanged by this feature.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A Manager can add a new column and see it appear on the
  board, in dropdowns, and in filters for that project without a full
  page reload, 100% of the time.
- **SC-002**: A rename or reorder of a project's columns is reflected
  everywhere that project's statuses are shown within one user action
  (no separate "publish" or "sync" step), 100% of the time.
- **SC-003**: 100% of pre-existing projects, after migration, show the
  standard four statuses with every work item retaining its exact
  pre-migration status — verified by automated pre/post snapshot tests
  on representative data.
- **SC-004**: 100% of attempts to leave a project with zero
  Open-category or zero Done-category statuses are refused, with no
  partial change applied.
- **SC-005**: 100% of delete-with-move actions either complete both the
  item moves and the column deletion together, or change nothing at
  all — no state where items moved but the column remains, or vice
  versa.
- **SC-006**: 100% of workflow-management attempts (view, add, rename,
  reorder, delete) by a non-Manager/Admin user are refused, both when
  attempted through the UI and when attempted directly against the
  API.
- **SC-007**: Two projects configured with different workflows (verified
  with at least 2 concurrently-configured projects) display and behave
  fully independently — changing one project's columns produces zero
  observable change in another project's board, dropdowns, chips, or
  filters.
- **SC-008**: 100% of pre-existing automated tests (backend and
  frontend) continue to pass after this feature ships.

## Assumptions

- A status's category (Open/Done) cannot be changed after creation;
  only name, position, and color are editable post-creation. This
  avoids the ambiguity of retroactively deciding whether existing items
  in a column became "done" or "undone" by a category edit — a team
  needing a different category creates a new column instead.
- A delete-with-move's destination column may be any of the project's
  remaining columns, regardless of category (an Open column's items may
  be moved into a Done column or vice versa, and the reverse) —
  consistent with this feature's explicit choice not to add transition
  rules restricting which column an item may move to.
- Column order has no enforced relationship to category — Open- and
  Done-category columns may be interleaved in position order if a
  Manager arranges them that way; only the *default* position for a
  newly added column (just before the first Done-category column)
  assumes the common case of Done columns trailing.
- The design system's approved chip palette and its "open hues" /
  "green family" groupings are a fixed, finite set of colors (extending
  Feature 004's status/priority chip approach); the exact palette
  membership and cycling order are a planning-level detail, not fixed
  by this spec beyond "Open cycles open hues, Done uses green."
- Work-item counts shown per column on the workflow-management screen
  count all work items currently in that status, regardless of type
  (Epic, Story, Task, SubTask) — consistent with how the board already
  counts items per column.
- Migration runs automatically as part of deployment with no manual
  trigger, and is one-way — there is no supported path back to a single
  system-wide fixed status list once a project's statuses have been
  migrated.
- No new keyboard-specific drag-and-drop interaction pattern is required
  for reordering columns, consistent with the same accepted scope
  boundary Feature 005 set for card dragging.
