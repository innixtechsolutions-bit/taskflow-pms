# API Contract: Work Item Modal & Quick Creation

All changes are additive to the existing `WorkItemsController` API
(research.md #12) — no existing route, verb, status code, or field is
removed or retyped. Error responses remain RFC 7807 `ProblemDetails` via
`Problem(statusCode:, detail:)`, same as every existing endpoint.

## Changed: `POST api/projects/{projectId}/work-items` (Create)

**Request body (`WorkItemRequest`)** — two new optional fields:

```jsonc
{
  "type": "Task",
  "title": "Fix login bug",
  "description": "…",
  "priority": "High",
  "statusId": 3,
  "assigneeUserId": 7,
  "dueDate": "2026-08-01",
  "parentWorkItemId": 12,
  "startDate": "2026-07-25",      // NEW — optional, date-only
  "labels": ["backend", "urgent"] // NEW — optional, 0–5 entries, 1–30 chars each
}
```

**New 400 error cases** (in addition to existing ones):
- `startDate` present, `dueDate` present, `startDate > dueDate` →
  `InvalidDateRangeException` → `Problem(400, "Start date must be on or before the due date.")`
- Any label name empty/whitespace or over 30 characters →
  `InvalidLabelException` → `Problem(400, "Each label must be 1–30 characters.")`
- More than 5 labels supplied →
  `TooManyLabelsException` → `Problem(400, "A work item may have at most 5 labels.")`

**Response (`WorkItemDto`, `201`)** — two new fields, always present:

```jsonc
{
  // ...existing fields unchanged...
  "startDate": "2026-07-25T00:00:00",  // NEW — null if not set
  "labels": ["backend", "urgent"]      // NEW — [] if none
}
```

## Changed: `PUT api/work-items/{id}` (Update)

Same request/response shape changes as Create above. `labels` is
**replace-the-whole-set** semantics, matching how every other field on this
endpoint already works (PUT replaces the resource) — omitting `labels`
entirely is equivalent to submitting an empty list (all labels removed), the
same way omitting `assigneeUserId` already clears the assignee today. The
same three new 400 error cases apply.

## Changed: `GET api/projects/{projectId}/work-items` (List)

**New query parameter**: `label` (string, optional) — filters to work items
carrying a label with this name (case-insensitive exact match within the
project). Combines with existing `statusId`/`type`/`priority`/
`assigneeUserId`/`search` filters (all still AND-ed together).

**Response (`WorkItemDto[]` inside `PagedResult`)**: gains `startDate` and
`labels` per item, as above.

## Changed: `GET api/projects/{projectId}/work-items/board`

**Response (`WorkItemBoardCardDto[]` inside `WorkItemBoardDto`)**: gains
`labels` only (no `startDate` — out of scope for board cards per spec).

## Changed: `GET api/projects/{projectId}/work-items/tree`

**Response (`WorkItemTreeNodeDto[]`)**: gains `labels` per node (recursively,
same as every other field on this DTO).

## Changed: `GET api/work-items/{id}` (Detail)

**Response (`WorkItemDetailDto`)**: gains `startDate` and `labels`. The
nested `Children: WorkItemChildDto[]` list is unchanged (data-model.md —
deliberately excluded, consistent with that DTO's existing omissions).

## New: `GET api/projects/{projectId}/labels`

Returns the project's labels currently attached to at least one work item
(research.md #5), for the modal's autocomplete suggestions and the List
view's label filter dropdown.

**Auth**: `[Authorize]` (any authenticated user) — same openness as the
existing status-list endpoint (`GET .../statuses`), since any user creating
or filtering work items needs this list regardless of role.

**Response** (`200`):

```jsonc
["backend", "urgent", "frontend"]
```

Ordered alphabetically. Empty array if the project has no labels in use yet
— not a 404 (mirrors `GetParentCandidatesAsync`'s "always-empty list, not an
error" convention for the Epic case).

**Error cases**:
- Project doesn't exist → `ProjectNotFoundException` → `Problem(404, ...)`

## Unchanged

`PATCH api/work-items/{id}/status`, `DELETE api/work-items/{id}`,
`GET api/projects/{projectId}/work-items/parent-candidates` — none of these
touch dates or labels and are not modified by this feature.

## Frontend-only contract: removed routes

| Old route | New behavior |
|---|---|
| `projects/:projectId/work-items/new` | Removed. Redirects to `projects/:projectId`. |
| `projects/:projectId/work-items/:id/edit` | Removed. Redirects to `projects/:projectId/work-items/:id`. |

No modal auto-opens on redirect arrival (research.md #10). Every in-app
entry point (board column "+", "Add child", project-detail's "New work item"
button and empty-state actions, list-row/detail "Edit" affordances) opens
`WorkItemModalComponent` via `MatDialog` directly instead of navigating to
either of these routes.
