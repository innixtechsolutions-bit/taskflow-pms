# Implementation Plan: Project Summary Dashboard & Activity Log

**Branch**: `009-summary-dashboard` | **Date**: 2026-07-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/009-summary-dashboard/spec.md`

## Summary

Add a new, append-only `ActivityLogEntry` table recording work item creation
and Status/Priority/Assignee/Sprint changes, written by a new
`ActivityLogService` hooked into `WorkItemService`'s existing
`CreateAsync`/`UpdateAsync`/`UpdateStatusAsync`/`UpdateSprintAsync` methods (no
new UI to create entries — they are a side effect of flows that already
exist). A new `GetSummaryAsync` method on `WorkItemService` computes stat
cards, status breakdown, priority breakdown, and team workload with the same
"one query + in-memory aggregation" shape `GetBoardAsync`/`GetTreeAsync`/
`GetBacklogAsync` already use. A new Summary tab becomes the first, default
tab in `ProjectDetailComponent` (alongside the existing Board/Backlog/List/
Tree), self-contained the same way Board/Backlog already own their own data
fetching, rendering stat cards, a dependency-free SVG donut (status), CSS bar
chart (priority), a team-workload list, and a shared `ActivityFeedComponent`
reused verbatim on `WorkItemDetailComponent` for the per-item activity
history (FR-019/FR-021's "consistent rendering" requirement, satisfied by
sharing one component rather than duplicating markup). No new npm
dependency — charts are plain SVG/CSS using colors already defined for
`StatusChipComponent`/`PriorityChipComponent`. All changes to existing
endpoints/DTOs are additive; all prior Board/Backlog/List/Tree behavior is
unchanged (spec's Non-functional / SC-009).

## Technical Context

**Language/Version**: C# 13 / .NET 10 (backend); TypeScript ~6.0 / Angular 22
(frontend), strict mode — unchanged from prior features.

**Primary Dependencies**: ASP.NET Core Web API, EF Core 10 (SQL Server
provider) — existing, no new backend package. No new frontend package either
— the status donut and priority bar chart are hand-rolled SVG/CSS (stacked
`<circle>` arcs via `stroke-dasharray`/`stroke-dashoffset` for the donut,
plain flexbox width-percentage bars for priority), a well-known
dependency-free technique, consistent with constitution Principle III (no
speculative complexity — a charting library would be unjustified for two
simple, static shapes at this feature's data scale).

**Storage**: SQL Server. One new table, `ActivityLogEntries` (`Id`,
`ProjectId` FK→Projects Cascade, `WorkItemId` — plain `int`, **no** FK/relationship
declared — see research.md #1, `WorkItemTitle`/`WorkItemType` snapshot
strings, `ActorUserId` FK→Users Restrict, `EventType` enum-as-string,
`Field` nullable enum-as-string, `OldValue`/`NewValue` nullable strings,
`CreatedAt`). One EF Core code-first migration (`AddActivityLog`), purely
additive — no backfill needed (new table only, no existing column touched).

**Testing**: xUnit + real SQL Server test databases (backend, existing
pattern); Vitest (frontend, existing pattern). New backend coverage (written
before implementation, Red-Green-Refactor): `ActivityLogServiceTests`
(record-created, record-field-change, project-scoping, entries persist with
their snapshot after the referenced work item is deleted — no such method as
"update" or "delete" exists on the service, so immutability is structural,
not a guarded check); `WorkItemServiceTests` extended for
`CreateAsync`/`UpdateAsync`/`UpdateStatusAsync`/`UpdateSprintAsync` each
writing the correct entry (or none, for untracked-field-only changes) and for
`GetSummaryAsync` (stat card totals/percent, due-soon boundary at exactly 7
days, status/priority breakdown shape including zero-count priority levels,
workload sorting/zero-load Manager inclusion/Unassigned row). Integration
tests: `WorkItemsEndpointsTests` extended with the three new GET routes
(allowed path — any authenticated user, matching FR-022's "no new permission
model"). New frontend coverage (pure logic, test-first per constitution
Principle I and the spec's own non-functional requirement): `relativeTime()`
(mirroring `overdue.spec.ts`'s structure — a small, bucketed pure function:
just now / N minutes / N hours / N days / falls back to `friendlyDate`),
`buildActivitySentence()` (constructs the "Actor changed Type 'Title' field
from X to Y" / "Actor created Type 'Title'" strings from a structured entry),
and `donutSegments()` (pure arc-math: turns `{ count, colorVar }[]` into
`{ colorVar, dashArray, dashOffset }[]`, mirroring `sprintDaysRemaining()`'s
"pure calculation extracted so the component template stays declarative"
pattern).

**Target Platform**: Browser (Angular SPA) + ASP.NET Core Web API —
unchanged.

**Project Type**: Web application — touches both `backend/` and `frontend/`.

**Performance Goals**: No new performance goal. `GetSummaryAsync` is one
query over the project's work items plus a handful of small in-memory
grouping passes (status/priority/assignee), the same shape already accepted
for `GetBoardAsync`/`GetTreeAsync`/`GetBacklogAsync` at this feature's scale
(tens to low hundreds of items per project). The project activity feed is a
single indexed, paginated query (`ProjectId` + `CreatedAt DESC`), the same
shape as `GetWorkItemsAsync`'s existing pagination.

**Constraints**: All API contract changes are additive — three entirely new
routes (`GET .../summary`, `GET .../activity`, `GET
api/work-items/{id}/activity`) and no changes to any existing request/response
shape — so, like Features 007/008, no breaking-change migration path is
needed. Activity-entry writes must occur in the same transaction as the
change they record (spec's non-functional requirement, FR-016) — see
research.md #6 for how `CreateAsync` (new `WorkItem.Id` not yet known) and
`UpdateAsync`/`UpdateStatusAsync`/`UpdateSprintAsync` (id already known)
satisfy this differently.

**Scale/Scope**: 1 new entity (`ActivityLogEntry`), 2 new enums
(`ActivityEventType`, `ActivityField`), 1 migration, 1 new service
(`ActivityLogService`), `WorkItemService` extended (new constructor
dependency on `ActivityLogService`; `CreateAsync` wrapped in an explicit
transaction plus a creation-entry write; `UpdateAsync`/`UpdateStatusAsync`/
`UpdateSprintAsync` each capture old display values and write field-change
entries; new `GetSummaryAsync` method), `WorkItemsController` extended (3 new
GET routes, new constructor dependency), 3 new backend DTOs
(`ActivityEntryDto`, `ProjectSummaryDto` + its 4 nested record types), every
`new WorkItemService(Db)` test call site updated to pass the new
`ActivityLogService` dependency. Frontend: `work-items.service.ts` extended
(3 new methods + 6 new interfaces), 6 new components/pure-fn pairs
(`summary/summary.component`, `summary/status-donut-chart.component` +
`donut-segments.ts`, `summary/priority-bar-chart.component`,
`summary/team-workload.component`, `activity-feed/activity-feed.component` +
`build-activity-sentence.ts`, `shared/relative-time.pipe.ts`),
`project-detail.component` extended (5th, default view-mode tab),
`work-item-detail.component` extended (activity section, reusing
`ActivityFeedComponent`). Comparable in shape to Feature 008 (a new
project-scoped concept with its own service, plus a new tab wired into the
existing `ProjectDetailComponent` tab mechanism) with the addition of two
small, genuinely new frontend chart primitives.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Test-First Development**: PASS (with plan). Every new business rule
  (`ActivityLogService`'s record methods, `WorkItemService`'s per-field
  change-detection, `GetSummaryAsync`'s stat/breakdown/workload/due-soon
  calculations) gets xUnit tests against a real SQL Server test database
  before the corresponding implementation, per the existing
  `WorkItemServiceTests`/`SprintServiceTests` pattern. The three new GET
  routes get integration test coverage for the allowed path (spec has no new
  denied path — FR-022 explicitly introduces no new permission model).
  Frontend: `relativeTime()`, `buildActivitySentence()`, and
  `donutSegments()` are pure functions given Vitest specs first, matching the
  spec's own explicit non-functional requirement ("Pure logic ... is
  test-first").
- **II. Secure by Default**: PASS. The three new endpoints reuse the
  controller's existing class-level `[Authorize]` with no additional role
  restriction — spec FR-022 is explicit that Summary/activity data has "no
  new permission model," matching every other read endpoint's current
  behavior (any authenticated user may view any project). No new mutation
  endpoint is introduced — activity entries are written only as a side
  effect of the existing, already-authorized `Create`/`Update`/`UpdateStatus`/
  `UpdateSprint` actions, so no new authorization surface is created at all.
- **III. Clarity Over Cleverness**: PASS. `ActivityLogEntry.WorkItemId` is
  deliberately a plain, unconstrained `int` rather than a real foreign key
  (research.md #1) — the one place in this feature where *not* modeling a
  relationship is the simpler, more correct choice, because the desired
  behavior (survive the referenced row's deletion, keeping the id and a
  separately captured title) isn't expressible by any FK delete behavior.
  `GetSummaryAsync` lives on `WorkItemService` rather than a new dedicated
  service — Summary has no independent lifecycle/CRUD of its own, the same
  reasoning already applied to why `Label` didn't get its own service in
  Feature 007 (research.md #9). No new npm charting dependency — two simple,
  static shapes (a donut, a bar chart) don't justify one.
- **IV. Consistent Code Quality & Review Gates**: PASS. New DTOs follow the
  existing flattened, record-based, manually-mapped convention. New routes
  are RESTful, nested under the existing `api/projects/{projectId}/...` and
  `api/work-items/{id}/...` groups `WorkItemsController` already hosts. Async
  I/O throughout; Angular strict mode, signals, no `any`. Every new exception
  path (none expected — these are read-only GETs with no new failure modes
  beyond the existing `ProjectNotFoundException`/`WorkItemNotFoundException`)
  reuses the established `Exception("message")` → controller `catch` →
  `Problem(...)` shape.
- **V. API Contract Stability & Versioning**: PASS. Every change is additive
  — three new routes, zero changes to any existing request/response shape.
  The new `ActivityLogEntries` table is added via one code-first migration
  (`AddActivityLog`), committed with a descriptive name.
- **VI. Teach While Building**: Concepts you will learn in this feature — a
  foreign-key column that is deliberately *not* a real EF Core relationship,
  and why (the first time this codebase has needed that); wrapping two
  `SaveChangesAsync()` calls in one explicit `IDbContextTransaction` (`await
  using var transaction = await dbContext.Database.BeginTransactionAsync()`)
  versus relying on one implicit transaction per call, and exactly when each
  is needed (research.md #6); building a percentage/dependency-free SVG donut
  chart with `stroke-dasharray`/`stroke-dashoffset`; an append-only audit-log
  table pattern (no `Update`/`Delete` service methods exist at all — the
  simplest possible way to guarantee immutability, no permission check
  required because the capability doesn't exist).
- **VII. Incremental, Feature-by-Feature Delivery**: CONDITIONAL PASS — see
  Complexity Tracking. Delivered as separate commits per user story
  (Setup/Foundational migration + `ActivityLogService` scaffold → US1
  stat cards/default tab → US4 activity log write-path + project feed → US2
  status/priority breakdowns → US3 team workload → US5 work item detail
  activity history), matching spec.md's own priority order (US1/US4 are both
  P1) and the same per-story-commit approach already accepted for Features
  006/007/008.
- **VIII. Human in the Loop**: PASS — no auto-chaining; no `[NEEDS
  CLARIFICATION]` markers remain in the spec (the one open judgment call
  discovered during planning — what "In Progress" means given the workflow
  model's Open/Done-only category system — was resolved and the spec amended
  to state the corrected rule, not left ambiguous; see research.md #10).

## Project Structure

### Documentation (this feature)

```text
specs/009-summary-dashboard/
├── plan.md                        # This file (/speckit-plan command output)
├── research.md                    # Phase 0 output (/speckit-plan command)
├── data-model.md                  # Phase 1 output (/speckit-plan command)
├── quickstart.md                  # Phase 1 output (/speckit-plan command)
├── contracts/
│   └── summary-and-activity-api.md   # Phase 1 output (/speckit-plan command)
├── checklists/
│   └── requirements.md
└── tasks.md                       # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
backend/
├── TaskFlow.Api/
│   ├── Data/
│   │   ├── Entities/
│   │   │   └── ActivityLogEntry.cs            # NEW — Id, ProjectId, WorkItemId (plain int,
│   │   │                                         no FK — research.md #1), WorkItemTitle,
│   │   │                                         WorkItemType (both snapshots), ActorUserId,
│   │   │                                         EventType (enum), Field (nullable enum),
│   │   │                                         OldValue, NewValue (nullable strings), CreatedAt
│   │   ├── AppDbContext.cs                    # MODIFIED — ActivityLogEntries DbSet;
│   │   │                                        Project FK Cascade; ActorUserId FK Restrict;
│   │   │                                        indexes on ProjectId and WorkItemId
│   │   │                                        (data-model.md)
│   │   └── Migrations/
│   │       └── <ts>_AddActivityLog.cs         # NEW — additive schema only, no backfill
│   ├── Dtos/
│   │   ├── ActivityEntryDto.cs                # NEW
│   │   └── ProjectSummaryDto.cs               # NEW — ProjectSummaryDto, StatCardsDto,
│   │                                            StatusBreakdownItemDto, PriorityBreakdownItemDto,
│   │                                            WorkloadRowDto
│   ├── Services/
│   │   ├── ActivityLogService.cs              # NEW — RecordCreated, RecordFieldChange
│   │   │                                        (Add-only, no SaveChanges — research.md #6),
│   │   │                                        GetProjectFeedAsync (paged),
│   │   │                                        GetWorkItemHistoryAsync
│   │   └── WorkItemService.cs                  # MODIFIED — + ActivityLogService dependency;
│   │                                            CreateAsync wraps in explicit transaction +
│   │                                            records creation; UpdateAsync/UpdateStatusAsync/
│   │                                            UpdateSprintAsync capture old display values and
│   │                                            record field changes; new GetSummaryAsync
│   └── Controllers/
│       └── WorkItemsController.cs              # MODIFIED — + ActivityLogService dependency;
│                                                  GET .../summary, GET .../activity,
│                                                  GET api/work-items/{id}/activity
├── Program.cs                                  # MODIFIED — AddScoped<ActivityLogService>()
└── TaskFlow.Api.Tests/
    ├── Services/
    │   ├── ActivityLogServiceTests.cs          # NEW
    │   └── WorkItemServiceTests.cs             # MODIFIED — creation/field-change entry cases;
    │                                              GetSummaryAsync cases; every existing
    │                                              `new WorkItemService(Db)` call site updated
    └── Integration/
        └── WorkItemsEndpointsTests.cs           # MODIFIED — new summary/activity route cases

frontend/
├── src/
│   └── app/
│       ├── shared/
│       │   ├── relative-time.pipe.ts            # NEW — mirrors friendly-date.pipe.ts's
│       │   │                                       structure; "just now"/"N minutes ago"/
│       │   │                                       "N hours ago"/"N days ago"/falls back to
│       │   │                                       friendlyDate beyond ~7 days
│       │   └── relative-time.pipe.spec.ts       # NEW
│       └── projects/
│           ├── work-items.service.ts            # MODIFIED — + getProjectSummary(),
│           │                                       getProjectActivity(), getWorkItemActivity();
│           │                                       + ProjectSummary/StatCards/
│           │                                       StatusBreakdownItem/PriorityBreakdownItem/
│           │                                       WorkloadRow/ActivityEntry interfaces
│           ├── activity-feed/                    # NEW — shared between Summary tab and
│           │   │                                    work-item-detail (FR-019/FR-021)
│           │   ├── activity-feed.component.ts/.html/.css
│           │   ├── activity-feed.component.spec.ts
│           │   ├── build-activity-sentence.ts     # pure fn
│           │   └── build-activity-sentence.spec.ts
│           ├── summary/                          # NEW
│           │   ├── summary.component.ts/.html/.css   # 5th, default tab — fetches
│           │   │                                        getProjectSummary() +
│           │   │                                        getProjectActivity() on init
│           │   ├── summary.component.spec.ts
│           │   ├── status-donut-chart.component.ts/.html/.css
│           │   ├── status-donut-chart.component.spec.ts
│           │   ├── donut-segments.ts               # pure fn (arc math)
│           │   ├── donut-segments.spec.ts
│           │   ├── priority-bar-chart.component.ts/.html/.css
│           │   ├── priority-bar-chart.component.spec.ts
│           │   ├── team-workload.component.ts/.html/.css
│           │   └── team-workload.component.spec.ts
│           ├── project-detail/
│           │   └── project-detail.component.ts/.html   # MODIFIED — 5th view-mode
│           │       ('summary'), now the default when no `view` query param is present,
│           │       <app-summary> embed, tab order Summary|Board|Backlog|List|Tree
│           └── work-item-detail/
│               └── work-item-detail.component.ts/.html  # MODIFIED — fetches
│                   getWorkItemActivity(); renders <app-activity-feed> in a new
│                   Activity section
└── (no other new shared/ components — reuses StatusChipComponent,
     PriorityChipComponent, UserAvatarComponent, EmptyStateComponent as-is)
```

**Structure Decision**: Existing two-project layout (`backend/TaskFlow.Api` +
`frontend/`), unchanged. The Summary tab is a fifth tab inside the existing
`ProjectDetailComponent` (alongside Board/Backlog/List/Tree), not a new
routed page — it needs the same project context those four already have, and
`ProjectDetailComponent`'s `viewMode` signal/query-param mechanism already
generalizes to a fifth value with no new routing infrastructure. Activity Log
gets its own service (`ActivityLogService`) because, unlike `Label`, it has
genuinely separate read surfaces (a paginated project feed, an unpaginated
per-item history) distinct from `WorkItemService`'s existing responsibilities
— but no controller of its own, since its three routes fit naturally beside
`WorkItemsController`'s existing mixed route groups (research.md #17).

## Complexity Tracking

> Fill ONLY if Constitution Check has violations that must be justified

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|---------------------------------------|
| Changed/new file count exceeds the ~15-file guideline in Principle VII | The feature has two genuinely coupled surfaces that both depend on the same new Activity Log concept: the write-path infrastructure (entity, migration, service, hooks into four existing `WorkItemService` methods) and the Summary tab that is the entire reason the log exists (stat cards, two charts, workload, and the activity feed itself — spec.md's own framing: "the recent-activity card needs real data to show, and no such record exists yet"). Splitting the write-path into its own feature branch would ship a table nobody can see, delivering zero user-visible value — the same reasoning already accepted for Features 006/007/008's own oversized-but-coupled scope. Delivered here as six separate commits, one per user story in spec.md's own priority order (Setup/Foundational → US1 → US4 → US2 → US3 → US5), not one undifferentiated diff. |
