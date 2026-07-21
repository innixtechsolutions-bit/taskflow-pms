# Tasks: Work Item Modal & Quick Creation

**Input**: Design documents from `D:\Projects\taskflow-pms\specs\007-work-item-modal\`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/work-item-modal-api.md, quickstart.md

**Tests**: Included and REQUIRED — Constitution Principle I (Test-First Development, NON-NEGOTIABLE) mandates tests before implementation for both backend (xUnit + real SQL Server test databases) and frontend (Vitest), and spec.md's Non-functional requirements explicitly require "modal logic that is pure (validation, create-another field retention, label normalization)" to be test-first.

**Organization**: Tasks are grouped by user story (spec.md priorities P1–P3) so each story is independently implementable, testable, and demoable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (no code dependency on an incomplete task — same-file test-case additions are still marked [P] when independent of each other, matching this repo's Feature 006 convention)
- **[Story]**: US1–US5, matching spec.md
- All paths are relative to `D:\Projects\taskflow-pms\`

## Path Conventions

Web application: `backend/TaskFlow.Api/` (+ `backend/TaskFlow.Api.Tests/`) and
`frontend/src/app/`.

---

## Phase 1: Setup

- [X] T001 Confirm the backend suite passes before starting (`cd backend/TaskFlow.Api.Tests && dotnet test`) — clean baseline for regression comparison (285/285 as of Feature 006)
- [X] T002 [P] Confirm the frontend suite passes before starting (`cd frontend && npm test`) — clean baseline for regression comparison (189/189 as of Feature 006)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The new `StartDate` column and `Label`/`WorkItemLabel` schema
(data-model.md), needed by US3 and US5 respectively, delivered as one
migration per plan.md's Technical Context ("one migration, purely additive").
US1/US2/US4 don't read this schema but aren't harmed by its early presence.

**⚠️ CRITICAL**: US3 and US5 cannot begin until this phase is complete. US1,
US2, and US4 have no dependency on it and could proceed in parallel if
staffed separately.

- [X] T003 Add `StartDate` (`DateTime?`) field and a `Labels` (`ICollection<WorkItemLabel>`) navigation collection to `WorkItem`, in `backend/TaskFlow.Api/Data/Entities/WorkItem.cs`
- [X] T004 [P] Create the `Label` entity (`Id`, `ProjectId`, `Project` nav, `Name`, `CreatedAt`, `WorkItemLabels` nav) in `backend/TaskFlow.Api/Data/Entities/Label.cs`
- [X] T005 [P] Create the `WorkItemLabel` join entity (`Id`, `WorkItemId`, `WorkItem` nav, `LabelId`, `Label` nav) in `backend/TaskFlow.Api/Data/Entities/WorkItemLabel.cs`
- [X] T006 Configure `AppDbContext`: `Labels`/`WorkItemLabels` `DbSet`s; unique index `(ProjectId, Name)` on `Label`; unique index `(WorkItemId, LabelId)` on `WorkItemLabel`; cascade `Project → Label`, `WorkItem → WorkItemLabel`; **`Label → WorkItemLabel` is `Restrict`, not `Cascade`** — SQL Server rejects the originally-planned `Cascade` here as a multiple-cascade-paths error (1785); corrected in data-model.md too — in `backend/TaskFlow.Api/Data/AppDbContext.cs` — depends on T003, T004, T005
- [X] T007 Generate the EF Core migration `AddWorkItemStartDateAndLabels` (additive schema only — new nullable column, two new tables, no data backfill) in `backend/TaskFlow.Api/Data/Migrations/` — depends on T006

**Checkpoint**: Schema exists; nothing user-visible changes yet.

---

## Phase 3: User Story 1 - Create and edit work items without leaving the current view (Priority: P1) 🎯 MVP

**Goal**: A `MatDialog`-based `WorkItemModalComponent` replaces the routed
`WorkItemFormComponent` for every existing creation/edit affordance, with full
field parity (type, parent, title, description, priority, status, assignee,
due date), the same validation and pre-selection behavior, targeted view
refresh on success, confirm-discard on close, in-dialog error display, and the
old full-page routes removed.

**Independent Test**: From the Board, click a column's "+" affordance and
confirm the modal opens with that column's status pre-selected, submits
without navigation, and the board refreshes in place at its prior scroll
position. Separately, open an existing item's edit affordance and confirm the
same modal opens pre-populated.

### Tests for User Story 1

- [X] T008 [P] [US1] Frontend test: `WorkItemModalComponent` create mode pre-selects `statusId` from dialog data (board "+") and `parentWorkItemId`/`type` from dialog data ("Add child") — new `frontend/src/app/projects/work-item-modal/work-item-modal.component.spec.ts`
- [X] T009 [P] [US1] Frontend test: `WorkItemModalComponent` edit mode loads and pre-populates every field from the existing work item — same spec file
- [X] T010 [P] [US1] Frontend test: `WorkItemModalComponent` closes immediately on Escape/close when untouched, but prompts via `confirm()` first once any field has changed (dirty-flag, research.md #2) — same spec file
- [X] T011 [P] [US1] Frontend test: `WorkItemModalComponent` displays a server/validation error inside the dialog without closing it, retaining entered values — same spec file
- [X] T012 [P] [US1] Frontend test: on a successful create/update (not "Create another"), `WorkItemModalComponent` shows a `NotificationService` toast, invokes the `onSaved` callback, and closes — same spec file

### Implementation for User Story 1

- [X] T013 [US1] Create `WorkItemModalComponent`, porting `WorkItemFormComponent`'s existing field logic (title Signal Form, type/description/priority/statusId/assigneeUserId/dueDate/parentWorkItemId signals, `toDateOnlyString`/`parseDateOnlyString` helpers, `loadStatuses`/`loadAssignableUsers`/`loadParentCandidates`) to read `MAT_DIALOG_DATA` (`{ mode: 'create' | 'edit', projectId, workItemId?, statusId?, parentWorkItemId?, type?, onSaved: () => void }`) instead of `ActivatedRoute` params, and close via `MatDialogRef` instead of `Router.navigateByUrl` — `frontend/src/app/projects/work-item-modal/work-item-modal.component.ts` (+ `.html` + `.css`) — depends on T008–T012 (tests written first, confirmed failing)
- [X] T014 [US1] Add the dirty-flag signal (set on first change to any field) and Escape/close handling that calls `confirm()` only when dirty, in `work-item-modal.component.ts` — depends on T013; makes T010 pass
- [X] T015 [US1] `BoardComponent`: column "+" opens `WorkItemModalComponent` via `MatDialog.open(..., { data: { mode: 'create', projectId, statusId: column.statusId, onSaved: refreshBoard } })` instead of `routerLink` — `frontend/src/app/projects/board/board.component.ts` (+ `.html`) — depends on T013
- [X] T016 [US1] `WorkItemDetailComponent`: "Add child" opens the modal (`parentWorkItemId`/`type` prefill); "Edit" opens the modal (edit mode); both pass an `onSaved` that re-fetches the detail view — `frontend/src/app/projects/work-item-detail/work-item-detail.component.ts` (+ `.html`) — depends on T013
- [X] T017 [US1] `ProjectDetailComponent`: the toolbar "New work item" button and both empty-state "Add work item" actions open the modal (create, no prefill); the list-row "Edit" link opens the modal (edit mode); each `onSaved` re-fetches whichever view (board/list/tree) is active — `frontend/src/app/projects/project-detail/project-detail.component.ts` (+ `.html`) — depends on T013
- [X] T018 [US1] Remove the `.../work-items/new` and `.../work-items/:id/edit` routes and replace them with `redirectTo` entries per research.md #10 (create → `.../projects/:projectId`; edit → `.../projects/:projectId/work-items/:id`); delete `WorkItemFormComponent` and its spec — `frontend/src/app/app.routes.ts`; delete `frontend/src/app/projects/work-item-form/` — depends on T015, T016, T017
- [X] T019 [US1] Update `board.component.spec.ts`, `work-item-detail.component.spec.ts`, and `project-detail.component.spec.ts` to assert `MatDialog.open(...)` calls (with the expected data) instead of the old `routerLink`/query-param assertions — depends on T015, T016, T017, T018

**Checkpoint**: Creating and editing work items happens entirely through the
modal from every existing entry point; the old full-page routes no longer
exist as pages — quickstart.md section 1 passes.

---

## Phase 4: User Story 2 - Assign a new item to myself in one click (Priority: P2)

**Goal**: An "Assign to me" control next to the Assignee field sets the
current user as assignee in one click, in both create and edit modes.

**Independent Test**: Open the create modal, click "Assign to me", confirm
the Assignee field shows the current user. Repeat in the edit modal on an
item assigned to someone else.

### Tests for User Story 2

- [X] T020 [P] [US2] Frontend test: clicking "Assign to me" in create mode sets `assigneeUserId` to `AuthService.currentUser()?.id` — `frontend/src/app/projects/work-item-modal/work-item-modal.component.spec.ts`
- [X] T021 [P] [US2] Frontend test: clicking "Assign to me" in edit mode on an item assigned to another user switches `assigneeUserId` to the current user — same spec file

### Implementation for User Story 2

- [X] T022 [US2] Add the "Assign to me" control next to the Assignee field in `work-item-modal.component.ts` (+ `.html`), setting `assigneeUserId` from `AuthService.currentUser()?.id` — depends on T013; makes T020, T021 pass

**Checkpoint**: "Assign to me" works in both modes — quickstart.md section 2
passes.

---

## Phase 5: User Story 3 - Set an optional start date (Priority: P2)

**Goal**: An optional, date-only Start date field is available in the modal
and on the detail view; when both Start date and Due date are set, the system
enforces start ≤ due on both client and server with a clear message.

**Independent Test**: Set a start date on or before an existing due date and
confirm it saves and displays on detail; set one after the due date and
confirm submission is blocked with a clear message, both client-side and
(bypassing the client) server-side.

### Tests for User Story 3

- [X] T023 [P] [US3] Backend test: `WorkItemService.CreateAsync`/`UpdateAsync` persist `StartDate`; reject when `StartDate > DueDate` (`InvalidDateRangeException`); accept when equal, when only one date is set, or when neither is set — `backend/TaskFlow.Api.Tests/Services/WorkItemServiceTests.cs`
- [X] T024 [P] [US3] Backend test: `POST`/`PUT` work-items return `400` with `InvalidDateRangeException`'s message when `startDate > dueDate` — `backend/TaskFlow.Api.Tests/Integration/WorkItemsEndpointsTests.cs`
- [X] T025 [P] [US3] Frontend test: `WorkItemModalComponent` blocks submit with an inline message when Start date is after Due date, and allows it otherwise — `frontend/src/app/projects/work-item-modal/work-item-modal.component.spec.ts`

### Implementation for User Story 3

- [X] T026 [US3] Add `InvalidDateRangeException` ("Start date must be on or before the due date.") to `backend/TaskFlow.Api/Services/WorkItemExceptions.cs`
- [X] T027 [US3] Add `StartDate` (`DateTime?`) to `WorkItemRequest` — `backend/TaskFlow.Api/Dtos/WorkItemRequest.cs`
- [X] T028 [P] [US3] Add `StartDate` to `WorkItemDto` and `WorkItemDetailDto` (not `WorkItemBoardCardDto` — out of scope per spec) — `backend/TaskFlow.Api/Dtos/WorkItemDto.cs`, `backend/TaskFlow.Api/Dtos/WorkItemDetailDto.cs`
- [X] T029 [US3] `WorkItemService.CreateAsync`/`UpdateAsync`: persist `StartDate`, throw `InvalidDateRangeException` when both dates are present and `StartDate > DueDate` — `backend/TaskFlow.Api/Services/WorkItemService.cs` — depends on T026, T027; makes T023 pass
- [X] T030 [US3] Catch `InvalidDateRangeException` → `Problem(400, ...)` in the `Create` and `Update` actions — `backend/TaskFlow.Api/Controllers/WorkItemsController.cs` — depends on T029; makes T024 pass
- [X] T031 [P] [US3] Add `startDate?: string` to the frontend `WorkItemRequest`/`WorkItem`/`WorkItemDetail` interfaces — `frontend/src/app/projects/work-items.service.ts`
- [X] T032 [US3] Add the Start date field (`MatDatepicker`, reusing `toDateOnlyString`/`parseDateOnlyString`) and client-side start≤due validation message to `work-item-modal.component.ts` (+ `.html`) — depends on T013, T031; makes T025 pass
- [X] T033 [US3] Display Start date alongside Due date on the detail view — `frontend/src/app/projects/work-item-detail/work-item-detail.component.ts` (+ `.html`) — depends on T028, T031

**Checkpoint**: Start date is settable, validated client- and server-side, and
visible on detail — quickstart.md section 3 passes.

---

## Phase 6: User Story 4 - Log several items in a row without re-opening the modal (Priority: P2)

**Goal**: A "Create another" checkbox keeps the create modal open after a
successful create, clearing title/description/labels but retaining type,
status, priority, assignee, parent, start date, and due date.

**Independent Test**: Check "Create another", submit several items in a row
without closing the modal, and confirm each retains the expected fields while
clearing title/description, and the underlying view refreshes after each one.

### Tests for User Story 4

- [ ] T034 [P] [US4] Frontend test: with "Create another" checked, a successful create clears `title`/`description`/`labels`, retains `type`/`statusId`/`priority`/`assigneeUserId`/`parentWorkItemId`/`startDate`/`dueDate`, and keeps the modal open — `frontend/src/app/projects/work-item-modal/work-item-modal.component.spec.ts`
- [ ] T035 [P] [US4] Frontend test: with "Create another" unchecked, a successful create closes the modal (US1 behavior unaffected) — same spec file
- [ ] T036 [P] [US4] Frontend test: the "Create another" checkbox is not shown (or has no effect) in edit mode — same spec file

### Implementation for User Story 4

- [ ] T037 [US4] Add the "Create another" checkbox to the modal's fixed footer (create mode only); on a successful create with it checked, reset `title`/`description`/labels signals, keep every other field, call `onSaved()`, show the success toast, and do not close the dialog — `work-item-modal.component.ts` (+ `.html`) — depends on T013, T032; makes T034, T035, T036 pass

**Checkpoint**: Batch entry works end-to-end with live view refresh per item
(research.md #9) — quickstart.md section 4 passes.

---

## Phase 7: User Story 5 - Tag work items with labels and filter by them (Priority: P3)

**Goal**: 0–5 free-form, project-scoped labels can be attached to a work item
(created inline by typing a new value, suggested from existing project
labels), shown as neutral chips on board cards, list rows, tree rows, and
detail, and filterable (single-select) in the List view.

**Independent Test**: Create a new label "backend" inline on one item, confirm
it appears as a chip on that item's board card/list row/tree row/detail,
appears as a typing suggestion on a different item, and that the List view
can be filtered down to items carrying it.

### Tests for User Story 5

- [ ] T038 [P] [US5] Backend test: the label-normalization helper trims whitespace, rejects names outside 1–30 characters (`InvalidLabelException`), rejects a 6th label (`TooManyLabelsException`), dedupes case-insensitively within one request, and reuses an existing project label on a case-insensitive name match instead of duplicating it — `backend/TaskFlow.Api.Tests/Services/WorkItemServiceTests.cs`
- [ ] T039 [P] [US5] Backend test: `GetProjectLabelsAsync` returns only labels referenced by ≥1 work item, alphabetically ordered, and an empty list (not a 404) for a project with none — same file
- [ ] T040 [P] [US5] Backend test: `GetWorkItemsAsync`'s new `label` filter matches case-insensitively and combines (AND) with the existing `statusId`/`type`/`priority`/`assigneeUserId`/`search` filters — same file
- [ ] T041 [P] [US5] Backend test: `POST`/`PUT` work-items return `400` for `InvalidLabelException`/`TooManyLabelsException`; `GET /api/projects/{projectId}/labels` returns the expected `200` shape and `404` for an unknown project — `backend/TaskFlow.Api.Tests/Integration/WorkItemsEndpointsTests.cs`
- [ ] T042 [P] [US5] Frontend test: `LabelChipComponent` renders a label's name with the shared neutral `.chip--label` class — new `frontend/src/app/shared/label-chip/label-chip.component.spec.ts`
- [ ] T043 [P] [US5] Frontend test: `WorkItemModalComponent`'s label input creates a new label on confirm, offers matching suggestions from `getProjectLabels()`, blocks a 6th label with a clear inline message, and ignores a duplicate attach attempt — `frontend/src/app/projects/work-item-modal/work-item-modal.component.spec.ts`
- [ ] T044 [P] [US5] Frontend test: `ProjectDetailComponent`'s label filter selects a label and includes it in the list query, combinable with existing filters — `frontend/src/app/projects/project-detail/project-detail.component.spec.ts`

### Implementation for User Story 5

- [ ] T045 [US5] Add `InvalidLabelException` and `TooManyLabelsException` to `backend/TaskFlow.Api/Services/WorkItemExceptions.cs`
- [ ] T046 [US5] Add `Labels` (`List<string>?`) to `WorkItemRequest` — `backend/TaskFlow.Api/Dtos/WorkItemRequest.cs` — depends on T027 (same file, US3's `StartDate` field already added)
- [ ] T047 [P] [US5] Add `Labels` (`List<string>`) to `WorkItemDto` and `WorkItemDetailDto` — `backend/TaskFlow.Api/Dtos/WorkItemDto.cs`, `backend/TaskFlow.Api/Dtos/WorkItemDetailDto.cs` — depends on T028 (same files, US3's `StartDate` field already added)
- [ ] T048 [P] [US5] Add `Labels` to `WorkItemBoardCardDto` — `backend/TaskFlow.Api/Dtos/WorkItemBoardCardDto.cs`
- [ ] T049 [P] [US5] Add `Labels` to `WorkItemTreeNodeDto` — `backend/TaskFlow.Api/Dtos/WorkItemTreeNodeDto.cs`
- [ ] T050 [US5] Add the `NormalizeAndAttachLabelsAsync(projectId, requestedNames)` helper (validate 1–30 chars, cap at 5, case-insensitive dedupe, find-or-create per project) called from `CreateAsync`/`UpdateAsync` (replace-the-whole-set semantics on update, matching every other field's PUT behavior); include `Labels` in the `ToDtoAsync`/`ToDetailDtoAsync`/`GetWorkItemsAsync`/`GetBoardAsync`/`GetTreeAsync` projections — `backend/TaskFlow.Api/Services/WorkItemService.cs` — depends on T045, T046, T047, T048, T049; makes T038 pass
- [ ] T051 [US5] Add `GetProjectLabelsAsync(projectId)` (labels with ≥1 `WorkItemLabel` reference, alphabetical) — `backend/TaskFlow.Api/Services/WorkItemService.cs` — depends on T050; makes T039 pass
- [ ] T052 [US5] Add the `label` filter parameter to `GetWorkItemsAsync` — `backend/TaskFlow.Api/Services/WorkItemService.cs` — depends on T050; makes T040 pass
- [ ] T053 [US5] Catch `InvalidLabelException`/`TooManyLabelsException` → `400` in `Create`/`Update`; add the `GET api/projects/{projectId}/labels` action; add the `label` query parameter to `GetWorkItems` — `backend/TaskFlow.Api/Controllers/WorkItemsController.cs` — depends on T050, T051, T052; makes T041 pass
- [ ] T054 [P] [US5] Create `LabelChipComponent` (single neutral `.chip--label` class added to `chip.css`, no `ColorKey`-keyed switch — research.md #6) — `frontend/src/app/shared/label-chip/label-chip.component.ts` (+ `.html`) — makes T042 pass
- [ ] T055 [US5] Add `labels: string[]` to the frontend `WorkItem`/`WorkItemDetail`/`WorkItemBoardCard`/`WorkItemTreeNode` interfaces, `labels?: string[]` to `WorkItemRequest`, `label?: string` to `WorkItemsFilter`, and a new `getProjectLabels(projectId)` method — `frontend/src/app/projects/work-items.service.ts` — depends on T031 (same file, US3's `startDate` fields already added)
- [ ] T056 [US5] Add the Labels input to `work-item-modal.component.ts` (+ `.html`) — inline create, suggestions from `getProjectLabels()`, 5-item cap, dedupe — depends on T013, T055; makes T043 pass
- [ ] T057 [US5] Add `<app-label-chip>` to board cards, project-detail list rows and tree rows, and the detail view — `frontend/src/app/projects/board/board-card.component.html`, `frontend/src/app/projects/project-detail/project-detail.component.html`, `frontend/src/app/projects/work-item-detail/work-item-detail.component.html` — depends on T054, T055
- [ ] T058 [US5] Add the label filter dropdown to `ProjectDetailComponent`'s filters row, sourced from `getProjectLabels()`, combined into `currentFilter()` — `frontend/src/app/projects/project-detail/project-detail.component.ts` (+ `.html`) — depends on T055; makes T044 pass

**Checkpoint**: Labels work end-to-end — create, suggest, display, filter —
quickstart.md section 5 passes; all five user stories are now complete,
matching spec.md's Success Check.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [ ] T059 [P] Add a "Feature 007: Work Item Modal & Quick Creation" entry to `README.md`'s "What I learned" log, matching Features 001–006's style, per the constitution's Definition of Done item 5
- [ ] T060 Run the full backend suite (`cd backend/TaskFlow.Api.Tests && dotnet test`) and confirm 100% pass
- [ ] T061 Run the full frontend suite (`cd frontend && npm test`) and confirm 100% pass
- [ ] T062 Cross-cutting checkpoint: grep the frontend for any leftover reference to the removed `WorkItemFormComponent` or a `routerLink` to `.../work-items/new` or `.../work-items/:id/edit` outside `app.routes.ts`'s new `redirectTo` entries — confirm none remain
- [ ] T063 Walk through `quickstart.md` sections 1–5 manually in a running app (backend + `ng serve`), including the direct-API date-range check (section 3.3) and the old-route redirect check (section 1.9)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — run first
- **Foundational (Phase 2)**: Depends on Setup — blocks US3 and US5 only (see note above); US1/US2/US4 do not depend on it
- **US1 (Phase 3)**: Depends on Setup only — the true MVP slice and the
  largest, since every other story extends the modal it introduces
- **US2 (Phase 4)**: Depends on US1 (`WorkItemModalComponent` must exist)
- **US3 (Phase 5)**: Depends on US1 (modal must exist) and Foundational
  (schema for `StartDate`)
- **US4 (Phase 6)**: Depends on US1 (modal) and US3 ("dates" in the retained
  field set includes the new Start date, per spec's User Story 4)
- **US5 (Phase 7)**: Depends on US1 (modal) and Foundational (schema for
  `Label`/`WorkItemLabel`) — independent of US2/US3/US4 otherwise
- **Polish (Phase 8)**: Depends on all five user stories being complete

### Within Each User Story

- Tests written and confirmed failing before their implementation task
  (Constitution Principle I)
- Backend: entity/DTO changes before service changes; service changes before
  controller actions
- Frontend: service/type changes before the component fields that consume
  them
- `WorkItemModalComponent` (US1) is extended in place by US2–US5, never
  rebuilt per story

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel
- Within Foundational: T004/T005 (new entity files) can run in parallel
- Within US1: T008–T012 (tests, same new spec file, independent cases) are
  parallel; T015/T016/T017 (different component files) are parallel once
  T013 exists
- Within US3: T023–T025 (tests) are parallel; T028 (DTOs) is parallel to
  T026/T027
- Within US5: T038–T044 (tests) are parallel; T047/T048/T049 (DTO files) are
  parallel; T054 (new `LabelChipComponent`) is parallel to backend work
- US2 and US3 can be implemented in parallel by different contributors once
  US1 is done (US2 has no Foundational dependency); US5 can proceed in
  parallel with US2/US3/US4 once US1 and Foundational are both done

---

## Parallel Example: User Story 1

```bash
# Tests (independent cases in the same new spec file):
Task: "Create-mode pre-selection test in frontend/src/app/projects/work-item-modal/work-item-modal.component.spec.ts"
Task: "Edit-mode pre-population test in the same spec file"
Task: "Dirty-flag/confirm-discard test in the same spec file"
Task: "In-dialog error display test in the same spec file"
Task: "Success toast + onSaved + close test in the same spec file"

