# Tasks: Sprints & Backlog

**Input**: Design documents from `D:\Projects\taskflow-pms\specs\008-sprints-backlog\`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/sprints-api.md, quickstart.md

**Tests**: Included and REQUIRED — Constitution Principle I (Test-First Development, NON-NEGOTIABLE) mandates tests before implementation for both backend (xUnit + real SQL Server test databases) and frontend (Vitest), and spec.md's Non-functional requirements explicitly require "Pure logic (days-remaining/overdue calculation, complete-sprint item-resolution, start/complete eligibility)" to be test-first.

**Organization**: Tasks are grouped by user story (spec.md priorities P1–P3) so each story is independently implementable, testable, and demoable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (no code dependency on an incomplete task — same-file test-case additions are still marked [P] when independent of each other, matching this repo's established convention)
- **[Story]**: US1–US6, matching spec.md
- All paths are relative to `D:\Projects\taskflow-pms\`

## Path Conventions

Web application: `backend/TaskFlow.Api/` (+ `backend/TaskFlow.Api.Tests/`) and
`frontend/src/app/`.

---

## Phase 1: Setup

- [X] T001 Confirm the backend suite passes before starting (`cd backend/TaskFlow.Api.Tests && dotnet test`) — clean baseline for regression comparison (309/309 as of Feature 007) — confirmed 309/309
- [X] T002 [P] Confirm the frontend suite passes before starting (`cd frontend && npm test`) — clean baseline for regression comparison (209/209 as of Feature 007) — confirmed 208/209; one pre-existing flaky test (`work-item-modal.component.spec.ts`'s Status-pre-selection test, a `whenStable()` timeout) fails consistently before any Feature 008 changes — not a regression, tracked separately from this feature's work

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The `Sprint` entity and the `WorkItems.SprintId` column
(data-model.md), delivered as one migration. Unlike Feature 007, every user
story in this feature reads or writes this schema — even US1's "create a
sprint" needs the `Sprints` table to exist — so this phase blocks all of
them.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T003 Create the `Sprint` entity (`Id`, `ProjectId`, `Project` nav, `Name`, `StartDate`, `EndDate`, `Status` as a new `SprintStatus` enum `{ Planned, Active, Completed }`, `WorkItems` nav) in `backend/TaskFlow.Api/Data/Entities/Sprint.cs`
- [X] T004 [P] Add `SprintId` (`int?`) and a `Sprint?` navigation property to `WorkItem`, in `backend/TaskFlow.Api/Data/Entities/WorkItem.cs`
- [X] T005 [P] Add a `Sprints` (`ICollection<Sprint>`) navigation property to `Project`, mirroring its existing `WorkflowStatuses` collection, in `backend/TaskFlow.Api/Data/Entities/Project.cs`
- [X] T006 Configure `AppDbContext`: `Sprints` `DbSet`; unique index `(ProjectId, Name)` on `Sprint`; `Status` stored via `HasConversion<string>()`; `Project → Sprint` `Cascade`; `WorkItem.SprintId` index and `Sprint → WorkItem` **`Restrict`** (not `Cascade` — avoids the same "multiple cascade paths" conflict already documented for `WorkflowStatus → WorkItem`, data-model.md) — in `backend/TaskFlow.Api/Data/AppDbContext.cs` — depends on T003, T004, T005
- [X] T007 Generate the EF Core migration `AddSprints` (additive schema only — new table, new nullable column, no data backfill) in `backend/TaskFlow.Api/Data/Migrations/` — depends on T006 — applied to dev DB, no cascade-path errors

**Checkpoint**: Schema exists; nothing user-visible changes yet.

---

## Phase 3: User Story 1 - Create a sprint (Priority: P1) 🎯 MVP

**Goal**: A Manager/Admin can create a sprint for a project with a name (2–50
chars, unique per project) and a start/end date (`end > start`); it appears
as `Planned` with 0 items. Server-enforced role check regardless of client.

**Independent Test**: As a Manager/Admin, open a project's new Backlog tab,
use "Create sprint" to create "Sprint 1" with a date range, and confirm it
appears in the sprint list as `Planned`. Repeat the duplicate-name and
invalid-date-range cases and confirm each is rejected with a clear error.

### Tests for User Story 1

- [X] T008 [P] [US1] Backend test: `SprintService.CreateAsync` — name length 2–50 (`InvalidSprintNameException`), case-insensitive per-project uniqueness (`DuplicateSprintNameException`), `EndDate > StartDate` (`InvalidSprintDateRangeException`), successful create returns `Status: Planned`, `ItemCount: 0` — new `backend/TaskFlow.Api.Tests/Services/SprintServiceTests.cs`
- [X] T009 [P] [US1] Backend test: `POST api/projects/{projectId}/sprints` returns `201` with the expected `SprintDto` shape on success; `400` for invalid name/date-range; `409` for a duplicate name; `404` for an unknown project — new `backend/TaskFlow.Api.Tests/Integration/SprintsEndpointsTests.cs`
- [X] T010 [P] [US1] Backend test: `POST .../sprints` and `GET .../sprints` — allowed path (Manager and Admin can create; any authenticated role can read) and denied path (a Developer's create attempt is rejected `403`) per constitution's "every protected endpoint... allowed and denied path" — same file
- [X] T011 [P] [US1] Frontend test: `SprintFormComponent` blocks submit when the name is empty/too short/too long or the end date isn't after the start date, and surfaces a server-returned duplicate-name error inline without closing — new `frontend/src/app/projects/sprint-form/sprint-form.component.spec.ts`
- [X] T011a [P] [US1] Frontend test (added during implementation — not in the original plan, per the user's strict-test-first instruction): `BacklogComponent`'s minimal US1 render/gating logic (renders sprints from `getSprints()`, empty state, Manager/Admin-only "Create sprint", refresh after save) — new `frontend/src/app/projects/backlog/backlog.component.spec.ts`

### Implementation for User Story 1

- [X] T012 [US1] Add `SprintDto` (`Id, ProjectId, Name, StartDate, EndDate, Status, ItemCount`) and `CreateSprintRequest` (`Name`, `StartDate`, `EndDate`, data-annotated per contracts/sprints-api.md) — `backend/TaskFlow.Api/Dtos/SprintDto.cs`, `backend/TaskFlow.Api/Dtos/CreateSprintRequest.cs`
- [X] T013 [US1] Create `InvalidSprintNameException`, `DuplicateSprintNameException`, `InvalidSprintDateRangeException` — new `backend/TaskFlow.Api/Services/SprintExceptions.cs`
- [X] T014 [US1] Implement `SprintService.CreateAsync` (validate name/uniqueness/date-range, default `Status = Planned`) and `GetSprintsAsync(projectId)` (ordered by `StartDate` ascending, with `ItemCount` per sprint) — new `backend/TaskFlow.Api/Services/SprintService.cs` — depends on T012, T013; makes T008 pass
- [X] T015 [US1] Create `SprintsController` (`api/projects/{projectId}/sprints`) — class-level `[Authorize]`; `GET` (any authenticated user); `POST` `[Authorize(Roles = "Manager,Admin")]` catching the three new exceptions per contracts/sprints-api.md's status codes — new `backend/TaskFlow.Api/Controllers/SprintsController.cs` — depends on T014; makes T009, T010 pass
- [X] T016 [US1] Register `SprintService` as scoped in DI — `backend/TaskFlow.Api/Program.cs` — depends on T014
- [X] T017 [P] [US1] Create `SprintsService` (mirrors `project-status.service.ts`): `Sprint`/`CreateSprintRequest` interfaces, `getSprints(projectId)`, `createSprint(projectId, request)` — new `frontend/src/app/projects/sprints.service.ts`
- [X] T018 [US1] Create `SprintFormComponent` (`MatDialog`, mirrors `WorkItemModalComponent`'s dialog mechanism): name/start-date/end-date fields, client-side validation, calls `SprintsService.createSprint`, closes with the created sprint on success, shows the server error inline on failure — new `frontend/src/app/projects/sprint-form/sprint-form.component.ts` (+ `.html`) — depends on T017; makes T011 pass
- [X] T019 [US1] Create `BacklogComponent` (minimal, read-only-first — same pattern `WorkflowComponent` used in Feature 006: "created here read-only and extended in place" by later stories): renders the project's sprints (name, date range, status, item count) from `getSprints()`; a "Create sprint" button (Manager/Admin only) opens `SprintFormComponent` and refreshes the list on save. Wire it in as a fourth view-mode tab (`'backlog'`) alongside Board/List/Tree — new `frontend/src/app/projects/backlog/backlog.component.ts` (+ `.html` + `.css`); `frontend/src/app/projects/project-detail/project-detail.component.ts` (+ `.html`) — depends on T017, T018; makes T011a pass. Also updated `project-detail.component.spec.ts`'s hardcoded tab-order assertion to include "Backlog".

**Checkpoint**: Creating a sprint works end-to-end through a real (if
minimal) screen — quickstart.md section 1 passes.

---

## Phase 4: User Story 2 - View the Backlog (Priority: P1)

**Goal**: The Backlog tab shows each sprint's own items (soonest-start-first)
above an unscheduled Backlog section, both filterable the same way as List
view; Epics appear for context in the Backlog section; a per-section
"+ Create" adds a new item pre-assigned to that section.

**Independent Test**: With a Planned sprint containing two items and three
unscheduled items (one an Epic), open the Backlog tab and confirm the sprint
section (name/dates/count) appears above a "Backlog" section listing the
three unscheduled items including the Epic; apply a filter and confirm both
sections filter consistently.

### Tests for User Story 2

- [X] T020 [P] [US2] Backend test: `WorkItemService.GetBacklogAsync` groups items into one section per sprint (ordered by `StartDate`) plus one Backlog section (`SprintId == null`, includes `Epic`-type items); the same `statusId`/`type`/`priority`/`assigneeUserId`/`search`/`label` filters apply identically to every section — `backend/TaskFlow.Api.Tests/Services/WorkItemServiceTests.cs`
- [X] T021 [P] [US2] Backend test: `CreateAsync`/`UpdateAsync` accept an optional `SprintId` — rejects one belonging to another project (`SprintNotFoundException`), rejects a non-null value on an `Epic`-type item (`EpicCannotBeInSprintException`), rejects setting to/clearing from a `Completed` sprint seeded directly in the test database (`SprintReadOnlyException`) — same file
- [X] T022 [P] [US2] Backend test: `GET api/projects/{projectId}/backlog` returns the expected `sprints[]`/`backlogItems[]` shape, honors the same query parameters as `GET .../work-items`, and returns `404` for an unknown project — `backend/TaskFlow.Api.Tests/Integration/WorkItemsEndpointsTests.cs`
- [X] T023 [P] [US2] Frontend test: `BacklogComponent` renders each sprint's items and the Backlog section (including an Epic, shown but not offered as a drag target) from `getBacklog()`, and re-fetches with the right query params when a filter changes — `frontend/src/app/projects/backlog/backlog.component.spec.ts`
- [X] T023a [P] [US2] Frontend test (added during implementation, closing `/speckit-analyze` finding C1 — FR-025 had zero task coverage): a sprint section with zero items shows an empty-state hint; a non-empty one does not — same file
- [X] T023b [P] [US2] Frontend test (added during implementation): `BacklogItemRowComponent` renders title/type/status chip/due date/assignee inline (FR-026) and links to the item's detail route — new `frontend/src/app/projects/backlog/backlog-item-row.component.spec.ts`

### Implementation for User Story 2

- [X] T024 [US2] Add `SprintId` (`int?`) to `WorkItemRequest` — `backend/TaskFlow.Api/Dtos/WorkItemRequest.cs`
- [X] T025 [P] [US2] Add `SprintId`/`SprintName` to `WorkItemDto` and `WorkItemDetailDto` — `backend/TaskFlow.Api/Dtos/WorkItemDto.cs`, `backend/TaskFlow.Api/Dtos/WorkItemDetailDto.cs`
- [X] T026 [P] [US2] Add `SprintNotFoundException` — `backend/TaskFlow.Api/Services/SprintExceptions.cs`
- [X] T027 [P] [US2] Add `EpicCannotBeInSprintException` and `SprintReadOnlyException` — `backend/TaskFlow.Api/Services/WorkItemExceptions.cs`
- [X] T028 [US2] Add a shared private `ResolveSprintIdAsync(projectId, type, requestedSprintId, currentSprintId)` helper (belongs-to-project check, `Epic` rejection, `Completed`-sprint-on-either-side rejection per data-model.md's validation table) and call it from `CreateAsync`/`UpdateAsync`; include `SprintId`/`SprintName` in the `ToDtoAsync`/`ToDetailDtoAsync`/`GetWorkItemsAsync` projections — `backend/TaskFlow.Api/Services/WorkItemService.cs` — depends on T024, T025, T026, T027; makes T021 pass
- [X] T029 [US2] Extract the five-filter predicate (`statusId`/`type`/`priority`/`assigneeUserId`/`search`/`label`) out of `GetWorkItemsAsync` into a shared private `BuildFilteredQuery` helper (research.md #5); add `GetBacklogAsync(projectId, ...filters)` using it plus a `Sprints` query (ordered by `StartDate`), grouping rows in memory by `SprintId` the same way `GetBoardAsync`/`GetTreeAsync` already group by `StatusId`/`ParentWorkItemId` — same file — depends on T028; makes T020 pass
- [X] T030 [US2] Add `WorkItemBacklogDto` (`Sprints: List<BacklogSprintSectionDto>`, `BacklogItems: List<WorkItemDto>`) and `BacklogSprintSectionDto` (`Id, Name, StartDate, EndDate, Status, Items`) — new `backend/TaskFlow.Api/Dtos/WorkItemBacklogDto.cs`
- [X] T031 [US2] Add the `GET api/projects/{projectId}/backlog` action; catch `SprintNotFoundException`/`EpicCannotBeInSprintException`/`SprintReadOnlyException` in `Create`/`Update` — `backend/TaskFlow.Api/Controllers/WorkItemsController.cs` — depends on T029, T030; makes T022 pass
- [X] T032 [P] [US2] Add `sprintId`/`sprintName` to the frontend `WorkItem`/`WorkItemRequest` interfaces, new `BacklogSprintSection`/`WorkItemBacklog` interfaces, and `getBacklog(projectId, filter)` — `frontend/src/app/projects/work-items.service.ts`
- [X] T033 [US2] Create `BacklogItemRowComponent` — title, type, status chip, due date (`FriendlyDatePipe`), assignee avatar shown inline (FR-026) — new `frontend/src/app/projects/backlog/backlog-item-row.component.ts` (+ `.html` + `.css`) — depends on T032; makes T023b pass
- [X] T034 [US2] Extend `BacklogComponent` to fetch `getBacklog()`: render each sprint section's items and the Backlog section (Epics included, not yet draggable — drag lands in US3) via `BacklogItemRowComponent`; an empty-state hint on a zero-item sprint section (FR-025); a filter row (status/type/priority/assignee/search) mirroring `ProjectDetailComponent`'s flat-view filters, applied via `getBacklog`'s query params — `backlog.component.ts` (+ `.html` + `.css`) — depends on T032, T033; makes T023, T023a pass
- [X] T035 [US2] Add a per-section "+ Create" action: extend `WorkItemModalComponent`'s dialog-data interface with an optional `sprintId` (create mode only — an invisible pass-through included in the create request, no new form control, since the modal itself gains no Sprint field per spec scope); wire each Backlog section's "+ Create" to open the modal with that section's `sprintId` (`undefined` for the Backlog section) — `frontend/src/app/projects/work-item-modal/work-item-modal.component.ts`, `frontend/src/app/projects/backlog/backlog.component.ts` (+ `.html`) — depends on T034

**Checkpoint**: The Backlog view is fully readable and filterable, and
quick-create works — quickstart.md section 2 passes.

---

## Phase 5: User Story 3 - Move items between Backlog and sprints (Priority: P2)

**Goal**: Users with edit rights can drag an item between the Backlog
section and a Planned/Active sprint section (or between two such sections),
with the Board's existing optimistic-update-with-revert behavior; Epics and
Completed-sprint sections are never valid drag targets.

**Independent Test**: With one Planned sprint and one unscheduled Story, drag
the Story into the sprint section and confirm the assignment persists on
reload; drag it back out and confirm it returns to the Backlog section.

### Tests for User Story 3

- [X] T036 [P] [US3] Backend test: `WorkItemService.UpdateSprintAsync` — reuses `EnsureCanEdit` (rejects a caller who is neither creator/assignee/Manager/Admin), rejects an `Epic`, rejects a target/source `Completed` sprint, rejects a cross-project sprint id, successfully sets and clears `SprintId` — `backend/TaskFlow.Api.Tests/Services/WorkItemServiceTests.cs`
- [X] T037 [P] [US3] Backend test: `PATCH api/work-items/{id}/sprint` — `200` on success, `403` denied path for a caller without edit rights, `400`/`404` for the Epic/read-only/not-found cases — `backend/TaskFlow.Api.Tests/Integration/WorkItemsEndpointsTests.cs`
- [X] T038 [P] [US3] Frontend test: `BacklogComponent.onDrop` moves an item optimistically between sections on a successful `PATCH`, and reverts the item to its original section with an error toast when the `PATCH` fails (mirrors `board.component.spec.ts`'s existing drag-revert test) — `frontend/src/app/projects/backlog/backlog.component.spec.ts`
- [X] T039 [P] [US3] Frontend test: `BacklogComponent.canDrag` returns `false` for an `Epic`, for a caller without edit rights on the item, and for any item inside a `Completed`-status section — same file

### Implementation for User Story 3

- [X] T040 [US3] Add `UpdateWorkItemSprintRequest` (`SprintId: int?`) — new `backend/TaskFlow.Api/Dtos/UpdateWorkItemSprintRequest.cs`
- [X] T041 [US3] Implement `WorkItemService.UpdateSprintAsync(callerId, callerRole, id, sprintId)` — `EnsureCanEdit` then `ResolveSprintIdAsync` (T028), mirroring `UpdateStatusAsync`'s shape exactly — `backend/TaskFlow.Api/Services/WorkItemService.cs` — depends on T040; makes T036 pass
- [X] T042 [US3] Add the `PATCH api/work-items/{id}/sprint` action, catching `WorkItemNotFoundException`/`NotAuthorizedToEditWorkItemException`/`SprintNotFoundException`/`EpicCannotBeInSprintException`/`SprintReadOnlyException` — `backend/TaskFlow.Api/Controllers/WorkItemsController.cs` — depends on T041; makes T037 pass
- [X] T043 [P] [US3] Add `updateWorkItemSprint(id, sprintId)` — `frontend/src/app/projects/work-items.service.ts`
- [X] T044 [US3] Add `cdkDropListGroup` wrapping a `cdkDropList` per section (each sprint + the Backlog section) to `BacklogComponent`; implement `canDrag` (reuses `canEditWorkItem`, plus `Epic`-type and `Completed`-section checks) and `onDrop` (optimistic move + revert-on-failure, mirroring `BoardComponent.onDrop`) — `backlog.component.ts` (+ `.html`) — depends on T043; makes T038, T039 pass

**Checkpoint**: Drag-and-drop planning works end-to-end —
quickstart.md section 3 passes.

---

## Phase 6: User Story 4 - Sprint lifecycle: start and complete (Priority: P2)

**Goal**: A Manager/Admin can start a Planned sprint (requires ≥1 item, no
other Active sprint) and complete an Active one (resolving not-Done items to
the backlog or another sprint when any exist); Completed sprints keep their
Done items and become read-only; empty never-started Planned sprints can be
deleted.

**Independent Test**: Start a Planned sprint with one item (becomes Active);
complete it choosing "move to backlog" for its remaining not-Done item
(becomes Completed, item unassigned); attempt to start a second sprint while
one is Active and confirm it's rejected naming the active one; delete an
empty, never-started sprint.

### Tests for User Story 4

- [X] T045 [P] [US4] Backend test: `SprintService.StartAsync` — success (`Planned → Active`), `EmptySprintException` (0 items), `SprintNotPlannedException` (already `Active`/`Completed`), `AnotherSprintActiveException` (message names the currently active sprint) — `backend/TaskFlow.Api.Tests/Services/SprintServiceTests.cs`
- [X] T045a [P] [US4] Backend test (added during implementation, closing `/speckit-analyze` finding C2): two independent `DbContext`s racing `StartAsync` on different sprints in the same project — exactly one succeeds; a direct dual-insert of two `Active` sprints proves the DB's filtered unique index itself rejects the second row, independent of any application-level timing — same file
- [X] T046 [P] [US4] Backend test: `SprintService.CompleteAsync` — succeeds with no resolution when zero not-Done items exist; `DestinationRequiredException` (carrying the not-Done count) when not-Done items exist and no resolution is given; `"Backlog"` resolution clears `SprintId` only on not-Done items; `"Sprint"` resolution reassigns them to a valid destination; `InvalidDestinationSprintException` for a missing/self/other-project/wrong-status/unrecognized-resolution destination; Done items keep their `SprintId`; `SprintNotActiveException` when not currently `Active` — same file
- [X] T047 [P] [US4] Backend test: `SprintService.DeleteAsync` — succeeds only for `Planned` + 0 items; `SprintNotDeletableException` otherwise (non-`Planned`, or has items) — same file
- [X] T048 [P] [US4] Backend test: `PUT .../start`, `PUT .../complete`, `DELETE .../{sprintId}` — success status codes, allowed vs. denied role path, and the `DestinationRequiredException` response's `itemCount` extension — `backend/TaskFlow.Api.Tests/Integration/SprintsEndpointsTests.cs`
- [X] T049 [P] [US4] Frontend test: `CompleteSprintDialogComponent` computes its not-Done count from the sprint's already-loaded items (no extra network call), requires a resolution choice when that count is > 0, and submits immediately when it's 0 — new `frontend/src/app/projects/complete-sprint-dialog/complete-sprint-dialog.component.spec.ts`
- [X] T049a [P] [US4] Frontend test (added during implementation): `BacklogComponent` shows Start (disabled when empty)/Delete (empty Planned only)/Complete actions per sprint status, Manager/Admin gated, and `openCompleteSprintDialog` computes the right not-Done count and destination candidates — `backlog.component.spec.ts`

### Implementation for User Story 4

- [X] T050 [US4] Add `SprintNotPlannedException`, `EmptySprintException`, `AnotherSprintActiveException(string activeSprintName)`, `SprintNotActiveException`, `DestinationRequiredException(int itemCount)`, `InvalidDestinationSprintException`, `SprintNotDeletableException` — `backend/TaskFlow.Api/Services/SprintExceptions.cs`
- [X] T050a [US4] (Closes `/speckit-analyze` finding C2) Add a filtered unique index `IX_Sprints_ProjectId_ActiveOnly` (`ProjectId`, `WHERE [Status] = 'Active'`) to `Sprint`'s config in `AppDbContext`, new migration `AddSprintActiveUniqueIndex`; `SprintService.StartAsync` catches the resulting `DbUpdateException` (SQL error 2601/2627) and translates it to the same `AnotherSprintActiveException` the ordinary check-then-act path throws — the DB constraint, not the in-memory check, is what actually guarantees "at most one Active sprint" under real concurrency
- [X] T051 [US4] Add `CompleteSprintRequest` (`Resolution: string?`, `DestinationSprintId: int?`) — new `backend/TaskFlow.Api/Dtos/CompleteSprintRequest.cs`
- [X] T052 [US4] Implement `SprintService.StartAsync(projectId, sprintId)` — `backend/TaskFlow.Api/Services/SprintService.cs` — depends on T050, T050a; makes T045, T045a pass
- [X] T053 [US4] Implement `SprintService.CompleteAsync(projectId, sprintId, request)` — `backend/TaskFlow.Api/Services/SprintService.cs` — depends on T050, T051; makes T046 pass
- [X] T054 [US4] Implement `SprintService.DeleteAsync(projectId, sprintId)` — `backend/TaskFlow.Api/Services/SprintService.cs` — depends on T050; makes T047 pass
- [X] T055 [US4] Add `PUT .../start`, `PUT .../complete`, `DELETE .../{sprintId}` actions (`[Authorize(Roles = "Manager,Admin")]`), catching each new exception per contracts/sprints-api.md, including the `ProblemDetails.Extensions["itemCount"]` pattern for `DestinationRequiredException` — `backend/TaskFlow.Api/Controllers/SprintsController.cs` — depends on T052, T053, T054; makes T048 pass
- [X] T056 [P] [US4] Add `startSprint(projectId, sprintId)`, `completeSprint(projectId, sprintId, request)`, `deleteSprint(projectId, sprintId)` — `frontend/src/app/projects/sprints.service.ts` — depends on T017 (same file)
- [X] T057 [US4] Create `CompleteSprintDialogComponent` — resolution choice (Backlog vs. an existing Planned/Active sprint picker), not-Done count derived from the sprint's items already loaded by `BacklogComponent` (research.md #8) — new `frontend/src/app/projects/complete-sprint-dialog/complete-sprint-dialog.component.ts` (+ `.html` + `.css`) — depends on T056; makes T049 pass
- [X] T058 [US4] Add Start/Complete/Delete actions to each sprint section header in `BacklogComponent` (Manager/Admin gated), `confirm()`-based delete, wiring Complete to `CompleteSprintDialogComponent` — `backlog.component.ts` (+ `.html`) — depends on T057; makes T049a pass

**Checkpoint**: The full sprint lifecycle works end-to-end —
quickstart.md section 4 passes.

---

## Phase 7: User Story 5 - Sprint-scoped Board (Priority: P2)

**Goal**: The Board gains an "All items"/"Active sprint" toggle; "Active
sprint" shows only the project's current Active sprint's items with
identical columns/drag/permission behavior; a clear empty state (linking to
the Backlog) appears when no sprint is Active.

**Independent Test**: With an Active sprint containing 2 of a project's 5
items, toggle the Board to "Active sprint" and confirm only those 2 appear
with normal drag/edit behavior; toggle back and confirm all 5 reappear.

### Tests for User Story 5

- [X] T059 [P] [US5] Backend test: `WorkItemService.GetBoardAsync` with a `sprintId` filters rows to that sprint while columns stay the full project list; omitting it behaves exactly as before (regression) — `backend/TaskFlow.Api.Tests/Services/WorkItemServiceTests.cs`
- [X] T060 [P] [US5] Backend test: `GET api/projects/{projectId}/work-items/board?sprintId=` returns the filtered `WorkItemBoardDto` — `backend/TaskFlow.Api.Tests/Integration/WorkItemsEndpointsTests.cs`
- [X] T061 [P] [US5] Frontend test: `BoardComponent` in "Active sprint" mode calls `getBoard` with the resolved Active sprint's id and renders the empty state (with a Backlog link) when none is Active; "All items" mode is unaffected (regression) — `frontend/src/app/projects/board/board.component.spec.ts`. Also updated `project-detail.component.spec.ts`'s three `TestBed` configs to provide a `SprintsService` mock, now that `BoardComponent` injects it.

### Implementation for User Story 5

- [X] T062 [US5] Add an optional `sprintId` parameter to `WorkItemService.GetBoardAsync`, filtering rows when present — `backend/TaskFlow.Api/Services/WorkItemService.cs` — makes T059 pass
- [X] T063 [US5] Add `[FromQuery] int? sprintId` to the `GetBoard` action — `backend/TaskFlow.Api/Controllers/WorkItemsController.cs` — depends on T062; makes T060 pass
- [X] T064 [P] [US5] Add an optional `sprintId` parameter to `getBoard(projectId, sprintId?)` — `frontend/src/app/projects/work-items.service.ts`
- [X] T065 [US5] Add the "All items"/"Active sprint" toggle to `BoardComponent`: self-fetch the project's sprints via `SprintsService`, derive the Active one, call `getBoard(projectId, activeSprintId)` in "Active sprint" mode, and render `EmptyStateComponent` with a link to the Backlog tab when none is Active — `board.component.ts` (+ `.html`) — depends on T064; makes T061 pass

**Checkpoint**: The Board's sprint-scoped mode works and "All items" is
unchanged — quickstart.md section 5 passes.

---

## Phase 8: User Story 6 - Days remaining / overdue indicator (Priority: P3)

**Goal**: Both the Backlog and the sprint-scoped Board show how many days
remain in the Active sprint, or that it's overdue; no indicator for
Planned/Completed sprints.

**Independent Test**: With an Active sprint ending in 3 days, confirm "3
days remaining" on its Backlog section and on the sprint-scoped Board; with
one ending in the past, confirm an overdue indicator instead, in both
places.

### Tests for User Story 6

- [ ] T066 [P] [US6] Frontend test: `sprintDaysRemaining(endDate, status)` returns a days-remaining count for a future end date, an overdue flag for a past end date, and nothing for a `Planned`/`Completed` sprint — new `frontend/src/app/projects/backlog/sprint-days-remaining.spec.ts` (written first, per spec's explicit test-first requirement)

### Implementation for User Story 6

- [ ] T067 [US6] Implement `sprintDaysRemaining()` — a pure function mirroring `overdue.ts`'s date-only string comparison (no `new Date().getters()` on `endDate`, to avoid the UTC/local calendar-day shift research.md #3 documents) — new `frontend/src/app/projects/backlog/sprint-days-remaining.ts` — makes T066 pass
- [ ] T068 [US6] Show the days-remaining/overdue indicator on each Active sprint's section header — `backlog.component.ts` (+ `.html`) — depends on T067
- [ ] T069 [US6] Show the same indicator in `BoardComponent`'s "Active sprint" mode header — `board.component.ts` (+ `.html`) — depends on T067

**Checkpoint**: All six user stories are complete, matching spec.md's
Success Check — quickstart.md section 6 passes.

---

## Phase 9: Polish & Cross-Cutting Concerns

- [ ] T070 [P] Add a "Feature 008: Sprints & Backlog" entry to `README.md`'s "What I learned" log, matching Features 001–007's style, per the constitution's Definition of Done item 5
- [ ] T071 Run the full backend suite (`cd backend/TaskFlow.Api.Tests && dotnet test`) and confirm 100% pass
- [ ] T072 Run the full frontend suite (`cd frontend && npm test`) and confirm 100% pass
- [ ] T073 Walk through `quickstart.md` sections 1–6 and its regression check manually in a running app (backend + `ng serve`), on a project untouched by this feature as well as one using it

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — run first
- **Foundational (Phase 2)**: Depends on Setup — blocks every user story (unlike Feature 007, all six read/write the new schema)
- **US1 (Phase 3)**: Depends on Foundational only — the MVP slice
- **US2 (Phase 4)**: Depends on US1 (`BacklogComponent`/`SprintsService` must exist to extend)
- **US3 (Phase 5)**: Depends on US2 (drags between sections that US2 renders)
- **US4 (Phase 6)**: Depends on US2 (`BacklogComponent`'s sections/data); independent of US3 otherwise — could be built in parallel with US3 by a second contributor
- **US5 (Phase 7)**: *Code*-wise, depends on US1 (`SprintsService`) and Foundational only — no shared file/service with US2/US3/US4, so it can be *built* in parallel with any of them. Fixes `/speckit-analyze` finding I1: spec.md's US5 "Why this priority" correctly notes a *user-value* dependency on US4 ("depends on a sprint being startable") — the toggle has nothing meaningful to show without a real Active sprint. Backend tests can still exercise it by seeding an Active sprint directly (no need to go through the Start endpoint), but a manual/demo walkthrough of US5 needs either US4 done first or a seeded database.
- **US6 (Phase 8)**: Depends on US2 (Backlog section headers) and US5 (Board header) existing to attach the indicator to
- **Polish (Phase 9)**: Depends on all six user stories being complete

### Within Each User Story

- Tests written and confirmed failing before their implementation task (Constitution Principle I)
- Backend: entity/DTO changes before service changes; service changes before controller actions
- Frontend: service/type changes before the component fields that consume them
- `BacklogComponent` (introduced minimally in US1) is extended in place by US2, US3, US4, and US6 — never rebuilt per story, the same pattern `WorkflowComponent` used in Feature 006

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel
- Within Foundational: T004/T005 (different entity files) can run in parallel
- Within US1: T008–T011 (tests, independent files/cases) are parallel
- Within US2: T020–T023 (tests) are parallel; T025/T026/T027 (different DTO/exception files) are parallel
- Within US3: T036–T039 (tests) are parallel
- Within US4: T045–T049 (tests) are parallel
- Within US5: T059–T061 (tests) are parallel
- US5 has no dependency on US3/US4 and can be implemented in parallel with either by a second contributor, once US1 and Foundational are done

---

## Parallel Example: User Story 1

```bash
# Tests (independent files/cases):
Task: "SprintService.CreateAsync validation tests in backend/TaskFlow.Api.Tests/Services/SprintServiceTests.cs"
Task: "POST/GET .../sprints response-shape tests in backend/TaskFlow.Api.Tests/Integration/SprintsEndpointsTests.cs"
Task: "Role-gate allowed/denied path tests in the same integration test file"
Task: "SprintFormComponent validation test in frontend/src/app/projects/sprint-form/sprint-form.component.spec.ts"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks every story here)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: quickstart.md section 1 — a sprint can be created
   and appears in the (minimal) Backlog tab
