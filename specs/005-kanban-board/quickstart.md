# Quickstart: Validating the Kanban Board

Manual/end-to-end validation steps to confirm the feature meets spec.md's
Success Criteria after implementation. Run these after `/speckit-implement`
and before considering the feature done, per Constitution Principle VIII.

## Prerequisites

- Backend running (`backend/TaskFlow.Api`) against the local SQL Server
  instance.
- Frontend running (`ng serve` in `frontend/`).
- A project seeded with a realistic mix: items in all four statuses,
  at least one Epic with children (some done, some not), at least one
  unassigned item, at least one item with a due date in the past and one
  in the future, and at least one item created by / assigned to a
  Developer-role test user (to exercise the permission checks) plus one
  neither created by nor assigned to that user.
- Two browser sessions (or one + an incognito window) signed in as
  different roles — e.g. a Developer who owns/isn't assigned some items,
  and a Manager or Admin.

## 1. In Review exists everywhere (US1, SC-007)

1. Open a work item's edit form. Confirm the Status dropdown reads To Do,
   In Progress, In Review, Done in that order.
2. Set an item to In Review and save. Confirm its chip (in the flat list,
   the tree view, and its own detail page) renders in one consistent
   color, distinct from the other three statuses.
3. On the project's flat view, filter by In Review. Confirm only In
   Review items are returned.
4. Note the project's open work item count before and after moving an
   item to In Review from To Do — the count should be unchanged (both
   statuses count as open).
5. Confirm every work item that existed before this feature's deployment
   still shows its original status (spot-check a few) — nothing was
   remapped.

## 2. View the board (US2, SC-001, SC-004)

1. Open a project's detail page. Confirm a Board option appears alongside
   Tree and Flat, and selecting it replaces the Tree/Flat content with
   four columns in order: To Do, In Progress, In Review, Done.
2. Confirm each column header shows its status name and a count matching
   the number of cards actually in that column.
3. For a sampled card in each column, confirm without opening it: title
   (wrapped/truncated if long), type, a priority chip, an assignee
   avatar or an "unassigned" indicator, and a friendly-formatted due date
   (e.g. "Jul 20, 2026", never a raw ISO string).
4. Confirm a card whose due date is in the past AND whose status is not
   Done is visually flagged as overdue; confirm a Done item with a past
   due date is NOT flagged, and an item due today or in the future is not
   flagged.
5. Confirm a card for an item with children shows "n/m done"; confirm a
   card for an item with no children shows no such indicator.
6. Confirm an Epic appears on the board like any other item, correctly
   labeled by type.
7. Resize the window / add enough cards to one column to overflow the
   viewport height — confirm that column scrolls independently. If the
   four columns together exceed the viewport width, confirm the board
   scrolls horizontally without disturbing the sidebar.
8. Switch to Tree, then back to Board, without a full page reload —
   confirm Board is still selected. Reload the page — no requirement that
   Board survives the reload.

## 3. Drag to change status (US3, SC-002, SC-003)

1. As a user permitted to edit a given item (its creator, its assignee,
   or a Manager/Admin), drag its card to a different column. Confirm it
   moves immediately, and reopening the item (or checking the flat list)
   confirms the status actually changed.
2. Temporarily break connectivity (or use browser dev tools to force the
   PATCH request to fail) and repeat a drag. Confirm the card snaps back
   to its original column and an error toast explains the save failed.
3. As a user who is neither the creator nor the assignee of a given item,
   and not a Manager/Admin, confirm that item's card cannot be dragged at
   all (or, if dragged, immediately reverts) with a clear explanation —
   and confirm the card never actually changes column.
4. With a tool like `curl`/Postman, call `PATCH api/work-items/{id}/
   status` directly as that same unauthorized user (bypassing the board
   UI entirely). Confirm the server responds 403 and the item's status is
   unchanged — the UI's permission check is not the only thing stopping
   this.
5. Open that same unauthorized item's detail/edit page instead. Confirm
   status can still be changed there by someone who *is* permitted — drag
   is not the only path to a status change.

## 4. Add from a column (US4, SC-005)

1. Click "+ Add" in the In Review column. Confirm the create form opens
   with Status pre-selected to In Review and the correct project already
   set.
2. Submit it. Confirm the new card appears in the In Review column
   without a page reload.

## 5. Card → detail → back to board (US5)

1. From the Board view, click a card (not a drag gesture). Confirm the
   correct work item's detail page opens.
2. Navigate back. Confirm the project detail page shows Board view again,
   not Tree or Flat.

## 6. Regression: full test suites (FR-024, SC-006)

```bash
# Backend
cd backend/TaskFlow.Api.Tests
dotnet test

# Frontend
cd frontend
npm test
```

Both MUST report 100% of pre-existing tests passing, plus the new tests
this feature adds.

## 7. Keyboard focus (carried over from Feature 004's shell)

Using only the keyboard, confirm the Board view toggle and each column's
"+ Add" affordance remain focusable and operable — drag-and-drop is not
required to be keyboard-operable itself (FR-016), but everything else on
the page must remain so.
