# Quickstart: Validating Custom Workflow Columns

Manual/end-to-end validation steps to confirm the feature meets spec.md's
Success Criteria after implementation. Run these after `/speckit-implement`
and before considering the feature done, per Constitution Principle VIII.

## Prerequisites

- Backend running (`backend/TaskFlow.Api`) against the local SQL Server
  instance, with the new migration applied.
- Frontend running (`ng serve` in `frontend/`).
- Two existing projects (from before this feature shipped) with a mix of
  work items across all four original statuses, including at least one
  project with items that have children (for "n/m done" checks).
- Two browser sessions (or one + an incognito window) signed in as
  different roles — a Developer, and a Manager or Admin.

## 1. Migration preserved everything (US1, FR-006, SC-003)

1. For each pre-existing project, open its Board view. Confirm exactly
   four columns appear: To Do, In Progress, In Review (Open), Done
   (Done), in that order, with the same visual colors as before this
   feature shipped.
2. Spot-check several work items that existed before deployment (flat
   list, tree view, detail page) — confirm each still shows its exact
   original status, unchanged.
3. Confirm the project's open-item count (projects list page) is
   unchanged from before deployment for a project where no items moved.

## 2. Two projects are fully independent (US1, SC-007)

1. Note both projects' status lists are identical initially (the
   standard four).
2. After completing section 3 below on Project A only, revisit Project
   B's board, dropdowns, and filters — confirm nothing about Project B
   changed.

## 3. View the Workflow screen (US2, SC-006)

1. As a Manager or Admin, open Project A and find the "Workflow" entry
   point (alongside the existing Edit/Delete project links). Open it.
2. Confirm all four statuses appear in position order, each showing its
   name, category, and current item count matching the board's column
   counts.
3. Sign in as a Developer. Confirm no "Workflow" entry point is visible
   on the project page.
4. As that Developer, attempt to call `GET
   /api/projects/{id}/statuses/../reorder` or any mutating
   endpoint directly (e.g. via browser dev tools or curl) with a valid
   JWT — confirm the mutating calls return 403; the plain `GET` list may
   succeed (it's read-only for any authenticated user, per
   contracts/workflow-api.md).

## 4. Add a column (US3, FR-010, SC-001)

1. As a Manager, add a column named "QA" with category Open.
2. Confirm it appears on the Workflow screen positioned just before
   Done, without needing to specify a position explicitly.
3. Open Project A's board — confirm a "QA" column now appears at the
   expected position with 0 items and a "+ Add" affordance.
4. Open the work-item create form for Project A — confirm "QA" appears
   in the Status dropdown.
5. Click "+ Add" in the QA column; confirm the create form opens with
   Status pre-selected to QA. Submit it; confirm the new item appears in
   the QA column.
6. Attempt to add an 11th status (after adding 6 more) — confirm it's
   refused with a clear message.
7. Attempt to add a status named "qa" (case-different) — confirm it's
   refused as a duplicate.

## 5. Rename a column (US4, FR-011)

1. As a Manager, rename "In Progress" to "Doing".
2. Confirm the board's column header, every card previously in that
   column, the work-item form's Status dropdown, and the project-detail
   filter all now show "Doing" — immediately, no reload needed beyond
   normal navigation.
3. Confirm the color and the items in that column are unchanged.
4. Attempt to rename another status to "doing" (case-different) —
   confirm it's refused as a duplicate.

## 6. Reorder columns (US5, FR-012, SC-002)

1. On the Workflow screen, drag "QA" to appear before "Doing" instead of
   after it.
2. Confirm the board's column order, the Workflow screen's own order,
   and the work-item form's Status dropdown order all reflect the new
   sequence.
3. Confirm Project B's column order is unaffected.

## 7. Delete a column (US6, FR-013/FR-014, SC-005)

1. Delete "QA" while it has 0 items (if you emptied it, or use a fresh
   empty test column) — confirm it disappears immediately from the
   board, Workflow screen, dropdowns, and filters.
2. Add another column ("Blocked", Open) and move 3 items into it via the
   board (drag) or the create form.
3. Attempt to delete "Blocked" — confirm you're prompted to choose a
   destination column, and the confirmation message names the item
   count, destination, and column being deleted (e.g. "Move 3 items to
   'Doing' and delete 'Blocked'?").
4. Confirm it and verify: "Blocked" is gone, and all 3 items now show
   the destination column's status, on the board and in the flat list.
5. Attempt to delete the project's only remaining Open-category column
   (temporarily reduce to one, if needed, via prior deletes) — confirm
   it's refused, regardless of whether it has items.
6. Attempt the same for the only remaining Done-category column —
   confirm it's refused for the same reason.

## 8. Category-based reasoning, not name-based (FR-019)

1. Rename "Done" to "Complete" (still category Done).
2. Confirm the project's open-item count is unaffected by the rename
   (still correctly excludes "Complete" items).
3. Confirm a parent item's tree-view "n/m done" count still correctly
   counts children whose status is "Complete" as done.
4. Confirm a card whose status is "Complete" with a past due date is
   NOT flagged as overdue (still respects category, not the literal
   string "Done").

## 9. Regression check (SC-008)

Run the full backend (`dotnet test`) and frontend (`ng test`) suites —
confirm 100% pass, including all pre-existing tests from Features
001–005.
