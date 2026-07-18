---

description: "Task list for Work Item Hierarchy"
---

# Tasks: Work Item Hierarchy

**Input**: Design documents from `/specs/003-work-item-hierarchy/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md (all present)

**Tests**: Included and REQUIRED — the project constitution (Principle I, NON-NEGOTIABLE) mandates tests written before implementation for every feature, and the Development Workflow section requires every protected endpoint's authorization logic to be covered by an integration test for both the allowed and denied path. Test tasks below MUST be completed, and MUST fail, before their corresponding implementation tasks.

**Organization**: Tasks are grouped by user story (from spec.md) to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1-US5)
- File paths are exact, per `plan.md`'s Project Structure

## Path Conventions

Web application, same split as Features 001/002: `backend/TaskFlow.Api/`
(+ `backend/TaskFlow.Api.Tests/`) and `frontend/src/app/`.

## Note: cascade-delete-with-confirmation is scoped under US3

The spec's Acceptance Criteria/Edge Cases describe cascade delete with a
descendant-count confirmation, but it isn't its own numbered user story.
It depends on `WorkItemDetailDto.totalDescendantCount` (research.md §6),
which US3 (Detail Navigation) introduces, so its tasks live in Phase 4
alongside US3 rather than as a separate phase.

---

## Phase 1: Foundational (Blocking Prerequisites)

**Purpose**: The `WorkItem.ParentWorkItemId` schema every user story depends on. No separate Setup phase — this feature extends Feature 002's existing `WorkItem` entity rather than scaffolding new projects.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T001 Add `ParentWorkItemId` (`int?`), `ParentWorkItem` (`WorkItem?`), and `Children` (`ICollection<WorkItem>`) to the `WorkItem` entity in `backend/TaskFlow.Api/Data/Entities/WorkItem.cs` per `data-model.md`
- [X] T002 Update `backend/TaskFlow.Api/Data/AppDbContext.cs`: configure `WorkItem`'s self-referencing `ParentWorkItem`/`Children` relationship as `DeleteBehavior.Restrict` (research.md §1 — SQL Server rejects a cascading self-referencing FK) and add an index on `ParentWorkItemId` — depends on T001
- [X] T003 Generate the EF Core migration `AddWorkItemHierarchy` in `backend/TaskFlow.Api/Data/Migrations/` via `dotnet ef migrations add AddWorkItemHierarchy --project backend/TaskFlow.Api`, and apply it to the local dev database via `dotnet ef database update` — depends on T002. **Confirmed clean**: migration applied with `ON DELETE NO ACTION` on the self-referencing FK (no cascade-path error), confirming research.md §1's Restrict design. 137/137 pre-existing backend tests still pass, 0 regressions.

**Checkpoint**: `WorkItems.ParentWorkItemId` exists — user story implementation can now begin.

---

## Phase 2: User Story 1 - Create a Work Item With a Valid Parent (Priority: P1) 🎯 MVP

**Goal**: Any signed-in user creating a work item can pick a parent from a list of valid candidates only, with every rule enforced server-side regardless of the UI.

**Independent Test**: Create an Epic, a Story under it, a Task under the Story, and a SubTask under the Task, verifying each parent picker only lists legal candidates.

### Tests for User Story 1 ⚠️

> Write these tests FIRST; confirm they FAIL before implementation (constitution Principle I)

- [ ] T004 [P] [US1] Unit tests for `WorkItemService.CreateAsync`'s parent validation in `backend/TaskFlow.Api.Tests/Services/WorkItemServiceTests.cs`: Epic rejects any `ParentWorkItemId`; Story requires an Epic parent; Task's parent is optional but must be a Story when set; SubTask requires a Task parent; parent from a different project is rejected; unknown `ParentWorkItemId` is rejected. (Self-parent/cycle rejection cannot be exercised here — a newly-created item has no id yet to reference; that case is genuinely testable only once an item exists, so it's covered by US4's `UpdateAsync` tests, T038/T040, per research.md §2.)
- [ ] T005 [P] [US1] Unit tests for `WorkItemService.GetParentCandidatesAsync` in `backend/TaskFlow.Api.Tests/Services/WorkItemServiceTests.cs`: returns only same-project items of the required parent type for the given `type`; returns an empty list for `type=Epic`
- [ ] T006 [P] [US1] Integration tests for `POST /api/projects/{projectId}/work-items`'s new parent-validation cases in `backend/TaskFlow.Api.Tests/Integration/WorkItemsEndpointsTests.cs`: `201` with a valid parent at each level of the chain, `400` for each rule violation (wrong type, cross-project, missing-when-required, present-when-forbidden)
- [ ] T007 [P] [US1] Integration tests for `GET /api/projects/{projectId}/work-items/parent-candidates?type=` in `backend/TaskFlow.Api.Tests/Integration/WorkItemsEndpointsTests.cs`: `200` with correct candidates per type, `200` empty array for `type=Epic`, `400` invalid/missing `type`, `404` unknown project, `401` with no token (allowed + denied paths, matching Feature 002's equivalent new-GET-endpoint tests)
- [ ] T008 [P] [US1] Vitest tests for the parent picker (create mode) in `frontend/src/app/projects/work-item-form/work-item-form.component.spec.ts`: picker's candidate list refetches and changes when `Type` changes, is required for `SubTask`, and is disabled/hidden for `Epic`

### Implementation for User Story 1

- [ ] T009 [P] [US1] Add `WorkItemLookupItemDto` in `backend/TaskFlow.Api/Dtos/WorkItemLookupItemDto.cs` (Id, Title) per `data-model.md`
- [ ] T010 [P] [US1] Add `ParentWorkItemId` (`int?`) to `WorkItemRequest` in `backend/TaskFlow.Api/Dtos/WorkItemRequest.cs`
- [ ] T011 [P] [US1] Add `ParentWorkItemId` (`int?`) to `WorkItemDto` in `backend/TaskFlow.Api/Dtos/WorkItemDto.cs`
- [ ] T012 [US1] Add hierarchy exceptions (`EpicCannotHaveParentException`, `ParentRequiredException`, `InvalidParentTypeException`, `ParentWorkItemNotFoundException`, `ParentMustBeSameProjectException`) in `backend/TaskFlow.Api/Services/WorkItemExceptions.cs` — depends on T009-T011
- [ ] T013 [US1] Implement a `RequiredParentType(WorkItemType)` helper and a shared parent-validation routine in `backend/TaskFlow.Api/Services/WorkItemService.cs` (data-model.md's Hierarchy rules table); wire it into `CreateAsync` — depends on T012
- [ ] T014 [US1] Implement `WorkItemService.GetParentCandidatesAsync` in `backend/TaskFlow.Api/Services/WorkItemService.cs` (project-scoped, type-filtered per the Hierarchy rules table — research.md §2, no exclude-self/descendants logic needed) — depends on T009
- [ ] T015 [US1] Implement `WorkItemsController.GetParentCandidates` (`GET /api/projects/{projectId}/work-items/parent-candidates`) and extend `Create`'s catch blocks for the new exceptions in `backend/TaskFlow.Api/Controllers/WorkItemsController.cs` — depends on T013, T014
- [ ] T016 [US1] Add `getParentCandidates()` to `frontend/src/app/projects/work-items.service.ts`; add `parentWorkItemId` to the `WorkItem`/`WorkItemRequest` interfaces — depends on T015
- [ ] T017 [US1] Extend the `work-item-form` component (create mode) with a parent picker that refetches candidates whenever `Type` changes, using native `<select>` with `[selected]`-per-`<option>` (Feature 002's research.md §6 convention) — depends on T016, T008

**Checkpoint**: User Story 1 is independently testable — a full four-level chain can be created with server- and UI-enforced parent rules.

---

## Phase 3: User Story 2 - View the Project's Work Items as an Indented Hierarchy Tree (Priority: P1)

**Goal**: A project's work items view shows children indented under parents with expand/collapse and per-parent done/total counts, while standalone items list normally.

**Independent Test**: Open a project already containing a full hierarchy chain (seeded via US1) and confirm the tree renders indentation, expand/collapse, and correct child counts.

### Tests for User Story 2 ⚠️

- [ ] T018 [P] [US2] Unit tests for `WorkItemService.GetTreeAsync` in `backend/TaskFlow.Api.Tests/Services/WorkItemServiceTests.cs`: correct nesting for a multi-level chain, `directChildrenCount`/`directChildrenDoneCount` count only direct children, standalone items appear as top-level nodes with empty `children`, ordering by `UpdatedAt` descending at every level
- [ ] T019 [P] [US2] Integration tests for `GET /api/projects/{projectId}/work-items/tree` in `backend/TaskFlow.Api.Tests/Integration/WorkItemsEndpointsTests.cs`: `200` with correctly nested shape, `404` unknown project, `401` with no token (allowed + denied paths, matching Feature 002's equivalent new-GET-endpoint tests)
- [ ] T020 [P] [US2] Vitest tests added to `frontend/src/app/projects/project-detail/project-detail.component.spec.ts`: Tree/Flat toggle switches views; tree renders indentation per level; expand/collapse hides/shows a parent's children; each parent row shows "n/m done"; standalone items render at the top level alongside hierarchical ones

### Implementation for User Story 2

- [ ] T021 [US2] Add `WorkItemTreeNodeDto` in `backend/TaskFlow.Api/Dtos/WorkItemTreeNodeDto.cs` (recursive `children` array) per `data-model.md`
- [ ] T022 [US2] Implement `WorkItemService.GetTreeAsync` in `backend/TaskFlow.Api/Services/WorkItemService.cs` (load the project's items once, group by `ParentWorkItemId` in memory, build nested nodes with direct-child/done counts, order by `UpdatedAt` descending — research.md §4, §7) — depends on T021
- [ ] T023 [US2] Implement `WorkItemsController.GetTree` (`GET /api/projects/{projectId}/work-items/tree`) in `backend/TaskFlow.Api/Controllers/WorkItemsController.cs` — depends on T022
- [ ] T024 [US2] Add `getWorkItemsTree()` to `frontend/src/app/projects/work-items.service.ts` — depends on T023
- [ ] T025 [US2] Add a Tree/Flat view toggle to `project-detail` (+ template): Tree is the new indented/expand-collapse rendering with per-parent counts; Flat is Feature 002's existing paginated `mat-table` list, unchanged — depends on T024, T020

**Checkpoint**: User Stories 1-2 work — a hierarchy can be built and seen as a tree.

---

## Phase 4: User Story 3 - Navigate the Tree from a Work Item's Detail View (Priority: P2)

**Goal**: A work item's detail view shows its parent as a link (when one exists) and its direct children with title/type/status/assignee, each linking onward; creating a child from this view pre-selects the current item as parent. Deleting an item with descendants states the total descendant count before removing the whole subtree in one operation.

**Independent Test**: Open the detail page of an item with both a parent and children; confirm the parent link navigates correctly and each child row links to its own detail view.

### Tests for User Story 3 ⚠️

- [ ] T026 [P] [US3] Unit tests for the detail projection in `backend/TaskFlow.Api.Tests/Services/WorkItemServiceTests.cs`: `parentWorkItemId`/`parentTitle` null when no parent, populated when one exists; `children` lists direct children only (title/type/status/assigneeName); `totalDescendantCount` sums every level, not just direct children
- [ ] T027 [P] [US3] Unit tests for `WorkItemService.DeleteAsync`'s cascade behavior in `backend/TaskFlow.Api.Tests/Services/WorkItemServiceTests.cs`: deleting an item with descendants removes the entire subtree in one call; siblings and ancestors are untouched; the authorization check (creator/Manager/Admin) applies only to the item being deleted, not to each descendant
- [ ] T028 [P] [US3] Integration tests for `GET /api/work-items/{id}`'s enriched response in `backend/TaskFlow.Api.Tests/Integration/WorkItemsEndpointsTests.cs`: response includes `parentWorkItemId`/`parentTitle`/`children`/`totalDescendantCount` as specified in `contracts/work-item-hierarchy-api.md`
- [ ] T029 [P] [US3] Integration tests for `DELETE /api/work-items/{id}`'s cascade behavior in `backend/TaskFlow.Api.Tests/Integration/WorkItemsEndpointsTests.cs`: deleting a parent with descendants returns `204` and removes the whole subtree; `403`/`404` unchanged from Feature 002
- [ ] T030 [P] [US3] Vitest tests for a new `work-item-detail` component in `frontend/src/app/projects/work-item-detail/work-item-detail.component.spec.ts`: renders the parent as a link when present and nothing when absent; lists children (title/type/status/assignee) each linking to that child's detail page; starting a new child pre-selects this item as parent; the delete confirmation states `totalDescendantCount` when greater than zero

### Implementation for User Story 3

- [ ] T031 [P] [US3] Add `WorkItemChildDto` (`backend/TaskFlow.Api/Dtos/WorkItemChildDto.cs`) and `WorkItemDetailDto` (`backend/TaskFlow.Api/Dtos/WorkItemDetailDto.cs`) per `data-model.md`
- [ ] T032 [US3] Implement a recursive descendant-id collection helper in `backend/TaskFlow.Api/Services/WorkItemService.cs`, shared by the detail projection's `totalDescendantCount` and `DeleteAsync`'s cascade (research.md §1, §6) — depends on T031
- [ ] T033 [US3] Change `WorkItemService.GetByIdAsync` to return `WorkItemDetailDto` via a new `ToDetailDtoAsync` (parent lookup, direct-children projection, `totalDescendantCount`) — depends on T032
- [ ] T034 [US3] Update `WorkItemService.DeleteAsync` to collect all descendant ids via the T032 helper and `RemoveRange` the entire subtree in one `SaveChangesAsync` call — depends on T032
- [ ] T035 [US3] Update `WorkItemsController.Get`'s return type to `WorkItemDetailDto` (no route change) — depends on T033
- [ ] T036 [US3] Build the `work-item-detail` component + template in `frontend/src/app/projects/work-item-detail/`, reusing `project-detail`'s existing edit/delete permission logic — depends on T030, T035
- [ ] T036a [US3] Wire the `work-item-detail` component's "create child" action (FR-019): a link/button, shown only when this item's type can legally have children (Epic, Story, or Task), that navigates to work-item creation with this item pre-selected as parent — e.g. a `parentWorkItemId` route/query param that `work-item-form`'s create mode reads and pre-fills (reusing T017's parent-picker population, just with an initial value) — depends on T036, T017
- [ ] T037 [US3] Add `getWorkItemDetail()` to `work-items.service.ts`; add the `projects/:projectId/work-items/:id` route in `frontend/src/app/app.routes.ts` (registered after the existing `.../work-items/new` and `.../work-items/:id/edit` routes); link work item titles (in both the flat list and the tree view) to the new detail page; update delete confirmations project-wide to show `totalDescendantCount` when greater than zero — depends on T034, T036

**Checkpoint**: User Stories 1-3 work — full tree creation, visibility, detail navigation, and safe cascade delete with an accurate confirmation.

---

## Phase 5: User Story 4 - Reassign or Clear an Existing Item's Parent (Priority: P2)

**Goal**: An item's parent can be changed or cleared subject to the same hierarchy rules and edit permissions as Feature 002; a `Type` change that would invalidate the item's existing parent or children is refused.

**Independent Test**: Change a Task's parent to a different Story (succeeds); attempt to change it to an Epic (refused).

### Tests for User Story 4 ⚠️

- [ ] T038 [P] [US4] Unit tests for `WorkItemService.UpdateAsync`'s parent reassignment in `backend/TaskFlow.Api.Tests/Services/WorkItemServiceTests.cs`: valid reparent within the same project succeeds; the same rule violations as `CreateAsync` (T004) are rejected; clearing an optional Task parent succeeds; attempting to set an item as its own parent (self-reference) is rejected — this is the one case only reachable once an item already has an id, and is caught by the same type check as every other violation (an item's own type never equals its required-parent type — research.md §2), not a special-cased algorithm
- [ ] T039 [P] [US4] Unit tests for `WorkItemService.UpdateAsync`'s type-change guard in `backend/TaskFlow.Api.Tests/Services/WorkItemServiceTests.cs`: refuses a type change when the item's existing parent's type no longer matches the new type's required parent type; refuses a type change when any existing child's required-parent type no longer matches the new type; allows a type change with no such conflict
- [ ] T040 [P] [US4] Integration tests for `PUT /api/work-items/{id}`'s reparent and type-change-guard cases in `backend/TaskFlow.Api.Tests/Integration/WorkItemsEndpointsTests.cs`, including a direct-API attempt to set an item's `parentWorkItemId` to its own id, asserting `400` with a `ProblemDetails` body naming the violated rule (spec.md Edge Cases; quickstart.md §4)
- [ ] T041 [P] [US4] Vitest tests for edit mode's parent picker in `work-item-form.component.spec.ts`: pre-fills the item's current parent; changing/clearing it submits correctly

### Implementation for User Story 4

- [ ] T042 [US4] Extend `WorkItemService.UpdateAsync` to reuse US1's parent-validation routine (T013) for the incoming `ParentWorkItemId` — depends on T013
- [ ] T043 [US4] Add the type-change guard to `WorkItemService.UpdateAsync` (data-model.md's Hierarchy rules table checked against the item's *current* parent and children — research.md §3), plus `TypeChangeInvalidatesParentException`/`TypeChangeInvalidatesChildrenException` in `WorkItemExceptions.cs` — depends on T042
- [ ] T044 [US4] Extend `WorkItemsController.Update`'s catch blocks for the new exceptions in `backend/TaskFlow.Api/Controllers/WorkItemsController.cs` — depends on T043
- [ ] T045 [US4] Extend `work-item-form` edit mode: pre-fill the current parent, refresh candidates on `Type` change (reusing T017's logic), submit `parentWorkItemId` — depends on T044, T041

**Checkpoint**: User Stories 1-4 work — reorganization is fully supported without breaking the hierarchy's integrity.

---

## Phase 6: User Story 5 - Use the Flat Filtered List Regardless of Tree Position (Priority: P3)

**Goal**: Feature 002's flat filtered/searched list keeps working unchanged once items have parents.

**Independent Test**: Run Feature 002's existing filter/search scenarios against a project that now contains hierarchical items and confirm results are unchanged in correctness.

### Tests for User Story 5 ⚠️

- [ ] T046 [P] [US5] Extend `backend/TaskFlow.Api.Tests/Services/WorkItemServiceTests.cs` and `Integration/WorkItemsEndpointsTests.cs`'s existing `GetWorkItemsAsync`/`GET /api/projects/{projectId}/work-items` filter/search/pagination tests with a mixed dataset (parented and standalone items across all four types) — confirms Feature 002's filtering logic (unchanged in this feature) still returns correct results regardless of tree position
- [ ] T047 [P] [US5] Extend `project-detail.component.spec.ts`'s existing Flat-view filter/search tests to run against a mix of parented and standalone items, confirming the Flat view still renders unindented and filters/searches correctly

### Implementation for User Story 5

- [ ] T048 [US5] No new production code is expected — `GetWorkItemsAsync` and the Flat view are untouched by Features US1-US4 per plan.md/research.md §4. If T046/T047 surface a gap (e.g., a template regression from `WorkItemDto`'s new field), fix it here.

**Checkpoint**: All five user stories are independently functional — the feature described in `spec.md` is complete end-to-end, and Feature 002's flat list/filter/search remains fully intact.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Feature-wide verification against the constitution's Definition of Done

- [ ] T049 [P] Review all new/changed C# files for the constitution's required educational comments — especially the self-referencing-cascade restriction (research.md §1), the rank-argument cycle proof (research.md §2), and validating a change against existing state rather than just the incoming request (research.md §3) — per plan.md's "Concepts You Will Learn"
- [ ] T050 [P] Extend Feature 002's `ProblemDetails`-shape regression test to cover this feature's new `400` error cases (hierarchy rule violations, type-change guard)
- [ ] T051 Verify `dotnet ef database update` applies `AddWorkItemHierarchy` cleanly to a fresh database; confirm zero pending model changes via a throwaway `dotnet ef migrations add`
- [ ] T052 Add a "What I learned" entry for this feature to the project `README.md`
- [ ] T053 Run all `quickstart.md` validation scenarios end-to-end manually (REST client + real browser)
- [ ] T054 Confirm zero build warnings (`dotnet build`) and all backend/frontend tests pass

---

## Dependencies & Execution Order

### Phase Dependencies

- **Foundational (Phase 1)**: No dependencies — start immediately. BLOCKS all user stories.
- **User Story 1 (Phase 2)**: Depends only on Foundational.
- **User Story 2 (Phase 3)**: Depends on Foundational; easiest to exercise once US1 exists (need a hierarchy to render), but its own files (`WorkItemTreeNodeDto`, `GetTreeAsync`, the tree endpoint) are independent of US1's.
- **User Story 3 (Phase 4)**: Depends on Foundational; shares `WorkItemService.cs`/`WorkItemsController.cs` with US1/US2, and its parent-validation reuse (none needed here) is independent — but its cascade-delete helper (T032) is standalone.
- **User Story 4 (Phase 5)**: Depends on Foundational and directly on US1's parent-validation routine (T013, reused by T042) — do US1 first.
- **User Story 5 (Phase 6)**: Depends on Foundational and on US1-US4 existing (it's a regression check against their combined output), and on US2's Tree/Flat toggle (T025) existing to verify Flat still renders correctly.
- **Polish (Phase 7)**: Depends on all five user stories being complete.

**Note on shared files**: `WorkItemService.cs`/`WorkItemsController.cs`/`WorkItemExceptions.cs` are extended across US1/US2/US3/US4 rather than recreated; `work-item-form` is extended across US1/US4; `project-detail` is extended across US2/US3. Each story only adds *new* methods/modes to these files, so stories remain functionally independent once their prerequisites exist — sequence same-file tasks rather than editing them simultaneously.

### Within Each User Story

- Tests are written and confirmed failing before implementation tasks (constitution Principle I).
- DTOs before service logic; service logic before controller endpoints; core logic before UI wiring.

### Parallel Opportunities

- Foundational: T001 then T002 (sequential, same reasoning as Feature 002's cascade-path config) then T003.
- Tests within a story: e.g. T004-T008 (US1), T018-T020 (US2), T026-T030 (US3), T038-T041 (US4), T046-T047 (US5) in parallel — different files.
- Once Foundational is done, US1 should go first (US4 depends on it); US2 and US3 can then proceed in parallel with each other and with US4 once US1 exists.

---

## Parallel Example: User Story 1

```bash
# Tests for User Story 1 together:
Task: "Unit tests for WorkItemService.CreateAsync's parent validation in backend/TaskFlow.Api.Tests/Services/WorkItemServiceTests.cs"
Task: "Unit tests for WorkItemService.GetParentCandidatesAsync in backend/TaskFlow.Api.Tests/Services/WorkItemServiceTests.cs"
Task: "Integration tests for POST /api/projects/{projectId}/work-items's new parent-validation cases in backend/TaskFlow.Api.Tests/Integration/WorkItemsEndpointsTests.cs"
Task: "Integration tests for GET /api/projects/{projectId}/work-items/parent-candidates in backend/TaskFlow.Api.Tests/Integration/WorkItemsEndpointsTests.cs"
Task: "Vitest tests for the parent picker (create mode) in frontend/src/app/projects/work-item-form/work-item-form.component.spec.ts"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Foundational
2. Complete Phase 2: User Story 1 (create with valid parent)
3. **STOP and VALIDATE**: build a full Epic→Story→Task→SubTask chain via the API/UI, confirm every rule violation is refused
4. Per constitution Principle VIII (Human in the Loop): pause here for maintainer review before continuing

