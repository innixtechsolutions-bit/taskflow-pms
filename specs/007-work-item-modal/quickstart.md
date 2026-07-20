# Quickstart: Validating Work Item Modal & Quick Creation

Prerequisites: backend running (`dotnet run` in `backend/TaskFlow.Api`) against
a SQL Server instance with the new migration applied
(`dotnet ef database update`), frontend running (`npm start` in `frontend/`),
signed in as a user with at least one project containing a few work items
across a couple of workflow columns.

Each scenario below maps to an acceptance scenario in `spec.md`. Field/route
names reference `data-model.md` and `contracts/work-item-modal-api.md`.

## 1. Modal replaces full-page create/edit (User Story 1)

1. Open a project's Board. Note its scroll position and any active filters.
2. Click a column's "+" affordance.
   - **Expect**: a modal dialog opens over the board (no navigation); the
     Status field is pre-set to that column.
3. Fill in a Title, submit.
   - **Expect**: modal closes, a success toast appears, the new card appears
     in the correct column, board scroll position/filters unchanged (no full
     reload).
4. From an item's detail view, click "Add child".
   - **Expect**: modal opens with Parent pre-set to that item and Type
     restricted to the legal child type; parent-candidates list matches
     `GET .../work-items/parent-candidates?type=`.
5. Open the modal, change a field, then press Escape.
   - **Expect**: a confirm-discard prompt appears before the modal closes.
6. Open the modal, change nothing, press Escape.
   - **Expect**: modal closes immediately, no prompt.
7. Submit the modal with an invalid combination (see scenario 3 below) —
   **expect** the error renders inside the modal; it stays open with entered
   values intact.
8. From an item's detail view or a list row, click "Edit".
   - **Expect**: the same modal opens, pre-populated with that item's current
     values.
9. Navigate directly to the old URL pattern `.../work-items/new` (bookmark or
   typed manually).
   - **Expect**: redirected to the project view (`contracts/work-item-modal-api.md`
     — no dead page). Same check for `.../work-items/:id/edit` → redirects to
     that item's detail view.

## 2. Assign to me (User Story 2)

1. Open the create modal. Click "Assign to me".
   - **Expect**: Assignee field is set to the current signed-in user without
     opening the assignee dropdown.
2. Open the edit modal on an item assigned to someone else. Click "Assign to me".
   - **Expect**: Assignee field switches to the current user; not persisted
     until Submit.

## 3. Start date validation (User Story 3)

1. Open the modal, set Due date to a date, set Start date to an earlier or
   equal date, submit.
   - **Expect**: saves successfully; item's detail view shows both dates.
2. Open the modal, set Start date to a date *after* the Due date, attempt submit.
   - **Expect**: inline validation message, submission blocked (client-side).
3. Repeat scenario 2 bypassing the client (e.g. `curl`/Postman `PUT` with
   `startDate` after `dueDate`) directly against the API.
   - **Expect**: `400` `ProblemDetails` with `InvalidDateRangeException`'s
     message (`contracts/work-item-modal-api.md`), proving server-side
     enforcement independent of the UI.

## 4. Create another (User Story 4)

1. Open the create modal, check "Create another".
2. Fill Title + a couple of other fields (Type, Status, Assignee, a Start/Due
   date), submit.
   - **Expect**: success toast, modal stays open, Title/Description are
     blank, Type/Status/Priority/Assignee/Parent/Start date/Due date retain
     their prior values, Labels are cleared (research.md #8).
3. Repeat 2–3 more times without closing the modal.
   - **Expect**: each create produces its own toast and the underlying board/
     list view updates live after each one (research.md #9), not only after
     the modal finally closes.
4. Uncheck "Create another", submit once more.
   - **Expect**: modal closes as in User Story 1.

## 5. Labels (User Story 5)

1. Open the modal on a project with no existing labels. Type "backend" into
   the Labels field and confirm it.
   - **Expect**: the value is accepted (a new project-scoped label is
     created on submit).
2. Submit. Check the created item's card (board), row (list view), row (tree
   view), and detail page.
   - **Expect**: a "backend" chip appears on all four surfaces.
3. Open the modal on a *different* item in the same project, type "back".
   - **Expect**: "backend" appears as a suggestion (sourced from
     `GET /api/projects/{projectId}/labels`).
4. Try to attach a 6th label to an item that already has 5.
   - **Expect**: blocked with a clear inline message (`TooManyLabelsException`
     server-side if bypassed).
5. Type "Backend" (different casing) as a label on another item.
   - **Expect**: reuses the existing "backend" label — no duplicate created
     (verify by checking the suggestions list still shows only one "backend").
6. In the List view, open the label filter and select "backend".
   - **Expect**: only items carrying that label are shown; combinable with
     the existing status/type/priority/assignee filters.

## Regression check

Run the full existing suites and confirm no prior test breaks:

```
# backend
dotnet test backend/TaskFlow.Api.Tests

# frontend
npm test --prefix frontend
```

All prior counts (285/285 backend, 189/189 frontend as of Feature 006) plus
this feature's new tests should pass.
