# Implementation Plan: Sprints & Backlog

**Branch**: `008-sprints-backlog` | **Date**: 2026-07-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/008-sprints-backlog/spec.md`

## Summary

Add a `Sprint` entity (per-project, name unique within project, start/end
date, `Planned`/`Active`/`Completed` lifecycle, at most one `Active` per
project) and let `Story`/`Task`/`SubTask` work items optionally belong to
one (`WorkItem.SprintId`, nullable — `Epic` never has one). A new Backlog
view — a fourth tab alongside the existing Board/List/Tree inside
`ProjectDetailComponent` — lists sprints soonest-first, each with its own
drag-and-drop-enabled item section, followed by an unscheduled Backlog
section sharing the List view's filters. The existing Board gains an "All
items" / "Active sprint" toggle that filters its existing query by
`sprintId`, self-contained the same way Board already owns its own data
fetching. Sprint CRUD and lifecycle transitions (`Start`/`Complete`/`Delete`)
get a new `SprintService`/`SprintsController` pair mirroring Feature 006's
`ProjectStatusService`/`ProjectStatusesController` (an entity with its own
lifecycle, unlike Feature 007's lifecycle-less `Label`). Item↔sprint
assignment reuses the field-scoped-PATCH pattern `UpdateStatusAsync`
established for the Board's drag (`PATCH .../work-items/{id}/sprint`), plus
a `WorkItemRequest.SprintId` field for full create/update and the Backlog's
per-section "+ Create" (FR-024). Days-remaining/overdue is a pure frontend
function mirroring the existing `isOverdue()` helper, not a server field —
same technique, same correctness pitfall already solved once. All changes to
existing endpoints/DTOs are additive; "all items" Board/List/Tree behavior
is unchanged when no sprint is involved (spec FR-020).

## Technical Context

**Language/Version**: C# 13 / .NET 10 (backend); TypeScript ~6.0 / Angular 22
(frontend), strict mode — unchanged from prior features.

**Primary Dependencies**: ASP.NET Core Web API, EF Core 10 (SQL Server
provider) — existing, no new backend package. Angular CDK's
`DragDropModule` (`cdkDropList`/`cdkDrag`) — already used by `BoardComponent`
and `WorkflowComponent`; the Backlog view is this dependency's third call
site, same pattern (per-section `cdkDropList`, optimistic update + revert on
failure). Angular Material's `MatDialog` — already used by
`WorkItemModalComponent` (Feature 007); the new sprint-create and
sprint-complete dialogs are this mechanism's second and third call sites. No
new frontend package.

**Storage**: SQL Server. One new table, `Sprints` (`Id`, `ProjectId` FK,
`Name`, `StartDate`, `EndDate`, `Status`; unique index on `(ProjectId,
Name)`), and one new nullable column, `WorkItems.SprintId` (`int?`, FK →
`Sprints`, `Restrict` — research.md #7). One EF Core code-first migration
(`AddSprints`), purely additive — no backfill needed (new table, new nullable
column).

**Testing**: xUnit + real SQL Server test databases (backend, existing
pattern); Vitest (frontend, existing pattern). New backend coverage (written
before implementation, Red-Green-Refactor): `SprintService` unit tests for
create validation (name length/uniqueness/date range), start eligibility
(item count, no-other-Active, error names the active sprint), complete
resolution (required only when not-Done items exist, Backlog vs. Sprint
destination, Done items retained, Completed becomes read-only), and delete
eligibility (Planned + zero items only); `WorkItemService` unit tests for
`UpdateSprintAsync` (permission reuse via `EnsureCanEdit`, Epic rejection,
Completed-sprint rejection, cross-project rejection) and `GetBacklogAsync`
(section grouping, filter parity with `GetWorkItemsAsync`). Integration
tests: new `SprintsEndpointsTests` covering the allowed and denied path for
every Manager/Admin-gated route (constitution's "every protected endpoint...
integration test... allowed and denied path"), plus `WorkItemsEndpointsTests`
extensions for the new PATCH/board-filter/backlog routes. New frontend
coverage (pure logic, test-first per constitution Principle I and the spec's
own non-functional requirement): `sprintDaysRemaining()` (mirroring
`overdue.spec.ts`), the Backlog drag's optimistic-update/revert logic
(mirroring `board.component.spec.ts`), and the sprint-create/complete
dialogs' client-side validation.

**Target Platform**: Browser (Angular SPA) + ASP.NET Core Web API —
unchanged.

**Project Type**: Web application — touches both `backend/` and `frontend/`.

**Performance Goals**: No new performance goal. The Backlog endpoint is one
query plus an in-memory grouping pass, the same shape already accepted for
`GetBoardAsync`/`GetTreeAsync` at this feature's scale (tens to low hundreds
of items per project).

**Constraints**: All API contract changes are additive — new optional
request field (`sprintId` on `WorkItemRequest`), new always-present response
fields (`sprintId`/`sprintName`), one new optional query parameter
(`sprintId` on the board endpoint), and three entirely new routes (sprint
CRUD/lifecycle, the sprint PATCH, the backlog GET) — so, like Feature 007, no
breaking-change migration path is needed.

**Scale/Scope**: 1 new entity (`Sprint`), 1 new enum (`SprintStatus`), 1
migration, 1 new service (`SprintService`) + its exceptions file, 1 new
controller (`SprintsController`), `WorkItemService` extended (3 new/changed
methods: `UpdateSprintAsync`, `GetBacklogAsync`, `GetBoardAsync`'s new
filter, plus a shared filter-predicate extraction), `WorkItemsController`
extended (1 new PATCH route, 1 new GET route, 1 new query param on an
existing route), 4 backend DTOs extended
(`WorkItemRequest`/`WorkItemDto`/`WorkItemDetailDto` +`SprintId`, new
`SprintDto`/`CreateSprintRequest`/`CompleteSprintRequest`/
`UpdateWorkItemSprintRequest`/`WorkItemBacklogDto`). Frontend: 1 new service
(`SprintsService`), `WorkItemsService` extended (sprint fields + 3 new
methods), `BoardComponent` extended (toggle + self-fetched sprints), 5 new
components (`BacklogComponent`, `BacklogItemRowComponent`,
`SprintFormComponent`, `CompleteSprintDialogComponent`, plus the pure
`sprint-days-remaining.ts` helper), `ProjectDetailComponent` extended (4th
view-mode tab). Comparable in shape to Feature 006 (a new per-project managed
entity with its own screen) plus Feature 005/007's drag/dialog patterns
reapplied, not reinvented.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Test-First Development**: PASS (with plan). Every new business rule
  (`SprintService`'s create/start/complete/delete guards, `WorkItemService`'s
  sprint-assignment guards) gets xUnit tests against a real SQL Server test
  database before the corresponding implementation, per the existing
  `ProjectStatusServiceTests`/`WorkItemServiceTests` pattern. Every
  Manager/Admin-gated route gets an integration test for both the allowed
  and denied path. Frontend: `sprintDaysRemaining()` and the Backlog drag's
  optimistic-update/revert logic are pure functions/signals given Vitest
  specs first, matching the spec's own explicit non-functional requirement
  ("Pure logic ... is test-first").
- **II. Secure by Default**: PASS. Sprint create/start/complete/delete are
  `[Authorize(Roles = "Manager,Admin")]`, the same explicit-per-action
  pattern `ProjectStatusesController` already established (class-level
  `[Authorize]` for reads, per-action role restriction for writes). Item
  sprint-assignment (`PATCH .../sprint`, and the `SprintId` field on
  Create/Update) reuses the existing `EnsureCanEdit` ownership/role check
  verbatim — no new authorization concept, no new gap.
- **III. Clarity Over Cleverness**: PASS. `Sprint` gets its own
  service/controller because — unlike Feature 007's `Label` — it has a real
  independent lifecycle, the same judgment call already applied to
  `WorkflowStatus` in Feature 006 (research.md #1). No `Position` column or
  reorder endpoint for sprints — display order is already fully determined
  by `StartDate`, so adding one would be speculative flexibility the spec
  never asks for (research.md #2). Days-remaining/overdue stays a pure
  frontend function, avoiding a second implementation of logic
  `isOverdue()` already solved (research.md #3). No dedicated
  completion-preview endpoint — the Backlog view already has the data a
  preview would return (research.md #8).
- **IV. Consistent Code Quality & Review Gates**: PASS. New DTOs follow the
  existing flattened, record-based, manually-mapped convention
  (`SprintId`/`SprintName` alongside `WorkItemDto`'s existing
  `AssigneeUserId`/`AssigneeName` pair). New routes are RESTful, nested
  under `api/projects/{projectId}/sprints` (plural noun); the field-scoped
  `PATCH .../work-items/{id}/sprint` mirrors the existing `.../status`
  route's shape exactly. Async I/O throughout; Angular strict mode, signals,
  no `any`. Every new exception follows the established
  `Exception("message")` → controller `catch` → `Problem(400/403/404/409,
  ...)` shape, including the `ProblemDetails.Extensions["itemCount"]`
  pattern already established for `DestinationStatusRequiredException`.
- **V. API Contract Stability & Versioning**: PASS. Every change is
  additive — one new optional request field, two new always-present
  response fields, one new optional query parameter, three genuinely new
  routes/endpoint-groups. No existing field is renamed or retyped. The new
  `Sprints` table and `WorkItems.SprintId` column are added via one
  code-first migration, committed with a descriptive name (`AddSprints`).
- **VI. Teach While Building**: Concepts you will learn in this feature — a
  second, independent state machine in the same codebase (`SprintStatus`,
  alongside `WorkflowStatusCategory`) and how its transition guards differ
  from a simple status flag; extracting a shared LINQ predicate-builder
  (`BuildFilteredQuery`) so two methods (`GetWorkItemsAsync`,
  `GetBacklogAsync`) share one `WHERE`-clause definition instead of
  duplicating it; a second `Restrict`-not-`Cascade` foreign key and *why*
  ("multiple cascade paths," SQL Server error 1785) it's needed again here
  for the same structural reason as `WorkflowStatus → WorkItem`; a field-
  scoped `PATCH` as a *pattern* (not a one-off) once you see the same shape
  reused for a second field (`SprintId` alongside `StatusId`).
- **VII. Incremental, Feature-by-Feature Delivery**: CONDITIONAL PASS — see
  Complexity Tracking. Delivered as separate commits per user story
  (Setup/Foundational migration+`SprintService` scaffold → US1 create sprint
  → US2 Backlog view (read-only) → US3 drag-and-drop → US4 start/complete/
  delete lifecycle → US5 sprint-scoped Board → US6 days-remaining
  indicator), matching the priority order in spec.md and the same
  per-story-commit approach already accepted for Features 006/007.
- **VIII. Human in the Loop**: PASS — no auto-chaining; no `[NEEDS
  CLARIFICATION]` markers remain in the spec (all open judgment calls —
  sprint ordering, days-remaining placement, completion-preview data source,
  cascade behavior — were resolved into documented research.md decisions,
  not left ambiguous).

## Project Structure

### Documentation (this feature)

```text
specs/008-sprints-backlog/
├── plan.md                        # This file (/speckit-plan command output)
├── research.md                    # Phase 0 output (/speckit-plan command)
├── data-model.md                  # Phase 1 output (/speckit-plan command)
├── quickstart.md                  # Phase 1 output (/speckit-plan command)
├── contracts/
│   └── sprints-api.md             # Phase 1 output (/speckit-plan command)
├── checklists/
│   └── requirements.md
├── visual-reference.png           # Jira Backlog layout reference (translated to this app's design-system tokens)
└── tasks.md                       # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
backend/
├── TaskFlow.Api/
│   ├── Data/
│   │   ├── Entities/
│   │   │   ├── Sprint.cs                      # NEW — Id, ProjectId, Name, StartDate,
│   │   │   │                                     EndDate, Status (SprintStatus enum)
│   │   │   ├── Project.cs                     # MODIFIED — + Sprints collection nav
│   │   │   └── WorkItem.cs                    # MODIFIED — + SprintId (int?), + Sprint nav
│   │   ├── AppDbContext.cs                    # MODIFIED — Sprints DbSet, FK config,
│   │   │                                        unique index, WorkItem.SprintId config
│   │   │                                        (data-model.md)
│   │   └── Migrations/
│   │       └── <ts>_AddSprints.cs             # NEW — additive schema only, no backfill
│   ├── Dtos/
│   │   ├── SprintDto.cs                       # NEW
│   │   ├── CreateSprintRequest.cs             # NEW
│   │   ├── CompleteSprintRequest.cs           # NEW
│   │   ├── UpdateWorkItemSprintRequest.cs     # NEW
│   │   ├── WorkItemBacklogDto.cs              # NEW — WorkItemBacklogDto, BacklogSprintSectionDto
│   │   ├── WorkItemRequest.cs                 # MODIFIED — + SprintId
│   │   ├── WorkItemDto.cs                     # MODIFIED — + SprintId, + SprintName
│   │   └── WorkItemDetailDto.cs               # MODIFIED — + SprintId, + SprintName
│   ├── Services/
│   │   ├── SprintService.cs                   # NEW — Create/Start/Complete/Delete/GetSprints
│   │   ├── SprintExceptions.cs                # NEW
│   │   └── WorkItemService.cs                 # MODIFIED — UpdateSprintAsync;
│   │                                            GetBacklogAsync; GetBoardAsync gains
│   │                                            sprintId filter; BuildFilteredQuery
│   │                                            extraction shared with GetWorkItemsAsync;
│   │                                            SprintId handling in Create/UpdateAsync
│   └── Controllers/
│       ├── SprintsController.cs               # NEW
│       └── WorkItemsController.cs             # MODIFIED — new PATCH .../sprint action;
│                                                new GET .../backlog action; sprintId
│                                                query param on GetBoard
└── TaskFlow.Api.Tests/
    ├── Services/
    │   ├── SprintServiceTests.cs               # NEW
    │   └── WorkItemServiceTests.cs             # MODIFIED — UpdateSprintAsync,
    │                                              GetBacklogAsync cases
    └── Integration/
        ├── SprintsEndpointsTests.cs             # NEW — allowed + denied path per role gate
        └── WorkItemsEndpointsTests.cs           # MODIFIED — new PATCH/GET/board-filter cases

