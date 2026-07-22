# API Contract: Summary & Activity Log

All three routes are added to the existing `WorkItemsController`
(`[Authorize]` class-level — any authenticated user; no new role
restriction, per spec FR-022 and research.md #16). No existing route,
request shape, or response shape changes.

## `GET /api/projects/{projectId}/summary`

Returns the current computed Summary for one project.

**Auth**: Any authenticated user (matches every other project read).

**404**: `ProjectNotFoundException` if `projectId` doesn't exist.

**200 response** (`ProjectSummaryDto`, see data-model.md):

```json
{
  "statCards": { "total": 42, "completed": 18, "completedPercent": 43, "inProgress": 24, "dueSoon": 5 },
  "statusBreakdown": [
    { "statusId": 1, "name": "To Do", "colorKey": "Slate", "count": 10 },
    { "statusId": 2, "name": "In Progress", "colorKey": "Blue", "count": 14 },
    { "statusId": 3, "name": "Done", "colorKey": "Green", "count": 18 }
  ],
  "priorityBreakdown": [
    { "priority": "Low", "count": 6 },
    { "priority": "Medium", "count": 20 },
    { "priority": "High", "count": 14 },
    { "priority": "Critical", "count": 2 }
  ],
  "workload": [
    { "userId": 3, "displayName": "Jane Doe", "openItemCount": 9 },
    { "userId": 7, "displayName": "Sam Lee", "openItemCount": 4 },
    { "userId": null, "displayName": "Unassigned", "openItemCount": 2 },
    { "userId": 11, "displayName": "Pat Manager", "openItemCount": 0 }
  ]
}
```

## `GET /api/projects/{projectId}/activity?page=&pageSize=`

Returns a page of this project's activity feed, newest first.

**Auth**: Any authenticated user.

**Query params**: `page` (default 1), `pageSize` (default 20, clamped to
100 max — same convention as `GetWorkItemsAsync`).

**404**: `ProjectNotFoundException` if `projectId` doesn't exist.

**200 response** (`PagedResult<ActivityEntryDto>`):

```json
{
  "items": [
    {
      "id": 501,
      "workItemId": 88,
      "workItemTitle": "Fix login",
      "workItemType": "Task",
      "eventType": "FieldChanged",
      "field": "Status",
      "oldValue": "To Do",
      "newValue": "In Progress",
      "actorUserId": 3,
      "actorName": "Jane Doe",
      "createdAt": "2026-07-22T09:14:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 137
}
```

## `GET /api/work-items/{id}/activity`

Returns the full activity history for one work item, newest first — no
pagination (spec doesn't ask for it; a single item's history is naturally
small).

**Auth**: Any authenticated user.

**404**: `WorkItemNotFoundException` if `id` doesn't exist (only while the
item still exists — deleted items' entries remain queryable only via the
project feed above, filtered by `workItemId`, not via this route, since the
route itself requires a live `id` to resolve; the project feed is the
durable long-term access path per spec FR-018).

**200 response** (`List<ActivityEntryDto>`, same shape as above).

## Non-endpoints (side effects only)

No new mutation route exists. Activity entries are written only as a side
effect of the existing:
- `POST /api/projects/{projectId}/work-items` (creation entry)
- `PUT /api/work-items/{id}` (field-change entries, 0+)
- `PATCH /api/work-items/{id}/status` (0 or 1 Status entry)
- `PATCH /api/work-items/{id}/sprint` (0 or 1 Sprint entry)

None of these four routes' request/response shapes change.
