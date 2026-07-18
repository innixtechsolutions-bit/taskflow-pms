# Quickstart: Validating Work Item Hierarchy

Prerequisites: backend (`dotnet run` in `backend/TaskFlow.Api`) and
frontend (`ng serve` in `frontend`) running against a migrated
`TaskFlowDb`, per Feature 001/002's existing setup. Signed in as any user,
with at least one project already created (Feature 002).

## 1. Build a full four-level chain (User Story 1 → SC-001)

1. In the project, create a work item with `type: Epic`, e.g. "Launch v2".
2. Create a second item with `type: Story`. Open its parent picker —
   confirm only "Launch v2" (and any other Epics in this project) appear,
   never Stories/Tasks/SubTasks or Epics from a different project. Set it
   as parent.
3. Create a `Task`, parent picker limited to Stories; set the Story from
   step 2 as parent. Confirm you could also leave the parent blank on a
   different Task (standalone Tasks stay legal).
4. Create a `SubTask`; confirm the parent field is required and limited to
   Tasks; set the Task from step 3.
5. **Expected**: all four creations succeed; the project's tree view (see
   step 2 below) shows the full chain correctly indented.

## 2. Tree view renders correctly (User Story 2 → SC-001, SC-003)

1. Open the project's work items view; switch to (or confirm default)
   Tree view.
2. **Expected**: "Launch v2" (Epic) renders at the top level with an
   expand/collapse control; the Story indents beneath it; the Task
   indents beneath the Story; the SubTask indents beneath the Task.
3. Mark the SubTask `Done`. Reload the tree.
4. **Expected**: the Task's row shows "1/1 done"; the Story's row shows
   "0/1 done" (it counts only its *direct* child, the Task, which is not
   itself Done) — confirms direct-children-only counting (FR-014).
5. Create one more standalone `Task` with no parent.
6. **Expected**: it lists at the top level, alongside "Launch v2", with no
   indentation and zero children.

## 3. Invalid parent assignment is refused (User Story 1 → SC-002)

Using a REST client (or browser devtools) against the running API:

1. `POST /api/projects/{projectId}/work-items` with
   `{ "type": "SubTask", "title": "bad", "parentWorkItemId": <the Epic's id> }`.
   **Expected**: `400`, error names the type mismatch (SubTask requires a
   Task parent).
2. `POST .../work-items` with `{ "type": "Epic", "title": "bad epic",
   "parentWorkItemId": <any existing item's id> }`.
   **Expected**: `400` — Epics can never have a parent.
3. `POST .../work-items` with `{ "type": "SubTask", "title": "bad", }`
   (no `parentWorkItemId`).
   **Expected**: `400` — SubTask requires a parent.
4. Create a second project; attempt to set an item's parent to an item
   from the *first* project.
   **Expected**: `400` — parent must be in the same project.

   Note: a self-parent/cycle attempt (spec.md Edge Cases; SC-002) can't be
   constructed via `POST` — a newly-created item has no id yet to
   reference — so that case is exercised in §4 below, once an item exists.

## 4. Reparenting and type-change guards (User Story 4)

1. Edit the Task from step 1.3 above and change its parent to a different
   Story in the same project. **Expected**: succeeds; tree reflects the
   move.
2. Edit that same Task and try to set its parent to the Epic.
   **Expected**: `400` — Task's parent must be a Story.
2a. `PUT /api/work-items/{that Task's id}` with `parentWorkItemId` set to
    the Task's *own* id (a direct self-parent attempt).
    **Expected**: `400` with a `ProblemDetails` body naming the rule
    violated — caught by the same type check as every other case (the
    Task's own type never matches its required parent type, Story —
    research.md §2), not a special-cased cycle check.
3. Try to change that Task's `type` to `Story` while it still has the
   SubTask as a child. **Expected**: `400` — naming that the existing
   SubTask child requires a Task parent, not a Story.

## 5. Detail view navigation (User Story 3)

1. Open the Story's detail view.
   **Expected**: shows a link to its parent Epic, and lists the Task as a
   direct child (title, type, status, assignee) linking to the Task's own
   detail view.
2. From the Story's detail view, start creating a new child item.
   **Expected**: the new item's form pre-selects the Story as parent.

## 6. Cascade delete with descendant confirmation (Edge Cases → SC-004)

1. Attempt to delete the Story from step 1.2 (which still has the Task and
   SubTask beneath it).
   **Expected**: confirmation states the total descendant count across all
   levels (2: the Task and the SubTask), not just direct children.
2. Confirm the deletion.
   **Expected**: the Story, the Task, and the SubTask are all gone; the
   Epic remains; the project's tree view no longer shows any of the three.

## 7. Flat list keeps working regardless of hierarchy (User Story 5 → SC-005)

1. Switch the project view to Flat/List mode.
2. Apply a status or type filter, and a text search matching a child
   item's title.
   **Expected**: results match Feature 002's existing filter/search
   behavior exactly — items appear regardless of tree depth, and the list
   itself shows no indentation.
3. Run the full Feature 002 backend/frontend test suites.
   **Expected**: all pass unchanged (SC-005).

## 8. Pre-existing items remain valid (Edge Cases → SC-006)

1. Using a work item created before this feature (via `git log` / an
   older seed, or simply an item created against Feature 002's schema
   before this migration) — after the migration runs, load its detail
   view.
   **Expected**: renders normally with `parentWorkItemId: null`, no error,
   no manual data fix required.
