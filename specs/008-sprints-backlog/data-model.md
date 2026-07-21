# Data Model: Sprints & Backlog

## New entity: `Sprint` (`Data/Entities/Sprint.cs`)

| Field | Type | Notes |
|---|---|---|
| `Id` | `int` | Identity PK, same convention as every other entity. |
| `ProjectId` | `int` | FK → `Project`. Immutable after creation — a sprint always belongs to the project it was created in (same convention as `WorkflowStatus.ProjectId`). |
| `Project` | `Project?` | Navigation. |
| `Name` | `string` | 2–50 characters, unique **per project, case-insensitively** — `HasIndex(s => new { s.ProjectId, s.Name }).IsUnique()`, relying on SQL Server's default collation, same mechanism as `WorkflowStatus.Name`/`Label.Name`/`Project.Name`/`User.Email` (research.md #4 in Feature 007, same technique reused here). |
| `StartDate` | `DateTime` | Required, date-only by convention (same transport convention as `WorkItem.DueDate`/`StartDate`). |
| `EndDate` | `DateTime` | Required. Must be strictly after `StartDate` — no database `CHECK` constraint (SQL Server can express this one, unlike the nullable-pair case in `WorkItem`, but it stays in `SprintService` for consistency with every other cross-field rule in this codebase, all of which live in the service layer). |
| `Status` | `SprintStatus` | `Planned` (default) → `Active` → `Completed`. Stored as text (`HasConversion<string>()`), same rationale as `User.Role`/`WorkItem.Type`/`WorkflowStatus.Category` — a readable column value is a deliberate teaching touch (constitution Principle VI) with no runtime cost. |
| `WorkItems` | `ICollection<WorkItem>` | Reverse navigation — every work item currently assigned to this sprint. |

```csharp
public enum SprintStatus { Planned, Active, Completed }
```

## Modified entity: `WorkItem` (`Data/Entities/WorkItem.cs`)

One new nullable field, alongside the existing `WorkflowStatusId`:

| Field | Type | Notes |
|---|---|---|
| `SprintId` | `int?` | Optional — `null` means "in the backlog" (no sprint). Only `Story`/`Task`/`SubTask` may have this set; `Epic` never does (enforced in `WorkItemService`, not the database — a column can't express "forbidden depending on Type," the same reasoning already applied to `ParentWorkItemId`'s hierarchy rules). |
| `Sprint` | `Sprint?` | Navigation. |

## `AppDbContext.OnModelCreating` additions

```csharp
modelBuilder.Entity<Sprint>(entity =>
{
    entity.HasIndex(s => new { s.ProjectId, s.Name }).IsUnique();

    // Deleting a project deletes its sprints -- consistent with the existing
    // Project -> WorkItem/WorkflowStatus/Label cascades.
    entity.HasOne(s => s.Project)
        .WithMany(p => p.Sprints)
        .HasForeignKey(s => s.ProjectId)
        .OnDelete(DeleteBehavior.Cascade);

    entity.Property(s => s.Status).HasConversion<string>();
});
```

```csharp
// Inside the existing modelBuilder.Entity<WorkItem>(entity => { ... }) block:

entity.HasIndex(w => w.SprintId);

// Restrict, not Cascade -- same "multiple cascade paths" reasoning already
// documented for WorkflowStatus -> WorkItem above (research.md #7): Project ->
// WorkItem is already Cascade, so a second Cascade path via Project -> Sprint
// -> WorkItem would be rejected by SQL Server outright (error 1785).
// Harmless in practice: a Project delete cascades both tables together, and a
// single Sprint is only ever deletable while empty (FR-010).
entity.HasOne(w => w.Sprint)
    .WithMany(s => s.WorkItems)
    .HasForeignKey(w => w.SprintId)
    .OnDelete(DeleteBehavior.Restrict);
```

`Project` also gains a `Sprints` collection navigation property, mirroring its
existing `WorkflowStatuses` collection.

## State transitions (`Sprint.Status`)

```
Planned --Start--> Active --Complete--> Completed
```

