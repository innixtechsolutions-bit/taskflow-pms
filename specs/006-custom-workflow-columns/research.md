# Research: Custom Workflow Columns

All items below were resolved during planning — no
`[NEEDS CLARIFICATION]` markers remain from the spec, and no unknowns
remain in Technical Context.

## 1. Position storage: sequential integers, full resequence on change

**Decision**: `WorkflowStatus.Position` is a plain `int`, 0-based, always
dense (no gaps) within a project. Any add, reorder, or delete operation
resequences every remaining status in that project (`0..n-1`) as part of
the same transaction/`SaveChangesAsync()` call.

**Rationale**: At a maximum of 10 statuses per project (FR-004),
resequencing all of them on every change is O(10) — cheaper than
implementing and reasoning about fractional-position or gap-based
ordering schemes (e.g. `Position = 1000, 2000, 3000...` with midpoint
inserts). Constitution Principle III (Clarity Over Cleverness) explicitly
rules out this kind of unused flexibility.

**Alternatives considered**: Fractional/gap-based positions (rejected —
solves a rebalancing problem this feature's scale doesn't have); a
separate ordered-list table (rejected — `Position` on the row itself is
simpler and sufficient).

## 2. Category is a fixed 2-value enum; Name is free text

**Decision**: `Category` is a C# enum (`Open`, `Done`), stored
`HasConversion<string>()` like every other enum in this codebase (`Type`,
`Priority`, `Role`). `Name` is a plain `string` column, 2–30 chars,
uniqueness enforced by a composite `(ProjectId, Name)` index relying on
SQL Server's default case-insensitive collation — the same mechanism
already used for `User.Email` and `Project.Name`.

**Rationale**: Category is exactly the kind of small, fixed,
system-meaningful set this codebase already models as an enum (see
`WorkItemType`, `WorkItemPriority`). Name is the one genuinely free-text,
user-managed part of this entity — no enum applies.

## 3. Color palette: a fixed `ChipColor` enum, not free-form colors

**Decision**: Add a `ChipColor` enum with a small, fixed membership
(e.g. `Slate`, `Blue`, `Violet`, `Amber`, `Teal`, `Rose`, `Indigo`,
`Cyan` for the "open hues" cycle, plus `Green`, `Emerald` for the Done
family) — 10 members total, covering the max-10-columns case with no
repeats. Stored the same way as `Category`. Each maps to one
`--color-chip-{key}-bg` / `--color-chip-{key}-text` token pair in
`design-tokens.scss`, replacing today's fixed
`--color-status-{todo,inprogress,inreview,done}-*` tokens (which were
keyed by status *name*, incompatible with arbitrary per-project names).

