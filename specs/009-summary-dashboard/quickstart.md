# Quickstart: Validating the Summary Dashboard & Activity Log

Prerequisites: backend running against a SQL Server instance with the
`AddActivityLog` migration applied; frontend running (`ng serve`); at least
one seeded project with a handful of work items across a couple of
statuses/priorities/assignees (reuse the same seed data prior features'
quickstarts use).

## 1. Default tab and stat cards (US1)

1. Sign in, open any project's detail page **without** a `?view=` query
   param.
2. Confirm the tab row reads **Summary | Board | Backlog | List | Tree** and
   Summary is already selected.
3. Confirm the four stat cards (Total, Completed, In Progress, Due Soon)
   match a manual count of the seeded items (`GET
   /api/projects/{id}/summary` returns the same numbers — cross-check via
   the Network tab or a direct `curl`/Postman call with a bearer token).
4. Navigate to `?view=board` directly; confirm Board — not Summary — is
   shown (explicit view links still win, per FR-002).

## 2. Status & priority breakdowns (US2)

1. On a project with custom workflow columns (Feature 006), confirm the
   status donut's segments match that project's own column names/order/
   colors — not a fixed global list.
2. Confirm the priority bar chart always shows Low/Medium/High/Critical,
   including any level currently at zero.
3. Compare the same charts on a second, differently-configured project;
   confirm they differ and don't leak the first project's data.

## 3. Team workload (US3)

1. Assign several open items to two different users, leave one open item
   unassigned, and confirm a Manager or Admin with zero assigned items in
   this project still appears.
2. Confirm rows are ordered by open-item count descending and an
   "Unassigned" row appears with the correct count.

## 4. Activity log write path + project feed (US4)

1. Via the existing work item modal, create a new work item. Confirm `GET
   /api/projects/{id}/activity` (or the Summary tab's feed) now shows a
   newest-first "... created ..." entry for it within one refresh.
2. Edit that item's Status via the Board (drag) or the modal. Confirm a new
   "changed ... status from X to Y" entry appears, with the actor's name and
   a relative timestamp ("a few seconds ago").
3. Change the item's title only (no tracked field). Confirm **no** new entry
   appears.
4. Change both Priority and Assignee in one modal save. Confirm **two**
   separate entries appear (one per field), not one combined entry
   (research.md #5).
5. Delete the work item. Confirm its prior entries remain visible in the
   project feed, still showing the original title (the "captured snapshot,"
   research.md #1) rather than erroring or vanishing.
6. Request more entries than fit on one page; confirm "load more"/pagination
   surfaces older entries, still newest-first.

## 5. Work item detail activity history (US5)

1. Make two or three tracked changes to one item.
2. Open that item's detail page; confirm its Activity section shows exactly
   those entries, in the same sentence format and ordering as the project
   feed (FR-021) — and no entries from any other item.

## 6. Cross-cutting checks

- Confirm a second, unrelated project's Summary/feed never shows the first
  project's stat cards, breakdowns, workload, or activity entries (SC-008).
- Confirm every prior Board/Backlog/List/Tree behavior — including explicit
  `?view=` links — is unchanged (SC-009).
- Run the backend (`dotnet test`) and frontend (`npm test`) suites; confirm
  all pre-existing tests still pass alongside the new ones.
