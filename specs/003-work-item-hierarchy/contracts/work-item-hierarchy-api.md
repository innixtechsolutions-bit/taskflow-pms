# Contract: Work Item Hierarchy API

Extends `002-projects-work-items/contracts/work-items-api.md`. All
responses use `application/json`; all error responses use `ProblemDetails`
/ `ValidationProblemDetails`; every endpoint requires a valid bearer token
(unchanged from Feature 002).

## Changed: POST /api/projects/{projectId}/work-items

**Request body** — adds one optional field to Feature 002's shape:
```json
{
  "type": "Epic|Story|Task|SubTask",
  "title": "string (3-200 chars)",
  "description": "string (<=5000 chars), optional",
  "priority": "Low|Medium|High|Critical, optional (default Medium)",
  "status": "ToDo|InProgress|Done, optional (default ToDo)",
  "assigneeUserId": "int, optional",
  "dueDate": "ISO-8601 date, optional",
  "parentWorkItemId": "int, optional/required depending on type — see data-model.md's Hierarchy rules table"
}
```

**Responses** (in addition to Feature 002's existing 201/400/404):
- `201 Created` → same shape as before, plus `"parentWorkItemId": int|null`.
- `400 Bad Request` — new cases, each naming the violated rule:
  - `type` is `Epic` and `parentWorkItemId` is supplied (Epics can never
    have a parent).
  - `type` is `SubTask` and `parentWorkItemId` is missing (a parent is
    required).
  - `parentWorkItemId` does not match an existing work item in the same
    `projectId`, or its `Type` is not the required parent type for the
    requested `type` (FR-001–FR-005; data-model.md's Hierarchy rules
    table). Because of that table, this check alone also rejects every
    would-be cycle and self/descendant selection (research.md §2) — no
    separate error case exists for "cycle detected."

## Changed: PUT /api/work-items/{id}

**Request body**: same shape as `POST` above (full replace, including
`parentWorkItemId`).

**Responses** (in addition to Feature 002's existing 200/400/403/404):
- `400 Bad Request` — the same new parent-validation cases as `POST`,
  plus: the requested `type` change is refused because it would invalidate
  the item's existing parent (parent's type no longer matches the new
  type's required parent type) or existing children (a child's required
  parent type no longer matches the new type) — FR-007, research.md §3.
  The error names which relationship (parent or children) is the conflict.

## Changed: DELETE /api/work-items/{id}

Behavior extended, request/response shape unchanged:
- `204 No Content` — the item **and its entire subtree** (all descendants,
  every level) are removed in one operation (FR-020/FR-021). The
  authorization check (creator or Manager/Admin) applies only to the item
  being deleted, per Feature 002's existing rule — not to each descendant
  individually (FR-022).
- `403 Forbidden` / `404 Not Found` — unchanged from Feature 002.

Frontend callers are expected to fetch `GET /api/work-items/{id}` first (or
already have it loaded) to read `totalDescendantCount` and show it in the
confirmation prompt before calling `DELETE` (FR-020).

## Changed: GET /api/work-items/{id}

Response body enriched — see `data-model.md`'s `WorkItemDetailDto`:
```json
{
  "id": 1, "projectId": 1, "type": "Task", "title": "string", "description": "string|null",
  "priority": "Medium", "status": "ToDo",
  "assigneeUserId": 2, "assigneeName": "string|null",
  "dueDate": "ISO-8601 date|null",
  "createdByUserId": 1, "createdByName": "string",
  "createdAt": "ISO-8601 datetime", "updatedAt": "ISO-8601 datetime",
  "parentWorkItemId": 5, "parentTitle": "string|null",
  "totalDescendantCount": 3,
  "children": [
    { "id": 10, "title": "string", "type": "SubTask", "status": "ToDo", "assigneeName": "string|null" }
  ]
}
```
- `parentTitle`/`parentWorkItemId` are both `null` when the item has no
  parent (FR-017).
- `children` is `[]` for a leaf item (FR-018).
- `totalDescendantCount` counts every level beneath this item, not just
  direct children (FR-020).
- `404 Not Found` — unchanged from Feature 002.

## New: GET /api/projects/{projectId}/work-items/tree

**Auth required**: Yes (any role) — same as the existing flat list
endpoint.

**Purpose**: The full project hierarchy for the tree view (FR-013–FR-016,
research.md §4). Not paginated — returns every item in the project as
nested nodes.

**Responses**:
- `200 OK` →
  ```json
  [
    {
      "id": 1, "type": "Epic", "title": "string", "status": "ToDo", "priority": "Medium",
      "assigneeName": "string|null",
      "directChildrenCount": 2, "directChildrenDoneCount": 1,
      "children": [
        { "id": 2, "type": "Story", "title": "string", "status": "InProgress", "priority": "High",
          "assigneeName": "string|null",
          "directChildrenCount": 0, "directChildrenDoneCount": 0, "children": [] }
      ]
    }
  ]
  ```
  Top-level array contains every item with no parent — hierarchical roots
  and standalone items alike (a standalone item is just a root with an
  empty `children` array and zero counts). Ordered, at every level, by
  `updatedAt` descending (FR-015).
- `404 Not Found` — `{projectId}` does not match an existing project.

## New: GET /api/projects/{projectId}/work-items/parent-candidates?type=

**Auth required**: Yes (any role).

**Purpose**: Populate the parent picker for a given `type` (FR-010,
research.md §5). Called by the frontend whenever the create/edit form's
`Type` field changes.

**Query parameters**: `type` (required, one of `Epic|Story|Task|SubTask`).

**Responses**:
- `200 OK` →
  ```json
  { "candidates": [ { "id": 1, "title": "string" } ] }
  ```
  Items of the required parent type for `type` (per data-model.md's
  Hierarchy rules table), scoped to `projectId`. Empty array when `type`
  is `Epic` (Epics never have a parent) — the frontend disables the picker
  entirely in that case rather than showing an always-empty dropdown.
- `400 Bad Request` — `type` is missing or not one of the four allowed
  values.
- `404 Not Found` — `{projectId}` does not match an existing project.