# Entry-point wiring (independent component files, once T013 exists):
Task: "Board '+' opens modal in frontend/src/app/projects/board/board.component.ts"
Task: "Add child / Edit open modal in frontend/src/app/projects/work-item-detail/work-item-detail.component.ts"
Task: "New work item / Edit open modal in frontend/src/app/projects/project-detail/project-detail.component.ts"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 3: User Story 1 (modal replaces every full-page entry
   point) — Foundational (Phase 2) is not required for US1 and can be
   deferred until US3/US5 begin
3. **STOP and VALIDATE**: quickstart.md section 1 — modal opens from every
   entry point, refreshes views in place, old routes are gone
4. Review with the maintainer before continuing (Constitution Principle VIII)

### Incremental Delivery

1. Setup → baseline confirmed
2. US1 → modal replaces every full-page create/edit entry point → demo (MVP!)
3. US2 → "Assign to me" → demo
4. Foundational → schema for Start date + Labels lands
5. US3 → Start date, validated client + server → demo
6. US4 → "Create another" batch entry → demo
7. US5 → Labels: create, suggest, display, filter → demo (feature-complete,
   matches spec.md's Success Check)
8. Phase 8 → docs + full regression + quickstart walkthrough

### Commit Convention

One commit per user story, `feat: US<N> <summary>` with passing test count,
e.g. `feat: US1 work item modal replaces full-page forms (297/297 backend,
198/198 frontend tests pass)`. Setup and Foundational commit as `chore:`;
Polish as `docs:`/`chore:` per file.

---

## Notes

- [P] tasks have no code dependency on an incomplete task; same-file test
  additions are still marked [P] when the test cases are independent of each
  other, matching Feature 006's convention
- Tests must be written and observed failing before their implementation
  task (Constitution Principle I)
- Commit after each user story completes
- Stop at any checkpoint to validate that story independently before
  continuing
- Foundational (Phase 2) is unusually "optional-until-needed" for this
  feature — it's real, blocking work for US3/US5, but US1/US2/US4 can proceed
  first if that better suits delivery order; the phase ordering above (US1 →
  US2 → Foundational → US3 → US4 → US5) reflects one reasonable sequencing,
  not the only valid one
