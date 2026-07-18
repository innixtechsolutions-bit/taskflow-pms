# Quickstart: Validating the App Shell & Design System

Manual/end-to-end validation steps to confirm the feature meets spec.md's
Success Criteria after implementation. Run these after `/speckit-implement`
and before considering the feature done, per Constitution Principle VIII.

## Prerequisites

- Backend running (`backend/TaskFlow.Api`) against the local SQL Server
  instance, with at least two seeded/created users of different roles
  (e.g. one `Admin`, one `Developer`) and one project with a small mix of
  work items (different statuses/priorities, at least one with an assignee,
  at least one project with **zero** work items).
- Frontend running (`ng serve` in `frontend/`).
- A browser window resizable to at least 1440px wide and down to ~1024px and
  ~768px for the tablet-breakpoint check.

## 1. Shell applies everywhere (SC-001)

1. Log in as the Admin user. Confirm you land inside the sidebar + content
   shell, not a bare page.
2. Visit Dashboard, Projects, a project detail page, a work item detail page,
   and Users. Confirm every one renders inside the same shell with a page
   header (title + subtitle, and an action button where applicable — e.g.
   "New Project" on Projects list).
3. Log out, then visit `/login` and `/register` directly. Confirm neither is
   wrapped in the shell (centered standalone forms, as before).

## 2. Navigation & role visibility (US2)

1. As Admin, confirm the sidebar shows Dashboard, Projects, and Users, with
   the current section highlighted as active, and a bottom user block
   showing avatar, name, and role.
2. Click each nav link; confirm the active highlight follows.
3. Log out via the bottom user menu; confirm you land back on `/login`.
4. Log in as a `Developer`-role user; confirm the Users nav item is **not**
   present. Attempt to navigate to `/users` directly by URL; confirm the
   existing `adminGuard`/403 behavior still applies unchanged (this feature
   must not have altered it).

## 3. Chip and avatar consistency (US3, SC-002, SC-003)

1. Open a project's detail page in tree view. Note the status/priority chip
   colors and an assignee's avatar color/initials for one work item.
2. Switch to the flat filtered list view of the same project. Confirm the
   same work item shows identical chip colors/labels and the identical
   avatar color/initials.
3. Open that work item's detail page. Confirm the same chip colors/labels
   and avatar again.
4. Confirm the same user's avatar (if they appear as assignee on multiple
   work items, or as the signed-in user in the sidebar) always renders the
   same background color everywhere.

## 4. Friendly dates, no raw ISO (SC-006)

1. On Projects list, project detail, and Users list, confirm every visible
   date renders like `"Jul 17, 2026"` — not an ISO timestamp
   (`2026-07-17T00:00:00Z`-style string).
2. If any date field is null/not set, confirm it renders a placeholder
   (e.g. `"—"`), not a blank or malformed cell.

## 5. Full-width list pages (SC-007)

1. At a browser width of ~1440px or more, open Projects list, Users list,
   and a project's flat work-item list. Confirm the primary content (card
   grid or table) visibly fills most of the available content width — not a
   narrow column hugging the left edge with large empty space to the right.
2. Confirm a page with very few rows (e.g. a project with a single work
   item) still uses the full sensible content width for its layout, rather
   than shrinking to fit the row count.

## 6. Tablet breakpoint collapse (SC-004)

1. Resize the browser window down to ~1024px, then ~768px. Confirm the
   sidebar collapses to an icons-only rail without a page reload.
2. Confirm nav items remain operable (clickable, with accessible
   tooltips/labels) in the collapsed state.
3. Confirm no horizontal scrollbar appears on the shell at any width from
   ~768px up through ~1920px.
4. Resize back above the breakpoint; confirm the sidebar expands back to
   icon+label.

## 7. Feedback & empty states (US4, SC-005)

1. Create a new project. Confirm a success toast appears within about a
   second.
2. Edit and then delete a work item. Confirm a success toast for each
   action.
3. Force a failure (e.g. submit an invalid edit, or simulate a network
   error) and confirm an error toast appears instead of silent failure.
4. Open the project with zero work items. Confirm a friendly empty state
   (icon + message + "Add work item" action) instead of plain text.
5. On the Users list, filter to a state with zero results (if filters
   exist) and confirm a matching empty state.

## 8. Regression: full test suites (FR-018, SC-008)

```bash
# Backend
cd backend/TaskFlow.Api
dotnet test

# Frontend
cd frontend
npm test
```

Both MUST report 100% of pre-existing tests passing (test count may only
decrease due to selector updates explicitly tied to template structure
changes called out in the implementation, never due to deleted coverage).

## 9. Keyboard focus (FR-016)

1. Using only the keyboard (Tab/Shift+Tab), navigate through the sidebar
   nav links, the user menu, and a page's primary action button. Confirm a
   visible focus indicator on every stop.