### Incremental Delivery

1. Foundational → foundation ready
2. User Story 1 → validate → review checkpoint (MVP: create with valid parent)
3. User Story 2 → validate → review checkpoint (+ tree view)
4. User Story 3 → validate → review checkpoint (+ detail navigation, safe cascade delete)
5. User Story 4 → validate → review checkpoint (+ reparent, type-change guard)
6. User Story 5 → validate → review checkpoint (+ flat-list regression confirmed)
7. Polish (Phase 7)

**Note on feature size**: this feature's total new/changed file count (~24 across backend + frontend + tests, see plan.md's Constitution Check) exceeds the constitution's "~15 files, readable in one sitting" guideline (Principle VII — a target, not NON-NEGOTIABLE), for the reason plan.md already documents: the hierarchy concept isn't separable into smaller independently-valuable slices. Treat each user-story checkpoint above as a review pause, per Principle VIII.

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps each task to its user story for traceability
- Tests MUST be written first and MUST fail before their implementation tasks (constitution Principle I)
- Cascade-delete-with-confirmation (T031-T034, T037) is scoped under US3 per the note at the top of this file, not as its own phase
- FR-019 ("pre-select parent when creating a child from a detail view") is implemented by T036a, added during `/speckit-analyze` triage alongside T036 rather than folded silently into T037's broader wiring task
- Commit after each user story or logical group, per the constitution's Commit Convention (Conventional Commits, `feat: US<n> ...` referencing the story and passing test count)
- Stop at any checkpoint to validate a story independently, per the constitution's Human-in-the-Loop principle
