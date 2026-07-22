# Data Model: Project Summary Dashboard & Activity Log

## New entity: `ActivityLogEntry`

An immutable, append-only record of one tracked event. No service method
ever updates or deletes a row of this table — immutability is structural
(the capability doesn't exist), not a guarded permission check.

| Field | Type | Notes |
|---|---|---|
| `Id` | `int` (identity PK) | |
| `ProjectId` | `int` | Real FK → `Project`, `Cascade` (research.md #2). Derived from the work item's project *at the time of the change* (spec FR-015) — never re-derived later. |
| `WorkItemId` | `int` | **Plain column, no FK/navigation** (research.md #1). Refers to a `WorkItem.Id` that may no longer exist. |
| `WorkItemTitle` | `string` | Snapshot, captured at write time — not a live join. Survives the work item's later deletion or rename. |
| `WorkItemType` | `string` | Snapshot (`"Epic"`/`"Story"`/`"Task"`/`"SubTask"`), same reasoning as `WorkItemTitle` — needed for the activity sentence ("...changed **Task** 'Fix login'...") and can't be live-joined once the item is gone. |
| `ActorUserId` | `int` | Real FK → `User`, `Restrict` (research.md #3) — same convention as `WorkItem.CreatedByUserId`. |
| `EventType` | `ActivityEventType` (string-converted) | `Created` or `FieldChanged`. |
| `Field` | `ActivityField?` (string-converted, nullable) | Null when `EventType == Created`. One of `Status`/`Priority`/`Assignee`/`Sprint` when `EventType == FieldChanged`. |
| `OldValue` | `string?` | Display-ready text (e.g. `"To Do"`, `"Jane Doe"`, `"Unassigned"`, `"Backlog"`). Null when `EventType == Created`. |
| `NewValue` | `string?` | Display-ready text, same convention as `OldValue`. Null when `EventType == Created`. |
| `CreatedAt` | `DateTime` (UTC) | Set once, at write time. |

**Enums** (new, in `Data/Entities`):

```csharp
public enum ActivityEventType { Created, FieldChanged }
public enum ActivityField { Status, Priority, Assignee, Sprint }
```

**Indexes**:
- `ProjectId` (+ implicit `CreatedAt DESC` ordering at query time) — the
  project feed's primary access path.
- `WorkItemId` — the per-item history's primary access path. (A plain,
  non-FK index — EF Core's `HasIndex` works on any scalar column regardless
  of whether it's also a foreign key.)

**Validation rules**: None enforced by request DTOs — there is no create/
update/delete endpoint for this entity at all. Every row is constructed
internally by `ActivityLogService`, called only from `WorkItemService`'s
existing, already-validated `CreateAsync`/`UpdateAsync`/`UpdateStatusAsync`/
`UpdateSprintAsync` methods, after those methods' own validation has already
passed.

**Relationships**:
- `Project` (1) → `ActivityLogEntry` (many), `Cascade`.
- `User` (1, as Actor) → `ActivityLogEntry` (many), `Restrict`.
- `WorkItem` → `ActivityLogEntry`: **no database relationship** (see
  research.md #1). The association exists only via the plain `WorkItemId`
  int value and the `WorkItemTitle`/`WorkItemType` snapshots.

## Modified: `WorkItemService`'s write paths (no entity/column changes)

No existing entity gains a new column. `WorkItemService.CreateAsync`,
`UpdateAsync`, `UpdateStatusAsync`, and `UpdateSprintAsync` are modified to
also write `ActivityLogEntry` rows as a side effect, per the write-path/
transaction rules in research.md #6 and the old-value lookup rules in
research.md #7:

| Method | Entry written | Old-value source |
|---|---|---|
| `CreateAsync` | One `Created` entry. | N/A — no prior state. |
| `UpdateAsync` | Zero or more `FieldChanged` entries (one per changed tracked field among Status/Priority/Assignee/Sprint — research.md #5). | Fetched before mutation: old status name, old priority (`.ToString()`, no lookup), old assignee name (`"Unassigned"` if null), old sprint name (`"Backlog"` if null). |
| `UpdateStatusAsync` | Zero or one `FieldChanged` (`Status`) entry — none if the new status equals the current one. | Old status name, fetched before mutation. |
| `UpdateSprintAsync` | Zero or one `FieldChanged` (`Sprint`) entry — none if the new sprint id equals the current one. | Old sprint name (`"Backlog"` if the item had no sprint), fetched before mutation. |

No other `WorkItem` field change (title, description, dates, labels,
hierarchy) writes an entry (spec FR-014).

## Computed view (not stored): `ProjectSummaryDto`

Returned by the new `WorkItemService.GetSummaryAsync(int projectId)`. Not a
table — assembled fresh from `WorkItem`/`WorkflowStatus`/`User` on every
call, the same way `WorkItemBoardDto`/`WorkItemTreeNodeDto` already are.

```csharp
public record ProjectSummaryDto(
    StatCardsDto StatCards,
    List<StatusBreakdownItemDto> StatusBreakdown,
    List<PriorityBreakdownItemDto> PriorityBreakdown,
    List<WorkloadRowDto> Workload);

public record StatCardsDto(
    int Total, int Completed, double CompletedPercent, int InProgress, int DueSoon);

public record StatusBreakdownItemDto(int StatusId, string Name, string ColorKey, int Count);

public record PriorityBreakdownItemDto(string Priority, int Count);

public record WorkloadRowDto(int? UserId, string DisplayName, int OpenItemCount);
```

**Computation rules** (research.md #9–#12):
- `Total`: every work item in the project (Epics included, spec FR-006).
- `Completed`/`CompletedPercent`: count where `WorkflowStatus.Category ==
  Done`; percent is `0` when `Total == 0` (avoid divide-by-zero), otherwise
  `round(Completed / Total * 100)`.
- `InProgress`: count where `Category == Open` — i.e. `Total − Completed`
  (research.md #9; the workflow model has no third category).
- `DueSoon`: count where `Category != Done`, `DueDate` is not null, and
  `DueDate.Value.Date` is within `[UtcNow.Date, UtcNow.Date.AddDays(7)]`
  inclusive (research.md #12).
- `StatusBreakdown`: one row per the project's `WorkflowStatus` rows, ordered
  by `Position`, each with its live item count (zero included, since every
  configured column is listed regardless of whether any item currently sits
  in it).
- `PriorityBreakdown`: exactly 4 rows, one per `WorkItemPriority` member, in
  enum declaration order (Low/Medium/High/Critical), zero-count levels
  included (spec FR-008).
- `Workload`: research.md #10's two-query merge — open-item counts per
  assignee, unioned with every system Manager/Admin (zero-count included),
  plus a synthetic `UserId: null, DisplayName: "Unassigned"` row when ≥1 open
  item has no assignee. Sorted by `OpenItemCount` descending; tie-break is
  unspecified (spec's own Assumptions section) — implemented as a stable sort
  by `DisplayName` for determinism, not a business requirement.

## Computed view (not stored): `ActivityEntryDto`

Returned by both the paginated project feed and the unpaginated item
history — same shape either way (research.md #15's "one shared rendering"
requirement starts here, at the DTO level, not just the component level).

```csharp
public record ActivityEntryDto(
    int Id,
    int WorkItemId,
    string WorkItemTitle,
    string WorkItemType,
    string EventType,
    string? Field,
    string? OldValue,
    string? NewValue,
    int ActorUserId,
    string ActorName,
    DateTime CreatedAt);
```

`ActorName` is a live join (`entry.Actor!.FullName`, research.md #3) —
everything else is a direct column read (including the two `WorkItemId`/
`WorkItemTitle`/`WorkItemType` values already captured as snapshots on the
row).

## State / lifecycle notes

`ActivityLogEntry` has no state machine and no transitions — every row is
written once, at event time, and never changes again. This is the simplest
possible lifecycle (none), which is itself the mechanism guaranteeing FR-017
("no API MUST expose the ability to edit or delete an entry") — there is no
service method that could do so, so there is nothing to authorize or guard
against.
