# Data Model: Work Item Modal & Quick Creation

## Modified entity: `WorkItem` (`Data/Entities/WorkItem.cs`)

One new field, added alongside the existing `DueDate`:

| Field | Type | Notes |
|---|---|---|
| `StartDate` | `DateTime?` | Optional, date-only by convention (same `YYYY-MM-DD` transport convention as `DueDate` — no time-of-day component is meaningful). No database constraint expresses start ≤ due (SQL Server can't compare two nullable columns in a `CHECK` the way this rule needs when either may be absent) — enforced entirely in `WorkItemService` (see Validation rules below), the same division of labor already used for the hierarchy rules in `ValidateParentAsync`. |

New navigation collection:

| Field | Type | Notes |
|---|---|---|
| `Labels` | `ICollection<WorkItemLabel>` | Join rows to this item's attached labels. Mirrors the existing `Children` collection's shape (a plain `ICollection`, initialized to an empty `List<>`). |

## New entity: `Label` (`Data/Entities/Label.cs`)

| Field | Type | Notes |
|---|---|---|
| `Id` | `int` | Identity PK, same convention as every other entity. |
| `ProjectId` | `int` | FK → `Project`. A label belongs to exactly one project (spec: "a label is project-scoped"). |
| `Project` | `Project?` | Navigation. |
| `Name` | `string` | 1–30 characters (spec), unique per project **case-insensitively** via `HasIndex(l => new { l.ProjectId, l.Name }).IsUnique()` relying on SQL Server's default collation — same mechanism as `WorkflowStatus.Name`/`Project.Name`/`User.Email` (research.md #4). |
| `CreatedAt` | `DateTime` | Set once, at first creation; no `UpdatedAt` — a label's name is never edited in v1 (no rename endpoint exists). |
| `WorkItemLabels` | `ICollection<WorkItemLabel>` | Reverse navigation — every work item currently carrying this label. |

Labels are **never deleted** by any code path in this feature (research.md
#5) — an unused label's row persists but is excluded from
`GetProjectLabelsAsync`'s result by a query-time `.Any()` filter, not by
deletion.

## New entity: `WorkItemLabel` (`Data/Entities/WorkItemLabel.cs`)

The explicit join entity for the `WorkItem` ↔ `Label` many-to-many
relationship — the first many-to-many in this codebase (research.md #3).

| Field | Type | Notes |
|---|---|---|
| `Id` | `int` | Identity PK (not a composite `(WorkItemId, LabelId)` key) — consistent with this codebase's "every entity gets its own int identity PK" convention (constitution: Database section). |
| `WorkItemId` | `int` | FK → `WorkItem`. |
| `WorkItem` | `WorkItem?` | Navigation. |
| `LabelId` | `int` | FK → `Label`. |
| `Label` | `Label?` | Navigation. |

**Indexes**:
- `HasIndex(wl => new { wl.WorkItemId, wl.LabelId }).IsUnique()` — prevents
  attaching the same label to an item twice (FR-019) and makes the 0–5 cap
  check a plain `Count()` against this index rather than a broader scan.
- `HasIndex(wl => wl.WorkItemId)` is implied by the composite index above
  (SQL Server can use a leading-column prefix of a composite index), so no
  separate single-column index is added.

**Cascade behavior** (`AppDbContext.OnModelCreating`):
- `WorkItem → WorkItemLabel`: `DeleteBehavior.Cascade` — deleting a work item
  removes its label attachments, consistent with `Project → WorkItem`'s
  existing cascade and needed for `WorkItemService.DeleteAsync`'s existing
  subtree-delete path to work without a separate cleanup step.
- `Label → WorkItemLabel`: `DeleteBehavior.Cascade` — never actually fires in
  this feature (no code path deletes a `Label`), but is the semantically
  correct rule for the relationship and costs nothing to declare now, avoiding
  a future schema change if a label-management screen is ever added.
- `Project → Label`: `DeleteBehavior.Cascade` — deleting a project deletes its
  labels, same rule as `Project → WorkItem`/`Project → WorkflowStatus`.

## Validation rules (enforced in `WorkItemService`, not the database)

| Rule | Where | Failure |
|---|---|---|
| `StartDate` ≤ `DueDate` when both are present | `CreateAsync`, `UpdateAsync` | `InvalidDateRangeException` → 400 |
| Each label name is 1–30 characters after trimming | `CreateAsync`, `UpdateAsync` (shared label-normalization helper) | `InvalidLabelException` → 400 |
| A work item carries at most 5 labels | same helper | `TooManyLabelsException` → 400 |
| No duplicate label on the same item | same helper (case-insensitive dedupe before attach; the unique index is a backstop, not the primary check) | silently deduped, not an error — matches spec Edge Cases: "the modal ignores the duplicate attempt rather than attaching it twice" |
| A label name reused across requests (case-insensitive) resolves to the same `Label` row within a project | same helper | not an error — existing row is found and reused, never duplicated |

## State / lifecycle notes

- No new state machine — `WorkItem`'s existing status/lifecycle (Feature 006)
  is untouched by this feature.
- `Label` has exactly one lifecycle event in this feature: creation (via the
  label-normalization helper's find-or-create). No update, no delete, no
  separate creation endpoint — a label only ever comes into existence as a
  side effect of `CreateAsync`/`UpdateAsync` on a `WorkItem`.
- `WorkItemLabel` rows are created/removed as a byproduct of a work item's
  `Labels` list changing on `UpdateAsync` (the full requested set replaces the
  prior set, the same "PUT replaces the whole resource" semantics
  `WorkItemRequest` already uses for every other field) and removed via
  cascade on `WorkItem` deletion.

## DTO changes (see `contracts/work-item-modal-api.md` for full shapes)

| DTO | Change |
|---|---|
| `WorkItemRequest` | `+ DateTime? StartDate`, `+ List<string>? Labels` |
| `WorkItemDto` | `+ DateTime? StartDate`, `+ List<string> Labels` |
| `WorkItemDetailDto` | `+ DateTime? StartDate`, `+ List<string> Labels` |
| `WorkItemBoardCardDto` | `+ List<string> Labels` (no `StartDate` — spec: "no board-card change in this feature" for start date) |
| `WorkItemTreeNodeDto` | `+ List<string> Labels` |
| `WorkItemChildDto` | unchanged — this DTO already omits fields (`Priority`, `DueDate`) that its compact detail-page children list doesn't need; `Labels` follows the same omission for the same reason |
| `WorkItemLookupItemDto`, `WorkItemBoardDto`, `BoardColumnDto`, `UpdateWorkItemStatusRequest` | unchanged |

New standalone response shape:

| DTO | Shape | Used by |
|---|---|---|
| (none — plain `List<string>`) | `List<string>` of label names | `GET /api/projects/{projectId}/labels` |

## Frontend type changes (`frontend/src/app/projects/work-items.service.ts`)

Mirrors the backend DTO changes 1:1 (existing convention — no client-side
mapping layer):

- `WorkItemRequest`: `+ startDate?: string`, `+ labels?: string[]`
- `WorkItem`: `+ startDate: string | null`, `+ labels: string[]`
- `WorkItemDetail` (extends `WorkItem`): inherits both fields
- `WorkItemBoardCard`: `+ labels: string[]` (no `startDate`)
- `WorkItemTreeNode`: `+ labels: string[]`
- `WorkItemsFilter`: `+ label?: string`
- New method: `getProjectLabels(projectId: number): Promise<string[]>`
