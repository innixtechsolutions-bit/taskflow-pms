# Tasks: Kanban Board

**Input**: Design documents from `D:\Projects\taskflow-pms\specs\005-kanban-board\`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/board-api.md, quickstart.md

**Tests**: Included and REQUIRED — Constitution Principle I (Test-First Development, NON-NEGOTIABLE) mandates tests before implementation for both backend (xUnit + real SQL Server test databases) and frontend (Vitest), and FR-024 explicitly requires board logic (column grouping, overdue detection, drag permission) to be test-first.

**Organization**: Tasks are grouped by user story (spec.md priorities P1–P2) so each story is independently implementable, testable, and demoable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1–US5, matching spec.md
- All paths are relative to `D:\Projects\taskflow-pms\`

## Path Conventions

Web application: `backend/TaskFlow.Api/` (+ `backend/TaskFlow.Api.Tests/`) and
`frontend/src/app/`. Unlike Feature 004, this feature touches both halves.

---

## Phase 1: Setup

- [ ] T001 Confirm the backend suite passes before starting (`cd backend/TaskFlow.Api.Tests && dotnet test`) — clean baseline for regression comparison
- [ ] T002 [P] Confirm the frontend suite passes before starting (`cd frontend && npm test`) — clean baseline for regression comparison

---

## Phase 2: Foundational

**Not applicable as a separate phase for this feature.** Unlike Feature
004 (where design tokens were genuine shared infrastructure every story
needed), this feature's closest thing to a blocking prerequisite —
**In Review existing as a real status** — is itself User Story 1, which
every other story depends on sequentially (see Dependencies below). There
is no additional cross-cutting infrastructure to build before US1 can
start.

---

## Phase 3: User Story 1 - In Review status integrated everywhere (Priority: P1) 🎯 MVP

**Goal**: In Review is a real, selectable status — in create/edit forms,
chips (with its own distinct color), filters, and open-item counts —
with zero remapping of existing items.

**Independent Test**: Set a work item to In Review via the edit form;
confirm its chip color is distinct everywhere it renders; filter the flat
list by In Review; confirm the project's open-item count still includes
it.

### Tests for User Story 1

- [ ] T003 [P] [US1] Backend test: creating/updating a work item with status `InReview` succeeds, in `backend/TaskFlow.Api.Tests/Services/WorkItemServiceTests.cs` (must fail to compile/RED until T007 adds the enum member)
- [ ] T004 [P] [US1] Backend test: a project's open-work-item count includes `InReview` items, in `backend/TaskFlow.Api.Tests/Services/ProjectServiceTests.cs` (create this file if it doesn't already exist, following the existing `Services/*ServiceTests.cs` pattern) — regression guard for research.md #7's verified-no-code-change finding
- [ ] T005 [P] [US1] Backend test: `GET api/projects/{projectId}/work-items?status=InReview` returns only matching items, in `backend/TaskFlow.Api.Tests/Integration/WorkItemsEndpointsTests.cs`
- [ ] T006 [P] [US1] Frontend test: `StatusChipComponent` renders label `'In Review'` and a color class distinct from the other three statuses, in `frontend/src/app/shared/status-chip/status-chip.component.spec.ts` (must fail to compile/RED until T009 adds `'InReview'` to the `WorkItemStatus` union)

### Implementation for User Story 1

- [ ] T007 [US1] Add `InReview` to the `WorkItemStatus` enum, ordered between `InProgress` and `Done`, in `backend/TaskFlow.Api/Data/Entities/WorkItem.cs` — no migration (research.md #1); makes T003 pass
- [ ] T008 [US1] Verify `WorkItemRequest`'s `Status` validation accepts `InReview` automatically now that the enum includes it; adjust only if a hard-coded allow-list exists instead of enum-based parsing, in `backend/TaskFlow.Api/Dtos/WorkItemRequest.cs` — depends on T007
- [ ] T009 [P] [US1] Add `'InReview'` to the `WorkItemStatus` string-literal union in `frontend/src/app/projects/work-items.service.ts`
- [ ] T010 [P] [US1] Add `--color-status-inreview-bg` / `--color-status-inreview-text` tokens (purple/violet family, distinct from the existing gray/blue/green status colors) to `frontend/src/design-tokens.scss`
- [ ] T011 [US1] Add the `InReview` case to `StatusChipComponent`'s label map and color switch in `frontend/src/app/shared/status-chip/status-chip.component.ts` — depends on T009, T010; makes T006 pass
- [ ] T012 [P] [US1] Add `'InReview'` to the `STATUSES` array (correct order) in `frontend/src/app/projects/work-item-form/work-item-form.component.ts` — depends on T009
- [ ] T013 [P] [US1] Add `'InReview'` to the `STATUSES` array (correct order) in `frontend/src/app/projects/project-detail/project-detail.component.ts` — depends on T009

**Checkpoint**: In Review is selectable, correctly colored everywhere,
correctly counted as open, and no existing item's status changed —
quickstart.md section 1 passes.

---

## Phase 4: User Story 2 - View the board (Priority: P1)

**Goal**: A project's Board view shows four columns (from backend data,
not a hard-coded frontend list) with accurate counts, and every card
shows enough to be useful without opening it.

**Independent Test**: Open Board view on a project with a realistic mix
of items; confirm column counts match reality and sampled cards show
title/type/priority/assignee/due-date(-overdue)/child-progress correctly.

### Tests for User Story 2

- [ ] T014 [P] [US2] Backend test: `GetBoardAsync` returns `Columns` as ordered `{status, label}` pairs (M1 — not bare status strings) and, for every item at any depth (not just tree roots), correct `DirectChildrenCount`/`DirectChildrenDoneCount`, sorted by `UpdatedAt` descending, in `backend/TaskFlow.Api.Tests/Services/WorkItemServiceTests.cs`
- [ ] T015 [P] [US2] Backend test: `GET api/projects/{projectId}/work-items/board` returns 200 with the expected `WorkItemBoardDto` shape, including each column's `label` text (M1), not just its status value; 404 for an unknown project, in `backend/TaskFlow.Api.Tests/Integration/WorkItemsEndpointsTests.cs`
- [ ] T016 [P] [US2] Frontend test: `isOverdue(dueDate, status)` per the precise definition in data-model.md (M2) — true for a date-only-strictly-before-today due date on a non-Done item; **false** for a due date equal to today's local calendar date (explicit boundary case, not just "not true"); false for a future date; false whenever `status === 'Done'` regardless of how far past the due date is; verify with a due date/status pair that would be overdue by UTC-date comparison but is NOT overdue by local-calendar-date comparison (or vice versa) to pin down the local-vs-UTC behavior — in `frontend/src/app/projects/board/overdue.spec.ts`
- [ ] T017 [P] [US2] Frontend test: `BoardCardComponent` renders title (wrapped/truncated), type, priority chip, avatar or an "Unassigned" indicator, friendly-formatted due date, the overdue flag, and "n/m done" only when children exist, in `frontend/src/app/projects/board/board-card.component.spec.ts`
- [ ] T018 [P] [US2] Frontend test: `BoardComponent` renders 4 columns from a `WorkItemBoardDto` fixture with correct header counts, cards grouped into the right column, and a per-column empty state for zero items, in `frontend/src/app/projects/board/board.component.spec.ts`

### Implementation for User Story 2

- [ ] T019 [P] [US2] Create `WorkItemBoardDto`, `BoardColumnDto` (M1 — `{Status, Label}` pair, not a bare string), and `WorkItemBoardCardDto` records (data-model.md shapes) in `backend/TaskFlow.Api/Dtos/WorkItemBoardDto.cs` and `backend/TaskFlow.Api/Dtos/WorkItemBoardCardDto.cs`
- [ ] T020 [US2] Implement `WorkItemService.GetBoardAsync(projectId)` — one query over all of a project's items, per-item direct-children-done counts via the same `Dictionary`/`GroupBy`-by-`ParentWorkItemId` shape `GetTreeAsync` already uses (research.md #2), applied to every item; builds the fixed four-entry `Columns` list with its `Label` text computed server-side (M1) — in `backend/TaskFlow.Api/Services/WorkItemService.cs` — depends on T019; makes T014 pass
- [ ] T021 [US2] Add the `GET api/projects/{projectId}/work-items/board` action to `backend/TaskFlow.Api/Controllers/WorkItemsController.cs` — depends on T020; makes T015 pass
- [ ] T022 [P] [US2] Create `isOverdue()` pure function in `frontend/src/app/projects/board/overdue.ts` — makes T016 pass
- [ ] T023 [US2] Add `getBoard(projectId): Promise<WorkItemBoard>` and the `WorkItemBoard`/`BoardColumn` (`{status, label}`, M1)/`WorkItemBoardCard` types to `frontend/src/app/projects/work-items.service.ts` — depends on T021
- [ ] T024 [US2] Create `BoardCardComponent` (title/type/priority chip/avatar-or-unassigned/friendly+overdue due date/n-m-done, using `<app-priority-chip>`/`<app-user-avatar>`/`FriendlyDatePipe` — no new ad-hoc styling per FR-023) in `frontend/src/app/projects/board/board-card.component.ts` (+ `.html` + `.css`) — depends on T022; makes T017 pass
- [ ] T025 [US2] Create `BoardComponent` (fetches via `getBoard()`, groups items into columns using `WorkItemBoardDto.columns`' order, renders each column header using its `label` field verbatim from the response — M1, no client-side status→label lookup anywhere in this component — with the item count, independent per-column vertical scroll, horizontal board scroll without breaking the app shell, per-column `<app-empty-state>` when empty) in `frontend/src/app/projects/board/board.component.ts` (+ `.html` + `.css`) — depends on T023, T024; makes T018 pass
- [ ] T026 [US2] Add `'board'` to `project-detail`'s `viewMode` union, a third toggle button, and sync the selected view with a `view` query param — read via `ActivatedRoute.queryParamMap` on init, written via `Router.navigate` with `queryParams` on toggle — so the selection survives navigating to a card's detail page and back (FR-019, needed again by US5), in `frontend/src/app/projects/project-detail/project-detail.component.ts` (+ `.html`) — depends on T025
- [ ] T027 [US2] Update `frontend/src/app/projects/project-detail/project-detail.component.spec.ts` for the Board toggle and the `view` query-param sync

**Checkpoint**: Board view renders real backend data with correct
columns, counts, and card content — quickstart.md section 2 passes.

---

## Phase 5: User Story 3 - Drag a card to change its status (Priority: P1)

**Goal**: Dragging a card between columns changes its status with
optimistic UI and revert-on-failure; the same creator/assignee/Manager/
Admin rule applies, enforced independently by the server.

**Independent Test**: As a permitted user, drag a card and confirm the
status persists; force a save failure and confirm it reverts with a
toast; as an unpermitted user, confirm the card can't be moved, and a
direct `PATCH` call as that user is refused by the server.

### Tests for User Story 3

- [ ] T028 [P] [US3] Backend test: `UpdateStatusAsync` succeeds for the creator, current assignee, Manager, or Admin; throws `NotAuthorizedToEditWorkItemException` otherwise; rejects an invalid status value; not-found for an unknown id, in `backend/TaskFlow.Api.Tests/Services/WorkItemServiceTests.cs`
- [ ] T029 [P] [US3] Backend test: `PATCH api/work-items/{id}/status` — 200 for an authorized caller, 403 for an unauthorized caller (called directly, independent of any UI — FR-015), 400 for an invalid status, 404 for an unknown id, in `backend/TaskFlow.Api.Tests/Integration/WorkItemsEndpointsTests.cs`
- [ ] T030 [P] [US3] Frontend test: `canEditWorkItem()` is true for the creator/assignee/Manager/Admin and false otherwise, in `frontend/src/app/projects/work-item-permissions.spec.ts`
- [ ] T031 [US3] Frontend test: extend `board.component.spec.ts` — (a) a permitted drag moves a card and calls the status-update service method with the target status; (b) **M3 — strengthened**: when the status-update call rejects (simulate both a generic failed-`PATCH` rejection and a 403-style rejection, e.g. `HttpErrorResponse` with `status: 403`), assert *both* that the card is found back in its original/source column's item list (not merely "not in the target column" — explicitly the source column) *and* that `NotificationService.error` was called, for each rejection case; (c) a card for an item the current user can't edit has dragging disabled

### Implementation for User Story 3

- [ ] T032 [US3] Extract a shared `EnsureCanEdit(workItem, callerId, callerRole)` private method in `backend/TaskFlow.Api/Services/WorkItemService.cs`, called by both the existing `UpdateAsync` (behavior unchanged — its own tests must keep passing) and the new status method — depends on T028
- [ ] T033 [US3] Create `UpdateWorkItemStatusRequest` record in `backend/TaskFlow.Api/Dtos/UpdateWorkItemStatusRequest.cs`
- [ ] T034 [US3] Implement `WorkItemService.UpdateStatusAsync(id, status, callerId, callerRole)` — depends on T032, T033; makes T028 pass
- [ ] T035 [US3] Add the `PATCH api/work-items/{id}/status` action to `backend/TaskFlow.Api/Controllers/WorkItemsController.cs` — depends on T034; makes T029 pass
- [ ] T036 [P] [US3] Create `canEditWorkItem()` pure function (data-model.md shape) in `frontend/src/app/projects/work-item-permissions.ts` — makes T030 pass
- [ ] T037 [US3] Update `frontend/src/app/projects/project-detail/project-detail.component.ts` to call the shared `canEditWorkItem()` instead of its own private `canEdit()` — depends on T036; existing `project-detail.component.spec.ts` assertions must keep passing unchanged
- [ ] T038 [US3] Update `frontend/src/app/projects/work-item-detail/work-item-detail.component.ts` to call the shared `canEditWorkItem()` instead of its own private `canEdit()` — depends on T036; existing `work-item-detail.component.spec.ts` assertions must keep passing unchanged
- [ ] T039 [US3] Add `updateWorkItemStatus(id, status): Promise<WorkItem>` to `frontend/src/app/projects/work-items.service.ts` — depends on T035
- [ ] T040 [US3] Wire `@angular/cdk/drag-drop` into `BoardComponent` (`cdkDropListGroup` around the board, one `cdkDropList` per column, `(cdkDropListDropped)` doing the optimistic move + `updateWorkItemStatus()` call; on *any* rejection — including a 403 — move the card back into its recorded source column's list and call `NotificationService.error` with a message explaining the save failed, per the M3-strengthened assertions in T031) in `frontend/src/app/projects/board/board.component.ts` (+ `.html`) — depends on T039; makes T031 pass
- [ ] T041 [US3] Bind `[cdkDragDisabled]` on each card to `!canEditWorkItem(item, currentUserId, currentRole)`, with a title/tooltip explaining why when disabled, in `frontend/src/app/projects/board/board-card.component.ts` (+ `.html`) — depends on T036; makes T031 pass

**Checkpoint**: Drag changes status with optimistic UI and revert;
permission enforced client- and server-side — quickstart.md section 3
passes.

---

## Phase 6: User Story 4 - Add a work item from a column (Priority: P2)

**Goal**: A column's "+ Add" affordance opens the create form with that
column's status pre-selected.

**Independent Test**: Click "+ Add" in the In Review column; confirm the
form opens with Status pre-selected to In Review.

### Tests for User Story 4

- [ ] T042 [P] [US4] Frontend test: extend `work-item-form.component.spec.ts` — a `status` query param pre-selects the Status field on create
- [ ] T043 [P] [US4] Frontend test: extend `board.component.spec.ts` — each column's "+ Add" link targets the work-item-form route with the correct project id and that column's status as a query param

### Implementation for User Story 4

- [ ] T044 [US4] Read an optional `status` query param in `WorkItemFormComponent.ngOnInit` (parallel to the existing `type`/`parentWorkItemId` handling) to pre-select Status, in `frontend/src/app/projects/work-item-form/work-item-form.component.ts` — makes T042 pass
- [ ] T045 [US4] Add a "+ Add" affordance per column, linking to the work-item-form route with the project id and that column's status as a query param, in `frontend/src/app/projects/board/board.component.html` — depends on T044; makes T043 pass

**Checkpoint**: Creating from a column pre-selects the right status —
quickstart.md section 4 passes.

---

## Phase 7: User Story 5 - Open a card's detail and return to the board (Priority: P2)

**Goal**: Clicking a card opens its detail page; returning shows Board
view again, not Tree or Flat.

**Independent Test**: Click a card, confirm the correct detail page
opens; navigate back, confirm Board view is still selected.

### Tests for User Story 5

- [ ] T046 [P] [US5] Frontend test: extend `board-card.component.spec.ts` — clicking a card (not a drag gesture) links to the correct work item detail route
- [ ] T047 [P] [US5] Frontend test: extend `project-detail.component.spec.ts` — with `?view=board` present on load, Board view is selected (confirms the T026 query-param mechanism also satisfies "return to the board" after a detail-page visit)

### Implementation for User Story 5

- [ ] T048 [US5] Wrap each card's clickable area (excluding the drag handle) in a `routerLink` to the work item detail route, in `frontend/src/app/projects/board/board-card.component.html` — makes T046 pass
- [ ] T049 [US5] Verification-only (L2 — no new code expected): run T047 against T026's existing `view` query-param mechanism and confirm it already restores Board view after back-navigation from a card's detail page. If T047 fails, that means T026 didn't fully implement the mechanism — fix it there, not by adding parallel logic in this file. `frontend/src/app/projects/project-detail/project-detail.component.ts` — makes T047 pass

**Checkpoint**: Full click-through → detail → back cycle preserves
Board view — quickstart.md section 5 passes.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [ ] T050 [P] Add the new In Review tokens to the token table in `frontend/src/app/shared/README.md`
- [ ] T051 Run the full backend suite (`cd backend/TaskFlow.Api.Tests && dotnet test`) and confirm 100% pass — SC-006
- [ ] T052 Run the full frontend suite (`cd frontend && npm test`) and confirm 100% pass — SC-006
- [ ] T053 Cross-cutting checkpoint: confirm no ad-hoc In Review color exists outside `design-tokens.scss`/`StatusChipComponent` (grep for hard-coded purple/violet hex elsewhere) — SC-007
- [ ] T054 Add a "Feature 005: Kanban Board" entry to `README.md`'s "What I learned" log, matching the style of Features 001-004's entries, per the constitution's Definition of Done item 5
- [ ] T055 Walk through `quickstart.md` sections 1-7 manually in a running app (backend + `ng serve`), including the direct-`PATCH`-bypassing-the-UI permission check (section 3.4) and the keyboard-focus check (section 7)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — run first
- **Foundational (Phase 2)**: N/A for this feature — see note above
- **US1 (Phase 3)**: Depends on Setup only — the true MVP slice
- **US2 (Phase 4)**: Depends on US1 (a board with only 3 real columns
  isn't the feature; the board's own column list also comes from the
  backend, independent of the frontend `STATUSES` arrays US1 touches, but
  testing it meaningfully wants In Review to already exist end-to-end)
- **US3 (Phase 5)**: Depends on US2 (`BoardComponent`/`BoardCardComponent`
  must exist before drag can be wired into them)
- **US4 (Phase 6)**: Depends on US2 (needs `BoardComponent`'s column
  structure) — independent of US3 (does not need drag to exist)
- **US5 (Phase 7)**: Depends on US2 (needs `BoardCardComponent` and the
  `view` query-param mechanism from T026) — independent of US3/US4
- **Polish (Phase 8)**: Depends on all five user stories being complete

### Within Each User Story

- Tests written and confirmed failing before their implementation task
  (Constitution Principle I)
- Backend DTOs before backend services; backend services before backend
  controller actions
- Frontend pure functions/services before the components that consume
  them
- `work-item-permissions.ts` (T036) before both existing components are
  updated to use it (T037, T038) and before the board binds it (T041)

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel
- Within US1: T003-T006 (tests) are parallel; T009/T010 are parallel;
  T012/T013 are parallel
- Within US2: T014-T018 (tests) are parallel; T019 and T022 are parallel
  (different files/stacks)
- Within US3: T028-T030 are parallel; T032-T035 (backend) can proceed
  alongside T036 (frontend) once both sets of tests exist
- US4 and US5 can be implemented in parallel by different contributors
  once US2 is done — neither depends on the other

---

## Parallel Example: User Story 2

```bash
# Tests (all independent files):
Task: "GetBoardAsync test in backend/TaskFlow.Api.Tests/Services/WorkItemServiceTests.cs"
Task: "GET board endpoint test in backend/TaskFlow.Api.Tests/Integration/WorkItemsEndpointsTests.cs"
Task: "isOverdue test in frontend/src/app/projects/board/overdue.spec.ts"
Task: "BoardCardComponent test in frontend/src/app/projects/board/board-card.component.spec.ts"
Task: "BoardComponent test in frontend/src/app/projects/board/board.component.spec.ts"

# Implementation (backend and frontend proceed independently once contracts/board-api.md is the shared reference):
Task: "WorkItemBoardDto/WorkItemBoardCardDto in backend/TaskFlow.Api/Dtos/"
Task: "isOverdue() in frontend/src/app/projects/board/overdue.ts"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 3: User Story 1 (In Review everywhere)
3. **STOP and VALIDATE**: quickstart.md section 1 — In Review selectable,
   correctly colored, correctly counted, no remapping
4. Review with the maintainer before continuing (Constitution Principle
   VIII)

### Incremental Delivery

1. Setup → baseline confirmed
2. US1 → In Review real everywhere → demo (smallest possible MVP)
3. US2 → Board view renders real data → demo
4. US3 → Drag actually works, permission-enforced → demo (feature's
   defining interaction now live)
5. US4 → per-column create → demo
6. US5 → card → detail → back → demo (feature-complete, matches
   spec.md's Success Check)
7. Phase 8 → docs + full regression + quickstart walkthrough

### Commit Convention

One commit per user story, `feat: US<N> <summary>` with passing test
count, e.g. `feat: US3 drag-and-drop status change (backend PATCH +
frontend CDK, 214/214 backend tests pass)`. Setup and Polish commit as
`chore:`/`docs:`.

---

## Notes

- [P] tasks touch different files and have no incomplete-task dependency
- This feature has real backend work (unlike Feature 004) — every
  backend task lists its exact file; none invent a new controller, since
  board/status-update concerns live in the existing `WorkItemsController`
  (research.md #2/#3)
- Tests must be written and observed failing before their implementation
  task (Constitution Principle I)
- Commit after each user story completes
- Stop at any checkpoint to validate that story independently before
  continuing
- T037/T038 are refactors of already-tested behavior — their own
  existing specs are the regression guard; no new assertions are expected
  there beyond confirming nothing broke
