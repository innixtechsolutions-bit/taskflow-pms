# Research: Project Summary Dashboard & Activity Log

## 1. `ActivityLogEntry.WorkItemId` is a plain `int`, not a real foreign key

**Decision**: `WorkItemId` is stored as an unconstrained `int` column with no
`HasOne`/navigation relationship configured in `AppDbContext`. `WorkItemTitle`
and `WorkItemType` are captured as their own snapshot string columns on the
entry itself, populated at write time.

**Rationale**: The spec requires an entry to remain fully readable ("using
the captured title snapshot") after its work item is deleted (FR-018,
Acceptance Scenario 8). Every standard EF Core `OnDelete` behavior fails this
requirement in a different way: `Restrict` would block the work item deletion
entirely (breaking the existing, already-shipped delete feature the moment
any activity entry exists — which, since creation itself always logs one, is
virtually every work item); `Cascade` would delete the very entries the
history is supposed to preserve; `SetNull` would erase which item the entry
was ever about, taking the id with it. None of the three delete behaviors
this codebase already uses elsewhere (see `WorkItem.ParentWorkItemId`,
`WorkItem.WorkflowStatusId`) fit a relationship that must specifically
*outlive* its target. The simplest correct answer is to not model it as a
database relationship at all — the same "enforced in the service, not the
database" philosophy already used throughout this codebase for constraints a
column can't express (e.g. `WorkItem.ParentWorkItemId`'s type-dependent
required-ness), just applied here to sidestep a delete-behavior problem
instead of a validation one.

**Alternatives considered**: A nullable FK with `SetNull` was the closest
real alternative, but it would silently lose the numeric id the moment a work
item is deleted, which is strictly worse than keeping a now-dangling id next
to a snapshot title — and gains nothing, since the snapshot columns are
required either way to survive deletion.

## 2. `ActivityLogEntry.ProjectId` is a real FK, `Cascade`

**Decision**: `ProjectId` has a normal `HasOne(...).WithMany()...
.OnDelete(DeleteBehavior.Cascade)` relationship to `Project`, the same as
`WorkflowStatus`/`Label`/`Sprint`'s existing Project-cascade configuration.

**Rationale**: Because decision #1 above means `WorkItemId` carries no
database relationship at all, there is only one path from `Project` to
`ActivityLogEntry` (the direct `ProjectId` FK) — so, unlike `WorkItem.Sprint`
(which had to be `Restrict` specifically to avoid a second cascade path
through `Project → WorkItem → Sprint`), there is no "multiple cascade paths"
conflict (SQL Server error 1785) to avoid here. Deleting a project cleanly
deletes its activity log too — the spec never asks entries to survive their
own project's deletion, only their work item's.

## 3. `ActivityLogEntry.ActorUserId` is a real FK, `Restrict` — no name snapshot

**Decision**: `ActorUserId` is a normal FK to `User` with `Restrict`, the same
as `WorkItem.CreatedByUserId`/`AssigneeUserId`. The actor's display name is
resolved via a live join at read time (`entry.Actor!.FullName`), not stored as
a snapshot column.

