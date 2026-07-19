# Implementation Plan: Custom Workflow Columns

**Branch**: `006-custom-workflow-columns` | **Date**: 2026-07-19 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/006-custom-workflow-columns/spec.md`

## Summary

Replace the system-wide, fixed `WorkItemStatus` enum (To Do / In Progress /
In Review / Done) with a new per-project **`WorkflowStatus`** entity — an
ordered, named, categorized (Open/Done), colored row that a project owns
1-to-many. `WorkItem.Status` (enum column) is replaced by
`WorkItem.WorkflowStatusId` (FK). A one-time EF Core migration seeds every
existing project with the standard four `WorkflowStatus` rows and backfills
every `WorkItem`'s new FK from its old enum value, preserving state exactly.
A new `ProjectStatusService`/`ProjectStatusesController` pair adds
Manager/Admin-gated add/rename/reorder/delete-with-move endpoints. Every
existing status-aware surface (board, chips, dropdowns, filters,
open-item/`"n/m done"` counts) switches from comparing a fixed enum value to
either displaying a `WorkflowStatus`'s name/color or reasoning about its
`Category`. The board itself needs no structural change — Feature 005
already renders its columns from a backend-supplied ordered list; that list
now comes from a project's own `WorkflowStatus` rows instead of
`Enum.GetValues<WorkItemStatus>()`.

## Technical Context

**Language/Version**: C# 13 / .NET 10 (backend); TypeScript ~6.0.2 / Angular
22 (frontend), strict mode

**Primary Dependencies**: ASP.NET Core Web API, EF Core 10 (SQL Server
provider) — all existing, no new backend package. `@angular/cdk/drag-drop` —
already a project dependency since Feature 005 (board card dragging); this
feature is its second consumer (column reordering in the new Workflow
management screen), still no new package.

**Storage**: SQL Server. New `WorkflowStatuses` table (Id, ProjectId, Name,
Position, Category, ColorKey). `WorkItems.Status` (string-converted enum
column) is dropped and replaced by `WorkItems.WorkflowStatusId` (int FK,
`ON DELETE RESTRICT`). One EF Core code-first migration performs the schema
change and a same-migration data backfill (see research.md #4) — no
dual-write period, matching FR-007.

**Testing**: xUnit + real SQL Server test databases (backend, existing
pattern); Vitest (frontend, existing pattern). New: a migration-specific
test that seeds pre-migration-shaped data and asserts the post-migration
shape matches exactly (FR-023, SC-003).

**Target Platform**: Browser (Angular SPA) + ASP.NET Core Web API, same as
all prior features.

**Project Type**: Web application — touches both `backend/` and `frontend/`.

**Performance Goals**: No new performance goal beyond matching existing
patterns — the statuses list per project is at most 10 rows (FR-004) and is
cheap to load with, or alongside, the board/work-item queries that already
run at this project's scale.

**Constraints**: This feature makes deliberate, coupled **breaking changes**
to existing API contracts (see research.md #8 and Complexity Tracking) —
`WorkItemRequest.Status`/`UpdateWorkItemStatusRequest.Status` (string enum
name) become `StatusId` (int); every work-item response DTO's single
`Status` string field is replaced by flattened `StatusId`/`StatusName`/
`StatusCategory`/`StatusColorKey` fields (mirroring the existing
`AssigneeUserId`/`AssigneeName` flattening convention already used
elsewhere in these DTOs). Per constitution Principle V this must be called
out explicitly with a migration path: the migration path is that
`backend/` and `frontend/` ship together in this one feature branch/PR, the
same single-coupled-deploy model every prior feature already uses — there
is no independent external consumer of these DTOs to break.

**Scale/Scope**: 1 new entity, 1 migration, 1 new service, 1 new
controller, ~5 new DTOs, changes to ~6 existing backend files (WorkItem
entity/service/controller, Project entity/service, AppDbContext), and a
comparable set of frontend changes (status typing, chip component, work-item
form, project-detail filters, board component's status references, plus a
new Workflow management screen/route). This is the largest single-feature
change so far — see Complexity Tracking.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Test-First Development**: PASS (with plan). Backend: service-level
  tests for `ProjectStatusService` (add/rename/reorder/delete-with-move,
  name-uniqueness, min-Open/min-Done guard, max-10 guard, default-completion
  computation) and endpoint tests (200/400/403/404/409) written before
  implementation. The migration's data backfill gets its own pre/post
  snapshot test (FR-023, SC-003), written before the migration is applied in
  the test database. Frontend: pure logic (category-based "n/m done" and
  open-count reasoning, position-resequencing) gets Vitest specs first.
- **II. Secure by Default**: PASS. All workflow-mutation endpoints
  (add/rename/reorder/delete) use `[Authorize(Roles = "Manager,Admin")]`,
  server-enforced independent of the UI hiding the entry point from other
  roles (FR-008) — an endpoint test calls each mutation route as a
  Developer and asserts 403, mirroring the existing pattern already used
  for `ProjectsController`'s Create/Update/Delete.
- **III. Clarity Over Cleverness**: PASS. Reordering resequences every
  status's `Position` as plain sequential integers on every reorder/add/
  delete action — no fractional-position or gap-based scheme, which would
  be cleverness this feature doesn't need at a max-10-rows scale (research.md
  #1). The default completion status is computed on demand (`MIN(Position)`
  where `Category = Done`) rather than stored and reassigned, removing a
  whole class of update-on-delete bookkeeping the spec's FR-024 explicitly
  ruled out. A new `ProjectStatusService`/`ProjectStatusesController` pair
  is justified, not premature abstraction: `WorkflowStatus` is its own
  entity with its own validation rules (uniqueness, category guards,
  atomic delete-with-move), mirroring the existing one-service-per-entity
  split (`ProjectService`, `WorkItemService`) rather than bloating either.
- **IV. Consistent Code Quality & Review Gates**: PASS. New DTOs are
  records, manually mapped; RESTful nested route
  (`/api/projects/{projectId}/statuses`) matching existing controller
  conventions; async I/O throughout; Angular strict mode, signals, no
  `any`. `Category` and `ColorKey` remain readable-text database columns
  (`HasConversion<string>()`), consistent with every other enum in this
  codebase (Principle VI teaching point carried forward, not reinvented).
- **V. API Contract Stability & Versioning**: CONDITIONAL PASS — see
  Technical Context's Constraints and Complexity Tracking. This feature
  makes intentional breaking changes to `WorkItemRequest`,
  `UpdateWorkItemStatusRequest`, and every work-item response DTO's status
  field. Documented here with its migration path (single coupled deploy);
  the database migration itself is additive-then-cleanup within one
  migration file, matching Principle V's "every migration is committed to
  git with a descriptive name" requirement (e.g.
  `AddPerProjectWorkflowStatuses`).
- **VI. Teach While Building**: Concepts you will learn in this feature —
  writing an EF Core migration that combines a schema change with a
  same-migration data backfill (`migrationBuilder.Sql(...)` for
  developer-authored, non-user-input SQL — distinct from the "raw SQL in
  application queries" the constitution restricts); modeling a fixed
  enum-like concept (`Category`) alongside a fully dynamic, user-managed
  one (`Name`) on the same entity; computing a value on demand
  (`GetDefaultCompletionStatus`) instead of storing and maintaining a
  denormalized flag; Angular CDK `cdkDropList`'s reuse for a second,
  unrelated drag interaction (list reordering vs. Feature 005's card
  movement between columns).
- **VII. Incremental, Feature-by-Feature Delivery**: CONDITIONAL PASS — see
  Complexity Tracking. This feature's change spans a new table/migration,
  a new service/controller, and edits across most of the existing
  work-item/project surface on both backend and frontend, likely the
  largest single feature so far and well past the ~15-file guideline;
  delivered as separate commits per user story (Setup/Foundational → US1
  per-project foundation+migration → US2 view screen → US3 add → US4
  rename → US5 reorder → US6 delete), not one undifferentiated diff.
- **VIII. Human in the Loop**: PASS — no auto-chaining; no
  `[NEEDS CLARIFICATION]` markers remain in the spec (the one open question
  from spec review was resolved into FR-024, not left ambiguous).

## Project Structure

### Documentation (this feature)

```text
specs/006-custom-workflow-columns/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md         # Phase 1 output (/speckit-plan command)
├── quickstart.md         # Phase 1 output (/speckit-plan command)
├── contracts/
│   └── workflow-api.md   # Phase 1 output (/speckit-plan command)
├── checklists/
│   └── requirements.md
└── tasks.md              # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
backend/
├── TaskFlow.Api/
│   ├── Data/
│   │   ├── Entities/
│   │   │   ├── WorkflowStatus.cs             # NEW — Id, ProjectId, Name, Position,
│   │   │   │                                   Category (enum), ColorKey (enum)
│   │   │   ├── Project.cs                     # MODIFIED — WorkflowStatuses collection
│   │   │   └── WorkItem.cs                    # MODIFIED — Status enum removed;
│   │   │                                        WorkflowStatusId (FK) + WorkflowStatus nav added
│   │   ├── AppDbContext.cs                    # MODIFIED — WorkflowStatuses DbSet, FK config,
│   │   │                                        (ProjectId, Name) unique index
│   │   └── Migrations/
│   │       └── <ts>_AddPerProjectWorkflowStatuses.cs  # NEW — schema + data backfill (research.md #4)
│   ├── Dtos/
│   │   ├── WorkflowStatusDto.cs               # NEW — {Id, Name, Category, ColorKey, Position, ItemCount}
│   │   ├── CreateWorkflowStatusRequest.cs     # NEW — {Name, Category, Position?}
│   │   ├── UpdateWorkflowStatusRequest.cs     # NEW — {Name?, ColorKey?}
│   │   ├── ReorderWorkflowStatusesRequest.cs  # NEW — {OrderedStatusIds: int[]}
│   │   ├── WorkItemRequest.cs                 # MODIFIED — Status (string) → StatusId (int?)
│   │   ├── UpdateWorkItemStatusRequest.cs     # MODIFIED — Status (string) → StatusId (int)
│   │   ├── WorkItemDto.cs / WorkItemDetailDto.cs / WorkItemChildDto.cs /
│   │   │   WorkItemTreeNodeDto.cs / WorkItemBoardCardDto.cs
│   │   │                                      # MODIFIED — Status (string) →
│   │   │                                        StatusId/StatusName/StatusCategory/StatusColorKey
│   │   └── BoardColumnDto.cs                  # MODIFIED — Status/Label → StatusId/Name/Category/ColorKey
│   ├── Services/
│   │   ├── ProjectStatusService.cs            # NEW — add/rename/reorder/delete-with-move,
│   │   │                                        GetDefaultCompletionStatusId(), validation rules
│   │   ├── ProjectStatusExceptions.cs         # NEW
│   │   ├── ProjectService.cs                  # MODIFIED — CreateAsync seeds standard 4 statuses;
│   │   │                                        open-item count uses Category, not enum
│   │   └── WorkItemService.cs                 # MODIFIED — Create/Update/UpdateStatus use StatusId;
│   │                                            GetBoardAsync/GetTreeAsync/GetWorkItemsAsync read
│   │                                            WorkflowStatus via navigation, "done" checks use
│   │                                            Category
│   └── Controllers/
│       ├── ProjectStatusesController.cs       # NEW — GET (any authenticated user), POST/PUT
│       │                                        rename/PUT reorder/DELETE (Manager/Admin only)
│       └── WorkItemsController.cs             # MODIFIED — request/response shape changes above
└── TaskFlow.Api.Tests/
    ├── Services/ProjectStatusServiceTests.cs  # NEW
    ├── Services/ProjectServiceTests.cs        # MODIFIED — seeding, category-based open count
    ├── Services/WorkItemServiceTests.cs       # MODIFIED — StatusId-based cases
    ├── Integration/ProjectStatusesEndpointsTests.cs  # NEW — 200/400/403/404/409 cases
    ├── Integration/WorkItemsEndpointsTests.cs # MODIFIED — StatusId-based cases
    └── Migrations/WorkflowStatusMigrationTests.cs    # NEW — pre/post snapshot (FR-023)

