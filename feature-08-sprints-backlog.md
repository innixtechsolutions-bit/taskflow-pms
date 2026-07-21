# Feature 08 — Sprints & Backlog (paste into /speckit-specify)

Add sprints to each project — a Backlog view for planning which work
happens when, and a sprint-scoped Board for running the active sprint.

## Why (context)

TaskFlow has projects, hierarchical work items, custom workflow columns,
and a Kanban board that shows everything in a project at once. Real
teams work in time-boxed sprints: a Backlog holds unscheduled work,
items get planned into upcoming sprints, one sprint is Active at a time,
and the Board should be able to show just that sprint's work. This
feature adds Sprint as a new concept and a Backlog screen for planning,
while keeping the existing all-items Board as-is by default and adding
a sprint-scoped mode to it.

## Core concept: Sprint

A sprint belongs to one project and has:
- a name (e.g. "Sprint 3"), unique within the project
- a start date and end date (end after start)
- a status: **Planned**, **Active**, or **Completed**

Rules:
- A project may have at most **one Active sprint** at a time.
- Starting a sprint (Planned → Active) requires at least one work item
  in it and sets it Active (any other Active sprint in the project must
  be completed first — the UI prevents starting a second one).
- Completing a sprint (Active → Completed) requires choosing what
  happens to its **not-Done** items: move them to the backlog (no
  sprint) or move them to a specific other Planned/Active sprint. Items
  already Done stay attached to the completed sprint (for history).
  Completed sprints are read-only afterward (no further item changes
  via the sprint relationship — items can still be edited normally).
- A sprint with zero items cannot be started, and an empty Planned
  sprint can be deleted; a sprint that has ever been started cannot be
  deleted (Completed sprints are kept for history).

## User stories

1. As a Manager or Admin, I can create a sprint for a project with a
   name and date range, so I can plan upcoming work.
2. As any signed-in user, I can open a project's Backlog view and see
   its sprints (soonest first) each listing their planned items, plus
   an unscheduled Backlog section below, so I can see everything not
   yet Done organized by when it's planned.
3. As any signed-in user with edit rights on an item, I can move a work
   item into a sprint or back to the backlog from the Backlog view
   (drag-and-drop, consistent with the Board's drag interaction), so
   planning is fast.
4. As a Manager or Admin, I can start a Planned sprint (making it
   Active) and later complete an Active sprint, choosing where its
   remaining open items go, so the sprint lifecycle progresses.
5. As any signed-in user, I can switch the project's Board to show
   only the Active sprint's items (instead of everything), so my daily
   board matches what the team committed to.
6. As any signed-in user, I can see at a glance how many days remain
   in the Active sprint (or that it's overdue) from both the Backlog
   and the sprint-scoped Board.

## Acceptance criteria

### Sprint management
- Create: name (2–50 chars, unique per project), start date, end date
  (must be after start). Only Manager/Admin; server-enforced.
- A newly created sprint is Planned.
- Only Epics are excluded from sprint assignment (Epics are containers
  spanning multiple sprints); Story/Task/SubTask may belong to a sprint.
- Start requires at least 1 item and no other Active sprint in the
  project (clear error naming the currently Active sprint if one exists).
- Complete requires resolving not-Done items per the rule above;
  the confirmation names how many items are moving and to where.
- Delete allowed only for empty, never-started Planned sprints.

### Backlog view
- New tab/view alongside Board/List/Tree (per project).
- Sprints listed soonest-start first; each section shows sprint name,
  date range, days-remaining/overdue indicator (Active only), item
  count, and a Start/Complete action appropriate to its status.
- Backlog section (items with no sprint) below all sprint sections,
  same filters as List view (status, type, priority, assignee, search).
- Dragging an item between a sprint section and the Backlog section
  (or between two Planned/Active sprints) updates its sprint
  assignment; the same edit-permission and optimistic-update-with-
  revert behavior as the Board's drag applies.
- Epics render for context (read-only placement) but are not
  draggable into sprints.

### Sprint-scoped Board
- A toggle on the Board view: "All items" (today's default behavior,
  unchanged) vs "Active sprint" (only the project's Active sprint's
  items, same columns/drag/permissions as today).
- If no sprint is Active, this mode shows a clear empty state
  explaining that and links to the Backlog to start one.
- The days-remaining/overdue indicator shows here too.

### Non-functional
- All existing Board/List/Tree behavior for "all items" is unchanged.
- Sprint mutations are server-enforced (Manager/Admin for
  create/start/complete/delete; item move follows existing edit rules).
- ProblemDetails errors; ordered lists, chip colors, and dates follow
  established design-system conventions.
- Pure logic (days-remaining/overdue calculation, complete-sprint
  item-resolution, start/complete eligibility) is test-first.

## Out of scope (do NOT include in this feature)

- Sprint velocity/burndown charts, story-point estimation
- Editing a sprint's name/dates after creation (delete-and-recreate
  covers the Planned case; Active/Completed sprints don't need this
  yet)
- Multiple Active sprints per project
- Recurring/auto-created sprints
- Assigning Epics to sprints
- Real-time multi-user updates to the Backlog (SignalR phase later)

## Success check

Feature is complete when: a Manager creates "Sprint 1" with a date
range; the Backlog shows it above a Backlog section; a Task is
dragged from Backlog into Sprint 1; Sprint 1 is started (becomes
Active, days-remaining shows); the Board's "Active sprint" mode shows
only Sprint 1's items; completing Sprint 1 with 2 items still open
prompts for a destination and moves them there; a second sprint cannot
be started while Sprint 1 is Active; an Epic appears in the Backlog for
context but cannot be dragged into a sprint; and the existing "All
items" Board/List/Tree views behave exactly as before.
