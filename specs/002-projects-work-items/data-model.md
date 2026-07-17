# Phase 1 Data Model: Projects & Work Items

## Project

Represents a named container of work (FR-001–FR-009).

| Field | Type | Rules |
|---|---|---|
| `Id` | `int` (identity PK) | |
| `Name` | `string` | Required, 3–100 chars. Unique, case-insensitive (SQL Server's default collation already compares this way, same mechanism as `User.Email` in Feature 001) — enforced via a unique index. |
| `Description` | `string?` | Optional, up to 2000 chars. |
| `CreatedByUserId` | `int` (FK → `User.Id`) | Required. `DeleteBehavior.Restrict` (research.md §2). |
| `CreatedAt` | `DateTime` | Set once, at creation. |

**Relationships**: One `Project` has many `WorkItem`s
(`Project.Id` ← `WorkItem.ProjectId`), `DeleteBehavior.Cascade` — deleting
a `Project` deletes all of its `WorkItem`s (FR-009).

**Indexes**: Unique index on `Name`. Index on `CreatedByUserId` isn't added
explicitly — EF Core adds one automatically for every foreign key, and no
query in this feature filters projects by creator.

**Derived/computed, not stored**: A project's *open work item count*
(`ProjectListItemDto.OpenWorkItemCount`, FR-006) and *total work item
count* (`ProjectDetailDto.TotalWorkItemCount`, research.md §5) are both
computed at query time (`COUNT` over related `WorkItem` rows), never
persisted columns — they'd otherwise need to stay in sync with every
work-item create/delete, which is exactly the kind of derived-state bug
class to avoid by just querying it fresh.

## WorkItem

Represents a single unit of trackable work belonging to exactly one
Project (FR-010–FR-018).

| Field | Type | Rules |
|---|---|---|
| `Id` | `int` (identity PK) | |
| `ProjectId` | `int` (FK → `Project.Id`) | Required. Immutable after creation (FR-014) — no update path ever sets this field. |
| `Type` | `WorkItemType` enum (`Epic`, `Story`, `Task`, `SubTask`) | Required. Stored as `string` (research.md §3). A label only — no parent/child relationship in this feature. |
| `Title` | `string` | Required, 3–200 chars. |
| `Description` | `string?` | Optional, up to 5000 chars. |
| `Priority` | `WorkItemPriority` enum (`Low`, `Medium`, `High`, `Critical`) | Defaults to `Medium` if not specified. Stored as `string`. |
| `Status` | `WorkItemStatus` enum (`ToDo`, `InProgress`, `Done`) | Defaults to `ToDo` if not specified. Stored as `string`. No restricted transitions — any status may be set to any other directly (spec does not define a state machine here). |
| `AssigneeUserId` | `int?` (FK → `User.Id`) | Optional. When set, must reference an existing `User` (FR-011). `DeleteBehavior.Restrict`. |
| `DueDate` | `DateTime?` | Optional. Any date accepted, including past dates (FR-012). |
| `CreatedByUserId` | `int` (FK → `User.Id`) | Required. `DeleteBehavior.Restrict`. |
| `CreatedAt` | `DateTime` | Set once, at creation. |
| `UpdatedAt` | `DateTime` | Set at creation, advanced on every successful edit (FR-013, FR-015). |

**Relationships**: Belongs to exactly one `Project` (required,
non-nullable `ProjectId`, immutable). Optionally references one `User` as
assignee; required reference to one `User` as creator.

**Indexes**: Index on `ProjectId` — every "list this project's work items"
query filters on it, and this is the feature's single highest-volume query
path. No other explicit indexes: `Status`/`Type`/`Priority`/`AssigneeUserId`
filtering happens at this feature's internal-tool scale (tens to low
hundreds of users, per plan.md Scale/Scope) without needing a dedicated
index yet — added later if a real performance need appears (YAGNI).

## Authorization derived from these entities (not a stored field)

- **Project** create/edit/delete: caller's `Role` must be `Manager` or
  `Admin` (checked via `[Authorize(Roles = "Manager,Admin")]`, no entity
  data needed).
- **WorkItem** edit: caller's user id must equal `CreatedByUserId` OR
  `AssigneeUserId`, OR caller's `Role` must be `Manager`/`Admin` (checked in
  `WorkItemService`, since it depends on the specific row).
- **WorkItem** delete: caller's user id must equal `CreatedByUserId`, OR
  caller's `Role` must be `Manager`/`Admin` (narrower than edit — the
  current assignee alone cannot delete, per FR-018).
