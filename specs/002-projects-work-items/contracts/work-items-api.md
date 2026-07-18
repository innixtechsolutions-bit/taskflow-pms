# Contract: Work Items API

All responses use `application/json`. All error responses use
`ProblemDetails` / `ValidationProblemDetails`. Every endpoint requires a
valid bearer token.

## POST /api/projects/{projectId}/work-items

**Auth required**: Yes (any role) — any signed-in user may create a work
item in any project (FR-010).

**Request body**:
```json
{
  "type": "Epic|Story|Task|SubTask",
  "title": "string (3-200 chars)",
  "description": "string (<=5000 chars), optional",
  "priority": "Low|Medium|High|Critical, optional (default Medium)",
  "status": "ToDo|InProgress|Done, optional (default ToDo)",
  "assigneeUserId": "int, optional",
  "dueDate": "ISO-8601 date, optional, any date including past"
}
```

**Responses**:
- `201 Created` →
  ```json
  {
    "id": 1, "projectId": 1, "type": "Task", "title": "string", "description": "string|null",
    "priority": "Medium", "status": "ToDo",
    "assigneeUserId": 2, "assigneeName": "string|null",
    "dueDate": "ISO-8601 date|null",
    "createdByUserId": 1, "createdByName": "string",
    "createdAt": "ISO-8601 datetime", "updatedAt": "ISO-8601 datetime"
  }
  ```
- `400 Bad Request` — title/description fail validation, `type`/`priority`/
  `status` isn't one of the allowed values, or `assigneeUserId` doesn't
  match an existing user (FR-011).
- `404 Not Found` — `{projectId}` does not match an existing project.

## GET /api/projects/{projectId}/work-items?page=&pageSize=&status=&type=&priority=&assigneeUserId=&search=

**Auth required**: Yes (any role).

**Purpose**: List, filter, and search a project's work items (FR-019–FR-023).
All query parameters besides `page`/`pageSize` are optional and combinable.

**Responses**:
- `200 OK` →
  ```json
  { "items": [ /* same item shape as POST's 201 body */ ], "page": 1, "pageSize": 20, "totalCount": 12 }
  ```
  `pageSize` defaults to 20 and is clamped to a maximum of 100, never
  rejected for exceeding it (see spec.md Edge Cases). `status`/`type`/
  `priority` filter by exact match; `assigneeUserId` filters to that exact
  assignee; `search` is a case-insensitive substring match on `title`.
  Sorted by `updatedAt` descending by default (FR-022). An empty `items`
  array with `totalCount: 0` corresponds to either "No work items yet" (no
  filters applied) or "No items match your filters." (filters applied) —
  the frontend distinguishes these by whether any filter/search parameter
  was supplied (FR-023).
- `404 Not Found` — `{projectId}` does not match an existing project.

## GET /api/work-items/{id}

**Auth required**: Yes (any role).

**Responses**:
- `200 OK` → same item shape as above.
- `404 Not Found` — `{id}` does not match an existing work item.

## PUT /api/work-items/{id}

**Auth required**: Yes, and the caller must be this item's creator, its
current assignee, or hold the `Manager`/`Admin` role (FR-015) — checked
against the item's actual data, not expressible as a routing attribute.

**Request body**: Same shape as `POST`'s body (a full replace of all
editable fields — `projectId` is never part of this body, since it cannot
change, FR-014).

**Responses**:
- `200 OK` → same item shape as `POST`'s response, with `updatedAt` advanced.
- `400 Bad Request` — validation failure, or `assigneeUserId` doesn't match
  an existing user.
- `403 Forbidden` — caller is none of creator/current assignee/Manager/Admin
  (FR-016).
- `404 Not Found` — `{id}` does not match an existing work item.

## DELETE /api/work-items/{id}

**Auth required**: Yes, and the caller must be this item's creator, or hold
the `Manager`/`Admin` role (FR-017) — narrower than edit: the current
assignee alone cannot delete (FR-018).

**Responses**:
- `204 No Content` — work item removed.
- `403 Forbidden` — caller is neither the creator nor a Manager/Admin.
- `404 Not Found` — `{id}` does not match an existing work item.
