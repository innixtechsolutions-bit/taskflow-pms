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
    List<BoardColumnDto> Columns,       // ordered; see below
    List<WorkItemBoardCardDto> Items);

public record BoardColumnDto(
    string Status,                      // "ToDo" | "InProgress" | "InReview" | "Done"
    string Label);                      // "To Do" | "In Progress" | "In Review" | "Done"
```

**Revised during triage (M1)**: `Columns` was originally a bare
`List<string>` of status names, with the frontend deriving each column's
header text from `StatusChipComponent`'s own label map. That breaks
FR-006's explicit forward-compat requirement — see research.md #2's
"Revised during triage" note. `Columns` now carries both the status value
*and* its display label, computed server-side, so the board component
never needs its own knowledge of what a given status/column is called.
Feature 006 (custom per-project columns) becomes purely a backend change;
the board component renders whatever `Columns` it's given.

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

## Board Column (frontend view-model — wraps a backend-supplied `BoardColumnDto`)

| Field | Type | Notes |
|---|---|---|
| `status` | `WorkItemStatus` | Taken verbatim from `BoardColumnDto.status` |
| `label` | `string` | Taken verbatim from `BoardColumnDto.label` — the backend's value, not client-derived (research.md #2, revised) |
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

**Revised during triage (M2) — precise definition**: an item is overdue
when, and only when, both hold:

1. `dueDate` is present, and its **date-only** portion (year/month/day —
   any time-of-day component on `dueDate` is ignored) is **strictly
   before** today's **date-only, local-calendar** date. "Today" means the
   date on the clock/calendar the browser is running in (`new Date()`'s
   local year/month/day), not a UTC date and not a date-time instant — a
   due date of today at any time, or a due date earlier today, is NOT
   overdue; only a due date belonging to a day strictly before today's
   local calendar date counts.
2. `status !== 'Done'`.

**Exact algorithm** (sharpened further while implementing T016 — the
straightforward-looking approach has a real bug): `dueDate` arrives from
the backend as a UTC ISO string (e.g. `"2026-07-20T00:00:00Z"`). Passing
that string through `new Date(dueDate)` and then reading its *local*
getters (`.getDate()` etc.) would silently convert it to local time first
— for a user behind UTC, a due date of `2026-07-20T00:00:00Z` becomes
`2026-07-19T‥` locally, shifting the calendar date by a day. Since a due
date is a date-only concept, not an instant, `isOverdue` never round-trips
it through timezone conversion at all: it reads the `YYYY-MM-DD`
substring directly off the front of the ISO string (`dueDate.slice(0,
10)`) and compares that lexicographically against *today's* local
calendar date, built from `new Date()`'s own local
`getFullYear()`/`getMonth()`/`getDate()` (zero-padded to the same
`YYYY-MM-DD` shape, so plain string comparison is correct chronological
ordering). Only `dueDate`'s literal digits are ever compared — never a
`Date` instant derived from it. This is the one place in the algorithm
"local" applies: to *today*, not to `dueDate`.

FR-010's plain-language "in the past" is intentionally sharpened here to
this exact algorithm so the test suite (T016) has one unambiguous
behavior to assert, including the explicit "due today is not overdue"
boundary case and a case that would come out differently under the naive
(and wrong) `new Date(dueDate).getDate()` approach.
