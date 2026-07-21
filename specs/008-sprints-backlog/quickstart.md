# Quickstart: Validating Sprints & Backlog

Prerequisites: backend running (`dotnet run` in `backend/TaskFlow.Api`)
against a SQL Server instance with the new migration applied (`dotnet ef
database update`), frontend running (`npm start` in `frontend/`), signed in
as a Manager or Admin on a project that already has a few work items
(Epic/Story/Task mix) across a couple of workflow columns.

Each scenario maps to a user story in `spec.md`. Field/route names reference
`data-model.md` and `contracts/sprints-api.md`.

## 1. Create a sprint (User Story 1)

1. Open a project, switch to the Backlog view.
2. Use "Create sprint", name it "Sprint 1", pick a start/end date, submit.
   - **Expect**: a new section appears, status Planned, 0 items, Start
     button disabled (or shows a clear error naming "needs at least one
     item" if clicked).
3. Try creating a second sprint named "Sprint 1" (any casing) in the same
   project.
   - **Expect**: rejected — duplicate name.
4. Try an end date on/before the start date.
   - **Expect**: rejected — invalid date range.
5. Repeat step 2 signed in as a Developer (non-Manager/Admin).
   - **Expect**: no "Create sprint" affordance in the UI, and a direct API
     call is rejected server-side regardless.

## 2. View the Backlog (User Story 2)

1. With two Planned sprints created out of start-date order, open the
   Backlog view.
   - **Expect**: sections appear soonest-start-first, each showing name,
     date range, and item count.
2. Note the unscheduled "Backlog" section beneath all sprint sections.
   - **Expect**: every item with no sprint appears there, including any
     Epic (shown for context, not draggable — verify no drag handle/cursor
     change on an Epic row).
3. Apply a status/type/priority/assignee/search filter.
   - **Expect**: both sprint sections and the Backlog section filter
     consistently, the same items List view's same filter would show.

## 3. Move items between Backlog and sprints (User Story 3)

1. Drag a Story from the Backlog section into a Planned sprint's section.
   - **Expect**: immediate (optimistic) move; reload the page — the
     assignment persisted.
2. Drag it back to the Backlog section.
   - **Expect**: `sprintId` cleared.
3. Drag an item between two different Planned/Active sprint sections.
   - **Expect**: reassigned to the new sprint directly.
4. Simulate a failed move (e.g. stop the backend mid-drag, or edit the
   item's permissions in another tab first) and drag again.
   - **Expect**: the card visually reverts to its original section; an
     error toast appears (same pattern as the Board's drag failure).
5. Attempt to drag an item you don't have edit rights on (signed in as a
   user who is neither creator, assignee, nor Manager/Admin).
   - **Expect**: drag is disabled/prevented client-side; a direct `PATCH
   .../sprint` call is rejected `403` server-side regardless.
6. Attempt to drag any item into or out of a Completed sprint's section.
   - **Expect**: not permitted (Completed sections behave as read-only).

## 4. Sprint lifecycle: start and complete (User Story 4)

1. Drag one item into "Sprint 1", then click Start.
   - **Expect**: status becomes Active; the project's other Planned sprint
     (if any) still shows a Start button; Sprint 1's now shows Complete.
2. From a second Planned sprint, click Start while Sprint 1 is Active.
   - **Expect**: rejected, error names "Sprint 1" as the currently active
     sprint.
3. Move a second item into Sprint 1 and mark one of its two items Done
   (via the Board or item edit); leave the other not-Done. Click Complete.
   - **Expect**: a confirmation states 1 item is moving, and asks for a
     destination (Backlog or another Planned/Active sprint).
4. Choose "Move to backlog", confirm.
   - **Expect**: Sprint 1 becomes Completed; the not-Done item now has no
     sprint (appears in the Backlog section); the Done item still appears
     under Sprint 1's (now Completed, read-only) section.
5. Try dragging any item into or out of the now-Completed Sprint 1.
   - **Expect**: not permitted.
6. Create and start a new empty-after-drag sprint with 1 item, complete it
   with that item already Done (no not-Done items).
   - **Expect**: completes without prompting for a destination.
7. Create a second, never-started, empty Planned sprint and delete it.
   - **Expect**: removed. Attempt to delete Sprint 1 (already
     started/Completed).
   - **Expect**: rejected.

## 5. Sprint-scoped Board (User Story 5)

1. With Sprint 1 Active and containing 2 items (out of 5 total in the
   project), open the Board and toggle to "Active sprint".
   - **Expect**: only Sprint 1's 2 items appear, same columns/drag/edit
     behavior as "All items" mode.
2. Toggle back to "All items".
   - **Expect**: all 5 items reappear.
3. Complete Sprint 1 (no other sprint Active), then toggle to "Active
   sprint" again.
   - **Expect**: empty state explaining no sprint is active, with a link to
     the Backlog view.

## 6. Days remaining / overdue indicator (User Story 6)

1. With an Active sprint whose end date is a few days out, check its
   section header in the Backlog and the sprint-scoped Board.
   - **Expect**: "N days remaining" (or equivalent) shown in both places.
2. Create and start a sprint whose end date is in the past (or wait for an
   existing Active sprint's end date to pass).
   - **Expect**: an overdue indicator replaces the days-remaining count, in
     both places.
3. Check a Planned or Completed sprint's section.
   - **Expect**: no days-remaining/overdue indicator shown.

## Regression check (spec FR-020)

Run the existing Board/List/Tree scenarios from `specs/005-kanban-board` and
`specs/003-work-item-hierarchy`'s quickstarts against a project untouched by
this feature (no sprints created) — every behavior must be identical to
before this feature shipped.
