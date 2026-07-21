# Research: Sprints & Backlog

Each item: Decision, Rationale, Alternatives considered. Grounded in the
actual current implementation (`backend/TaskFlow.Api/`, `frontend/src/app/`)
as of Feature 007, not general best practice.

## 1. Sprint gets its own service/controller, not folded into WorkItemService

**Decision**: A new `SprintService` + `SprintsController`
(`api/projects/{projectId}/sprints`), mirroring `ProjectStatusService`/
`ProjectStatusesController` (Feature 006) — not folded into `WorkItemService`
the way Feature 007 folded `Label` in.

**Rationale**: `Label` had no independent lifecycle (no rename/delete
endpoint, no state machine), so a dedicated service would have been
unjustified ceremony. `Sprint` is the opposite: it has its own CRUD, its own
uniqueness rule (name per project), and a 3-state lifecycle (Planned → Active
→ Completed) with real transition rules — exactly the shape `WorkflowStatus`
already has, which is why that one already got its own service/controller
pair. Same judgment call, same precedent, applied consistently (constitution
Principle III).

**Alternatives considered**: Folding sprint CRUD into `WorkItemService`
(rejected — would mix two independent lifecycles, WorkItem's and Sprint's,
into one file, the opposite of what Feature 006 already established).

## 2. Sprint ordering: query-time `OrderBy(StartDate)`, no stored `Position`

**Decision**: Sprints are always returned soonest-start-first via
`OrderBy(s => s.StartDate)` at query time. No `Position` column, no reorder
endpoint.

**Rationale**: `WorkflowStatus.Position` exists because column order is an
arbitrary, manually-curated sequence with no other natural ordering. A
sprint's display order is never arbitrary — the spec defines it as "soonest
start date first," a value the row already carries. Adding a `Position`
column and a reorder endpoint would duplicate information already present in
`StartDate` for no behavior the spec asks for (constitution Principle III —
no speculative flexibility).

**Alternatives considered**: A `Position` column mirroring `WorkflowStatus`
(rejected — solves a problem this feature doesn't have; two sprints could
even share a start date and the tie-break — insertion order via `Id` — needs
no extra column either).

## 3. Days-remaining/overdue: a pure frontend function, not a server field

**Decision**: `SprintDto` carries raw `StartDate`/`EndDate`/`Status` only. A
new pure function, `sprintDaysRemaining(endDate, status)` (sibling to the
existing `frontend/src/app/projects/board/overdue.ts`), computes either
`{ overdue: true }` or `{ daysRemaining: N }` on the client, using the same
date-only-string-comparison technique `isOverdue()` already uses (never
running the date through `new Date().getters()`, which shifts the calendar
day for users behind UTC).

**Rationale**: This is the exact same problem `isOverdue()` already solved
for `DueDate` one feature ago, with the same correctness pitfall (UTC/local
shift) and the same fix. Recomputing it as a raw server field would duplicate
that logic in two languages and two places; a pure client function is also
what the spec's own non-functional requirement asks to be test-first
("days-remaining/overdue calculation ... is test-first"), matching
`overdue.spec.ts`'s existing precedent exactly.

**Alternatives considered**: Computing `daysRemaining`/`isOverdue` server-side
per request (rejected — `WorkItemBoardCardDto` already deliberately omits a
server-computed overdue flag for the identical reason; introducing one here
for sprints alone would be an inconsistent split of the same responsibility).

## 4. Item↔sprint assignment: a full-edit field plus a field-scoped PATCH

**Decision**: `WorkItemRequest` gains an optional `SprintId` (used by
Create/Update, replace-the-resource semantics — same convention as every
other optional field on that DTO, per its own file comment). Separately, a
new field-scoped endpoint, `PATCH api/work-items/{id}/sprint`, mirrors the
existing `PATCH api/work-items/{id}/status` (`UpdateStatusAsync`) exactly —
same `EnsureCanEdit` check, same "only this one field, never anything else"
shape.