No other transitions exist in this feature (no `Active`/`Completed` back to
`Planned`, no re-opening a `Completed` sprint) — see spec's Out of Scope.

| Transition | Guard | Effect |
|---|---|---|
| Create | Name 2–50 chars, unique in project (case-insensitive); `EndDate > StartDate` | New row, `Status = Planned` |
| `Planned → Active` (Start) | ≥1 `WorkItem` with `SprintId` = this sprint; no other `Sprint` in the same project has `Status = Active` | `Status = Active` |
| `Active → Completed` (Complete) | If any assigned item's `WorkflowStatus.Category != Done` ("not-Done"), a resolution is required: `Backlog` (clear `SprintId` on those items) or `Sprint` (reassign `SprintId` to a `Planned`/`Active` sprint in the same project, not itself). If no not-Done items exist, no resolution is needed. | `Status = Completed`; not-Done items resolved per above; Done items keep `SprintId` unchanged (history). |
| Delete | `Status = Planned` (implies never started — research.md #9) and zero assigned `WorkItem`s | Row removed |

Once `Status = Completed`, the sprint is **read-only from the item-assignment
side**: no `WorkItem`'s `SprintId` may be set to, or cleared from, a
`Completed` sprint (`SprintReadOnlyException`). The items themselves remain
normally editable (title, status, assignee, etc.) — only the sprint
*relationship* is frozen.

## Validation rules (enforced in `SprintService`/`WorkItemService`, not the database)

| Rule | Where | Failure |
|---|---|---|
| `Name` is 2–50 characters | `SprintService.CreateAsync` | `InvalidSprintNameException` → 400 |
| `Name` is unique within the project (case-insensitive) | `SprintService.CreateAsync` | `DuplicateSprintNameException` → 409 |
| `EndDate` strictly after `StartDate` | `SprintService.CreateAsync` | `InvalidSprintDateRangeException` → 400 |
| Start requires ≥1 assigned item | `SprintService.StartAsync` | `EmptySprintException` → 400 |
| Start requires no other `Active` sprint in the project | `SprintService.StartAsync` | `AnotherSprintActiveException` (names the active sprint) → 409 |
| Start requires `Status == Planned` | `SprintService.StartAsync` | `SprintNotPlannedException` → 400 |
| Complete requires `Status == Active` | `SprintService.CompleteAsync` | `SprintNotActiveException` → 400 |
| Complete requires a resolution when not-Done items exist | `SprintService.CompleteAsync` | `DestinationRequiredException` (carries the not-Done item count, same `ProblemDetails.Extensions["itemCount"]` pattern as `DestinationStatusRequiredException`) → 400 |
| Complete's destination sprint (when `Resolution = "Sprint"`) must exist, belong to the same project, be `Planned` or `Active`, and not be the sprint being completed | `SprintService.CompleteAsync` | `InvalidDestinationSprintException` → 400 |
| Delete requires `Status == Planned` and zero items | `SprintService.DeleteAsync` | `SprintNotDeletableException` → 400 |
| An item's `SprintId` (when set) must belong to the item's own project | `WorkItemService.CreateAsync`/`UpdateAsync`/`UpdateSprintAsync` | `SprintNotFoundException` → 404 |
| `Epic`-type items may never have a non-null `SprintId` | same three methods | `EpicCannotBeInSprintException` → 400 |
| An item's `SprintId` may not be set to, or cleared from, a `Completed` sprint | same three methods | `SprintReadOnlyException` → 400 |

## Backlog view response shape (read model, not a stored entity)

`WorkItemService.GetBacklogAsync` returns sections built at query time, not a
persisted structure:

- One section per `Sprint` in the project (ordered by `StartDate` ascending —
  research.md #2), each carrying its own filtered `WorkItem` list.
- One implicit "Backlog" section: every `WorkItem` in the project with
  `SprintId == null` (this is where `Epic`s always appear, since they can
  never have a `SprintId` — research.md/spec's "Epics render for context").
- All sections share the same five-filter predicate already used by
  `GetWorkItemsAsync` (`statusId`/`type`/`priority`/`assigneeUserId`/`search`/
  `label`), extracted into one shared private query-builder (research.md #5).
