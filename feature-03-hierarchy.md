# Feature 03 — Work Item Hierarchy (paste this into /speckit-specify)

Give work items a parent-child hierarchy (Epic → Story → Task → Sub-task)
so large work can be broken down and tracked as a tree.

## Why (context)

Feature 002 gave work items a Type label (Epic / Story / Task / SubTask)
but no relationships between them — every item is flat. Real teams break
an Epic into Stories, Stories into Tasks, Tasks into Sub-tasks. This
feature adds that structure and the rules that keep it sane. It is the
foundation for the task-details view (subtasks tab), boards, and sprint
planning in later features.

## The hierarchy rules (the heart of this feature)

The chain is strict and skips no levels:

- **Epic** — top level only; can never have a parent.
- **Story** — parent must be an Epic.
- **Task** — parent is optional; when set, it must be a Story.
  (Standalone Tasks with no parent remain legal — small teams live on
  loose tasks, and every existing Feature 002 item stays valid.)
- **SubTask** — parent is required and must be a Task.

Additional structural rules:
- Parent and child must belong to the same project.
- An item cannot be its own ancestor (no cycles).
- An item's type cannot change if the change would make its existing
  parent or children invalid (e.g., a Story with Sub-... a Task that
  has SubTask children cannot become a Story).
- Deleting an item that has children requires a confirmation stating
  how many descendants will be deleted with it, and deletes the whole
  subtree (consistent with Feature 002's project-delete behavior).

## User stories

1. As any signed-in user, when creating a work item I can pick a parent
   (from a list of valid candidates only — the form never offers me an
   illegal parent), so the tree is correct by construction.
2. As any signed-in user, I can change or clear an existing item's
   parent (subject to the same rules and the same edit permissions as
   Feature 002), so I can reorganize work.
3. As any signed-in user, viewing a project's work items I can see the
   hierarchy at a glance — children indented under parents, with
   expand/collapse per parent — while standalone items list normally.
4. As any signed-in user, opening a work item I can see its parent
   (as a link) and a list of its direct children with their status,
   so I can navigate the tree in both directions.
5. As any signed-in user, I can still use the flat filtered list from
   Feature 002 when I want it — filtering/searching returns matching
   items regardless of tree position (a tree view and a flat view are
   both available; filters apply to the flat view).

## Acceptance criteria

### Parent assignment
- The parent picker only ever offers valid candidates: correct type
  per the chain, same project, and not the item itself or any of its
  own descendants.
- Attempting an invalid parent via direct API call (wrong type, other
  project, cycle, missing required parent for SubTask) is refused with
  a clear ProblemDetails error naming the violated rule.
- Existing Feature 002 items (all parentless) remain valid without any
  data migration beyond adding the new column.

### Tree display (project view)
- Children render indented under their parent with expand/collapse;
  collapse state need not persist across reloads.
- Each parent row shows a count of its direct children and how many
  are Done (e.g., "3/5 done").
- Items are ordered within each level by most recently updated first,
  consistent with Feature 002.

### Work item detail
- Shows the parent as a navigable link when one exists.
- Shows direct children with title, type, status, assignee; each links
  to that child's detail.
- Creating a child from the detail view pre-selects this item as the
  parent when this item's type can legally have children.

### Deletion with descendants
- The confirmation states the total number of descendants (all levels)
  that will be deleted, e.g. "This will also delete 4 nested items."
- Delete permissions per item are unchanged from Feature 002
  (creator / Manager / Admin); the permission check applies to the
  item being deleted, and the subtree goes with it.

### Non-functional
- All rules enforced server-side; the UI's filtered pickers are UX
  only, per constitution.
- ProblemDetails error shape maintained.
- Existing Feature 002 flat list, filters, and tests keep working.

## Out of scope (do NOT include in this feature)

- Reordering siblings / manual sort order
- Moving items between projects
- Progress roll-up beyond the direct-children "n/m done" count
  (e.g., no recursive percentage on Epics yet)
- Drag-and-drop tree manipulation (Kanban and DnD come later)
- Custom hierarchy levels or renaming the four types

## Success check

Feature is complete when: a user creates an Epic, a Story under it, a
Task under the Story, and a SubTask under the Task, and the project
view shows the indented tree with correct child counts; the parent
picker never offers an Epic as a parent for a SubTask (and a direct
API attempt returns a clear error); changing the Task's parent to
another Story works while changing it to an Epic is refused; deleting
the Story warns it will also delete its 2 nested items and afterward
the whole subtree is gone while the Epic remains; and Feature 002's
flat filtered list still passes all its existing behavior.