**Rationale**: `UpdateStatusAsync`'s own comment already explains why the
Board's drag needs a field-scoped endpoint instead of the full `PUT`: a drag
interaction must never risk clobbering fields (Description,
`ParentWorkItemId`, now also `Labels`/`StartDate`) it doesn't carry. The
Backlog view's drag is the same interaction pattern one field over
(`SprintId` instead of `StatusId`), so it gets the same treatment. The
`WorkItemRequest.SprintId` field is separate and serves a different caller —
the Backlog's per-section "+ Create" (FR-024) and, incidentally, the general
edit modal, which should be able to move an item to/from a sprint like any
other field it owns.

**Alternatives considered**: Only the field-scoped PATCH, no `WorkItemRequest`
field (rejected — FR-024's "create an item pre-assigned to this section"
needs sprint assignment available at creation time, which only the
create/update path can carry). Only the `WorkItemRequest` field, no PATCH
(rejected — would force the Backlog's drag to resubmit the entire resource,
the exact risk `UpdateStatusAsync` was already introduced to avoid one
feature ago).

## 5. Backlog endpoint: one query + in-memory grouping, in `WorkItemService`

**Decision**: `WorkItemService.GetBacklogAsync(projectId, statusId?, type?,
priority?, assigneeUserId?, search?, label?)` — one query for the project's
work items (same five-filter predicate as `GetWorkItemsAsync`, extracted into
a shared private `BuildFilteredQuery` helper so both methods build identical
`WHERE` logic from one place), grouped in memory by `SprintId` afterward.
Sprint section metadata (name/dates/status) comes from a second, small query
against `Sprints`.

**Rationale**: This is the same shape `GetBoardAsync` and `GetTreeAsync`
already use — one flat query, then a `Dictionary`/`GroupBy` pass in memory —
justified there by the same reasoning that applies here: no recursive/dynamic
SQL grouping needed at this feature's scale, and the in-memory pass is
already an established, reviewed pattern rather than a new one. Extracting
the five-filter predicate avoids maintaining two near-identical
`.Where()`-chains that would silently drift apart the next time a filter is
added.

