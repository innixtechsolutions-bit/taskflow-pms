# Tasks: Custom Workflow Columns

**Input**: Design documents from `D:\Projects\taskflow-pms\specs\006-custom-workflow-columns\`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/workflow-api.md, quickstart.md

**Tests**: Included and REQUIRED — Constitution Principle I (Test-First Development, NON-NEGOTIABLE) mandates tests before implementation for both backend (xUnit + real SQL Server test databases) and frontend (Vitest), and FR-023 explicitly requires the migration and new pure logic (category reasoning, delete-with-move, ordering, name uniqueness) to be test-first.

**Organization**: Tasks are grouped by user story (spec.md priorities P1–P3) so each story is independently implementable, testable, and demoable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1–US6, matching spec.md
- All paths are relative to `D:\Projects\taskflow-pms\`

## Path Conventions

Web application: `backend/TaskFlow.Api/` (+ `backend/TaskFlow.Api.Tests/`) and
`frontend/src/app/`.

---

## Phase 1: Setup

- [X] T001 Confirm the backend suite passes before starting (`cd backend/TaskFlow.Api.Tests && dotnet test`) — clean baseline for regression comparison
- [X] T002 [P] Confirm the frontend suite passes before starting (`cd frontend && npm test`) — clean baseline for regression comparison

---

## Phase 2: Foundational

**Not applicable as a separate phase for this feature.** Every existing
status touchpoint (board, chips, dropdowns, filters, tree "n/m done",
open-item counts) must move from a fixed enum to a per-project,
category-aware model before any management action is meaningful — that
migration-and-read-path work is itself User Story 1, which every other
story depends on sequentially (see Dependencies below).

---

## Phase 3: User Story 1 - Every project's workflow is its own, everywhere (Priority: P1) 🎯 MVP

**Goal**: `WorkflowStatus` exists as a per-project entity; every
pre-existing project is migrated to the standard four statuses with every
item's exact status preserved; every existing status-aware surface reads
from a project's own list and reasons about category, not name.

**Independent Test**: Before any management UI exists, confirm every
pre-existing project's board/dropdowns/chips/filters show the standard
four statuses with all items unchanged, and that two different projects
each show only their own statuses.

### Tests for User Story 1

- [X] T003 [P] [US1] Backend test: migration seeds the standard four `WorkflowStatus` rows per pre-existing project and backfills every `WorkItem.WorkflowStatusId` to match its original status exactly (no state change) — in `backend/TaskFlow.Api.Tests/Migrations/WorkflowStatusMigrationTests.cs` (new)
- [X] T004 [P] [US1] Backend test: `ProjectService.CreateAsync` seeds the standard four `WorkflowStatus` rows (position/category/colorKey per data-model.md) for a newly created project — in `backend/TaskFlow.Api.Tests/Services/ProjectServiceTests.cs`
- [X] T005 [P] [US1] Backend test: `ProjectService.GetProjectsAsync`'s open-item count is based on `WorkflowStatus.Category`, not name — an item in a renamed/custom Done-category status is excluded; one in an oddly-named Open-category status is included — in `backend/TaskFlow.Api.Tests/Services/ProjectServiceTests.cs`
- [X] T006 [P] [US1] Backend test: `WorkItemService` create/update accept `StatusId`, default to the project's first Open-category status (by position) when omitted, and reject a `StatusId` belonging to a different project — in `backend/TaskFlow.Api.Tests/Services/WorkItemServiceTests.cs`
- [X] T007 [P] [US1] Backend test: `WorkItemService.GetTreeAsync`'s "n/m done" count is based on `Category`, not a literal name match (a renamed/custom Done-category status still counts as done) — in `backend/TaskFlow.Api.Tests/Services/WorkItemServiceTests.cs`
- [X] T008 [P] [US1] Backend test: `WorkItemService.GetBoardAsync`'s columns come from the calling project's own ordered `WorkflowStatus` list (not a fixed enum), and two projects with different statuses return different column sets — in `backend/TaskFlow.Api.Tests/Services/WorkItemServiceTests.cs`
- [X] T009 [P] [US1] Backend test: `WorkItemService.GetWorkItemsAsync`'s status filter accepts a `StatusId` scoped to the project — in `backend/TaskFlow.Api.Tests/Services/WorkItemServiceTests.cs`
- [X] T010 [P] [US1] Backend test: `ProjectStatusService.GetStatusesAsync` returns a project's statuses in position order with accurate item counts; `GetDefaultCompletionStatusId` returns the lowest-position Done-category status and recomputes correctly if that status's position changes (FR-024) — new `backend/TaskFlow.Api.Tests/Services/ProjectStatusServiceTests.cs`
- [X] T011 [P] [US1] Backend test: `GET api/projects/{projectId}/statuses` returns 200 with the expected list shape for **any authenticated role, including Developer** (read access is intentionally not Manager/Admin-restricted — spec.md FR-008); 404 for an unknown project — new `backend/TaskFlow.Api.Tests/Integration/ProjectStatusesEndpointsTests.cs`
- [X] T012 [P] [US1] Frontend test: `StatusChipComponent` renders using `name`/`colorKey` inputs (no fixed `WorkItemStatus` union) — rewrite `frontend/src/app/shared/status-chip/status-chip.component.spec.ts`

### Implementation for User Story 1

- [X] T013 [US1] Add `WorkflowStatusCategory` (`Open`/`Done`) and `ChipColor` (10-member palette, research.md #3) enums, plus the `WorkflowStatus` entity (Id, ProjectId, Name, Position, Category, ColorKey, Project nav) in `backend/TaskFlow.Api/Data/Entities/WorkflowStatus.cs`
- [X] T014 [P] [US1] Add a `WorkflowStatuses` navigation collection to `Project` in `backend/TaskFlow.Api/Data/Entities/Project.cs` — depends on T013
- [X] T015 [P] [US1] Remove the `WorkItemStatus` enum and `Status` property; add `WorkflowStatusId` (FK) + `WorkflowStatus` navigation to `WorkItem` in `backend/TaskFlow.Api/Data/Entities/WorkItem.cs` — depends on T013
- [X] T016 [US1] Configure `AppDbContext`: `WorkflowStatuses` `DbSet`, composite unique index on `(ProjectId, Name)`, `Category`/`ColorKey` as `HasConversion<string>()`, `WorkItem.WorkflowStatusId` FK (`OnDelete(Restrict)`) + index, in `backend/TaskFlow.Api/Data/AppDbContext.cs` — depends on T013, T014, T015
- [X] T017 [US1] Generate and hand-verify the EF Core migration `AddPerProjectWorkflowStatuses`: create `WorkflowStatuses` table; add `WorkItems.WorkflowStatusId` as nullable; `migrationBuilder.Sql(...)` backfill (seed 4 rows per existing project per data-model.md's table, then set every `WorkItem.WorkflowStatusId` from its old `Status` value); alter the column to `NOT NULL`; drop the old `Status` column — in `backend/TaskFlow.Api/Data/Migrations/` — depends on T016; makes T003 pass
- [X] T018 [US1] `ProjectService.CreateAsync` seeds the standard four `WorkflowStatus` rows for a newly created project in the same `SaveChangesAsync()` batch, in `backend/TaskFlow.Api/Services/ProjectService.cs` — depends on T017; makes T004 pass
- [X] T019 [US1] `ProjectService.GetProjectsAsync`'s open-item count uses `WorkflowStatus.Category` via navigation instead of the removed enum, in `backend/TaskFlow.Api/Services/ProjectService.cs` — depends on T017; makes T005 pass
- [X] T020 [P] [US1] `WorkItemRequest.Status` (string) → `StatusId` (`int?`); `UpdateWorkItemStatusRequest.Status` → `StatusId` (`int`) in `backend/TaskFlow.Api/Dtos/WorkItemRequest.cs` and `backend/TaskFlow.Api/Dtos/UpdateWorkItemStatusRequest.cs`
- [X] T021 [P] [US1] Flatten `Status` (string) into `StatusId`/`StatusName`/`StatusCategory`/`StatusColorKey` on `WorkItemDto`, `WorkItemDetailDto`, `WorkItemChildDto`, `WorkItemTreeNodeDto`, `WorkItemBoardCardDto` in `backend/TaskFlow.Api/Dtos/`
- [X] T022 [P] [US1] `BoardColumnDto`: `{Status, Label}` → `{StatusId, Name, Category, ColorKey}` in `backend/TaskFlow.Api/Dtos/WorkItemBoardDto.cs`
- [X] T023 [US1] `WorkItemService.CreateAsync`/`UpdateAsync`: validate `StatusId` belongs to the target project, default to its first Open-category status by position when omitted, in `backend/TaskFlow.Api/Services/WorkItemService.cs` — depends on T020, T021, T022; makes T006 pass
- [X] T024 [US1] `WorkItemService.UpdateStatusAsync`: accept `StatusId` in `backend/TaskFlow.Api/Services/WorkItemService.cs` — depends on T020
- [X] T025 [US1] `WorkItemService.GetTreeAsync`'s done-count uses `Category`, in `backend/TaskFlow.Api/Services/WorkItemService.cs` — depends on T021; makes T007 pass
- [X] T026 [US1] `WorkItemService.GetBoardAsync`'s columns come from the project's own `WorkflowStatus` list ordered by `Position`, in `backend/TaskFlow.Api/Services/WorkItemService.cs` — depends on T021, T022; makes T008 pass
- [X] T027 [US1] `WorkItemService.GetWorkItemsAsync`'s status filter matches `StatusId` scoped to the project, in `backend/TaskFlow.Api/Services/WorkItemService.cs` — depends on T021; makes T009 pass
- [X] T028 [P] [US1] Create `WorkflowStatusDto` record (`{Id, Name, Category, ColorKey, Position, ItemCount}`) in `backend/TaskFlow.Api/Dtos/WorkflowStatusDto.cs`
- [X] T029 [US1] `ProjectStatusService.GetStatusesAsync(projectId)` (position order + item counts) and `GetDefaultCompletionStatusId(projectId)` (FR-024) in new `backend/TaskFlow.Api/Services/ProjectStatusService.cs` — depends on T028; makes T010 pass
- [X] T030 [US1] New `ProjectStatusesController` with the `GET` action only (any authenticated user — read-only) in `backend/TaskFlow.Api/Controllers/ProjectStatusesController.cs` — depends on T029; makes T011 pass
- [X] T031 [P] [US1] Remove the `WorkItemStatus` string-literal union; add a `ProjectStatus` interface and `getStatuses(projectId)`; flatten `statusId`/`statusName`/`statusCategory`/`statusColorKey` onto `WorkItem`/`WorkItemBoardCard`; change `WorkItemRequest`/`updateWorkItemStatus()` to `statusId`; rename `WorkItemsFilter.status` (string) → `statusId` (number) and its corresponding `params['status']` → `params['statusId']` construction in `getWorkItems()`, in `frontend/src/app/projects/work-items.service.ts`
- [X] T032 [P] [US1] Replace the fixed per-status-name `--color-status-*` tokens with a fixed `ChipColor` palette (`--color-chip-{slate,blue,violet,amber,teal,rose,indigo,cyan,green,emerald}-{bg,text}`) in `frontend/src/design-tokens.scss`
- [X] T033 [US1] `StatusChipComponent`: replace the `status: WorkItemStatus` input with `name: string` + `colorKey: string` inputs; exhaustive switch over the fixed `ChipColor` set, in `frontend/src/app/shared/status-chip/status-chip.component.ts` (+ `.html`) — depends on T031, T032; makes T012 pass
- [X] T034 [US1] Update every `<app-status-chip>` call site to pass `name`/`colorKey` instead of `status`, in `frontend/src/app/projects/project-detail/project-detail.component.html` and `frontend/src/app/projects/work-item-detail/work-item-detail.component.html` — depends on T033
- [X] T035 [US1] `WorkItemFormComponent`'s Status dropdown sourced from `getStatuses(projectId)`, binds `statusId`, in `frontend/src/app/projects/work-item-form/work-item-form.component.ts` (+ `.html`) — depends on T031
- [X] T036 [US1] `ProjectDetailComponent`'s status filter sourced from `getStatuses(projectId)`, sends `statusId` via T031's renamed `WorkItemsFilter.statusId` field, in `frontend/src/app/projects/project-detail/project-detail.component.ts` (+ `.html`) — depends on T031
- [X] T037 [US1] `BoardComponent` groups cards by `statusId` using the board response's own column list, in `frontend/src/app/projects/board/board.component.ts` (+ `.html`) — depends on T031
- [X] T038 [US1] Replace every `=== 'Done'` / status-name-based "is it done" check with `statusCategory === 'Done'`, in `frontend/src/app/projects/board/overdue.ts` and any other frontend file comparing against a literal status name (grep for `'Done'` under `frontend/src/app/projects/`)
- [X] T039 [US1] Update existing specs across every file touched above (`work-item-form`, `project-detail`, `board`, `board-card`, `work-item-detail`, `status-chip`) for the new `statusId`/`statusName`/`statusCategory`/`statusColorKey` fields — corresponding `*.spec.ts` files

**Checkpoint**: Every pre-existing project shows the standard four
statuses with all items unchanged; two projects display independently;
every "is it done" check uses category — quickstart.md sections 1, 2, 8
pass.

---

## Phase 4: User Story 2 - View a project's workflow (Priority: P1)

**Goal**: A Manager or Admin can open a project's Workflow management
screen and see its statuses in position order with name, category, and
item count; the entry point is hidden from other roles.

**Independent Test**: As a Manager/Admin, open the Workflow screen and
confirm the list matches reality; as a Developer, confirm no entry point
is shown.

### Tests for User Story 2

- [X] T040 [P] [US2] Frontend test: `WorkflowComponent` lists statuses in position order with name/category/item count, in new `frontend/src/app/projects/workflow/workflow.component.spec.ts`
- [X] T041 [P] [US2] Frontend test: the "Workflow" entry point is hidden for a Developer and shown for a Manager/Admin, extending `frontend/src/app/projects/project-detail/project-detail.component.spec.ts`

### Implementation for User Story 2

- [X] T042 [US2] Create `WorkflowComponent` (read-only list for now: name, category, item count, in position order) in `frontend/src/app/projects/workflow/workflow.component.ts` (+ `.html` + `.css`) — depends on T031; makes T040 pass
- [X] T043 [US2] Add the `/projects/:id/workflow` route and a "Workflow" link on the project-detail header, gated by the existing `canManageProject()` check (same as today's Edit/Delete project links), in `frontend/src/app/app.routes.ts` and `frontend/src/app/projects/project-detail/project-detail.component.html` — depends on T042; makes T041 pass
- [X] T044 [US2] Create `ProjectStatusService` (Angular service — add/rename/reorder/delete calls extended by later stories; only `getStatuses()`-adjacent wiring needed now) in `frontend/src/app/projects/project-status.service.ts`

**Checkpoint**: Workflow screen shows accurate, position-ordered data;
hidden from Developers — quickstart.md section 3 (read-only part) passes.

---

## Phase 5: User Story 3 - Add a workflow column (Priority: P1)

**Goal**: A Manager or Admin can add a column with a name and category,
positioned before the first Done-category column by default.

**Independent Test**: Add a column named "QA" (Open); confirm it appears
on the Workflow screen and board at the expected position, is selectable
everywhere for that project, and a second project is unaffected.

### Tests for User Story 3

- [X] T045 [P] [US3] Backend test: `ProjectStatusService.CreateAsync` — name 2–30 chars, case-insensitive per-project uniqueness, max 10 statuses, default position (immediately before the first Done-category status) vs. an explicit position, and automatic color assignment (Open cycles open hues, Done uses green family, research.md #3) — in `backend/TaskFlow.Api.Tests/Services/ProjectStatusServiceTests.cs`
- [X] T046 [P] [US3] Backend test: `POST api/projects/{projectId}/statuses` — 201 for Manager/Admin; 403 for any other role; 400 for an invalid name/category; 409 for a duplicate name or exceeding the 10-status max; 404 for an unknown project — in `backend/TaskFlow.Api.Tests/Integration/ProjectStatusesEndpointsTests.cs`
- [X] T047 [P] [US3] Frontend test: `WorkflowComponent`'s add-form submits name + category and appends the new status to the list, extending `frontend/src/app/projects/workflow/workflow.component.spec.ts`

### Implementation for User Story 3

- [X] T048 [US3] Create `CreateWorkflowStatusRequest` record with `[Required]`/`[StringLength(30, MinimumLength = 2)]` on `Name`, matching `WorkItemRequest.Title`'s existing data-annotation convention (`Category` is enum-like and parsed/validated in the service, like `WorkItemRequest.Type`/`Status`), in `backend/TaskFlow.Api/Dtos/CreateWorkflowStatusRequest.cs`
- [X] T049 [US3] `ProjectStatusService.CreateAsync` (validation, default-position-before-first-Done, automatic color assignment, resequencing) in `backend/TaskFlow.Api/Services/ProjectStatusService.cs` — depends on T048; makes T045 pass
- [X] T050 [US3] Add `DuplicateStatusNameException` and `MaxStatusCountExceededException` in `backend/TaskFlow.Api/Services/ProjectStatusExceptions.cs`
- [X] T051 [US3] Add the `POST` action (`[Authorize(Roles = "Manager,Admin")]`) to `backend/TaskFlow.Api/Controllers/ProjectStatusesController.cs` — depends on T049, T050; makes T046 pass
- [X] T052 [US3] Add the add-form UI to `WorkflowComponent`, calling `ProjectStatusService`, in `frontend/src/app/projects/workflow/workflow.component.ts` (+ `.html`) — depends on T044; makes T047 pass
- [X] T053 [US3] Verify each board column's "+ Add" affordance (Feature 005) still pre-selects the correct status by id for a column added after this feature shipped, in `frontend/src/app/projects/board/board.component.html` — adjust only if the id-based wiring from T037 needs it

**Checkpoint**: Adding a column works end-to-end and is refused for
non-Manager/Admin roles, completing US2's server-refusal scenario —
quickstart.md section 4 passes.

---

## Phase 6: User Story 4 - Rename a workflow column (Priority: P2)

**Goal**: A Manager or Admin can rename a column (and/or change its
color); the new name appears everywhere immediately, unchanged category/
color/items aside from an explicit recolor.

**Independent Test**: Rename "In Progress" to "Doing"; confirm the board,
chips, dropdowns, and filters for that project all reflect it
immediately, with color and contained items unchanged.

### Tests for User Story 4

- [X] T054 [P] [US4] Backend test: `ProjectStatusService.UpdateAsync` renames and/or recolors a status, rejects a case-insensitive duplicate name **and a new name outside 2–30 chars** (contracts/workflow-api.md's documented 400 case — same length rule as add), and leaves category/position/work items unchanged — in `backend/TaskFlow.Api.Tests/Services/ProjectStatusServiceTests.cs`
- [X] T055 [P] [US4] Backend test: `PUT api/projects/{projectId}/statuses/{statusId}` — 200/400/403/404/409 cases — in `backend/TaskFlow.Api.Tests/Integration/ProjectStatusesEndpointsTests.cs`
- [X] T056 [P] [US4] Frontend test: `WorkflowComponent`'s inline rename updates the list and surfaces a duplicate-name error, extending `frontend/src/app/projects/workflow/workflow.component.spec.ts`

### Implementation for User Story 4

- [X] T057 [US4] Create `UpdateWorkflowStatusRequest` record with `[StringLength(30, MinimumLength = 2)]` on the optional `Name` field (same convention as T048) in `backend/TaskFlow.Api/Dtos/UpdateWorkflowStatusRequest.cs`
- [X] T058 [US4] `ProjectStatusService.UpdateAsync` — re-validates name length and uniqueness on rename, same as `CreateAsync` — in `backend/TaskFlow.Api/Services/ProjectStatusService.cs` — depends on T057; makes T054 pass
- [X] T059 [US4] Add the `PUT .../statuses/{statusId}` action to `backend/TaskFlow.Api/Controllers/ProjectStatusesController.cs` — depends on T058; makes T055 pass
- [X] T060 [US4] Add inline rename (and recolor) UI to `WorkflowComponent`, in `frontend/src/app/projects/workflow/workflow.component.ts` (+ `.html`) — depends on T044; makes T056 pass

**Checkpoint**: Rename propagates everywhere immediately — quickstart.md
section 5 passes.

---

## Phase 7: User Story 5 - Reorder workflow columns (Priority: P2)

**Goal**: A Manager or Admin can drag columns into a new order; the board
and every dropdown/filter for that project follow, without affecting
other projects.

**Independent Test**: Drag columns into a new order on the Workflow
screen; confirm the board's column order and every dropdown's option
order match, and a second project's order is unaffected.

### Tests for User Story 5

- [ ] T061 [P] [US5] Backend test: `ProjectStatusService.ReorderAsync` resequences per a given id order and rejects a non-permutation (missing id, unknown id, duplicate) — in `backend/TaskFlow.Api.Tests/Services/ProjectStatusServiceTests.cs`
- [ ] T062 [P] [US5] Backend test: `PUT api/projects/{projectId}/statuses/reorder` — 200/400/403/404 cases (403 for a non-Manager/Admin caller, same rule as every other mutation) — in `backend/TaskFlow.Api.Tests/Integration/ProjectStatusesEndpointsTests.cs`
- [ ] T063 [P] [US5] Frontend test: `WorkflowComponent`'s drag reorder calls the reorder endpoint with the new id sequence, extending `frontend/src/app/projects/workflow/workflow.component.spec.ts`

### Implementation for User Story 5

- [ ] T064 [US5] Create `ReorderWorkflowStatusesRequest` record in `backend/TaskFlow.Api/Dtos/ReorderWorkflowStatusesRequest.cs`
- [ ] T065 [US5] `ProjectStatusService.ReorderAsync` in `backend/TaskFlow.Api/Services/ProjectStatusService.cs` — depends on T064; makes T061 pass
- [ ] T066 [US5] Add the `PUT .../statuses/reorder` action to `backend/TaskFlow.Api/Controllers/ProjectStatusesController.cs` — depends on T065; makes T062 pass
- [ ] T067 [US5] Wire `@angular/cdk/drag-drop`'s `cdkDropList`/`cdkDrag` into `WorkflowComponent` for column reordering (second consumer of this dependency, after Feature 005's board cards), in `frontend/src/app/projects/workflow/workflow.component.ts` (+ `.html`) — depends on T044; makes T063 pass

**Checkpoint**: Reordering propagates everywhere for that project only —
quickstart.md section 6 passes.

---

## Phase 8: User Story 6 - Delete a workflow column (Priority: P3)

**Goal**: A Manager or Admin can delete an empty column directly, or a
non-empty one by choosing a destination column, atomically; a project can
never be left with zero Open- or zero Done-category statuses.

**Independent Test**: Delete an empty column directly. Delete a
3-item column, confirm the destination prompt and its wording, confirm
after confirming that all 3 items show the destination status and the
column is gone. Attempt to delete the last Open (or Done) column and
confirm it's refused.

### Tests for User Story 6

- [ ] T068 [P] [US6] Backend test: `ProjectStatusService.DeleteAsync` — direct delete for an empty status; a non-empty status requires `destinationStatusId`; the move + delete is atomic (one `SaveChangesAsync`); rejects deleting the last Open-category or last Done-category status regardless of item count; rejects an invalid or self-referential destination — in `backend/TaskFlow.Api.Tests/Services/ProjectStatusServiceTests.cs`
- [ ] T069 [P] [US6] Backend test: `DELETE api/projects/{projectId}/statuses/{statusId}` — 204/400/403/404 cases, including the destination-required error body shape (item count included) — in `backend/TaskFlow.Api.Tests/Integration/ProjectStatusesEndpointsTests.cs`
- [ ] T070 [P] [US6] Frontend test: `WorkflowComponent`'s delete flow — an empty column deletes directly; a non-empty column prompts a destination picker with the exact "Move N items to 'X' and delete 'Y'?" wording; a last-Open/last-Done delete attempt surfaces the server's error — extending `frontend/src/app/projects/workflow/workflow.component.spec.ts`

### Implementation for User Story 6

- [ ] T071 [US6] Add `DestinationStatusRequiredException`, `InvalidDestinationStatusException`, `LastStatusInCategoryException` in `backend/TaskFlow.Api/Services/ProjectStatusExceptions.cs`
- [ ] T072 [US6] `ProjectStatusService.DeleteAsync` (item-count check, destination validation, atomic reassign-then-delete in one `SaveChangesAsync`, min-Open/min-Done guard per FR-003) in `backend/TaskFlow.Api/Services/ProjectStatusService.cs` — depends on T071; makes T068 pass
- [ ] T073 [US6] Add the `DELETE` action (with the optional `destinationStatusId` query param) to `backend/TaskFlow.Api/Controllers/ProjectStatusesController.cs` — depends on T072; makes T069 pass
- [ ] T074 [US6] Add the delete button and destination-picker confirmation dialog to `WorkflowComponent`, in `frontend/src/app/projects/workflow/workflow.component.ts` (+ `.html`) — depends on T044; makes T070 pass

**Checkpoint**: Delete-with-move works atomically and the min-category
guard holds — quickstart.md section 7 passes; all six user stories are
now complete, matching spec.md's Success Check.

---

## Phase 9: Polish & Cross-Cutting Concerns

- [ ] T075 [P] Add the `WorkflowStatus`/`ChipColor` model and token table to `frontend/src/app/shared/README.md`
- [ ] T076 Cross-cutting regression test confirming spec.md FR-008/SC-006's read/write authorization boundary end-to-end: as a Developer, `GET api/projects/{projectId}/statuses` succeeds (200); `POST`, `PUT .../{statusId}`, `PUT .../reorder`, and `DELETE .../{statusId}` are all refused (403) — in `backend/TaskFlow.Api.Tests/Integration/ProjectStatusesEndpointsTests.cs` — depends on T030, T051, T059, T066, T073 (all five actions must exist)
- [ ] T077 Run the full backend suite (`cd backend/TaskFlow.Api.Tests && dotnet test`) and confirm 100% pass — SC-008
- [ ] T078 Run the full frontend suite (`cd frontend && npm test`) and confirm 100% pass — SC-008
- [ ] T079 Cross-cutting checkpoint: grep the frontend and backend for any leftover reference to the removed `WorkItemStatus` enum or a hard-coded status-name comparison (`'ToDo'`, `'InReview'`, `== "Done"`, etc.) outside the migration's own backfill SQL — confirm none remain
- [ ] T080 Add a "Feature 006: Custom Workflow Columns" entry to `README.md`'s "What I learned" log, matching Features 001–005's style, per the constitution's Definition of Done item 5
- [ ] T081 Walk through `quickstart.md` sections 1–9 manually in a running app (backend + `ng serve`), including the direct-API-bypassing-the-UI permission checks (section 3.4) and the two-projects-independence check (section 2)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — run first
- **Foundational (Phase 2)**: N/A for this feature — see note above
- **US1 (Phase 3)**: Depends on Setup only — the true MVP slice (and the
  largest, since it's the migration + universal read-path switch)
- **US2 (Phase 4)**: Depends on US1 (needs `getStatuses()` and the
  `GET .../statuses` endpoint to exist)
- **US3 (Phase 5)**: Depends on US2 (`WorkflowComponent` must exist for
  the add-form to live in) — also completes US2's "server refuses a
  non-Manager/Admin mutation" scenario, since US2 alone has no mutating
  endpoint yet
- **US4 (Phase 6)**: Depends on US2 (`WorkflowComponent`) — independent
  of US3's specifics beyond both needing the component to exist
- **US5 (Phase 7)**: Depends on US2 (`WorkflowComponent`) — independent
  of US3/US4
- **US6 (Phase 8)**: Depends on US2 (`WorkflowComponent`) — independent
  of US3/US4/US5
- **Polish (Phase 9)**: Depends on all six user stories being complete

### Within Each User Story

- Tests written and confirmed failing before their implementation task
  (Constitution Principle I)
- Backend: entity/DTO changes before service changes; service changes
  before controller actions
- Frontend: service/type changes before the components that consume them
- `ProjectStatusService` (frontend, T044) exists once (US2) and is
  extended in place by US3–US6, rather than rebuilt per story

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel
- Within US1: T003–T012 (tests) are parallel; T020–T022 (DTO shape
  changes) are parallel; T031/T032 (frontend service/tokens) are
  parallel; T039's spec updates can proceed per-file in parallel once
  their corresponding implementation task lands
- Within US3/US4/US5/US6: each story's three test tasks are parallel;
  backend and frontend implementation tasks can proceed in parallel once
  their shared DTO/contract is agreed (contracts/workflow-api.md)
- US4, US5, and US6 can be implemented in parallel by different
  contributors once US2 is done — none depends on the others

---

## Parallel Example: User Story 1

```bash
# Tests (all independent files):
Task: "Migration snapshot test in backend/TaskFlow.Api.Tests/Migrations/WorkflowStatusMigrationTests.cs"
Task: "ProjectService seeding/open-count tests in backend/TaskFlow.Api.Tests/Services/ProjectServiceTests.cs"
Task: "WorkItemService StatusId/category tests in backend/TaskFlow.Api.Tests/Services/WorkItemServiceTests.cs"
Task: "ProjectStatusService list/default-completion test in backend/TaskFlow.Api.Tests/Services/ProjectStatusServiceTests.cs"
Task: "GET statuses endpoint test in backend/TaskFlow.Api.Tests/Integration/ProjectStatusesEndpointsTests.cs"
Task: "StatusChipComponent test in frontend/src/app/shared/status-chip/status-chip.component.spec.ts"

