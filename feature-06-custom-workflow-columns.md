# Feature 06 — Custom Workflow Columns (paste this into /speckit-specify)

Let each project define its own workflow columns (statuses) — add,
rename, reorder, and remove columns — so the Kanban board matches how
each team actually works.

## Why (context)

Feature 005 shipped the Kanban board with four fixed statuses. Real
teams differ: one wants To Do → Design → Development → QA → Done;
another wants three simple columns. This feature converts workflow
status from a fixed system-wide list into a per-project, manageable
list. The board already renders columns from a backend-supplied list
(a deliberate Feature 005 constraint), so the board itself should need
minimal change — this feature is mostly data model, management rules,
and a column-management UI.

## Core concept: per-project statuses with categories

Every project owns an ordered list of statuses. Each status has:
- a name (unique within the project, 2–30 chars)
- a position (defines column order everywhere)
- a **category**: one of **Open** or **Done**

Category is what the system reasons about; names are for humans.
Everything that previously asked "is it Done?" now asks "is its
status's category Done?" — open-item counts, the tree view's
"n/m done", overdue highlighting, and any reporting. Every project
must have at least one Open status and exactly one Done-category
status may be marked as the **default completion status** (used
wherever the system needs "the" done column, e.g. quick-complete
actions if any exist).

New projects are created with the standard four:
To Do, In Progress, In Review (Open) and Done (Done category).

## Migration of existing data

- The four current global statuses become per-project rows for every
  existing project, preserving each item's current status — no item
  changes state, no user-visible difference on day one.
- The old fixed-status field is fully replaced by a reference to the
  project's status list (one-way migration; no dual-write period).

## User stories

1. As a Manager or Admin, I can open a "Workflow" management screen
   for a project and see its columns in order, each showing name,
   category, and how many items currently sit in it.
2. As a Manager or Admin, I can add a new column with a name and
   category, appearing at a chosen position (default: just before the
   first Done-category column).
3. As a Manager or Admin, I can rename a column; every card, chip,
   dropdown, and filter reflects the new name immediately.
4. As a Manager or Admin, I can reorder columns by dragging them in
   the management screen; the board and all ordered lists follow the
   new order.
5. As a Manager or Admin, I can delete a column that has no items.
   If it has items, I must first choose a destination column and the
   items are moved there as part of the same confirmed action ("Move
   12 items to 'In Progress' and delete 'QA'?").
6. As any user, the Kanban board, status dropdowns, chips, and filters
   for a project always show that project's own columns — two projects
   with different workflows display independently and correctly.

## Acceptance criteria

### Management rules (server-enforced)
- Only Manager/Admin can manage a project's workflow (UI hides it from
  Developers; direct API attempts are refused).
- Name uniqueness is per project, case-insensitive; clear error on
  duplicates.
- A project can never end up with zero Open statuses or zero
  Done-category statuses; attempts are refused with a clear message.
- Deleting the destination-selection flow is atomic: either the items
  move and the column is deleted, or nothing changes.
- Maximum 10 columns per project (guard against runaway boards).

### Status chips & colors
- With arbitrary column names, fixed per-status colors no longer
  suffice: each status gets a color chosen at creation from the design
  system's approved chip palette (defaulting sensibly: Open statuses
  cycle the palette's open hues; Done uses the green family). Rename
  keeps the color; color can be changed in management.
- Chip colors remain consistent for a given status everywhere it
  appears.

### Board & existing views
- The board renders each project's columns and counts from its own
  list — since Feature 005 required render-from-a-list, this should
  need no structural board change.
- Per-column "+ Add" pre-selects that column's status.
- Tree view's "n/m done" and any open-item counts use category, not
  name.
- Filters list the project's own statuses.

### Migration
- Runs automatically; after it, every pre-existing project shows the
  standard four columns with all items exactly where they were.
- Verified by tests that snapshot pre/post state on representative
  data.

### Non-functional
- All workflow rules enforced server-side; ProblemDetails errors.
- Existing test suites pass; new pure logic (category reasoning,
  delete-with-move, ordering) is test-first.

## Out of scope (do NOT include in this feature)

- WIP limits per column
- Column-level permissions (who may move into a column)
- Workflow templates / copying a workflow between projects
- Transition rules (e.g., "can only move from QA to Done") — any
  column to any column remains allowed
- Manual card ordering within columns

## Success check

Feature is complete when: a Manager adds a "QA" column to one project
and renames "In Progress" to "Doing", and the board, chips, dropdowns,
and filters reflect it while a second project remains unchanged;
dragging a card into "QA" works; deleting "QA" with 3 items in it
forces choosing a destination and moves the items atomically; a
Developer cannot open or call workflow management; open-item counts
and tree "n/m done" respect categories with the custom names; and all
pre-existing projects came through the migration with the standard
four columns and no item state changes.