frontend/
├── src/
│   └── app/
│       ├── projects/
│       │   ├── work-items.service.ts           # MODIFIED — sprintId/sprintName fields,
│       │   │                                      updateWorkItemSprint(), getBacklog(),
│       │   │                                      getBoard() gains sprintId param
│       │   ├── sprints.service.ts               # NEW — mirrors project-status.service.ts
│       │   ├── backlog/                         # NEW
│       │   │   ├── backlog.component.ts/.html/.css
│       │   │   ├── backlog.component.spec.ts     # drag optimistic-update/revert logic
│       │   │   ├── backlog-item-row.component.ts/.html   # status/due-date/assignee inline (FR-026)
│       │   │   └── sprint-days-remaining.ts + .spec.ts   # pure fn, mirrors board/overdue.ts
│       │   ├── sprint-form/                      # NEW — MatDialog, create sprint
│       │   │   ├── sprint-form.component.ts/.html
│       │   │   └── sprint-form.component.spec.ts
│       │   ├── complete-sprint-dialog/           # NEW — MatDialog, resolution picker
│       │   │   ├── complete-sprint-dialog.component.ts/.html
│       │   │   └── complete-sprint-dialog.component.spec.ts
│       │   ├── board/
│       │   │   └── board.component.ts/.html      # MODIFIED — "All items"/"Active sprint"
│       │   │                                        toggle; self-fetched sprints list;
│       │   │                                        empty state when no Active sprint
│       │   └── project-detail/
│       │       └── project-detail.component.ts/.html  # MODIFIED — 4th view-mode tab
│       │                                                 ('backlog'), <app-backlog> embed
└── (no new shared/ components — reuses StatusChipComponent, UserAvatarComponent,
     FriendlyDatePipe, EmptyStateComponent as-is)