# DTO shape changes (independent files):
Task: "WorkItemRequest/UpdateWorkItemStatusRequest in backend/TaskFlow.Api/Dtos/"
Task: "Flattened status fields on work-item DTOs in backend/TaskFlow.Api/Dtos/"
Task: "BoardColumnDto in backend/TaskFlow.Api/Dtos/WorkItemBoardDto.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 3: User Story 1 (migration + universal read-path switch)
3. **STOP and VALIDATE**: quickstart.md sections 1, 2, 8 — migration
   preserved everything, projects are independent, category-based
   reasoning holds
4. Review with the maintainer before continuing (Constitution Principle
   VIII)

### Incremental Delivery

1. Setup → baseline confirmed
2. US1 → migration + universal read-path switch → demo (largest, riskiest
   slice; smallest possible MVP given this feature's premise)
3. US2 → Workflow screen renders real per-project data → demo
4. US3 → Add a column, works end-to-end → demo
5. US4 → Rename propagates everywhere → demo
6. US5 → Reorder propagates everywhere → demo
7. US6 → Delete-with-move, atomic, category-guarded → demo
   (feature-complete, matches spec.md's Success Check)
8. Phase 9 → docs + full regression + quickstart walkthrough

### Commit Convention

One commit per user story, `feat: US<N> <summary>` with passing test
count, e.g. `feat: US1 per-project workflow statuses + migration
(268/268 backend tests pass)`. Setup and Polish commit as
`chore:`/`docs:`.

---

## Notes

- [P] tasks touch different files and have no incomplete-task dependency
- This is the largest feature so far (plan.md's Complexity Tracking) —
  US1 alone is comparable in size to a full prior feature, because
  replacing a system-wide fixed enum with a per-project entity touches
  nearly every existing status-aware surface by definition
- Tests must be written and observed failing before their implementation
  task (Constitution Principle I)
- Commit after each user story completes
- Stop at any checkpoint to validate that story independently before
  continuing
- T053 is verification-only unless the id-based board wiring from T037
  needs an adjustment — don't add parallel logic if T037 already handles
  it correctly