5. Review with the maintainer before continuing (Constitution Principle VIII)

### Incremental Delivery

1. Setup + Foundational → schema ready
2. US1 → create a sprint, minimal Backlog tab → demo (MVP!)
3. US2 → full Backlog view: sections, filters, quick-create → demo
4. US3 → drag-and-drop planning → demo
5. US4 → start/complete/delete lifecycle → demo
6. US5 → sprint-scoped Board toggle → demo (a second contributor could build
   this in parallel once US1 is done — no shared files with US2/US3/US4 — but
   a real demo needs an Active sprint, i.e. US4 done or one seeded directly)
7. US6 → days-remaining/overdue indicator → demo (feature-complete, matches
   spec.md's Success Check)
8. Phase 9 → docs + full regression + quickstart walkthrough

### Commit Convention

One commit per user story, `feat: US<N> <summary>` with passing test count,
e.g. `feat: US1 create sprints (317/317 backend, 213/213 frontend tests
pass)`. Setup and Foundational commit as `chore:`; Polish as `docs:`/`chore:`
per file.

---

## Notes

- [P] tasks have no code dependency on an incomplete task; same-file test
  additions are still marked [P] when the test cases are independent of each
  other
- Tests must be written and observed failing before their implementation
  task (Constitution Principle I)
- Commit after each user story completes
- Stop at any checkpoint to validate that story independently before
  continuing
- US3/US4 both build directly on US2's `BacklogComponent`; US5 branches off
  US1/Foundational instead and can proceed on a separate track if staffed
  that way
