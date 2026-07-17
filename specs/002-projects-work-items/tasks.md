---

description: "Task list for Projects & Work Items"
---

# Tasks: Projects & Work Items

**Input**: Design documents from `/specs/002-projects-work-items/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md (all present)

**Tests**: Included and REQUIRED — the project constitution (Principle I, NON-NEGOTIABLE) mandates tests written before implementation for every feature, and the Development Workflow section requires every protected endpoint's authorization logic to be covered by an integration test for both the allowed and denied path. Test tasks below MUST be completed, and MUST fail, before their corresponding implementation tasks.

**Organization**: Tasks are grouped by user story (from spec.md) to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1-US6)
- File paths are exact, per `plan.md`'s Project Structure

## Path Conventions

Web application, same split as Feature 001: `backend/TaskFlow.Api/`
(+ `backend/TaskFlow.Api.Tests/`) and `frontend/src/app/`.

## ⚠️ Frontend styling: use Angular Material (superseding research.md §6)

A dedicated styling pass (separate commit, after this file was originally
written) set up Angular Material properly — `provideAnimationsAsync()` in
`app.config.ts`, an app-wide layout shell (`app.html`/`app.css`,
`.app-content` + `.auth-page` in `styles.css`) — and restyled every Feature
001 page (`login`, `register`, `home`, the `header` toolbar, `users-list`)
using `MatCardModule`, `MatFormFieldModule`/`MatInputModule`,
`MatButtonModule`, `MatToolbarModule`, and `MatTableModule`. This
**supersedes research.md §6**'s original decision to continue Feature 001's
plain-HTML pattern — that decision predates this styling pass and no longer
reflects the codebase.

**Every frontend task still to be built** (anything in US4-US6 and Phase
8's header-nav update — i.e., new template code, not yet written) should
follow the now-established patterns instead of plain HTML:
- `mat-card` for page/section containers (see `home.component.html`)
- `mat-form-field` + `matInput` for text/textarea inputs, with `mat-error`
  for field-level validation messages (see `login`/`register`)
- `mat-table` for the projects list and the work-item list (see
  `users-list.component.html` for the `matColumnDef` pattern)
- `mat-toolbar` for any additional top-level chrome
- `mat-button`/`mat-flat-button`/`mat-stroked-button` for actions
- **Exception, deliberately**: keep the `<select>` elements for
  status/type/priority/assignee dropdowns as **plain native `<select>`**,
  not `mat-select` — every dropdown in this feature already uses the
  `[selected]`-per-`<option>` pattern (research.md §6) to sidestep a real
  Feature 001 bug, and `mat-select` doesn't expose that same native
  element for tests to drive the way the existing `WorkItemsService`/
  `UsersService` component tests currently do.

**Already-shipped exception, resolved**: `project-form` (T012),
`projects-list` (T020), `project-detail` (T021), and `work-item-form`
(T033) were all built *before* the styling pass, using plain HTML — they
are not automatically retroactively Material-ized just because this note
exists. **T058a** (Phase 8) schedules that retrofit explicitly, so the
feature doesn't ship in a mixed style. Until T058a runs, US4-US6's
*extensions* to these same files (edit mode, delete confirmation, the
work-item list itself) may land on top of the current plain-HTML
structure — T058a will restyle the whole file in one pass rather than
patching it twice.

---

## Phase 1: Foundational (Blocking Prerequisites)

**Purpose**: The `Project`/`WorkItem` schema every user story depends on. No separate Setup phase — this feature adds to Feature 001's existing backend/frontend projects rather than scaffolding new ones.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T001 Create the `Project` entity in `backend/TaskFlow.Api/Data/Entities/Project.cs` per `data-model.md` (Id, Name, Description, CreatedByUserId, CreatedAt)
- [X] T002 [P] Create the `WorkItem` entity and its `WorkItemType`/`WorkItemPriority`/`WorkItemStatus` enums in `backend/TaskFlow.Api/Data/Entities/WorkItem.cs` per `data-model.md` (colocated with the entity, matching how `Role` lives in `User.cs` per Feature 001's precedent)
- [X] T003 Update `backend/TaskFlow.Api/Data/AppDbContext.cs`: add `Projects`/`WorkItems` `DbSet`s, configure `Project`→`WorkItem` as `DeleteBehavior.Cascade`, every foreign key pointing at `User` as `DeleteBehavior.Restrict` (research.md §2 — required to avoid SQL Server's multiple-cascade-paths error), a unique index on `Project.Name`, an index on `WorkItem.ProjectId`, and `HasConversion<string>()` for the three new enums — depends on T001, T002
- [X] T004 Generate the EF Core migration `AddProjectsAndWorkItems` in `backend/TaskFlow.Api/Data/Migrations/` via `dotnet ef migrations add AddProjectsAndWorkItems --project backend/TaskFlow.Api`, and apply it to the real local dev database via `dotnet ef database update` — depends on T003. **Confirmed clean**: migration applies with no SQL Server cascade-path error, confirming the Restrict/Cascade design from research.md §2 is correct.

**Checkpoint**: `Projects` and `WorkItems` tables exist — user story implementation can now begin.

---

## Phase 2: User Story 1 - Create a Project (Priority: P1) 🎯 MVP

**Goal**: A Manager or Admin can create a project with a name and description.

**Independent Test**: Sign in as a Manager, submit a unique project name and description, and confirm it appears in the project list showing name, creator, and created date.

### Tests for User Story 1 ⚠️

> Write these tests FIRST; confirm they FAIL before implementation (constitution Principle I)

- [X] T005 [P] [US1] Unit tests for `ProjectService.CreateAsync` in `backend/TaskFlow.Api.Tests/Services/ProjectServiceTests.cs`: creates a project recording creator and timestamp, rejects a duplicate name case-insensitively. **Confirmed RED**: compile errors (`ProjectService`/`ProjectRequest` didn't exist).
- [X] T006 [P] [US1] Integration tests for `POST /api/projects` in `backend/TaskFlow.Api.Tests/Integration/ProjectsEndpointsTests.cs`: `201` on success, `409` on duplicate name, `400` on invalid name/description, `403` for a non-Manager/Admin caller (allowed + denied paths). Promotes a fresh registrant to Manager via the seeded Admin, then re-logs-in (role only takes effect on next token, per Feature 001).
- [X] T007 [P] [US1] Vitest tests for the project-create flow in `frontend/src/app/projects/project-form/project-form.component.spec.ts`: valid submit navigates to the new project, duplicate-name error displayed. **Confirmed RED**: `Cannot find module './project-form.component'` build error.

### Implementation for User Story 1

- [X] T008 [US1] Create `ProjectRequest` DTO with data-annotation validation (Name 3-100 chars, Description optional ≤2000 chars) in `backend/TaskFlow.Api/Dtos/ProjectRequest.cs` — shared by create and edit (research.md, plan.md Dtos note)
- [X] T009 [US1] Create `ProjectDetailDto` in `backend/TaskFlow.Api/Dtos/ProjectDetailDto.cs` (Id, Name, Description, CreatedByName, CreatedAt, TotalWorkItemCount)
- [X] T010 [US1] Implement `ProjectService.CreateAsync` in `backend/TaskFlow.Api/Services/ProjectService.cs` (case-insensitive uniqueness check, records creator/timestamp) — depends on T008, T009; register `ProjectService` as Scoped in `Program.cs`. **Confirmed GREEN**: 2/2 new `ProjectServiceTests` pass.
- [X] T011 [US1] Implement `ProjectsController.Create` (`POST /api/projects`) with `[Authorize(Roles = "Manager,Admin")]` in `backend/TaskFlow.Api/Controllers/ProjectsController.cs` — depends on T010. **Confirmed GREEN**: 59/59 backend tests pass (was 52; +7 new, 0 regressions).
- [X] T012 [US1] Build the Angular `project-form` component (create mode) in `frontend/src/app/projects/project-form/` (+ template) — depends on T007
- [X] T013 [US1] Add `projects.service.ts` (`frontend/src/app/projects/projects.service.ts`) with a `createProject()` method; wire the form's submit to call it and navigate to the new project's detail page on success — depends on T012. **Confirmed GREEN**: 37/37 frontend tests pass (was 35; +2 new, 0 regressions).

**Checkpoint**: User Story 1 is independently testable — a Manager can create a project.

---

## Phase 3: User Story 2 - View Projects and Their Work Items (Priority: P1)

**Goal**: Any signed-in user can see the project list and open a project to view its (possibly empty) work items.

**Independent Test**: Sign in as any role, view the project list, and open a project to see its work-item area (empty state if none exist yet).

### Tests for User Story 2 ⚠️

- [X] T014 [P] [US2] Unit tests for `ProjectService.GetProjectsAsync`/`GetProjectByIdAsync` in `backend/TaskFlow.Api.Tests/Services/ProjectServiceTests.cs`: paginated shape sorted newest-first, `openWorkItemCount` excludes Done items, detail includes `totalWorkItemCount`, throws for an unknown id
- [X] T015 [P] [US2] Integration tests for `GET /api/projects` and `GET /api/projects/{id}` in `backend/TaskFlow.Api.Tests/Integration/ProjectsEndpointsTests.cs`: `200` + paginated list for any authenticated caller, `200` detail, `404` unknown id, `401` with no token (allowed + denied paths). **Confirmed RED**: compile errors (`GetProjectsAsync`/`GetProjectByIdAsync`/`ProjectListItemDto` didn't exist).
- [X] T016 [P] [US2] Vitest tests for `frontend/src/app/projects/projects-list/projects-list.component.spec.ts` (renders paginated list with open-item counts) and `frontend/src/app/projects/project-detail/project-detail.component.spec.ts` (renders project header info; shows "No work items yet" when empty)

### Implementation for User Story 2

- [X] T017 [US2] Create `ProjectListItemDto` in `backend/TaskFlow.Api/Dtos/ProjectListItemDto.cs` (Id, Name, CreatedByName, CreatedAt, OpenWorkItemCount)
- [X] T018 [US2] Implement `ProjectService.GetProjectsAsync` (paginated, sorted by `CreatedAt` desc, `openWorkItemCount` via a `COUNT` of not-Done work items) and `GetProjectByIdAsync` (`totalWorkItemCount`, throws `ProjectNotFoundException`) in `backend/TaskFlow.Api/Services/ProjectService.cs` — depends on T017. Added `CreatedBy`/`Assignee` navigation properties to `Project`/`WorkItem` (not separately tasked) to make these queries' `CreatedByName` projections possible — verified via a throwaway `dotnet ef migrations add` that this produced *zero* schema change (empty `Up`/`Down`), then removed it. **Confirmed GREEN**: 4/4 new `ProjectServiceTests` pass.
- [X] T019 [US2] Implement `ProjectsController.GetProjects` (`GET /api/projects`) and `GetProject` (`GET /api/projects/{id}`) in `backend/TaskFlow.Api/Controllers/ProjectsController.cs` — depends on T018. Moved to a class-level bare `[Authorize]` (baseline: any authenticated user) with `Create` layering its own `[Authorize(Roles = "Manager,Admin")]` on top. **Confirmed GREEN**: 67/67 backend tests pass (was 59; +8 new, 0 regressions).
- [X] T020 [US2] Build the `projects-list` component in `frontend/src/app/projects/projects-list/` (+ template) — depends on T016
- [X] T021 [US2] Build the `project-detail` component (project header info + "No work items yet" empty state; the work-item list itself is built out in US3) in `frontend/src/app/projects/project-detail/` (+ template) — depends on T016
- [X] T022 [US2] Add `getProjects()`/`getProject()` to `projects.service.ts`; wire `projects-list`/`project-detail` to call them; add `/projects` and `/projects/:id` routes (guarded by the existing `authGuard`, any role) to `frontend/src/app/app.routes.ts` — depends on T020, T021. Also added `/projects/new` (routed before `/projects/:id`, since route matching is order-dependent) for US1's create form. **Confirmed GREEN**: 40/40 frontend tests pass (was 37; +3 new, 0 regressions).

**Checkpoint**: User Stories 1 AND 2 both work — a project can be created and viewed.

---

## Phase 4: User Story 3 - Create a Work Item (Priority: P1)

**Goal**: Any signed-in user can create a work item inside a project.

**Independent Test**: Open an existing project and create a work item with a title and type; confirm it appears in that project's item list immediately.

### Tests for User Story 3 ⚠️

- [X] T023 [P] [US3] Unit tests for `WorkItemService.CreateAsync` in `backend/TaskFlow.Api.Tests/Services/WorkItemServiceTests.cs`: applies default priority/status when omitted, rejects an assignee id that isn't an existing user, accepts a past due date, records creator/timestamps
- [X] T024 [P] [US3] Unit tests for `UserService.GetAssignableUsersAsync` in `backend/TaskFlow.Api.Tests/Services/UserServiceTests.cs`: returns every user's id and full name only (research.md §9)
- [X] T025 [P] [US3] Integration tests for `POST /api/projects/{projectId}/work-items` in `backend/TaskFlow.Api.Tests/Integration/WorkItemsEndpointsTests.cs`: `201` on success, `400` invalid title/unknown assignee, `404` unknown project, `401` with no token
- [X] T026 [P] [US3] Integration test for `GET /api/users/lookup` in `backend/TaskFlow.Api.Tests/Integration/UsersEndpointsTests.cs`: `200` for a non-Admin caller (unlike the existing `GET /api/users`/`PUT .../role`, which must still both return `403` for non-Admins — a regression check on the attribute-relocation in T030). **Confirmed RED**: compile errors (`WorkItemService`/`WorkItemRequest` didn't exist for T023/T025; `GetAssignableUsersAsync`/`/lookup` didn't exist for T024/T026).
- [X] T027 [P] [US3] Vitest tests for the work-item-create flow in `frontend/src/app/projects/work-item-form/work-item-form.component.spec.ts`: submits with title + type, defaults shown before submit; assignee/type/priority/status `<select>` elements correctly pre-select their bound value on initial render (guards against the Feature 001 Phase 7 bug — research.md §6). **Confirmed RED**: `Cannot find module './work-item-form.component'` build error.

### Implementation for User Story 3

- [X] T028 [US3] Create `WorkItemRequest` DTO with data-annotation validation (Title 3-200 chars, Description optional ≤5000 chars) in `backend/TaskFlow.Api/Dtos/WorkItemRequest.cs` — shared by create and edit
- [X] T029 [US3] Create `WorkItemDto` in `backend/TaskFlow.Api/Dtos/WorkItemDto.cs` (Id, ProjectId, Type, Title, Description, Priority, Status, AssigneeUserId, AssigneeName, DueDate, CreatedByUserId, CreatedByName, CreatedAt, UpdatedAt)
- [X] T030 [US3] Add `UserLookupItemDto` (`backend/TaskFlow.Api/Dtos/UserLookupItemDto.cs`) and `UserService.GetAssignableUsersAsync` (`backend/TaskFlow.Api/Services/UserService.cs`); in `backend/TaskFlow.Api/Controllers/UsersController.cs`, move the class-level `[Authorize(Roles = "Admin")]` down onto the two existing actions individually and add a new `GetLookup` action (`GET /api/users/lookup`) with plain `[Authorize]` (research.md §9) — depends on T024, T026. **Handled with extra care per explicit instruction**: only the `[Authorize]` placement changed on `UsersController` (bare class-level baseline + `[Authorize(Roles = "Admin")]` re-added to `GetUsers`/`ChangeRole` individually) — no existing method body touched. **Confirmed GREEN**: all pre-existing `UsersEndpointsTests`/`UserServiceTests` from Feature 001 still pass unchanged, alongside the 2 new tests.
- [X] T031 [US3] Implement `WorkItemService.CreateAsync` in `backend/TaskFlow.Api/Services/WorkItemService.cs` (default Medium/ToDo, assignee-exists check, project-exists check) — depends on T028, T029; register `WorkItemService` as Scoped in `Program.cs`
- [X] T032 [US3] Implement `WorkItemsController.Create` (`POST /api/projects/{projectId}/work-items`) in `backend/TaskFlow.Api/Controllers/WorkItemsController.cs` — depends on T031. **Confirmed GREEN**: 80/80 backend tests pass (was 67; +13 new, 0 regressions — Feature 001's `UsersEndpointsTests`/`UserServiceTests` specifically re-verified green per the extra-care instruction).
- [X] T033 [US3] Build the `work-item-form` component (create mode) in `frontend/src/app/projects/work-item-form/` (+ template), using `[selected]` on each `<option>` for every dropdown from the start (research.md §6) — depends on T027
- [X] T034 [US3] Add `work-items.service.ts` (`frontend/src/app/projects/work-items.service.ts`) with `createWorkItem()` and `getAssignableUsers()`; wire `project-detail`'s "New work item" control to open the form and refresh the item list on success — depends on T030, T033. **Scope note**: the *rendered* work-item list itself is T056 (US6) per this file's own Phase 7 — `GET /api/projects/{projectId}/work-items` (the listing endpoint) doesn't exist until then either. For US3, "refresh" means: creating an item navigates back to `project-detail`, which re-fetches the project (a fresh component instance via routing) and so its `totalWorkItemCount`-driven "No work items yet" placeholder and the project list's `openWorkItemCount` both update correctly — verified in the browser below. The itemized table arrives in US6. **Confirmed GREEN**: 43/43 frontend tests pass (was 40; +3 new, 0 regressions).

**Checkpoint**: User Stories 1-3 work — a project can be created, viewed, and populated with work items. **Verified end-to-end in a real browser** against the live API/database: registering a project shows "No work items yet"; creating a work item (title, High priority, assigned to a real user picked from the non-Admin-accessible lookup dropdown) navigates back to the project, the placeholder correctly disappears (`totalWorkItemCount` now > 0), and the project list's "Open items" column shows the updated count. Zero console errors.

---

## Phase 5: User Story 4 - Edit or Delete a Work Item and Update Its Status (Priority: P2)

**Goal**: A work item's creator, current assignee, or a Manager/Admin can edit its fields, including status. The creator, a Manager, or an Admin can also delete it (a narrower set — the assignee alone cannot), with a simple confirmation (FR-016 edit / FR-017-018 delete).

**Independent Test**: As an item's creator, change its status to Done via the edit form and confirm the change is reflected in the list; then, still as the creator, delete a different item and confirm it disappears from the list.

### Tests for User Story 4 ⚠️

- [ ] T035 [P] [US4] Unit tests for `WorkItemService.UpdateAsync` in `backend/TaskFlow.Api.Tests/Services/WorkItemServiceTests.cs`: creator/current assignee/Manager/Admin can update any field including status and `UpdatedAt` advances; any other caller is rejected; nothing can change `ProjectId`
- [ ] T036 [P] [US4] Integration tests for `PUT /api/work-items/{id}` and `GET /api/work-items/{id}` in `backend/TaskFlow.Api.Tests/Integration/WorkItemsEndpointsTests.cs`: `PUT` returns `200` for creator/assignee/Manager/Admin, `403` for an unrelated caller, `400` invalid input, `404` unknown id; `GET` returns `200` with the full item for any authenticated caller and `404` for an unknown id
- [ ] T037 [P] [US4] Vitest tests for edit mode in `frontend/src/app/projects/work-item-form/work-item-form.component.spec.ts`: pre-fills existing values (including each `<select>` correctly pre-selecting its current value), submits changes
- [ ] T038 [P] [US4] Vitest tests added to `frontend/src/app/projects/project-detail/project-detail.component.spec.ts`: the **edit** control is shown for a work item's creator/current assignee/Manager/Admin and hidden for an unrelated viewer
- [ ] T038a [P] [US4] Unit tests for `WorkItemService.DeleteAsync` in `backend/TaskFlow.Api.Tests/Services/WorkItemServiceTests.cs`: creator/Manager/Admin can delete; the current assignee alone (not also creator/Manager/Admin) is rejected; an unrelated caller is rejected; an unknown id throws not-found
- [ ] T038b [P] [US4] Integration tests for `DELETE /api/work-items/{id}` in `backend/TaskFlow.Api.Tests/Integration/WorkItemsEndpointsTests.cs`: `204` for creator/Manager/Admin, `403` for the assignee-alone case and for an unrelated caller, `404` for an unknown id
- [ ] T038c [P] [US4] Vitest tests added to `frontend/src/app/projects/project-detail/project-detail.component.spec.ts`: the **delete** control is shown only for a work item's creator/Manager/Admin and hidden for the assignee-alone case and for an unrelated viewer — narrower than the edit control tested in T038
- [ ] T038d [P] [US4] Integration test in `backend/TaskFlow.Api.Tests/Integration/AuthEndpointsTests.cs` asserting `POST /api/auth/login`'s `AuthResponse` and `GET /api/auth/me`'s `MeResponse` both include the caller's own `id`, matching the signed-in user's actual id — must be written and confirmed **RED** before T039 is implemented (mirrors T024/T026 preceding T030 in Feature 001)

### Implementation for User Story 4

- [ ] T039 [US4] Add `Id` to `AuthResponse` (`backend/TaskFlow.Api/Dtos/AuthResponse.cs`) and `MeResponse` (`backend/TaskFlow.Api/Dtos/MeResponse.cs`), update `AuthService.IssueToken` and `AuthController.Me` to populate it (research.md §8); add `id: number` to `AuthState`/the internal API-response interface in `frontend/src/app/auth/auth.service.ts`, and update the existing `AuthState` object literals in `auth.service.spec.ts`, `app.spec.ts`, `shared/header/header.component.spec.ts`, `auth/auth.guard.spec.ts`, and `auth/admin.guard.spec.ts` accordingly — depends on T038d (confirmed RED)
- [ ] T040 [US4] Implement `WorkItemService.UpdateAsync` (creator/assignee/role check, full-field replace, advances `UpdatedAt`) in `backend/TaskFlow.Api/Services/WorkItemService.cs` — depends on T031
- [ ] T041 [US4] Implement `WorkItemsController.Update` (`PUT /api/work-items/{id}`) and `Get` (`GET /api/work-items/{id}`) in `backend/TaskFlow.Api/Controllers/WorkItemsController.cs` — depends on T040
- [ ] T041a [US4] Implement `WorkItemService.DeleteAsync` (creator/Manager/Admin check — narrower than `UpdateAsync`, no assignee-alone case) in `backend/TaskFlow.Api/Services/WorkItemService.cs` — depends on T031
- [ ] T041b [US4] Implement `WorkItemsController.Delete` (`DELETE /api/work-items/{id}`, returns `204`) in `backend/TaskFlow.Api/Controllers/WorkItemsController.cs` — depends on T041a
- [ ] T042 [US4] Extend the `work-item-form` component to support edit mode (pre-fill from an existing item) — depends on T037, T033
- [ ] T043 [US4] Add `getWorkItem()`/`updateWorkItem()` to `work-items.service.ts`; show/hide each item's **edit** control in `project-detail` by comparing `AuthService`'s current user id/role against the item's creator/assignee (creator, current assignee, or Manager/Admin) — depends on T038, T039, T041, T042
- [ ] T043a [US4] Add `deleteWorkItem()` to `work-items.service.ts`; show/hide each item's **delete** control in `project-detail` using the narrower creator/Manager/Admin rule (no assignee-alone), and wire a simple confirmation (research.md §5) before calling delete — depends on T038c, T039, T041b

**Checkpoint**: User Stories 1-4 work — the full create/view/edit/delete loop is functional.

---

## Phase 6: User Story 5 - Edit or Delete a Project (Priority: P2)

**Goal**: A Manager or Admin can edit a project, or delete it (and all its work items) after an explicit, item-count-aware confirmation.

**Independent Test**: As a Manager, delete a project containing several work items, confirm the count shown matches, and verify both the project and its items are gone afterward.

### Tests for User Story 5 ⚠️

- [ ] T044 [P] [US5] Unit tests for `ProjectService.UpdateAsync`/`DeleteAsync` in `backend/TaskFlow.Api.Tests/Services/ProjectServiceTests.cs`: edit updates fields and rejects a name that duplicates a *different* project (not itself); delete removes the project and, via cascade, all of its work items
- [ ] T045 [P] [US5] Integration tests for `PUT /api/projects/{id}` and `DELETE /api/projects/{id}` in `backend/TaskFlow.Api.Tests/Integration/ProjectsEndpointsTests.cs`: `200`/`204` for Manager/Admin, `403` for a Developer, `404` unknown id, `409` on a duplicate name
- [ ] T046 [P] [US5] Vitest tests for edit mode in `project-form.component.spec.ts`, and for `project-detail`'s delete confirmation in `project-detail.component.spec.ts`: confirmation states the exact work-item count from `totalWorkItemCount`; edit/delete controls hidden for a non-Manager/Admin

### Implementation for User Story 5

- [ ] T047 [US5] Implement `ProjectService.UpdateAsync` and `DeleteAsync` (duplicate-name check excludes the project being edited) in `backend/TaskFlow.Api/Services/ProjectService.cs` — depends on T018
- [ ] T048 [US5] Implement `ProjectsController.Update` (`PUT`) and `Delete` (`DELETE`) with `[Authorize(Roles = "Manager,Admin")]` in `backend/TaskFlow.Api/Controllers/ProjectsController.cs` — depends on T047
- [ ] T049 [US5] Extend `project-form` to support edit mode; add a delete control and confirmation dialog (using the already-fetched `totalWorkItemCount`) to `project-detail` — depends on T046, T012, T021
- [ ] T050 [US5] Add `updateProject()`/`deleteProject()` to `projects.service.ts`; show "Edit"/"Delete" controls in `project-detail` and `projects-list` only when `currentRole()` is Manager or Admin — depends on T049

**Checkpoint**: User Stories 1-5 work — full project and work-item lifecycle management.

---

## Phase 7: User Story 6 - Filter, Search, and Page Through Work Items (Priority: P3)

**Goal**: Any signed-in user can filter and search a project's work items and page through long lists.

**Independent Test**: In a project with items of mixed priority, filter to "High" and confirm only High-priority items appear; search a title substring and confirm only matches appear.

### Tests for User Story 6 ⚠️

- [ ] T051 [P] [US6] Unit tests for `WorkItemService.GetWorkItemsAsync` in `backend/TaskFlow.Api.Tests/Services/WorkItemServiceTests.cs`: paginated shape, each filter individually and in combination, case-insensitive title search, default sort by `UpdatedAt` descending, `pageSize` beyond 100 is clamped rather than rejected
- [ ] T052 [P] [US6] Integration tests for `GET /api/projects/{projectId}/work-items` in `backend/TaskFlow.Api.Tests/Integration/WorkItemsEndpointsTests.cs`: filters/search/pagination combinations, `404` unknown project, `400` when a `status`/`type`/`priority` query value doesn't parse as its enum (rather than the value being silently ignored)
- [ ] T053 [P] [US6] Vitest tests added to `project-detail.component.spec.ts`: applying filters/search narrows the shown items; "No work items yet" (no filters, project genuinely empty) vs. "No items match your filters." (filters applied, no match) are distinguished; pagination controls page correctly

### Implementation for User Story 6

- [ ] T054 [US6] Implement `WorkItemService.GetWorkItemsAsync` (conditionally appended `.Where()` per supplied filter, case-insensitive title search, `pageSize` clamped to 100) in `backend/TaskFlow.Api/Services/WorkItemService.cs` — depends on T031
- [ ] T055 [US6] Implement `WorkItemsController.GetWorkItems` (`GET /api/projects/{projectId}/work-items`) in `backend/TaskFlow.Api/Controllers/WorkItemsController.cs` — depends on T054
- [ ] T056 [US6] Build the work-item list, filter/search bar, and pagination controls within `project-detail` (+ template) — depends on T053
- [ ] T057 [US6] Add `getWorkItems()` (with filter/search/page params) to `work-items.service.ts`; wire `project-detail`'s filter bar and pagination to it — depends on T056

**Checkpoint**: All six user stories are independently functional — the feature described in `spec.md` is complete end-to-end.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Feature-wide verification against the constitution's Definition of Done

- [ ] T058 [P] Add a "Projects" navigation entry to the header, visible to every signed-in user (unlike "Users", which stays Admin-only), in `frontend/src/app/shared/header/`
- [ ] T058a [P] Retrofit `project-form`, `projects-list`, `project-detail`, and `work-item-form` (all built pre-styling-pass with plain HTML) to Angular Material components (`mat-card`, `mat-form-field`/`mat-input`, `mat-table`, `mat-button`), matching Feature 001's restyled pages, so the feature doesn't ship in a mixed style — keep every native `<select>` with `[selected]`-per-`<option>` exactly as-is (research.md §6); all existing Vitest tests for these components must stay green, updating only selectors that change because of the template restructuring
- [ ] T059 [P] Review all new and changed C# files for the constitution's required educational comments — especially the cascade-path rule (research.md §2), combined role-and-ownership authorization, and conditional `IQueryable` filtering (plan.md "Concepts You Will Learn")
- [ ] T059a [P] Add an integration test verifying every error response (`400`/`403`/`404`/`409`) from the Projects and Work Items endpoints is a valid RFC 7807 `ProblemDetails` body, extending Feature 001's equivalent `ProblemDetails`-shape test
- [ ] T060 Verify `dotnet ef database update` applies this feature's migration cleanly to a fresh database, extending Feature 001's equivalent check
- [ ] T061 Add a "What I learned" entry for this feature to the project `README.md`
- [ ] T062 Run all `quickstart.md` validation scenarios end-to-end manually
- [ ] T063 Confirm zero build warnings (`dotnet build`) and all tests pass (`dotnet test`, and the frontend Vitest suite)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Foundational (Phase 1)**: No dependencies — start immediately. BLOCKS all user stories.
- **User Story 1 (Phase 2)**: Depends only on Foundational.
- **User Story 2 (Phase 3)**: Depends only on Foundational (not on US1's create code, though it shares `ProjectService.cs`/`ProjectsController.cs` files — see note below).
- **User Story 3 (Phase 4)**: Depends on Foundational; easiest to exercise once US1/US2 exist (need a project to create items in), but its own files are independent.
- **User Story 4 (Phase 5)**: Depends on Foundational and on US3's work-item files existing (shares `WorkItemService.cs`/`WorkItemsController.cs`/`work-item-form`). Also touches Feature 001's auth files (T039, gated by T038d being confirmed RED first) — do this before T043/T043a, which consume it.
- **User Story 5 (Phase 6)**: Depends on Foundational and on US1/US2's project files existing (shares `ProjectService.cs`/`ProjectsController.cs`/`project-form`/`project-detail`).
- **User Story 6 (Phase 7)**: Depends on Foundational and on US3's work-item files existing (shares `WorkItemService.cs`/`WorkItemsController.cs`/`project-detail`).
- **Polish (Phase 8)**: Depends on all six user stories being complete.

**Note on shared files**: Several stories add methods to the same files rather than creating new ones (`ProjectService.cs`/`ProjectsController.cs` across US1/US2/US5; `WorkItemService.cs`/`WorkItemsController.cs` across US3/US4/US6; `project-form`/`project-detail`/`work-item-form` similarly). Each story only adds *new* methods/modes to these files, so stories remain functionally independent once their prerequisites exist, but sequence same-file tasks rather than editing them simultaneously.

### Within Each User Story

- Tests are written and confirmed failing before implementation tasks (constitution Principle I).
- DTOs before services; services before controllers/components; core logic before UI wiring.

### Parallel Opportunities

- Foundational: T001, T002 in parallel; T003 follows both; T004 follows T003.
- Tests within a story: e.g. T005-T007 (US1), T023-T027 (US3), T035-T038d (US4) in parallel — different files.
- Once Foundational is done, US1 and US2 can proceed in parallel by different developers (mind the shared-file note); once US3 exists, US4 and US6 can similarly proceed in parallel; US5 can proceed in parallel with US3/US4/US6 once US1/US2 exist.

---

## Parallel Example: User Story 3

```bash
# Tests for User Story 3 together:
Task: "Unit tests for WorkItemService.CreateAsync in backend/TaskFlow.Api.Tests/Services/WorkItemServiceTests.cs"
Task: "Unit tests for UserService.GetAssignableUsersAsync in backend/TaskFlow.Api.Tests/Services/UserServiceTests.cs"
Task: "Integration tests for POST /api/projects/{projectId}/work-items in backend/TaskFlow.Api.Tests/Integration/WorkItemsEndpointsTests.cs"
Task: "Integration test for GET /api/users/lookup in backend/TaskFlow.Api.Tests/Integration/UsersEndpointsTests.cs"
Task: "Vitest tests for the work-item-create flow in frontend/src/app/projects/work-item-form/work-item-form.component.spec.ts"
```

---

## Implementation Strategy

### MVP First (User Stories 1-3 Only)

1. Complete Phase 1: Foundational
2. Complete Phase 2: User Story 1 (create a project)
3. Complete Phase 3: User Story 2 (view projects/items)
4. Complete Phase 4: User Story 3 (create a work item)
5. **STOP and VALIDATE**: a Manager creates a project, any user opens it and adds a work item, sees it in the list
6. Per constitution Principle VIII (Human in the Loop): pause here for maintainer review before continuing

### Incremental Delivery

1. Foundational → foundation ready
2. User Story 1 → validate → review checkpoint
3. User Story 2 → validate → review checkpoint (MVP: create + view)
4. User Story 3 → validate → review checkpoint (MVP: + create work items)
5. User Story 4 → validate → review checkpoint (+ edit/status)
6. User Story 5 → validate → review checkpoint (+ project lifecycle)
7. User Story 6 → validate → review checkpoint (+ filter/search/page)
8. Polish (Phase 8)

**Note on feature size**: this feature's total new/changed file count (~50 across backend + frontend + tests, see plan.md's Constitution Check) again exceeds the constitution's "~15 files, readable in one sitting" guideline (Principle VII — a target, not NON-NEGOTIABLE), for the same reason accepted for Feature 001 (Projects and Work Items are inseparable as a first slice of TaskFlow's actual product) plus two small, additive touches to Feature 001's own files (T039, T030) that turned out to be genuine prerequisites rather than avoidable scope. Treat each user-story checkpoint above as a review pause, per Principle VIII.

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps each task to its user story for traceability
- Tests MUST be written first and MUST fail before their implementation tasks (constitution Principle I)
- T039 and T030 touch Feature 001 files directly (`AuthResponse`/`MeResponse`/`AuthService`/`AuthController`/`auth.service.ts` and their existing specs; `UsersController`/`UserService`/`UsersEndpointsTests`) — review these two tasks' diffs with particular care, since regressions here would affect the already-shipped Feature 001, not just this feature
- Commit after each task or logical group
- Stop at any checkpoint to validate a story independently, per the constitution's Human-in-the-Loop principle
