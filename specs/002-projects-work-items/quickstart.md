# Quickstart: Validate Projects & Work Items

## Prerequisites

- Feature 001 already set up and working (seeded Admin account, backend and
  frontend dev servers running — see `specs/001-user-auth/quickstart.md`).
- At least one Manager-role account: sign in as the seeded Admin, use the
  Users page to promote a Developer account to Manager (or promote your own
  test account), per Feature 001.

## Setup

```
# Backend: apply the new migration, then run the API
dotnet ef database update --project backend/TaskFlow.Api
dotnet run --project backend/TaskFlow.Api

# Frontend: run the Angular dev server (if not already running)
npm run start --prefix frontend
```

## Validation scenarios

Each scenario below maps directly to a user story in `spec.md`. Endpoint
shapes are documented in `contracts/`; entity shape in `data-model.md`.

1. **Create a project** (User Story 1): Signed in as a Manager, create a
   project with a unique name. Expect: it appears at the top of the
   project list, showing your name as creator and today's date. Try
   creating another project with the same name in different case →
   expect "A project with this name already exists." Sign in as a
   Developer and attempt to create a project → expect the action refused
   (and no create control shown in the UI).

2. **View projects and work items** (User Story 2): As any signed-in user,
   open the project list — confirm it shows name, creator, created date,
   and open (not-Done) item count for every project, paginated. Open a
   project with no work items yet → expect "No work items yet."

3. **Create a work item** (User Story 3): Inside a project, create a work
   item with just a title and type — expect it to appear in the item list
   immediately, with priority defaulted to Medium and status to ToDo. Set
   an assignee and a due date in the past → both accepted.

4. **Edit or delete a work item** (User Story 4): As the item's creator,
   change its status to Done via the edit form — confirm the change is
   reflected in the list right away. Sign in as a user who is neither its
   creator, assignee, nor a Manager/Admin → confirm edit controls are
   hidden, and a direct API call to update it is refused. As the item's
   creator, delete a different item — expect a simple confirmation, and
   the item gone from the list once confirmed. Sign in as that item's
   current assignee (who is not also its creator, nor a Manager/Admin) →
   confirm the delete control is hidden (narrower than edit, FR-018), and a
   direct API call to delete it is refused.

5. **Edit or delete a project** (User Story 5): As a Manager, open a
   project containing several work items and request to delete it —
   expect a confirmation stating the exact number of work items that will
   be removed. Confirm → expect the project and all its items gone from
   their respective lists. As a Developer, attempt to edit or delete a
   project (UI and direct API call) → expect both refused.

6. **Filter, search, and page** (User Story 6): In a project with several
   work items of mixed priority and status, filter to a single priority →
   expect only matching items. Search a substring of a title → expect only
   matching items. Apply a filter that matches nothing → expect "No items
   match your filters." With more items than one page holds, page through
   the list → expect correct slices, most-recently-updated first by
   default.

7. **Full success check** (spec.md Success check): A Manager creates a
   project; a Developer opens it, adds a Task with priority High assigned
   to themselves, sees it in the list, filters to High priority and finds
   it, edits it to Done. A Developer cannot create or delete a project by
   any means (UI or direct API call). A Manager deleting a project sees
   the accurate item-count confirmation, and afterward the project and its
   items are gone. All lists paginate and refresh correctly after each
   change, with no manual page reload required.