At creation, `ProjectStatusService` assigns the next unused `ChipColor`
in the Open cycle (round-robin, skipping colors already used by that
project's other Open statuses when possible) for an Open-category status,
or the next unused Green-family color for a Done-category status —
matching the spec's "Open cycles open hues, Done uses green family."

**Rationale**: A truly free-form color picker (hex input) would let a
Manager pick a color that fails accessibility contrast or clashes with
the rest of the design system — this codebase already treats chip colors
as a closed, curated set (Feature 004's priority chips), and this feature
extends that same approach rather than introducing a new one.

**Alternatives considered**: Free-form hex color (rejected — accessibility
and design-consistency risk, no precedent in this codebase); deriving
color from `Category` alone, e.g. "all Open statuses share one color"
(rejected — spec explicitly requires each status to have its own
distinguishable color, since two Open statuses like "To Do" and "Design"
must remain visually distinct).

## 4. Migration: additive column + same-migration data backfill, one file

**Decision**: One EF Core migration, `AddPerProjectWorkflowStatuses`,
performs, in order:
1. Create the `WorkflowStatuses` table.
2. Add `WorkItems.WorkflowStatusId` as a **nullable** `int` column (so the
   table alteration itself can't fail on existing rows).
3. `migrationBuilder.Sql(...)`: for every existing `Project`, insert its
   four standard `WorkflowStatus` rows (Position 0–3, categories/colors
   per FR-005); then, for every existing `WorkItem`, set
   `WorkflowStatusId` to the matching new row (same `ProjectId`, `Name`
   corresponding to its old `Status` enum value).
4. Alter `WorkItems.WorkflowStatusId` to **NOT NULL**.
5. Drop the old `WorkItems.Status` column.

**Rationale**: This is standard EF Core practice for a "backfill then
tighten" migration and keeps the entire change — schema and data — in one
committed, named migration file per constitution Principle V
("descriptive name... `AddTaskPriorityColumn`, not `Update1`"). Using
`migrationBuilder.Sql()` for developer-authored, static backfill SQL is
not the same category of thing as the constitution's "raw SQL requires
justification" rule, which targets application-code queries built at
runtime (a risk of unparameterized user input) — migration data seeding
is neither runtime nor user-input-driven.

**Alternatives considered**: A separate one-off console/script-based data
migration outside of EF Core migrations (rejected — harder to test,
harder to guarantee runs exactly once, and breaks from this codebase's
"schema changes exclusively via EF Core code-first migrations" rule);
a dual-write period where both the old enum column and the new FK
coexist for a transition window (rejected — spec explicitly calls for a
one-way migration with no dual-write period, FR-007).

**Testing**: A dedicated migration test seeds a pre-migration-shaped
database (the old `Status` enum column, populated across all four
values) and asserts, after the migration runs, that every item's
resulting `WorkflowStatus.Name`/`Category` matches its original enum
value exactly, and that every project has exactly the standard four rows
in the standard order (FR-023, SC-003).

## 5. Default completion status: computed on demand, never stored

**Decision**: `ProjectStatusService.GetDefaultCompletionStatusId(projectId)`
returns the `Id` of the `WorkflowStatus` with `Category == Done` and the
lowest `Position` for that project — a plain query, not a stored/reassigned
flag (FR-024, resolved during spec review 2026-07-19).

**Rationale**: Removes an entire class of "reassign the flag when the
current default is deleted" bookkeeping the earlier draft's Assumptions
section flagged as unnecessary. Nothing in the codebase consumes this
value yet — it exists purely so a future feature (sprint completion,
quick-complete) has a deterministic, already-defined answer without
needing its own spec/plan discussion of "which Done status counts."

## 6. Delete-with-move atomicity: one `SaveChangesAsync()` call

**Decision**: `ProjectStatusService.DeleteAsync(projectId, statusId,
destinationStatusId?)` loads all affected `WorkItem` rows, reassigns their
`WorkflowStatusId` to the destination in memory, removes the
`WorkflowStatus` entity, and calls `SaveChangesAsync()` once.

**Rationale**: EF Core's `SaveChangesAsync()` already wraps all pending
changes in a single implicit database transaction — the same mechanism
`WorkItemService.DeleteAsync` already relies on to atomically remove a
work item and its full descendant subtree in one call (see
`WorkItemService.cs`). No explicit `BeginTransaction()`/`CommitAsync()` is
needed; if any part fails, EF Core rolls back the whole batch
automatically, satisfying FR-014's atomicity requirement with no new
mechanism.

## 7. Breaking API/DTO changes: status becomes identity-based, not name-based

**Decision**: `WorkItemRequest.Status` (string enum name) becomes
`StatusId` (nullable `int`, defaulting to the project's first Open
status when omitted, mirroring today's "defaults to ToDo when omitted"
behavior). `UpdateWorkItemStatusRequest.Status` becomes `StatusId` (int,
required). Every work-item response DTO's single `Status` (string) field
is replaced by four flattened fields: `StatusId` (int), `StatusName`
(string), `StatusCategory` (string, "Open"/"Done"), `StatusColorKey`
(string) — mirroring this codebase's existing flattening convention
(`AssigneeUserId` + `AssigneeName` alongside each other, rather than a
nested `Assignee` object).

**Rationale**: Once status names are per-project and renameable
(FR-011), a string-keyed reference silently breaks the moment a column is
renamed, and can't disambiguate two different projects' same-named
statuses. FR-018 (per-column "+ Add" pre-selects "by identity, not
name") already establishes that identity-based references are this
feature's intended direction — this decision applies that same principle
consistently to every status reference, not just the board's "+ Add."

**Migration path for this breaking change** (Principle V): `backend/` and
`frontend/` for this feature ship together in one PR/branch, the same
single-coupled-deploy model every prior TaskFlow feature already uses —
there are no external API consumers to coordinate with.

## 8. New service/controller pair, not folded into existing ones

**Decision**: `ProjectStatusService` + `ProjectStatusesController`,
routed at `/api/projects/{projectId}/statuses`.

**Rationale**: `WorkflowStatus` is its own entity with its own validation
rules (name uniqueness, category guards, atomic delete-with-move,
max-10) — mirroring this codebase's existing one-service-per-entity split
(`ProjectService` for projects, `WorkItemService` for work items). Adding
these methods to `ProjectService` would conflate "project CRUD" with "a
project's workflow-column CRUD," and adding them to `WorkItemService`
would conflate "work item CRUD" with a concern work items merely
reference, not own.

**Alternatives considered**: Nesting status management inside
`ProjectService` (rejected — bloats an existing service with an
unrelated entity's full CRUD + validation surface); a generic
"Settings"-style controller (rejected — no such concept exists elsewhere
in this codebase, would be premature abstraction for a single settings
type).

## 9. Frontend: status becomes a project-scoped list, not a fixed union

**Decision**: The `WorkItemStatus` string-literal union (`'ToDo' |
'InProgress' | 'InReview' | 'Done'`) is removed. A new `ProjectStatus`
interface (`{ id, name, category, colorKey, position, itemCount? }`)
replaces it, fetched via a new `getStatuses(projectId)` call (backed by
`GET /api/projects/{projectId}/statuses`). Each `WorkItem`/
`WorkItemBoardCard` carries flattened `statusId`/`statusName`/
`statusCategory`/`statusColorKey` fields mirroring the backend DTO shape
1:1 — no client-side transformation into a nested object.

`StatusChipComponent`'s `status: WorkItemStatus` input is replaced by two
inputs, `name: string` and `colorKey: string` — its internal color-class
switch becomes an exhaustive switch over the fixed, small `ChipColor` set
(still a compile-time-checked exhaustive switch, just keyed on color
rather than on status name, since status *names* are no longer a closed
set the compiler can reason about).

**Rationale**: The whole point of this feature is that the status set is
no longer closed/system-wide, so a TypeScript literal union (which only
works for closed sets) can no longer model it. Colors remain a closed,
small set (`ChipColor`, research.md #3), so the chip component's
exhaustiveness guarantee is preserved — just re-keyed to the thing that's
actually still fixed.

## 10. Workflow management screen: new route, reuses existing CDK dependency

**Decision**: New route `/projects/:id/workflow` → new
`WorkflowComponent`, linked from project-detail's header alongside the
existing Manager/Admin-gated "Edit project"/"Delete project" links
(reusing the already-present `canManageProject()` check). Column
reordering reuses `@angular/cdk/drag-drop` (already a dependency since
Feature 005's card dragging) — a second, independent consumer of the same
package, not a new one.

**Rationale**: Matches the existing pattern for gating management-only
UI (project-detail already conditionally renders Edit/Delete links via
`canManageProject()`); avoids a second drag-and-drop library for what is
functionally the same "reorder a short list" interaction Angular CDK
already provides.
