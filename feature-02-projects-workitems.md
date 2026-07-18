# Feature 02 — Projects & Work Items (paste this into /speckit-specify)

Add projects and flat work items (tasks) to TaskFlow PMS so teams can
organize and track their work.

## Why (context)

TaskFlow now has authenticated users with roles (Feature 001). But there
is nothing to manage yet. This feature adds the two core nouns of the
product: **Projects** (containers of work) and **Work Items** (the units
of work inside them). This slice is deliberately FLAT: a work item has a
type label (Epic / Story / Task / Sub-task) but no parent-child
hierarchy yet — hierarchy rules arrive in a later feature. Sprints,
boards, and comments also come later. This feature is the foundation
they will all build on.

## Roles recap (from Feature 001)

- **Developer** — default role; works on items.
- **Manager** — plans work; can create projects.
- **Admin** — full control.

## User stories

1. As a Manager or Admin, I can create a project with a name and
   description so my team has a place to track work.
2. As any signed-in user, I can see the list of all projects and open a
   project to view its work items. (Team/membership restrictions are a
   future feature; for now the whole company can see everything.)
3. As any signed-in user, I can create a work item inside a project
   with: type (Epic / Story / Task / Sub-task), title, description
   (optional), priority (Low / Medium / High / Critical), status
   (To Do / In Progress / Done), optional assignee (any user), and
   optional due date.
4. As any signed-in user, I can edit a work item's fields and change
   its status, so the board reflects reality. (Drag-and-drop Kanban is
   a later feature — for now status changes via the edit form or a
   status control on the item.)
5. As a Manager or Admin, I can edit or delete a project. Deleting a
   project deletes its work items, after an explicit confirmation that
   states how many items will be removed.
6. As any signed-in user, I can filter and search a project's work
   items (by status, type, priority, assignee, and title text) and page
   through long lists, so I can find things quickly.

## Acceptance criteria

### Projects
- Name: required, 3–100 chars, unique per system (case-insensitive).
  Duplicate name error: "A project with this name already exists."
- Description: optional, up to 2000 chars.
- Every project records who created it and when.
- Create/edit/delete project: Manager and Admin only. Developers
  attempting these actions (by UI or direct API call) are refused.
- Viewing projects and their items: any authenticated user.
- Project list shows: name, creator, created date, and a count of open
  (not Done) work items; sorted by most recently created first;
  paginated.
- Deleting a project requires a confirmation that includes the number
  of work items that will be deleted with it (e.g. "This will also
  delete 12 work items."). After deletion, its items are gone.

### Work items
- Title: required, 3–200 chars. Description: optional, up to 5000
  chars.
- Type is one of: Epic, Story, Task, SubTask. Type is a label only in
  this feature — no parent/child rules yet.
- Priority is one of: Low, Medium, High, Critical (default Medium).
- Status is one of: ToDo, InProgress, Done (default ToDo).
- Assignee: optional; must be an existing, current user when set.
- Due date: optional; when set it may be any date (past dates allowed —
  imported/overdue work is legitimate).
- Every work item belongs to exactly one project and records who
  created it and when, and when it was last updated.
- Any authenticated user can create a work item in any project.
- Editing: the item's creator, its current assignee, and any Manager
  or Admin can edit all fields; other users cannot edit (refused at
  the API as well as hidden/disabled in the UI).
- Deleting a work item: creator, Manager, or Admin only, with a simple
  confirmation.
- A work item's project cannot be changed after creation (moving items
  between projects is out of scope for now).

### Listing, filtering, search
- A project's work-item list is paginated (page + pageSize, default
  20, max 100).
- Filters: status, type, priority, assignee (each optional, combinable).
- Search: case-insensitive substring match on title.
- Default sort: most recently updated first.
- Empty states are explicit: a project with no items shows "No work
  items yet"; a filter with no matches shows "No items match your
  filters."

### Non-functional
- All new endpoints require authentication; role rules above are
  enforced server-side (frontend hiding is UX only, per constitution).
- All error responses keep the established ProblemDetails shape.
- Counts and lists a user sees must reflect their action immediately
  after create/edit/delete (no stale list after returning from a form).

## Out of scope (do NOT include in this feature)

- Parent/child hierarchy and its validation rules (next feature)
- Sprints, backlog, story points
- Kanban board with drag-and-drop (status changes are form-based here)
- Comments, attachments, activity log
- Project membership/teams and per-project permissions
- Moving a work item to a different project
- Editing or deleting projects by Developers
- Real-time updates (SignalR comes in a later phase)

## Success check

Feature is complete when: a Manager can create a project; a Developer
can open it, add a Task with priority High assigned to themselves, see
it in the list, filter the list to High priority and find it, edit it
to Done; a Developer cannot create or delete a project by any means
(UI or direct API); a Manager deleting a project sees the item-count
confirmation and afterward the project and its items are gone; and all
lists paginate and refresh correctly after changes.