frontend/
├── src/
│   ├── design-tokens.scss                     # MODIFIED — fixed per-status-name tokens replaced
│   │                                            by a fixed ColorKey palette (e.g.
│   │                                            --color-chip-slate-{bg,text}, …-violet-, …-green-)
│   └── app/
│       ├── projects/
│       │   ├── work-items.service.ts          # MODIFIED — WorkItemStatus union removed;
│       │   │                                    ProjectStatus type + getStatuses(); WorkItem/
│       │   │                                    BoardCard carry statusId/statusName/
│       │   │                                    statusCategory/statusColorKey
│       │   ├── project-status.service.ts      # NEW — add/rename/reorder/delete-with-move calls
│       │   ├── work-item-form/
│       │   │   └── work-item-form.component.ts/.html  # MODIFIED — Status dropdown sourced from
│       │   │                                            getStatuses(), binds statusId
│       │   ├── project-detail/
│       │   │   └── project-detail.component.ts/.html  # MODIFIED — status filter sourced from
│       │   │                                            getStatuses(); "Workflow" management
│       │   │                                            link (Manager/Admin only, reusing
│       │   │                                            canManageProject())
│       │   ├── work-item-detail/
│       │   │   └── work-item-detail.component.ts       # MODIFIED — statusId-based display
│       │   ├── board/
│       │   │   ├── board.component.ts/.html            # MODIFIED — columns grouped by statusId,
│       │   │   │                                          not a fixed enum
│       │   │   └── board-card.component.ts/.html        # MODIFIED — status chip via
│       │   │                                              statusName/statusColorKey
│       │   └── workflow/
│       │       ├── workflow.component.ts/.html/.css     # NEW — management screen: list, add
│       │       │                                          form, inline rename, CDK reorder,
│       │       │                                          delete (+ destination-picker) flow
│       │       └── workflow.component.spec.ts           # NEW
│       ├── app.routes.ts                       # MODIFIED — /projects/:id/workflow route
│       └── shared/
│           └── status-chip/
│               └── status-chip.component.ts/.html        # MODIFIED — inputs become
│                                                            name/colorKey instead of a fixed
│                                                            WorkItemStatus union
```

**Structure Decision**: Existing two-project layout (`backend/TaskFlow.Api`
+ `frontend/`), unchanged. The new per-project-status domain gets its own
service/controller pair (`ProjectStatusService`/`ProjectStatusesController`),
matching the existing one-service-per-entity convention, rather than folding
into `ProjectService` (statuses aren't a project *field*, they're a related,
independently-managed collection with their own rules) or `WorkItemService`
(work items *reference* statuses but don't own their lifecycle). The new
Workflow management screen lives at `frontend/src/app/projects/workflow/`,
a sibling of `board/`, `project-detail/`, etc. — consistent with this
codebase's flat, feature-grouped layout under `projects/`.

## Complexity Tracking

> Fill ONLY if Constitution Check has violations that must be justified

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|---------------------------------------|
| Changed/new file count far exceeds the ~15-file guideline in Principle VII, spanning a new table/migration, a new service/controller, and edits across nearly every existing work-item-status touchpoint on both backend and frontend | The feature's entire premise (spec.md) is replacing one system-wide fixed status list with a per-project one — every existing surface that reads or writes a work item's status (board, chips, dropdowns, filters, tree "n/m done", open-item counts, create/edit forms) is, by definition, a touchpoint that must change from "compare a fixed enum" to "read this project's own `WorkflowStatus` row." There is no smaller slice that delivers real value: a Workflow management screen that lets a Manager add/rename/reorder/delete columns is worthless if the board and forms still render a hard-coded four-status list underneath it. Splitting into "backend migration + entity" and "frontend consumption" across two separate feature branches was considered, but the first half alone ships no observable change (by design — FR-006's migration must be invisible on day one) and the second half cannot be built or tested without the first — they are one coherent, sequentially-dependent unit of work, delivered here as separate commits per user story (Setup/Foundational migration → US1 read-path parity → US2 view screen → US3 add → US4 rename → US5 reorder → US6 delete), not one undifferentiated diff, matching the same approach already accepted for Feature 005. |
| Breaking changes to `WorkItemRequest`, `UpdateWorkItemStatusRequest`, and every work-item response DTO's status field (Principle V) | A per-project, arbitrarily-named status cannot be represented by the existing fixed string enum name (`"ToDo"`, `"InReview"`, etc.) once names are user-defined and renameable — continuing to key off name would silently break the moment a Manager renamed a column, and FR-018 explicitly requires identity-based (not name-based) status references. | Keeping the string-based `Status` field and mapping status *names* to per-project `WorkflowStatus` rows by string match was considered, but breaks the instant a column is renamed (FR-011's whole point) and cannot express two projects having differently-named, differently-ordered, or differently-categorized statuses that happen to collide on name — the feature's core requirement (FR-001/FR-002) is incompatible with a name-keyed reference. |
