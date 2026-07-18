# Phase 1 Data Model: Kanban Board

## Work Item (existing entity, extended)

`backend/TaskFlow.Api/Data/Entities/WorkItem.cs` — `WorkItemStatus` enum
gains one member:

```csharp
public enum WorkItemStatus { ToDo, InProgress, InReview, Done }
```

No other field changes. `Status` remains a `HasConversion<string>()`
column (research.md #1) — no migration.

**Validation rule**: unchanged — any status may be set to any other
directly (no state machine), per the existing entity's own comment. The
board's UI groups by status but does not introduce transition
restrictions.

## WorkItemBoardDto (new — backend response shape)

```csharp
public record WorkItemBoardDto(
    List<string> Columns,               // ["ToDo","InProgress","InReview","Done"], ordered
    List<WorkItemBoardCardDto> Items);
```

## WorkItemBoardCardDto (new)

```csharp
public record WorkItemBoardCardDto(
    int Id,
    string Type,
    string Title,
    string Status,
    string Priority,
    int? AssigneeUserId,
    string? AssigneeName,
    DateTime? DueDate,
    DateTime UpdatedAt,
    int CreatedByUserId,
    int DirectChildrenCount,
    int DirectChildrenDoneCount);
```

Field selection rationale: exactly what a card displays (FR-009/FR-011)
plus `CreatedByUserId`/`AssigneeUserId` (needed client-side for the
drag-permission check, mirroring the same fields `WorkItemDto` already
exposes) — deliberately excludes `Description`/`ParentWorkItemId`, which
the card never shows and which the new status-only `PATCH` endpoint (next
section) makes unnecessary to carry around (research.md #3).

**Ordering**: returned already sorted by `UpdatedAt` descending (matches
FR-012 — "most recently updated first" within a column — the frontend
groups by `Status` after receiving; it does not need to re-sort).

## UpdateWorkItemStatusRequest (new)

```csharp
public record UpdateWorkItemStatusRequest(string Status);
```

Validated the same way `WorkItemRequest.Status` already is (must parse to
a defined `WorkItemStatus` value) — reuses existing validation, not a new
rule.

## Board Column (frontend-only concept, not a new backend entity)

| Field | Type | Notes |
|---|---|---|
| `status` | `WorkItemStatus` | One of the values in `WorkItemBoardDto.columns` |
| `label` | `string` | Derived client-side from `StatusChipComponent`'s existing status→label map — not sent by the backend (research.md #2) |
| `items` | `WorkItemBoardCardDto[]` | This column's cards, filtered client-side from `WorkItemBoardDto.items` by `status` |

## `canEditWorkItem` (frontend, shared pure function)

```ts
function canEditWorkItem(
  item: { createdByUserId: number; assigneeUserId: number | null },
  currentUserId: number,
  currentRole: UserRole | null
): boolean
```

Returns `item.createdByUserId === currentUserId || item.assigneeUserId ===
currentUserId || currentRole === 'Manager' || currentRole === 'Admin'` —
the exact rule already enforced server-side in `WorkItemService.
EnsureCanEdit` (research.md #3/#5), extracted to one shared location used
by `project-detail`, `work-item-detail`, and the new board.

## `isOverdue` (frontend, pure function)

```ts
function isOverdue(dueDate: string | null, status: WorkItemStatus): boolean
```

Returns `true` only when `dueDate` is present, strictly before the current
date (date-only comparison, not time-of-day), and `status !== 'Done'`
(FR-010).