**Alternatives considered**: A raw grouped SQL query (`GROUP BY SprintId`)
(rejected — the endpoint needs full item rows per section, not aggregates, so
SQL-side grouping buys nothing `OrderBy`/`GroupBy` in memory doesn't already
give at this scale, and raw SQL needs constitution-level justification this
feature doesn't have). Duplicating the filter `.Where()` chain instead of
extracting it (rejected — needless drift risk for a five-line extraction).

## 6. Sprint-scoped Board: one optional query parameter, not a second endpoint

**Decision**: `GET api/projects/{projectId}/work-items/board` gains one
optional query parameter, `sprintId`. `WorkItemService.GetBoardAsync` filters
rows to that sprint when present; the column list (`WorkflowStatuses`) is
unaffected either way. `BoardComponent` becomes self-contained for the
toggle: it fetches the project's sprints itself, derives the Active one, and
calls `getBoard(projectId, activeSprintId)` when "Active sprint" mode is
selected — the same encapsulation it already has for its own `board` signal
and its own `refresh()` method.

**Rationale**: The spec requires "same columns/drag/permissions as today" for
sprint-scoped mode — an optional filter on the existing endpoint guarantees
that by construction (it's the same code path, one extra `.Where()`),
whereas a second endpoint would risk the two drifting apart over time. Board
already owns its own data-fetching independent of `ProjectDetailComponent`
(research.md precedent from Feature 005/006), so the toggle's state belongs
there too, not threaded through the parent.

**Alternatives considered**: A dedicated `GET .../work-items/board/active`
endpoint (rejected — duplicates `GetBoardAsync`'s column-fetch and row-shape
logic for no behavior difference). Having `ProjectDetailComponent` own the
active-sprint lookup and pass it down as an `@Input` (rejected — breaks
`BoardComponent`'s existing self-contained-data-fetching pattern for no
reason; every other project-scoped list it needs, it already fetches itself).

## 7. `WorkItem.SprintId` is `Restrict`, not `Cascade`

**Decision**: `Sprint → WorkItem` (`WorkItem.SprintId`) is
`DeleteBehavior.Restrict`, same as `WorkflowStatus → WorkItem`
(`WorkItem.WorkflowStatusId`) and for the identical reason.

**Rationale**: `Project → WorkItem` is already `Cascade`. If `Sprint →
WorkItem` were also `Cascade`, `WorkItem` would be reachable from `Project`
two ways (directly, and via `Project → Sprint → WorkItem`) — SQL Server's
"multiple cascade paths" error (1785), the same conflict already documented
for `WorkflowStatus`/`CreatedBy`/`Assignee` in `AppDbContext`. It's harmless
in practice: deleting a `Project` cascades to both its `Sprint` and `WorkItem`
rows together in the same operation, so no `WorkItem` row ever survives
pointing at a deleted `Sprint`; and deleting a single `Sprint` directly is
only ever allowed when it has zero items (FR-010), so the `Restrict` never
actually fires there either.

**Alternatives considered**: `SetNull` (rejected — `Sprint → WorkItem`
`SetNull` is unnecessary once single-sprint deletion is already guaranteed
empty by the business rule, and `SetNull` here would still need to coexist
correctly with the `Project` cascade above, which `Restrict` already does via
the exact precedent this codebase established for `WorkflowStatus`).

## 8. "How many items are moving" comes from data the client already has

**Decision**: No dedicated "preview" endpoint for sprint completion. The
Backlog view already loads each sprint's full item list (to render its
section); the not-Done count the completion confirmation needs (FR-007
acceptance scenario 6) is a client-side filter (`statusCategory !== 'Done'`)
over data already in memory.

**Rationale**: The equivalent "Move N items..." confirmation on
`WorkflowStatus` deletion (Feature 006) needed a dedicated round-trip only
because that screen doesn't otherwise load every affected item — the
Workflow management screen shows counts, not item lists. The Backlog view is
different by construction: it already renders every item in every sprint
section, so the count is already on the client, and a second network call
would just re-fetch what's already rendered.

**Alternatives considered**: A `GET .../sprints/{id}/completion-preview`
endpoint mirroring the status-delete pattern (rejected — solves a problem
the Backlog view's own data-loading already avoids).

## 9. "Never started" delete-eligibility needs no extra flag

**Decision**: `SprintService.DeleteAsync` allows deletion exactly when
`Status == Planned` (no items check is even reachable otherwise — see below).

**Rationale**: The only transition into `Active` is `Start`, and there is no
transition back from `Active` or `Completed` to `Planned` anywhere in this
feature (spec: Out of scope — "Editing a sprint... after creation"; no
"reopen" story exists). So `Status == Planned` is already exactly the set of
sprints that have never been started — no separate `HasEverStarted` column or
audit flag is needed to answer that question.

**Alternatives considered**: A dedicated boolean flag set once on first
`Start` (rejected — `Status` alone already encodes the same fact given the
feature's fixed one-way transition graph; a second column tracking the same
information would be redundant state that could theoretically drift from
`Status`).

## 10. Sprint create/complete UI: `MatDialog`, following Feature 007's precedent

**Decision**: "Create sprint" and "Complete sprint" (destination picker) are
small `MatDialog`-based components (`SprintFormComponent`,
`CompleteSprintDialogComponent`), the same mechanism
`WorkItemModalComponent` introduced to this codebase in Feature 007.

**Rationale**: `MatDialog` is now an established pattern with one real
precedent to follow (focus trapping, Escape-to-close, backdrop handling
already solved there) — introducing a second, different modal mechanism for
this feature's two small forms would be unjustified inconsistency.

**Alternatives considered**: Inline expand/collapse forms directly in the
Backlog view (rejected — "Complete sprint" in particular needs to block on a
required choice before proceeding, which a modal expresses more directly
than an inline form competing for space in an already-dense section header).
