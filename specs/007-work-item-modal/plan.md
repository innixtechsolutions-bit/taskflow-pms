# Implementation Plan: Work Item Modal & Quick Creation

**Branch**: `007-work-item-modal` | **Date**: 2026-07-20 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/007-work-item-modal/spec.md`

## Summary

Replace the routed full-page work-item create/edit form
(`WorkItemFormComponent` at `.../work-items/new` and `.../work-items/:id/edit`)
with a `MatDialog`-based `WorkItemModalComponent` reachable from every
existing creation/edit affordance (board column "+", "Add child",
project-detail's "New work item"/empty-state actions, list-row/detail "Edit"
links), preserving every existing field, validation rule, and pre-selection
behavior. On top of the modal itself, this feature adds four small
accelerators the spec calls out by name: an "Assign to me" one-click shortcut,
an optional `StartDate` field (new `WorkItem` column, validated start ≤ due on
both client and server), a "Create another" checkbox that keeps the create
modal open and retains most fields between saves, and project-scoped Labels
(a new `Label`/`WorkItemLabel` many-to-many, created inline by name, shown as
neutral chips on board/list/tree/detail, filterable single-select in the List
view). All API changes are additive (new optional request fields, new
response fields, one new GET endpoint) — no existing contract breaks.

## Technical Context

**Language/Version**: C# 13 / .NET 10 (backend); TypeScript ~6.0 / Angular 22
(frontend), strict mode — unchanged from prior features.

**Primary Dependencies**: ASP.NET Core Web API, EF Core 10 (SQL Server
provider) — existing, no new backend package. Angular Material's
`MatDialogModule`/`MatDialog`/`MatDialogRef`/`MAT_DIALOG_DATA` — Angular
Material is already a project dependency (`^22.0.4`); this is the first
feature to use its Dialog module specifically (research.md #1). No new
frontend package.

**Storage**: SQL Server. One new nullable column, `WorkItems.StartDate`
(`DateTime?`), and two new tables: `Labels` (`Id`, `ProjectId` FK, `Name`,
`CreatedAt`; unique index on `(ProjectId, Name)`) and `WorkItemLabels` (`Id`,
`WorkItemId` FK, `LabelId` FK; unique index on `(WorkItemId, LabelId)`) — the
first many-to-many relationship in this codebase, modeled as an explicit join
entity rather than EF Core's implicit `UsingEntity<>()` (research.md #3). One
EF Core code-first migration, purely additive — no data backfill needed (new
nullable column, new empty tables).

**Testing**: xUnit + real SQL Server test databases (backend, existing
pattern); Vitest (frontend, existing pattern). New backend coverage: start≤due
validation (both directions, both null-date cases), label normalization
(trim/length/case-insensitive-reuse/dedupe/5-item cap), the new
`GET .../labels` endpoint, the new `label` list-filter query param, and 400
endpoint tests for all three new exception types. New frontend coverage
(pure logic, test-first per constitution Principle I): the modal's dirty-flag
tracking (research.md #2), "Create another" field-retention logic
(research.md #8), and label-input normalization (trim/dedupe/cap) on the
client side, mirroring the server-side rules so the client can block obviously
invalid input before a round-trip.

**Target Platform**: Browser (Angular SPA) + ASP.NET Core Web API — unchanged.

**Project Type**: Web application — touches both `backend/` and `frontend/`.

**Performance Goals**: No new performance goal. Labels add one small indexed
lookup (`GET .../labels`, `Any()`-filtered) and one additional `AND` condition
on the existing list query, at the same small per-project scale every prior
feature already targets.

**Constraints**: All API contract changes are additive — new optional request
fields (`startDate`, `labels`), new always-present response fields, one new
GET endpoint (research.md #12) — so, unlike Feature 006, no coupled-breaking-
change migration path is needed. The frontend-only removal of the
`.../work-items/new` and `.../work-items/:id/edit` routes (replaced by
`redirectTo`) is not an API contract change and carries no server-side
migration concern.

**Scale/Scope**: 2 new entities (`Label`, `WorkItemLabel`), 1 migration, 3 new
exceptions (`InvalidDateRangeException`, `InvalidLabelException`,
`TooManyLabelsException`), 1 new endpoint (`GET .../labels`), 1 new query
param (`label` on the list endpoint), 5 existing DTOs extended, 1 new
`WorkItemModalComponent` (replacing the deleted `WorkItemFormComponent`), 1 new
`LabelChipComponent`, and edits across every existing creation/edit entry
point (board, project-detail list/tree, work-item detail) plus routing. Larger
in frontend touch-point count than backend footprint — see Complexity
Tracking.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Test-First Development**: PASS (with plan). Backend: `WorkItemService`
  gets new unit tests for start≤due (both orders, both-null, one-null cases)
  and the label-normalization helper (trim, 1–30 length, case-insensitive
  reuse, dedupe, >5 rejection) written before the corresponding implementation
  code; endpoint tests cover the 3 new 400 cases plus the new `GET .../labels`
  route and the new `label` filter param. Frontend: the modal's dirty-flag
  logic, "Create another" retention logic, and client-side label
  normalization are pure functions/signals given Vitest specs first, per
  Principle I and the spec's own Non-functional requirement ("modal logic
  that is pure ... is test-first").
- **II. Secure by Default**: PASS. No new role or permission tier is
  introduced — labels are created/attached under the exact same
  `EnsureCanEdit` check `UpdateAsync`/`CreateAsync` already enforce for every
  other field (creator, current assignee, or Manager/Admin), matching spec's
  "same permission as editing the item's other fields" requirement. The new
  `GET .../labels` endpoint is `[Authorize]`-only (any authenticated user), the
  same openness already granted to the read-only status-list endpoint, since
  the modal's suggestions and the List view's filter both need it regardless
  of role — not a new gap, the same deliberate boundary Feature 006 already
  established for statuses.
- **III. Clarity Over Cleverness**: PASS. No new service/controller pair for
  Labels — `Label` has no independent lifecycle in v1 (no create/rename/
  delete endpoint), so its lookup/normalization logic lives in the existing
  `WorkItemService`/`WorkItemsController` rather than mirroring Feature 006's
  `ProjectStatusService` pattern where it wouldn't be justified (research.md
  #11). Label suggestions use a query-time `.Any()` filter instead of
  orphan-delete bookkeeping scattered across Update/Delete (research.md #5).
  The modal's discard-confirmation uses a single dirty flag and the codebase's
  existing `confirm()` pattern, not a new value-diffing engine or a second
  confirm-dialog component (research.md #2).
- **IV. Consistent Code Quality & Review Gates**: PASS. New DTOs/fields follow
  the existing flattened, record-based, manually-mapped convention; the new
  endpoint is RESTful (`GET /api/projects/{projectId}/labels`, plural noun,
  nested under project); async I/O throughout; Angular strict mode, signals,
  no `any`; every new exception follows the established
  `Exception("message")` → controller `catch` → `Problem(400, ...)` shape.
- **V. API Contract Stability & Versioning**: PASS, and simpler than Feature
  006 — every change here is additive (new optional request fields, new
  always-present response fields, one new endpoint); no existing field is
  renamed or retyped, so no breaking-change migration path needs
  documenting (research.md #12). The `Labels` unique index and
  `WorkItemLabels` composite unique index are added via one code-first
  migration, committed with a descriptive name (`AddWorkItemStartDateAndLabels`),
  per this principle's migration-naming rule.
- **VI. Teach While Building**: Concepts you will learn in this feature —
  modeling a many-to-many relationship with an explicit join entity in EF
  Core (this codebase's first one — `WorkItemLabel`, not an implicit
  `UsingEntity<>()` table); using Angular Material's `MatDialog` for the first
  time in this app (`MAT_DIALOG_DATA` for passing data in, `MatDialogRef` for
  closing/passing a result out); a "find-or-create" upsert pattern for
  case-insensitively-unique rows (the label-normalization helper); extending
  an existing PUT endpoint's "replace the whole resource" semantics to a new
  collection field (`Labels` on Update replaces the full set, the same way
  `AssigneeUserId` already does for a single value).
- **VII. Incremental, Feature-by-Feature Delivery**: CONDITIONAL PASS — see
  Complexity Tracking. Touches more files than the ~15-file guideline once
  every existing create/edit entry point (board, project-detail list/tree,
  work-item detail, routing) is updated to open the modal instead of
  navigating — delivered as separate commits per user story (Setup/
  Foundational migration+modal-scaffold → US1 modal core replacing full-page
  forms → US2 assign-to-me → US3 start date → US4 create-another → US5
  labels), not one undifferentiated diff, the same approach already accepted
  for Feature 006.
- **VIII. Human in the Loop**: PASS — no auto-chaining; no
  `[NEEDS CLARIFICATION]` markers remain in the spec (open judgment calls —
  redirect target, label cleanup mechanics, "Create another" + labels
  interaction — were resolved into documented Assumptions/research.md
  decisions, not left ambiguous).

## Project Structure

### Documentation (this feature)

```text
specs/007-work-item-modal/
├── plan.md                        # This file (/speckit-plan command output)
├── research.md                    # Phase 0 output (/speckit-plan command)
├── data-model.md                  # Phase 1 output (/speckit-plan command)
├── quickstart.md                  # Phase 1 output (/speckit-plan command)
├── contracts/
│   └── work-item-modal-api.md     # Phase 1 output (/speckit-plan command)
├── checklists/
│   └── requirements.md
├── visual-reference-01.png … -08.png   # Jira "New task" modal reference screenshots
└── tasks.md                       # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
backend/
├── TaskFlow.Api/
│   ├── Data/
│   │   ├── Entities/
│   │   │   ├── WorkItem.cs                    # MODIFIED — StartDate (DateTime?), Labels
│   │   │   │                                    (ICollection<WorkItemLabel>) added
│   │   │   ├── Label.cs                       # NEW — Id, ProjectId, Name, CreatedAt
│   │   │   └── WorkItemLabel.cs               # NEW — Id, WorkItemId, LabelId (join entity)
│   │   ├── AppDbContext.cs                    # MODIFIED — Labels/WorkItemLabels DbSets,
│   │   │                                        FK config, unique indexes (data-model.md)
│   │   └── Migrations/
│   │       └── <ts>_AddWorkItemStartDateAndLabels.cs  # NEW — additive schema only, no backfill
│   ├── Dtos/
│   │   ├── WorkItemRequest.cs                 # MODIFIED — + StartDate, + Labels
│   │   ├── WorkItemDto.cs                     # MODIFIED — + StartDate, + Labels
│   │   ├── WorkItemDetailDto.cs               # MODIFIED — + StartDate, + Labels
│   │   ├── WorkItemBoardCardDto.cs            # MODIFIED — + Labels (no StartDate)
│   │   └── WorkItemTreeNodeDto.cs             # MODIFIED — + Labels
│   ├── Services/
│   │   ├── WorkItemService.cs                 # MODIFIED — start≤due check in Create/Update;
│   │   │                                        NormalizeAndAttachLabelsAsync helper;
│   │   │                                        GetProjectLabelsAsync; label filter in
│   │   │                                        GetWorkItemsAsync
│   │   └── WorkItemExceptions.cs              # MODIFIED — + InvalidDateRangeException,
│   │                                            InvalidLabelException, TooManyLabelsException
│   └── Controllers/
│       └── WorkItemsController.cs             # MODIFIED — new GET .../labels action; new
│                                                catch blocks in Create/Update; new label
│                                                query param in GetWorkItems
└── TaskFlow.Api.Tests/
    ├── Services/WorkItemServiceTests.cs        # MODIFIED — date-range + label-normalization cases
    └── Integration/WorkItemsEndpointsTests.cs  # MODIFIED — new 400 cases, GET .../labels,
                                                   label filter param

