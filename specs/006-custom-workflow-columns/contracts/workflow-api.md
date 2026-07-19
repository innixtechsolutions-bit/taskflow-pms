# Workflow (Project Status) API Contract

A new controller, `ProjectStatusesController`
(`backend/TaskFlow.Api/Controllers/ProjectStatusesController.cs`), routed
at `api/projects/{projectId}/statuses`, following `ProjectsController`'s
existing pattern: class-level `[Authorize]` for the read baseline, with
`[Authorize(Roles = "Manager,Admin")]` layered on each mutating action.

Also documents the **breaking changes** (Constitution Principle V,
justified in plan.md's Complexity Tracking) to existing `WorkItemsController`
routes: `Status` fields become `StatusId`-based throughout.

---

## `GET api/projects/{projectId}/statuses`

Returns a project's statuses in position order, each with its current
item count. Used by the Workflow management screen, the work-item
create/edit form's Status dropdown, project-detail's status filter, and
the board's column list.

**Auth**: any authenticated user (read-only — matches how
`GetWorkItems`/`GetTree`/`GetBoard` are already open to all authenticated
users; only the *mutating* actions below are Manager/Admin-only, per
FR-008 — the read endpoint itself has no sensitive-management-only
information beyond what the board/dropdowns already expose today).

**Response 200** — `List<WorkflowStatusDto>`:
```json
[
  { "id": 11, "name": "To Do", "category": "Open", "colorKey": "Slate", "position": 0, "itemCount": 4 },
  { "id": 12, "name": "In Progress", "category": "Open", "colorKey": "Blue", "position": 1, "itemCount": 2 },
  { "id": 13, "name": "In Review", "category": "Open", "colorKey": "Violet", "position": 2, "itemCount": 1 },
  { "id": 14, "name": "Done", "category": "Done", "colorKey": "Green", "position": 3, "itemCount": 9 }
]
```

**Response 404**: unknown `projectId`.

---

## `POST api/projects/{projectId}/statuses`

Adds a new status. Implements US3/FR-010.

**Auth**: Manager or Admin only (`[Authorize(Roles = "Manager,Admin")]`);
403 for any other authenticated role (FR-008, SC-006).

**Request** — `CreateWorkflowStatusRequest`:
```json
{ "name": "QA", "category": "Open", "position": 2 }
```
`position` is optional; when omitted, the new status is inserted
immediately before the project's first `Done`-category status
(FR-010). `colorKey` is never supplied by the client — assigned
automatically per research.md #3.

**Response 201** — the created `WorkflowStatusDto` (`itemCount: 0`).

**Response 400**: `name` outside 2–30 chars; `category` not `Open`/`Done`.

**Response 404**: unknown `projectId`.

**Response 409**: `name` already exists in this project, case-insensitive
(FR-002/US3 scenario 3) — `DuplicateStatusNameException`; OR the project
already has 10 statuses (FR-004) — `MaxStatusCountExceededException`.

---

## `PUT api/projects/{projectId}/statuses/{statusId}`

Renames a status and/or changes its color. Implements US4/FR-011 and the
color-editing part of FR-015. `Category` has no field here — it cannot be
changed after creation (FR-021).

**Auth**: Manager or Admin only.

**Request** — `UpdateWorkflowStatusRequest`:
```json
{ "name": "Doing", "colorKey": "Amber" }
```
Both fields optional; a request with neither is a no-op 200 (mirrors
`ProjectService.UpdateAsync`'s tolerant style). Position is NOT
settable here — see the dedicated reorder endpoint below.

**Response 200** — the updated `WorkflowStatusDto`.

**Response 400**: `name` outside 2–30 chars, or `colorKey` not one of the
fixed `ChipColor` set.

**Response 404**: unknown `projectId` or `statusId`, or `statusId` does
not belong to `projectId`.

**Response 409**: new `name` collides case-insensitively with another
status already in this project (US4 scenario 2).

---

## `PUT api/projects/{projectId}/statuses/reorder`

Resequences all of a project's statuses in one call. Implements
US5/FR-012.

**Auth**: Manager or Admin only.

**Request** — `ReorderWorkflowStatusesRequest`:
```json
{ "orderedStatusIds": [13, 11, 12, 14] }
```
Must be a permutation of exactly this project's current status ids — no
more, no fewer.

**Response 200** — `List<WorkflowStatusDto>`, the full list in its new
order (so the caller can re-render without a second `GET`).

**Response 400**: `orderedStatusIds` is not a permutation of the
project's current status ids (missing one, contains an unknown id, or a
duplicate) — `InvalidStatusOrderException`.

**Response 404**: unknown `projectId`.

---

## `DELETE api/projects/{projectId}/statuses/{statusId}`

Deletes a status directly (if empty) or via delete-with-move (if it has
items and a `destinationStatusId` is supplied). Implements US6/FR-013/
FR-014, atomically (research.md #6).

**Auth**: Manager or Admin only.

**Query parameter**: `destinationStatusId` (`int`, optional) — required
only when the status being deleted currently has ≥1 work item.

**Response 204**: deleted (empty status, or non-empty status with a
valid `destinationStatusId` — all affected items reassigned in the same
call).

**Response 400**:
- The status has ≥1 work item and no `destinationStatusId` was
  supplied — `DestinationStatusRequiredException`, response body includes
  the current item count so the client can render the confirmation
  message ("Move 12 items to..."). US6 scenario 2.
- `destinationStatusId` does not belong to this project, or equals
  `statusId` itself — `InvalidDestinationStatusException`.
- Deleting would leave the project with zero `Open`-category or zero
  `Done`-category statuses — `LastStatusInCategoryException` (FR-003,
  US6 scenarios 4–5); this check runs regardless of whether the status
  currently has items.

**Response 404**: unknown `projectId` or `statusId`.

---

## Breaking changes to existing `WorkItemsController` routes

All of the following are within this same feature/PR (single coupled
deploy — research.md #7). No route paths change, only request/response
field shapes.

### `POST/PUT api/projects/{projectId}/work-items`, `api/work-items/{id}`

`WorkItemRequest.Status` (`string`, enum name) → `StatusId` (`int?`).
When omitted, defaults to the project's first `Open`-category status by
position (mirrors today's "defaults to ToDo" behavior).

**New 400**: `StatusId` does not correspond to a status belonging to this
work item's project — `InvalidWorkItemStatusException` (same exception
type, new trigger condition).

### `PATCH api/work-items/{id}/status`

`UpdateWorkItemStatusRequest.Status` (`string`) → `StatusId` (`int`,
required). Same 403/404 behavior as today (FR-015, unchanged).

### Every work-item response shape (`WorkItemDto`, `WorkItemDetailDto`,
`WorkItemChildDto`, `WorkItemTreeNodeDto`, `WorkItemBoardCardDto`)

`Status: string` → four flattened fields:
```json
{
  "statusId": 12,
  "statusName": "In Progress",
  "statusCategory": "Open",
  "statusColorKey": "Blue"
}
```

### `GET api/projects/{projectId}/work-items/board`

`BoardColumnDto` shape:
```json
{ "columns": [{ "statusId": 11, "name": "To Do", "category": "Open", "colorKey": "Slate" }], "items": [ /* WorkItemBoardCardDto, per above */ ] }
```

### `GET api/projects/{projectId}/work-items` (filter)

Query parameter `status` (string name) → `statusId` (`int`).

---

## Non-goals of this contract

- No endpoint to fetch a single status by id in isolation — the list
  endpoint is cheap enough (≤10 rows) that every consumer just fetches
  the whole list.
- No endpoint exposing or setting the "default completion status" —
  it's computed on demand server-side wherever needed (research.md #5),
  not client-facing in this feature.
- No WebSocket/SignalR push for workflow changes — same accepted
  last-write-wins staleness already established for Feature 005's status
  changes (spec.md Edge Cases).
