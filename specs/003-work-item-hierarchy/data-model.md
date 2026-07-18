# Phase 1 Data Model: Work Item Hierarchy

## WorkItem (extended)

Feature 002's `WorkItem` entity gains one new field. All other fields and
rules are unchanged from `002-projects-work-items/data-model.md`.

| Field | Type | Rules |
|---|---|---|
| `ParentWorkItemId` | `int?` (FK → `WorkItem.Id`, same table) | Optional at the column level; whether it's actually required depends on `Type` (see "Hierarchy rules" below). `DeleteBehavior.Restrict` (research.md §1) — subtree deletion is application code, not a database cascade. |

**New navigation properties**: `ParentWorkItem` (`WorkItem?`, the one
parent) and `Children` (`ICollection<WorkItem>`, direct children only —
this feature does not need a recursive navigation property; deeper levels
are reached by querying, per `WorkItemService`).

**Indexes**: New index on `ParentWorkItemId` — every tree-build and
descendant-collection query filters or groups on it, and it is this
feature's single highest-volume new query path (mirrors the existing
`ProjectId` index rationale from Feature 002).

## Hierarchy rules (the parent-type table)

This table is the single source of truth `WorkItemService` codes against;
every rule below is enforced server-side regardless of client UI (FR-008).

| Item `Type` | Required parent `Type` | Parent required? |
|---|---|---|
| `Epic` | — (none; must have no parent) | N/A — parent must be absent |
| `Story` | `Epic` | Yes |
| `Task` | `Story` | No (may be null) |
| `SubTask` | `Task` | Yes |

Additional structural rules, all enforced in `WorkItemService`:
- Parent and child `ProjectId` must match (FR-005).
- No item can be its own ancestor — automatically true given the table
  above; see research.md §2 for the proof (no runtime check needed beyond
  the type-and-project filter already required by FR-002–FR-005).
- A `Type` change is rejected if the item's existing parent's type no
  longer matches the new type's required parent type, or if any existing
  child's required-parent type no longer matches the new type (FR-007,
  research.md §3).

## WorkItemDetailDto (new)

Returned only by `GET /api/work-items/{id}` (research.md §6). Superset of
the existing `WorkItemDto` fields, plus:

| Field | Type | Notes |
|---|---|---|
| `parentWorkItemId` | `int?` | Also present on the base `WorkItemDto` used elsewhere. |
| `parentTitle` | `string?` | Null when there is no parent. |
| `totalDescendantCount` | `int` | All levels, computed at query time (same "derived, not stored" approach as `ProjectDetailDto.TotalWorkItemCount` in Feature 002) — used by the delete confirmation (FR-020). |
| `children` | `WorkItemChildDto[]` | Direct children only (FR-018). |

## WorkItemChildDto (new)

Lightweight row for a detail view's children list (FR-018).

| Field | Type |
|---|---|
| `id` | `int` |
| `title` | `string` |
| `type` | `string` |
| `status` | `string` |
| `assigneeName` | `string?` |

## WorkItemTreeNodeDto (new)

Returned by `GET /api/projects/{projectId}/work-items/tree` (research.md
§4) — a recursive node. Top-level array = items with no parent (whether or
not they have children).

| Field | Type | Notes |
|---|---|---|
| `id` | `int` | |
| `type` | `string` | |
| `title` | `string` | |
| `status` | `string` | |
| `priority` | `string` | |
| `assigneeName` | `string?` | |
| `directChildrenCount` | `int` | FR-014. |
| `directChildrenDoneCount` | `int` | FR-014 (the "n/m done" count). |
| `children` | `WorkItemTreeNodeDto[]` | Ordered by `UpdatedAt` descending (research.md §7); empty array for a leaf or standalone item. |

## WorkItemLookupItemDto (new)

Returned by `GET
/api/projects/{projectId}/work-items/parent-candidates?type=` (research.md
§5) — mirrors Feature 002's `UserLookupItemDto`.

| Field | Type |
|---|---|
| `id` | `int` |
| `title` | `string` |

## WorkItemDto / WorkItemRequest (extended)

- `WorkItemDto` (used by `POST`/`PUT` responses, the flat list, and the
  tree's flat-lookup needs) gains `parentWorkItemId` (`int?`).
- `WorkItemRequest` (the shared create/edit request body) gains
  `parentWorkItemId` (`int?`). Its presence/absence is validated against
  the "Hierarchy rules" table above inside `WorkItemService`, not via a
  data-annotation attribute — required-ness depends on the request's own
  `Type` value, which data annotations can't express cleanly (same
  reasoning Feature 002 already applied to `Priority`/`Status` parsing).

## Authorization derived from these entities (not a stored field)

Unchanged from Feature 002 (`002.../data-model.md`'s final section):
work-item edit requires creator/assignee/Manager/Admin; delete requires
creator/Manager/Admin. This feature adds no new authorization dimension —
the delete check applies once, to the item being deleted, and the subtree
is removed under that same check (FR-022).