**Rationale**: Matches the existing, already-accepted convention: this
codebase never snapshots a user's name anywhere (`WorkItemDto.CreatedByName`
is always a live join) because no user-deletion feature exists yet, so a live
join can never actually go stale. The spec's edge case ("actor later
deactivated or deleted... name remains readable") describes a capability this
system doesn't have and isn't in scope to add — the same reasoning already
applied elsewhere (e.g. `WorkItem.CreatedByUserId`'s FK comment: "no
user-deletion feature yet"). If user deletion/deactivation is ever added
later, it will need to solve this for `CreatedByName`/`AssigneeName` too, at
which point the same fix (whatever it is) applies uniformly rather than this
feature inventing a one-off snapshot mechanism nothing else uses.

## 4. `ActivityEventType`/`ActivityField` are real enums, string-converted

**Decision**: Two new enums — `ActivityEventType { Created, FieldChanged }`
and `ActivityField { Status, Priority, Assignee, Sprint }` — both stored via
`.HasConversion<string>()`, the same convention already used for `User.Role`,
`WorkItem.Type`/`Priority`, `WorkflowStatus.Category`/`ColorKey`, and
`Sprint.Status`.

**Rationale**: Both are small, genuinely closed sets (the spec explicitly
scopes tracked fields to exactly these four, "in v1") — a lookup table would
be speculative generality with no current requirement driving it (constitution
Principle III).

## 5. One `ActivityLogEntry` row per changed tracked field, not one combined row

**Decision**: If a single save changes two tracked fields at once (e.g.
`UpdateAsync` changes both `Status` and `AssigneeUserId` in one PUT), two
separate `ActivityLogEntry` rows are added — not one row with two diffs.

**Rationale**: The entity shape itself (`Field`/`OldValue`/`NewValue` are
singular per row) mirrors the spec's own sentence format, which is always
about exactly one field ("Jane changed Task 'Fix login' status from To Do to
In Progress"). A combined-diff row would need a different shape (a list of
field/old/new triples) for a case the spec never asks to render specially —
two entries, both written in the same `SaveChangesAsync()` call, satisfy
FR-016's "same transaction" requirement identically to one entry, so there is
no correctness reason to combine them.

## 6. Write path and transaction strategy

**Decision**: `ActivityLogService`'s `RecordCreated`/`RecordFieldChange`
methods only call `dbContext.ActivityLogEntries.Add(...)` — they never call
`SaveChangesAsync()` themselves. `WorkItemService`'s existing single
`SaveChangesAsync()` call (already present at the end of
`UpdateAsync`/`UpdateStatusAsync`/`UpdateSprintAsync`) persists both the field
mutation and the new log entry together, exactly the same "one
`SaveChangesAsync()` = one transaction" pattern this codebase already relies
on for `Label` attachment in `CreateAsync`/`UpdateAsync` (new `Label`/
`WorkItemLabel` rows are `Add()`-ed without their own `SaveChanges` call).

`CreateAsync` needs one adjustment: the "item created" entry needs the new
`WorkItem`'s real, database-generated `Id`, but (per decision #1)
`ActivityLogEntry.WorkItemId` is a plain `int`, not a navigation property EF
Core can fix up automatically the way it already does for `WorkItemLabels` in
this same method. So `CreateAsync` is wrapped in one explicit
`IDbContextTransaction` (`await using var transaction = await
dbContext.Database.BeginTransactionAsync()`): the work item is saved first
(assigning its real `Id`), then the creation entry is added and saved in a
second `SaveChangesAsync()` call, then the transaction commits. This is the
one place in the feature that needs an explicit transaction; every other
write path (existing item, `Id` already known) needs only the implicit,
single-`SaveChangesAsync()` transaction already in place today.

**Alternatives considered**: Giving `ActivityLogEntry` a real `WorkItem?`
navigation property (so EF Core's relationship fixup could resolve the id
automatically, the same trick `WorkItemLabels` already uses) was rejected —
it would reintroduce exactly the FK-relationship problem decision #1 avoids,
just deferred to the fixup step. Simpler to accept one extra explicit
transaction in the one place (`CreateAsync`) that genuinely needs it.

## 7. Old-value display names are looked up before mutation, not derived after

**Decision**: `UpdateAsync`/`UpdateStatusAsync`/`UpdateSprintAsync` each fetch
the *old* display name (status name, assignee name, sprint name) via a small
lookup query against the item's current, pre-mutation id — before overwriting
the field — then compare it against the newly resolved display name,
emitting an entry only when the two differ. Assignee display uses
`"Unassigned"` when either side is null; Sprint display uses `"Backlog"` when
either side is null (spec's Sprint-removal edge case) — Status is always
non-null (every work item always has a status).

**Rationale**: `dbContext.WorkItems.FindAsync(id)` (used by all three
methods today) loads only the entity itself, not its `WorkflowStatus`/
`Assignee`/`Sprint` navigation properties — so the old display name isn't
already in memory and must be fetched explicitly. This mirrors the existing
small-lookup-query style `ResolveStatusIdAsync`/`ResolveSprintIdAsync` already
use in the same class, rather than introducing a new querying pattern.
Priority needs no lookup at all — it's a plain enum field, not a foreign key,
so `oldPriority.ToString()`/`newPriority.ToString()` is the whole "lookup."

## 8. `GetSummaryAsync` lives on `WorkItemService`, not a new service

**Decision**: Stat cards, status breakdown, priority breakdown, and team
workload are all computed by one new `WorkItemService.GetSummaryAsync(int
projectId)` method, returning a single `ProjectSummaryDto`.

**Rationale**: This is exactly the shape `GetBoardAsync`/`GetTreeAsync`/
`GetBacklogAsync` already are — one query over the project's `WorkItems` plus
an in-memory aggregation pass — and Summary has no independent lifecycle or
CRUD of its own to justify a dedicated service, the same judgment call
already applied to why `Label` didn't get its own service/controller pair in
Feature 007 (unlike `Sprint`/`WorkflowStatus`, which do have one because they
have a real lifecycle). The activity *feed* itself is a separate concern with
its own service (`ActivityLogService`, research.md #6) since it has genuinely
distinct read shapes (paginated project feed, unpaginated item history) that
don't fit `WorkItemService`'s existing per-work-item-list methods.

## 9. "In Progress" means "every Open-category item" (spec correction)

**Decision**: The stat card's In Progress count is every work item in an
Open-category status — equivalently, `Total − Completed`. `spec.md`'s
Acceptance Scenario 4 (User Story 1) was corrected during planning to state
this explicitly, replacing an earlier draft that assumed a separate "initial/
backlog" status category.

**Rationale**: Feature 006's `WorkflowStatusCategory` enum has exactly two
members, `Open` and `Done` — there is no third category distinguishing "not
yet started" from "actively being worked," and adding one is out of scope for
this feature (it would be a workflow-model change, not a Summary-dashboard
change). Given the actual data model, "in progress" can only mean "not yet
Done," so that's what's implemented and what the corrected spec now states.

## 10. Team workload: two queries merged in memory, no project-membership model

**Decision**: `GetSummaryAsync`'s workload rows come from (a) the project's
open (`Category == Open`) work items grouped by `AssigneeUserId`, and (b)
every `User` in the whole system with `Role` `Manager` or `Admin` — not
scoped to "this project's managers" in any way, because no such membership
concept exists anywhere in this codebase (there is no `ProjectMember` entity;
every project is visible to every authenticated user today, per FR-022 and
every prior feature's own behavior). The two sets are merged by user id in
memory; a null-`AssigneeUserId` bucket with count > 0 becomes a synthetic
`"Unassigned"` row. Rows sort by count descending.

**Rationale**: This matches spec FR-009's literal wording — "a Manager/Admin
**of the system**" — and there is no narrower concept available to scope it
to. Introducing a project-membership model to make this narrower would be
speculative scope creep well beyond this feature's stated boundaries.

## 11. Status/priority breakdown query shapes

**Decision**: Status breakdown reuses `GetBoardAsync`'s existing "project's
own `WorkflowStatuses` ordered by `Position`" query, joined with a grouped
count of work items per status. Priority breakdown iterates the fixed
4-member `WorkItemPriority` enum directly (not a `GroupBy` over existing
data) so all four levels always appear, including at zero (FR-008) — a
`GroupBy` alone would simply omit any level with no matching items.

## 12. Due-soon window: UTC, date-only, inclusive of the 7th day

**Decision**: An item is "due soon" when it has a non-null `DueDate`, its
category is not `Done`, and `DueDate.Value.Date` falls between
`DateTime.UtcNow.Date` and `DateTime.UtcNow.Date.AddDays(7)` inclusive.

**Rationale**: Matches the same UTC/date-only convention the existing
frontend `isOverdue()` already established (`WorkItem.DueDate` is
"date-only by convention" per its own entity comment) — due-soon and overdue
are two windows over the same underlying rule (`overdue`: due strictly before
today; `due soon`: due from today through today+7 inclusive), just evaluated
here on the backend since `GetSummaryAsync`'s stat cards, like every other
Board/Backlog/Tree aggregation, are computed server-side, not client-side.

## 13. "Pure logic is test-first" — what's actually pure in this feature

**Decision**: Backend-side calculations (percent, due-soon windowing,
breakdown counts) are covered by ordinary `WorkItemServiceTests`-style xUnit
tests against a real SQL Server test database, asserting on
`GetSummaryAsync`'s output for seeded data — the same indirect testing
approach this codebase already uses for every other computed aggregate
(`GetBoardAsync`'s/`GetTreeAsync`'s `doneCount`, for instance, is never unit
tested as an isolated private function). Genuinely new, isolable pure
*frontend* logic — `relativeTime()`, `buildActivitySentence()`,
`donutSegments()` — gets its own small pure function plus a dedicated Vitest
spec file, mirroring the existing `overdue.ts`/`sprint-days-remaining.ts`
pattern exactly.

**Rationale**: This reconciles the spec's own non-functional requirement
("Pure logic ... is test-first") with how "pure logic" has concretely been
interpreted in every prior feature in this codebase — as standalone,
extractable frontend functions, not backend private calculation helpers
(which are always exercised indirectly through the owning public service
method's own DB-backed tests).

## 14. Charts: no new npm dependency; colors reuse existing chip tokens

**Decision**: The status breakdown renders as a dependency-free SVG donut
(stacked `<circle>` elements per status, positioned via
`stroke-dasharray`/`stroke-dashoffset`); the priority breakdown renders as
plain flexbox bars with inline `width: {percent}%`. Both use each
status's/priority's *existing* `--color-chip-{key}-text` /
`--color-priority-{level}-text` custom property (defined in
`design-tokens.scss`, already the more saturated half of each existing
pastel background/text pair) as the segment/bar's solid fill color — no new
design tokens are introduced.

**Rationale**: The maintainer's own instruction was "using our own tokens and
chip colors, not Jira's exact palette" — reusing the already-existing
`-text` values (rather than the pastel `-bg` values, which read as too washed
out for a solid chart fill) satisfies that directly with zero new tokens,
and guarantees the donut/bar colors visually match the same status/priority
chips already shown elsewhere on the same page (Board cards, the status
filter, etc.) — one palette, one source of truth. No charting library
(ngx-charts, Chart.js, D3) is added — two simple, static, small-data-set
shapes don't justify one (constitution Principle III), and none is currently
a dependency of this project.

## 15. `ActivityFeedComponent` is one shared component, not two

**Decision**: A single `projects/activity-feed/activity-feed.component.ts`
renders a list of `ActivityEntry` values via `buildActivitySentence()` +
`RelativeTimePipe`. The Summary tab uses it with pagination ("load more"
appends another page); `WorkItemDetailComponent`'s activity section uses it
with a single, unpaginated fetch. Both pass it the same `entries: input<...>`
shape.

**Rationale**: FR-019 and FR-021 both require "the same rendering" between
the project feed and an item's own history. One shared component makes that
true by construction — there is no second template to drift out of sync with
the first, unlike duplicating the sentence-building markup in two places and
relying on developer discipline to keep them matching.

## 16. Endpoint placement: existing `WorkItemsController`, no new controller

**Decision**: The three new routes — `GET
api/projects/{projectId}/summary`, `GET api/projects/{projectId}/activity`,
`GET api/work-items/{id}/activity` — are added to the existing
`WorkItemsController`, which gains a second constructor dependency
(`ActivityLogService`) alongside its existing `WorkItemService`.

**Rationale**: `WorkItemsController` already mixes several route groups
(work-item CRUD, `backlog`, `tree`, `board`, `labels`,
`parent-candidates`) under one controller — these three fit the same
established pattern rather than justifying an entirely new
`ActivityController` for what is, from the API surface's perspective, still
"read views over this project's work items."