frontend/
├── src/
│   └── app/
│       ├── projects/
│       │   ├── work-items.service.ts           # MODIFIED — startDate/labels fields,
│       │   │                                      WorkItemsFilter.label, getProjectLabels()
│       │   ├── work-item-modal/                # NEW — replaces work-item-form/
│       │   │   ├── work-item-modal.component.ts
│       │   │   ├── work-item-modal.component.html
│       │   │   ├── work-item-modal.component.css
│       │   │   └── work-item-modal.component.spec.ts   # dirty-flag, create-another retention,
│       │   │                                              label normalization (pure logic, test-first)
│       │   ├── work-item-form/                 # REMOVED — component and route deleted
│       │   ├── board/
│       │   │   ├── board.component.ts/.html    # MODIFIED — "+" opens modal via MatDialog,
│       │   │   │                                 not routerLink; onSaved refreshes board
│       │   │   └── board-card.component.html    # MODIFIED — label chips
│       │   ├── project-detail/
│       │   │   └── project-detail.component.ts/.html  # MODIFIED — "New work item"/empty-state
│       │   │                                            actions + list-row "Edit" open modal;
│       │   │                                            label filter dropdown; label chips on
│       │   │                                            list rows and tree rows
│       │   └── work-item-detail/
│       │       └── work-item-detail.component.ts/.html # MODIFIED — "Edit"/"Add child" open
│       │                                                 modal; Start date shown; label chips
│       ├── app.routes.ts                       # MODIFIED — old create/edit routes replaced
│       │                                          with redirectTo (research.md #10)
│       └── shared/
│           └── label-chip/
│               ├── label-chip.component.ts      # NEW — single neutral `.chip--label` class,
│               │                                  no ColorKey-keyed switch (research.md #6)
│               └── label-chip.component.html
```

**Structure Decision**: Existing two-project layout (`backend/TaskFlow.Api` +
`frontend/`), unchanged. Labels are folded into the existing
`WorkItemService`/`WorkItemsController`/`work-items.service.ts` rather than
given their own service/controller pair (research.md #11) — unlike Feature
006's `WorkflowStatus`, `Label` has no independent lifecycle to justify one.
The new `WorkItemModalComponent` replaces `WorkItemFormComponent` in place
(same `projects/` grouping, sibling of `board/`, `project-detail/`, etc.);
`work-item-form/` is deleted outright rather than kept alongside the modal,
since the spec requires the full-page routes to stop existing, not merely
become unreachable dead code.

## Complexity Tracking

> Fill ONLY if Constitution Check has violations that must be justified

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|---------------------------------------|
| Changed/new file count exceeds the ~15-file guideline in Principle VII, spanning a new migration/entities, DTO changes across 5 files, and edits to every existing work-item creation/edit entry point on the frontend (board, project-detail list/tree, work-item detail, routing) | The feature's entire premise is replacing *every* full-page create/edit entry point with a modal — by definition, board's "+", detail's "Add child", project-detail's "New work item"/empty-state actions, and both "Edit" affordances (list row, detail) all currently navigate to the routes being removed, so each one is a required touchpoint, not optional scope-creep. Splitting "introduce the modal component" from "wire up all its entry points" across two feature branches was considered, but a modal nothing opens delivers zero user-visible value and cannot be demonstrated or tested end-to-end — they are one coherent unit of work, delivered here as separate commits per user story (Setup/Foundational → US1 modal core → US2 assign-to-me → US3 start date → US4 create-another → US5 labels), matching the same approach already accepted for Feature 006, not one undifferentiated diff. |
