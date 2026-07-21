# API Contract: Sprints & Backlog

Error responses are RFC 7807 `ProblemDetails` via `Problem(statusCode:,
detail:)`, same as every existing endpoint (constitution Principle IV). All
routes require `[Authorize]`; mutating sprint routes additionally require
`[Authorize(Roles = "Manager,Admin")]`, matching
`ProjectStatusesController`'s established split between read (any
authenticated user) and write (Manager/Admin) actions.

## New: `SprintsController` (`api/projects/{projectId}/sprints`)

### `GET api/projects/{projectId}/sprints`

Any authenticated user. Returns the project's sprints, soonest-start-first.

```jsonc
// 200
[
  { "id": 5, "projectId": 1, "name": "Sprint 1", "startDate": "2026-07-21T00:00:00", "endDate": "2026-08-04T00:00:00", "status": "Active", "itemCount": 4 },
  { "id": 6, "projectId": 1, "name": "Sprint 2", "startDate": "2026-08-05T00:00:00", "endDate": "2026-08-19T00:00:00", "status": "Planned", "itemCount": 0 }
]
```

404 `ProjectNotFoundException` if the project doesn't exist.

### `POST api/projects/{projectId}/sprints`

Manager/Admin only.

```jsonc
// Request
{ "name": "Sprint 1", "startDate": "2026-07-21", "endDate": "2026-08-04" }
```

Response: `201`, `SprintDto` (`status: "Planned"`, `itemCount: 0`).

Errors: `404 ProjectNotFoundException`; `400 InvalidSprintNameException`
(name not 2–50 chars); `409 DuplicateSprintNameException`; `400
InvalidSprintDateRangeException` (`endDate <= startDate`).

### `PUT api/projects/{projectId}/sprints/{sprintId}/start`

Manager/Admin only. No request body.

Response: `200`, updated `SprintDto` (`status: "Active"`).

Errors: `404 ProjectNotFoundException` / `SprintNotFoundException`; `400
SprintNotPlannedException`; `400 EmptySprintException` (zero items); `409
AnotherSprintActiveException` — `detail` names the currently active sprint,
e.g. `"Sprint 1" is already active in this project.`.

### `PUT api/projects/{projectId}/sprints/{sprintId}/complete`

Manager/Admin only.

```jsonc
// Request -- resolution/destinationSprintId only required when the sprint
// has at least one not-Done item; omit both otherwise.
{ "resolution": "Backlog" }
// or
{ "resolution": "Sprint", "destinationSprintId": 6 }
```

Response: `200`, updated `SprintDto` (`status: "Completed"`).

Errors: `404 ProjectNotFoundException` / `SprintNotFoundException`; `400
SprintNotActiveException`; `400 DestinationRequiredException` — not-Done
items exist but `resolution` was omitted; carries the count via
`ProblemDetails.Extensions["itemCount"]`, same pattern as
`DestinationStatusRequiredException` (Feature 006); `400
InvalidDestinationSprintException` — `destinationSprintId` missing when
`resolution: "Sprint"`, or doesn't exist/isn't `Planned`/`Active`/is the
sprint itself.

### `DELETE api/projects/{projectId}/sprints/{sprintId}`

Manager/Admin only.

Response: `204`.

Errors: `404 ProjectNotFoundException` / `SprintNotFoundException`; `400
SprintNotDeletableException` — status isn't `Planned`, or it has ≥1 item.

## Changed: `WorkItemsController`

### Changed: `POST api/projects/{projectId}/work-items` (Create) and `PUT api/work-items/{id}` (Update)

**Request body (`WorkItemRequest`)** — one new optional field:

```jsonc
{
  // ...existing fields unchanged...
  "sprintId": 5   // NEW -- optional. Omitted/null = no sprint (backlog).
}
```

**New 400/404 error cases** (in addition to existing ones):
- `sprintId` doesn't belong to this item's own project →
  `SprintNotFoundException` → `Problem(404, ...)`
- `type` is (or resolves to) `Epic` and `sprintId` is non-null →
  `EpicCannotBeInSprintException` → `Problem(400, ...)`
- `sprintId` refers to a `Completed` sprint, or (Update only) the item's
  *current* `SprintId` refers to a `Completed` sprint and the request would
  change it → `SprintReadOnlyException` → `Problem(400, ...)`

**Response (`WorkItemDto`)** — two new fields, always present:

```jsonc
{
  // ...existing fields unchanged...
  "sprintId": 5,           // NEW -- null if no sprint
  "sprintName": "Sprint 1" // NEW -- null if no sprint
}
```

`WorkItemDetailDto` gains the same two fields.

### New: `PATCH api/work-items/{id}/sprint`

Field-scoped, mirrors `PATCH api/work-items/{id}/status` exactly — used by
the Backlog view's drag interaction so a move never resubmits fields it
doesn't carry (research.md #4).

```jsonc
// Request
{ "sprintId": 5 }   // or { "sprintId": null } to move to the backlog
```

Response: `200`, `WorkItemDto`.

Errors: `404 WorkItemNotFoundException`; `403
NotAuthorizedToEditWorkItemException`; `404 SprintNotFoundException`; `400
EpicCannotBeInSprintException`; `400 SprintReadOnlyException`.

### Changed: `GET api/projects/{projectId}/work-items/board`

**New query parameter**: `sprintId` (int, optional). When present, only
items whose `SprintId` equals it are returned; the `columns` array (the
project's `WorkflowStatus` list) is unaffected. Omitted = today's "all
items" behavior, unchanged (spec FR-020).

### New: `GET api/projects/{projectId}/backlog`

Any authenticated user. Same query parameters as `GET
.../work-items` (`statusId`, `type`, `priority`, `assigneeUserId`, `search`,
`label`), applied identically to every section.

```jsonc
// 200
{
  "sprints": [
    {
      "id": 5, "name": "Sprint 1", "startDate": "2026-07-21T00:00:00", "endDate": "2026-08-04T00:00:00", "status": "Active",
      "items": [ /* WorkItemDto[] -- items with sprintId == 5, matching the filters */ ]
    }
  ],
  "backlogItems": [ /* WorkItemDto[] -- items with sprintId == null, matching the filters (includes Epics) */ ]
}
```

404 `ProjectNotFoundException` if the project doesn't exist. 400 for the same
invalid `type`/`priority` cases `GET .../work-items` already returns.
