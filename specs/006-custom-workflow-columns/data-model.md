# Data Model: Custom Workflow Columns

## Entity: WorkflowStatus (new)

Replaces the system-wide `WorkItemStatus` enum. One row = one column, owned
by exactly one project.

| Field | Type | Notes |
|---|---|---|
| `Id` | `int` (PK) | Identity. |
| `ProjectId` | `int` (FK → `Project`) | Not null. `OnDelete(Cascade)` — deleting a project deletes its statuses (consistent with the existing `Project → WorkItem` cascade). |
| `Name` | `string` | 2–30 chars. Unique within `ProjectId`, case-insensitive (composite index on `(ProjectId, Name)`, relying on SQL Server's default collation — same mechanism as `Project.Name`/`User.Email`). |
| `Position` | `int` | 0-based, dense, no gaps, unique within `ProjectId`. Defines column order everywhere (FR-002, FR-012). |
| `Category` | `enum { Open, Done }` | `HasConversion<string>()`. Fixed at creation (FR-021) — no update path changes it. |
| `ColorKey` | `enum ChipColor { Slate, Blue, Violet, Amber, Teal, Rose, Indigo, Cyan, Green, Emerald }` | `HasConversion<string>()`. Assigned at creation (research.md #3); editable afterward via the rename/recolor endpoint (FR-015), independent of `Name`. |

**Validation rules** (enforced in `ProjectStatusService`, not the database,
except where noted):
- A project MUST always have ≥1 `Category = Open` and ≥1 `Category = Done`
  row (FR-003). Checked before every add/rename(n/a)/reorder(n/a)/delete
  that could reduce a category to zero — in practice only **delete**
  can violate this, since add only increases counts and rename/reorder
  don't touch `Category`.
- A project MUST NOT have >10 rows (FR-004) — checked before add.
- `Name` uniqueness is a database-enforced unique index (composite,
  case-insensitive via collation) — `ProjectStatusService` also
  pre-checks to raise a friendly `DuplicateStatusNameException` rather
  than surfacing a raw SQL unique-violation.
- `Category` has no update path (FR-021) — `UpdateWorkflowStatusRequest`
  simply has no `Category` field, so this is enforced by omission, not a
  runtime check.

**Derived (not stored)**:
- **Default completion status** (FR-024) = the `WorkflowStatus` with
  `Category = Done` and the lowest `Position`, for a given project.
  Computed by `ProjectStatusService.GetDefaultCompletionStatusId(projectId)`
  on demand; not a column, not maintained on delete (research.md #5).
- **Item count** (shown on the management screen, FR-009) =
  `COUNT(WorkItem WHERE WorkflowStatusId = this.Id)`, computed per
  request, not cached.

## Entity: Project (extended)

| Field | Change |
|---|---|
| `WorkflowStatuses` | NEW navigation collection, `ICollection<WorkflowStatus>` — mirrors the existing `WorkItems` collection pattern. |

No changes to existing `Project` fields. `ProjectService.CreateAsync` gains
one responsibility: after inserting the new `Project`, insert its four
standard `WorkflowStatus` rows (FR-005) in the same
`SaveChangesAsync()` batch:

| Position | Name | Category | ColorKey |
|---|---|---|---|
| 0 | To Do | Open | Slate |
| 1 | In Progress | Open | Blue |
| 2 | In Review | Open | Violet |
| 3 | Done | Done | Green |

(`Slate`/`Blue`/`Violet`/`Green` chosen to visually match the existing
fixed token colors exactly — see research.md #3 — so a freshly created
project's board/chips look identical to what today's fixed enum already
produces.)

## Entity: WorkItem (changed)

| Field | Change |
|---|---|
| `Status` (`WorkItemStatus` enum) | **REMOVED.** |
| `WorkflowStatusId` | NEW, `int` (FK → `WorkflowStatus`). Not null (post-migration). `OnDelete(Restrict)` — the database is a safety net; application code (`ProjectStatusService.DeleteAsync`) always reassigns referencing `WorkItem` rows before removing a `WorkflowStatus`, so this FK should never actually block a delete in practice. |
| `WorkflowStatus` | NEW navigation property. |

No other `WorkItem` field changes. All existing fields (`Type`, `Title`,
`Description`, `Priority`, `AssigneeUserId`, `DueDate`,
`ParentWorkItemId`, etc.) are untouched by this feature.

**Indexes**: `WorkItems.WorkflowStatusId` gets an index (every board/filter
query groups or filters on it — the same rationale already applied to
`ProjectId` and `ParentWorkItemId` on this table).

## Migration mapping (existing data → new rows)

For every existing `Project`, insert the standard four `WorkflowStatus`
rows from the table above. For every existing `WorkItem`, set
`WorkflowStatusId` to the row with matching `ProjectId` and a `Name`
corresponding to its old `Status` enum value:

| Old `WorkItemStatus` enum value | New `WorkflowStatus.Name` |
|---|---|
| `ToDo` | To Do |
| `InProgress` | In Progress |
| `InReview` | In Review |
| `Done` | Done |

No item's effective state changes — same category, same relative order,
same visual color (FR-006, SC-003).

## Category-based reasoning (replaces name/enum-based checks)

Every existing computation that used to check `Status == WorkItemStatus.Done`
(or the string `"Done"`) now checks `WorkflowStatus.Category ==
WorkflowStatusCategory.Done` via the navigation property, or an
equivalent join/`Include()`:

| Surface | Old check | New check |
|---|---|---|
| `ProjectService.GetProjectsAsync` open-item count | `w.Status != WorkItemStatus.Done` | `w.WorkflowStatus!.Category != WorkflowStatusCategory.Done` |
| `WorkItemService.GetTreeAsync` "n/m done" | `c.Status == "Done"` (string) | `c.StatusCategory == "Done"` (flattened DTO field, itself sourced from `WorkflowStatus.Category`) |
| Overdue highlighting (frontend `isOverdue()`) | `item.status !== 'Done'` | `item.statusCategory !== 'Done'` |

## Request/response DTO shape changes

| DTO | Old | New |
|---|---|---|
| `WorkItemRequest` | `Status: string?` (enum name) | `StatusId: int?` (defaults to the project's first Open status by position when omitted, mirroring today's "defaults to ToDo" behavior) |
| `UpdateWorkItemStatusRequest` | `Status: string` | `StatusId: int` |
| `WorkItemDto` / `WorkItemDetailDto` / `WorkItemChildDto` / `WorkItemTreeNodeDto` / `WorkItemBoardCardDto` | `Status: string` | `StatusId: int`, `StatusName: string`, `StatusCategory: string`, `StatusColorKey: string` |
| `BoardColumnDto` | `{ Status: string, Label: string }` | `{ StatusId: int, Name: string, Category: string, ColorKey: string }` |
| `WorkflowStatusDto` (new) | — | `{ Id, Name, Category, ColorKey, Position, ItemCount }` |

See [contracts/workflow-api.md](./contracts/workflow-api.md) for full
request/response examples and status codes.
