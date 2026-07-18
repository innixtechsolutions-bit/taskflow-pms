# Board API Contract

Two new endpoints on the existing `WorkItemsController`
(`backend/TaskFlow.Api/Controllers/WorkItemsController.cs`), matching its
established attribute-routing style (no class-level `[Route]`, full path
per action) and its blanket `[Authorize]` (any authenticated user may
call these — the authorization *rule* for status changes is enforced
inside the service method, not via a role attribute, exactly like
`Update` already works).

Both are strictly additive — no existing route, request, or response
shape changes (Constitution Principle V).

---

## `GET api/projects/{projectId}/work-items/board`

Returns the ordered column list and every work item in the project as a
flat, board-ready card list — unpaginated (FR-020), unlike `GET
api/projects/{projectId}/work-items`.

**Auth**: any authenticated user (read-only; matches `GetWorkItems`/
`GetTree`'s existing access level).

**Response 200** — `WorkItemBoardDto`:
```json
{
  "columns": ["ToDo", "InProgress", "InReview", "Done"],
  "items": [
    {
      "id": 42,
      "type": "Task",
      "title": "Fix the login bug",
      "status": "InProgress",
      "priority": "High",
      "assigneeUserId": 3,
      "assigneeName": "Grace Hopper",
      "dueDate": "2026-07-20T00:00:00Z",
      "updatedAt": "2026-07-18T09:00:00Z",
      "createdByUserId": 1,
      "directChildrenCount": 2,
      "directChildrenDoneCount": 1
    }
  ]
}
```

**Response 404**: unknown `projectId` (matches `GetWorkItems`/`GetTree`'s
existing not-found behavior for the same case).

**Ordering**: `items` sorted by `updatedAt` descending; the frontend
groups them into columns by `status`, preserving that order within each
column (data-model.md).

---

## `PATCH api/work-items/{id}/status`

Changes a single work item's status only. Introduced so the board's drag
interaction never has to submit (and risk clobbering) fields it doesn't
carry — see research.md #3.

**Auth**: authenticated; the caller MUST be the work item's creator,
its current assignee, or hold the Manager or Admin role — identical to
the rule `PUT api/work-items/{id}` already enforces, via the same shared
`EnsureCanEdit` check.

**Request** — `UpdateWorkItemStatusRequest`:
```json
{ "status": "InReview" }
```

**Response 200** — the updated `WorkItemDto` (same shape `PUT` already
returns, for consistency — lets the frontend refresh a single item's full
record from one response if it ever needs to, though the board itself
only needs to know the write succeeded).

**Response 400**: `status` does not parse to a defined `WorkItemStatus`
value (same validation `WorkItemRequest.Status` already applies).

**Response 403**: caller is not the creator, current assignee, Manager,
or Admin — `NotAuthorizedToEditWorkItemException`, the same exception
type `PUT` already throws for the same rule (FR-015; verified by an
endpoint test that calls this route directly, bypassing the board UI
entirely, as an unauthorized user).

**Response 404**: unknown work item id (matches `PUT`/`DELETE`'s existing
behavior).

---

## Non-goals of this contract

- No new endpoint for the status *list* alone — it is always returned
  together with board card data in one response (research.md #2), since
  the board is the only consumer today.
- No pagination on the board endpoint — intentional (FR-020), distinct
  from the existing paginated flat-list endpoint, which is unaffected by
  this feature.
- No WebSocket/SignalR real-time push — explicitly out of scope
  (spec.md's Edge Cases: last-write-wins, no conflict detection this
  feature).