```

**Structure Decision**: Existing two-project layout (`backend/TaskFlow.Api` +
`frontend/`), unchanged. `Sprint` gets its own service/controller pair
(`SprintService`/`SprintsController`), following `WorkflowStatus`'s
precedent rather than `Label`'s, because it has an independent lifecycle
(research.md #1). The Backlog view is a fourth tab inside the existing
`ProjectDetailComponent` (alongside Board/List/Tree), not a new routed page
— it needs the same project context, filters, and modal wiring those three
already have, and `ProjectDetailComponent`'s `viewMode` signal/query-param
mechanism already generalizes to a fourth value with no new routing
infrastructure.

## Complexity Tracking

> Fill ONLY if Constitution Check has violations that must be justified

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|---------------------------------------|
| Changed/new file count exceeds the ~15-file guideline in Principle VII | The feature has two genuinely separate surfaces that both depend on the same new `Sprint` concept: sprint lifecycle management (create/start/complete/delete, its own service/controller/tests) and the Backlog view that makes that lifecycle usable (drag-and-drop, per-section create, days-remaining), plus the smaller sprint-scoped Board toggle. Splitting these into separate feature branches was considered, but a `Sprint` entity with no UI to create/view/act on it delivers zero user-visible value (the same reasoning already accepted for Feature 007's modal-plus-every-entry-point scope) — they are one coherent unit of work. Delivered here as seven separate commits, one per user story in spec.md's own priority order (Setup/Foundational → US1 → US2 → US3 → US4 → US5 → US6), not one undifferentiated diff, matching the approach already accepted for Features 006 and 007. |
