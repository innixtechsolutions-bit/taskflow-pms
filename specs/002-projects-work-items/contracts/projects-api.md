# Contract: Projects API

All responses use `application/json`. All error responses use
`ProblemDetails` / `ValidationProblemDetails` (matching Feature 001's
established shape). Every endpoint requires a valid bearer token.

## POST /api/projects

**Auth required**: Yes, and caller's role must be `Manager` or `Admin`.

**Request body**:
```json
{ "name": "string (3-100 chars)", "description": "string (<=2000 chars), optional" }
```

**Responses**:
- `201 Created` → same shape as `GET /api/projects/{id}` (below) —
  `totalWorkItemCount` is trivially `0` for a just-created project:
  ```json
  { "id": 1, "name": "string", "description": "string|null", "createdByName": "string", "createdAt": "ISO-8601 datetime", "totalWorkItemCount": 0 }
  ```
- `400 Bad Request` (`ValidationProblemDetails`) — name/description fail
  validation (FR-001).
- `409 Conflict` (`ProblemDetails`, `detail`: "A project with this name
  already exists.") — duplicate name, case-insensitive (FR-002).
- `403 Forbidden` — caller is not a Manager or Admin (FR-004).

## GET /api/projects?page={int}&pageSize={int}

**Auth required**: Yes (any role).

**Purpose**: List every project (FR-005), newest-first.

**Responses**:
- `200 OK` →
  ```json
  {
    "items": [
      { "id": 1, "name": "string", "createdByName": "string", "createdAt": "ISO-8601 datetime", "openWorkItemCount": 3 }
    ],
    "page": 1,
    "pageSize": 20,
    "totalCount": 5
  }
  ```
  Sorted by `createdAt` descending (FR-007). `openWorkItemCount` counts
  this project's work items whose status is not `Done` (FR-006).

## GET /api/projects/{id}

**Auth required**: Yes (any role).

**Purpose**: Project detail — includes `totalWorkItemCount` (every work
item regardless of status), used by the frontend to render the delete
confirmation (research.md §5) — distinct from the list endpoint's
`openWorkItemCount`.

**Responses**:
- `200 OK` →
  ```json
  { "id": 1, "name": "string", "description": "string|null", "createdByName": "string", "createdAt": "ISO-8601 datetime", "totalWorkItemCount": 12 }
  ```
- `404 Not Found` — `{id}` does not match an existing project.

## PUT /api/projects/{id}

**Auth required**: Yes, and caller's role must be `Manager` or `Admin`.

**Request body**: Same shape as `POST /api/projects`.

**Responses**:
- `200 OK` → same shape as `GET /api/projects/{id}`, updated.
- `400 Bad Request` — validation failure.
- `409 Conflict` — new name duplicates a *different* existing project.
- `403 Forbidden` — caller is not a Manager or Admin.
- `404 Not Found` — `{id}` does not match an existing project.

## DELETE /api/projects/{id}

**Auth required**: Yes, and caller's role must be `Manager` or `Admin`.

**Purpose**: Delete a project and, via cascade, every one of its work items
(FR-009). The "this will also delete N work items" confirmation is a
frontend step using the count already fetched from `GET /api/projects/{id}`
— this endpoint itself is a plain, unconditional delete once called
(research.md §5).

**Responses**:
- `204 No Content` — project and all its work items removed.
- `403 Forbidden` — caller is not a Manager or Admin.
- `404 Not Found` — `{id}` does not match an existing project.
