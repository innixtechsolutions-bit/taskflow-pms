---

description: "Task list template for feature implementation"
---

# Tasks: E2E Testing Foundation (Playwright)

**Input**: Design documents from `/specs/010-e2e-playwright/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/api-usage.md, quickstart.md (all present)

**Tests**: This feature's deliverable *is* the test suite — there is no separate "tests for the tests" phase. Each user story's tasks culminate in the actual Playwright spec file for that journey.

**Organization**: Tasks are grouped by user story (journey) per spec.md's priorities, so each journey can be implemented and run independently. Zero production code changes are required — see research.md Decision 5.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story/journey this task belongs to (US1–US7)
- Every task lists its exact file path

## Path Conventions

All new files live under `frontend/` (existing web app structure — see plan.md Project Structure):
- `frontend/playwright.config.ts`, `frontend/package.json` (edit)
- `frontend/e2e/global-setup.ts`
- `frontend/e2e/fixtures/*.ts`
- `frontend/e2e/pages/*.page.ts`
- `frontend/e2e/tests/*.spec.ts`

---

## Phase 1: Setup

**Purpose**: Get Playwright installed and wired into the frontend project, fully separate from Vitest.

- [ ] T001 Add `@playwright/test` as a devDependency in `frontend/package.json` (`npm install -D @playwright/test` from `frontend/`) and install the Chromium browser binary (`npx playwright install chromium`)
- [ ] T002 [P] Create `frontend/playwright.config.ts`: `testDir: './e2e/tests'`, `globalSetup: './e2e/global-setup.ts'`, `use: { baseURL: 'http://localhost:4300', trace: 'retain-on-failure', screenshot: 'only-on-failure' }`, `retries: 1`, single `projects: [{ name: 'chromium', use: devices['Desktop Chrome'] }]` per FR-018
- [ ] T003 [P] Add `"e2e": "playwright test"` and `"e2e:report": "playwright show-report"` scripts to `frontend/package.json`, alongside (not replacing) the existing `"test": "ng test"`, per FR-014
- [ ] T004 [P] Add Playwright output directories to `frontend/.gitignore`: `/e2e/.auth/`, `/test-results/`, `/playwright-report/`

**Checkpoint**: `npx playwright test` runs (with zero spec files) without config errors.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Account provisioning and the one page object (`login.page.ts`) every journey depends on. No user story work can start until this phase is complete.

- [ ] T005 Create `frontend/e2e/global-setup.ts`: log in as the config-seeded Admin (`POST /api/auth/login`) via Playwright's `request` fixture; register a Manager-candidate and a Developer test account (`POST /api/auth/register`), treating a `409` as "already provisioned" for idempotent reruns; promote the Manager-candidate via `PUT /api/users/{id}/role` using the Admin's token (see research.md Decisions 2–3, contracts/api-usage.md); save each account's `storageState` (or credentials) to `frontend/e2e/.auth/{role}.json` for fixtures to consume
- [ ] T006 [P] Create `frontend/e2e/fixtures/auth.fixture.ts`: a `test.extend` exporting `adminPage`, `managerPage`, and `developerPage` fixtures, each a browser context pre-authenticated via the `storageState` files written by `global-setup.ts` (per data-model.md's Seeded Test Accounts table)
- [ ] T007 [P] Create `frontend/e2e/pages/login.page.ts`: `goto()`, `login(email, password)`, `errorMessage` locator — built on Material's accessible labels (`getByLabel`), no new markup needed (research.md Decision 5)

**Checkpoint**: Foundation ready — `global-setup.ts` successfully provisions all three seeded accounts against a freshly-reset E2E database (verify manually per quickstart.md steps 1–3 before continuing).

---

## Phase 3: User Story 1 - Auth journey (Priority: P1) 🎯 MVP

**Goal**: Register → dashboard as Developer; logout → login → back inside the app; unauthenticated visit to a protected URL → redirected to login with a working `returnUrl`.

**Independent Test**: Run `auth.spec.ts` alone — registers a fresh throwaway account, logs out/in, and hits one protected URL while logged out.

### Implementation for User Story 1

- [ ] T008 [P] [US1] Create `frontend/e2e/pages/register.page.ts`: `goto()`, `register(fullName, email, password)`, `errorMessage` locator (Material accessible labels, no new markup)
- [ ] T009 [US1] Create `frontend/e2e/tests/auth.spec.ts` covering all 3 acceptance scenarios: (a) register a fresh account → assert landed on the dashboard as a Developer; (b) log out then log back in with the same credentials → assert back inside the app, not stuck on `/login`; (c) with no session, `goto()` a protected URL (e.g. `/projects`) → assert redirect to `/login?returnUrl=...`, then log in → assert navigated back to the originally-requested URL (depends on T007, T008)

**Checkpoint**: `npx playwright test auth.spec.ts` passes independently, 3 consecutive runs.

---

## Phase 4: User Story 2 - Permission-boundary journey (Priority: P1)

**Goal**: A Developer sees no create/edit/delete/workflow-column/user-management controls, and a direct API delete attempt is rejected server-side (FR-012).

**Independent Test**: Run `permissions.spec.ts` alone — logs in as the seeded Developer, checks UI absence, and separately issues one direct API call.

### Implementation for User Story 2

- [ ] T010 [P] [US2] Create `frontend/e2e/pages/sidebar-nav.page.ts`: locators for each nav item by role/label, `hasUsersLink()` — wraps `frontend/src/app/shared/sidebar-nav/sidebar-nav.component.ts`
- [ ] T011 [US2] Create `frontend/e2e/tests/permissions.spec.ts`: in a `beforeAll`, create one scratch project directly via `POST /api/projects` using the Manager's token (request fixture) — kept independent of US4's UI flow; then, as the Developer: assert no create/edit/delete controls appear on the project list or the scratch project's page, assert no workflow-column management controls, assert `sidebarNavPage.hasUsersLink()` is false; finally, using the Developer's token via the `request` fixture, call `DELETE /api/projects/{scratchProjectId}` and assert `403` (depends on T006, T010; see contracts/api-usage.md)

**Checkpoint**: `npx playwright test permissions.spec.ts` passes independently.

---

## Phase 5: User Story 3 - Navigation journey (Priority: P2)

**Goal**: Every sidebar item navigates correctly; Admin sees a Users link that works, Developer doesn't see it and is blocked from the URL directly.

**Independent Test**: Run `navigation.spec.ts` alone — logs in as Admin and as Developer, walks the sidebar.

### Implementation for User Story 3

- [ ] T012 [US3] Create `frontend/e2e/tests/navigation.spec.ts`: as a logged-in user, click every item returned by `sidebarNavPage`'s locators and assert each lands on the correct route; as Admin, assert the Users item is present and navigates to `/users`; as Developer, assert the Users item is absent from `sidebarNavPage`, and separately `goto('/users')` directly and assert the app blocks it (redirects away, does not render the Users list) (depends on T007, T010 — reuses `sidebar-nav.page.ts` from US2, no new page object)

**Checkpoint**: `npx playwright test navigation.spec.ts` passes independently.

---

## Phase 6: User Story 4 - Project and work item journey (Priority: P2)

**Goal**: Manager/Admin creates a project, a Story, and a child Task; both appear correctly, with correct parent-child relationship, in List and Tree views.

**Independent Test**: Run `project-work-items.spec.ts` alone as the seeded Manager.

### Implementation for User Story 4

- [ ] T013 [P] [US4] Create `frontend/e2e/pages/work-item-modal.page.ts`: `open()`, `fillTitle(title)`, `selectType(type)`, `selectParent(title)`, `submit()` — wraps the work item create/edit modal (Feature 007)
- [ ] T014 [US4] Create `frontend/e2e/pages/project-detail.page.ts` with project-creation and List/Tree view helpers: `createProject(name)` (drives `ProjectFormComponent` at `/projects/new`), `switchToListView()` / `switchToTreeView()` (the `?view=flat`/`?view=tree` toggles), row locators (`.tree-item-title`, `#tree-work-item-{id}`, list-view row by title) — wraps `frontend/src/app/projects/project-detail/project-detail.component.ts`
- [ ] T015 [US4] Create `frontend/e2e/tests/project-work-items.spec.ts`: as Manager, create a project, create a Story via `work-item-modal.page.ts`, create a Task as the Story's child; assert both appear in List view; switch to Tree view and assert the parent-child relationship is visibly correct (depends on T013, T014)

**Checkpoint**: `npx playwright test project-work-items.spec.ts` passes independently.

---

## Phase 7: User Story 5 - Board drag journey (Priority: P2)

**Goal**: An authorized drag's new status persists after reload; an unauthorized drag visibly reverts.

**Independent Test**: Run `board-drag.spec.ts` alone against a project/work item the spec creates for itself.

### Implementation for User Story 5

- [ ] T016 [US5] Extend `frontend/e2e/pages/project-detail.page.ts` with board-mode helpers: `switchToBoardView()`, `dragCardToColumn(workItemId, columnName)` — targets a specific card via `page.locator('.board-card').filter({ hasText: '#' + workItemId })` (no production markup change needed — the card already renders a unique `#{{id}}`, per research.md Decision 5), then performs a CDK-compatible drag to the target column's drop list
- [ ] T017 [US5] Create `frontend/e2e/tests/board-drag.spec.ts`: in setup, create a project and one work item via API as Manager; as an authorized user, drag the card to a different column, reload the page, assert the new column still shows the card; as a user without status-change permission (per `frontend/src/app/projects/work-item-permissions.ts`'s `canEditWorkItem` rule — confirm exact restriction during implementation), attempt the same drag and assert the card visibly reverts to its original column (depends on T016)

**Checkpoint**: `npx playwright test board-drag.spec.ts` passes independently, including after a page reload.

---

## Phase 8: User Story 6 - Sprint journey (Priority: P3)

**Goal**: Create a sprint, drag a backlog item into it, start it, confirm Board's active-sprint filtering, complete it and pick a destination for any open item.

**Independent Test**: Run `sprint.spec.ts` alone as the seeded Manager.

### Implementation for User Story 6

- [ ] T018 [P] [US6] Create `frontend/e2e/pages/sprint-dialogs.page.ts`: create-sprint dialog helpers (wraps `sprint-form.component.ts`) and complete-sprint dialog helpers including the destination picker (wraps `complete-sprint-dialog.component.ts`)
- [ ] T019 [US6] Extend `frontend/e2e/pages/project-detail.page.ts` with backlog-mode helpers: `switchToBacklogView()`, `dragItemIntoSprint(workItemId, sprintName)`, and the Board's active-sprint toggle locator (`.active-sprint-toggle`)
- [ ] T020 [US6] Create `frontend/e2e/tests/sprint.spec.ts`: in setup, create a project and a backlog work item via API as Manager; create a sprint via `sprint-dialogs.page.ts`, drag the item into it from the Backlog, start the sprint; switch the Board to active-sprint mode and assert only that sprint's item is shown; complete the sprint, choose a destination for the still-open item via the completion dialog, and assert the item ends up at the chosen destination (depends on T018, T019)

**Checkpoint**: `npx playwright test sprint.spec.ts` passes independently.

---

## Phase 9: User Story 7 - Role-change journey (Priority: P3)

**Goal**: An Admin changes another user's role; that user's next login reflects the new role and permissions.

**Independent Test**: Run `role-change.spec.ts` alone.

### Implementation for User Story 7

- [ ] T021 [P] [US7] Create `frontend/e2e/pages/users.page.ts`: Users list row locators, role-change control, save action — wraps `frontend/src/app/users/users-list/users-list.component.ts`
- [ ] T022 [US7] Create `frontend/e2e/tests/role-change.spec.ts`: register a **dedicated throwaway user** via the `request` fixture (do not reuse the shared seeded Developer/Manager accounts from `global-setup.ts`, so this journey's role mutation can never affect any other spec file's assumptions about those accounts' roles, per FR-005); as Admin, change that user's role via `users.page.ts`; log out; log back in as that user; assert their sidebar navigation and available actions match the new role, not the old one (depends on T021)

**Checkpoint**: `npx playwright test role-change.spec.ts` passes independently and leaves the shared seeded accounts' roles untouched.

---

## Phase 10: Polish & Cross-Cutting Concerns

**Purpose**: Documentation and final suite-wide validation, per the spec's Developer Experience and Success Criteria.

- [ ] T023 [P] Add a "Running the E2E suite" entry to the root `README.md`'s "What I learned" log (Feature 010 subsection, matching the existing per-feature convention), and reference `specs/010-e2e-playwright/quickstart.md` from the "Running locally" section
- [ ] T024 [P] Replace `frontend/README.md`'s stub "Running end-to-end tests" section with the real `npm run e2e` instructions (prerequisites: reset DB, start backend, start frontend — link to quickstart.md rather than duplicating)
- [ ] T025 [P] Create `backend/TaskFlow.Api/scripts/reset-e2e-db.ps1` wrapping the `dotnet ef database drop --force` / `dotnet ef database update` commands from quickstart.md step 1, as a developer convenience (not required by the suite itself — see research.md Decision 2 for why this stays a documented manual step rather than being called from `global-setup.ts`)
- [ ] T026 Run the full quickstart.md validation end-to-end 3 consecutive times (SC-001, SC-002, SC-004): reset DB → start backend → start frontend → `npm run e2e`, confirming all 7 spec files pass each time within a few minutes
- [ ] T027 Verify SC-003 per quickstart.md's "Verifying the suite actually catches regressions" section: temporarily disable `authGuard` (or `adminGuard`) on one route in `frontend/src/app/app.routes.ts`, re-run the suite, confirm at least one journey fails, then revert the change — no application behavior change ships as part of this feature (FR-016)

**Checkpoint**: All 7 journeys pass reliably 3 times in a row; the suite is proven to catch a real regression; README documents the command.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately.
- **Foundational (Phase 2)**: Depends on Setup. BLOCKS all user stories (every journey needs `global-setup.ts`'s provisioned accounts and `login.page.ts`).
- **User Stories (Phase 3–9)**: All depend on Foundational. Recommended order follows spec.md priority (P1 → P1 → P2 → P2 → P2 → P3 → P3) since later stories reuse page objects created by earlier ones (US3 reuses US2's `sidebar-nav.page.ts`; US5/US6 extend US4's `project-detail.page.ts`) — see the file-reuse notes below before parallelizing across stories.
- **Polish (Phase 10)**: Depends on all 7 journeys being complete and passing.

### User Story Dependencies (file-level, not behavioral)

- **US1 (Auth)**: No dependency on other stories.
- **US2 (Permission-boundary)**: No behavioral dependency; creates its own scratch project via API rather than relying on US4.
- **US3 (Navigation)**: Reuses `sidebar-nav.page.ts` created in US2 (T010) — implement after US2, or duplicate the file if US2/US3 must be built in parallel by different people.
- **US4 (Project/work item)**: No dependency; first story to create `project-detail.page.ts`.
- **US5 (Board drag)**: Extends `project-detail.page.ts` (T014, from US4) with board helpers — implement after US4.
- **US6 (Sprint)**: Extends `project-detail.page.ts` (T014, from US4) with backlog helpers — implement after US4.
- **US7 (Role-change)**: No dependency; uses its own throwaway test user, never the shared seeded accounts.

All 7 spec files remain independently *runnable* once their page-object dependencies exist — none of them requires another spec file to have executed first.

### Parallel Opportunities

- T002, T003, T004 (Setup) can run in parallel.
- T006, T007 (Foundational, after T005) can run in parallel.
- T008 (US1) can run in parallel with T010 (US2) — different files, both only depend on Foundational.
- T013 (US4's work-item-modal page) can run in parallel with T014 (US4's project-detail page) — different files.
- T018 (US6's sprint-dialogs page) can run in parallel with T021 (US7's users page) — different files, different stories.
- T023, T024, T025 (Polish docs/scripts) can all run in parallel.

---

## Parallel Example: Foundational + User Story 1

```bash
# After T005 (global-setup.ts) completes:
Task: "Create frontend/e2e/fixtures/auth.fixture.ts"
Task: "Create frontend/e2e/pages/login.page.ts"

# Once Foundational is done, start US1 and US2 in parallel:
Task: "Create frontend/e2e/pages/register.page.ts"      # US1
Task: "Create frontend/e2e/pages/sidebar-nav.page.ts"    # US2
```

---

## Implementation Strategy

### MVP First (User Stories 1 + 2 — both P1)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational (critical — blocks everything).
3. Complete Phase 3 (US1 Auth) and Phase 4 (US2 Permission-boundary) — together these are the security-critical MVP the spec's own priorities call out (both P1; nothing else in the app is trustworthy to test until login and authorization are proven to work).
4. **STOP and VALIDATE**: run `auth.spec.ts` and `permissions.spec.ts` independently, 3 times each.

### Incremental Delivery

1. Setup + Foundational → foundation ready.
2. US1 + US2 (P1) → validate → this is the MVP.
3. US3, US4, US5 (P2) → validate each independently → richer coverage.
4. US6, US7 (P3) → validate each independently → full 7-journey suite.
5. Phase 10 Polish → README + 3-run validation + regression-catch proof → feature DONE per spec's Success Check.

### Solo Implementation Note

Given the file-reuse dependencies above (US3→US2, US5/US6→US4), a single implementer should follow priority order (P1, P1, P2, P2, P2, P3, P3) rather than an arbitrary order, even though the spec frames each journey as independently *testable* once built.

---

## Notes

- [P] tasks = different files, no dependencies on incomplete same-phase work.
- [Story] label maps each task to its journey for traceability against spec.md.
- No test-writing-before-implementation split: for this feature, writing the Playwright spec *is* the implementation, per plan.md's Constitution Check (Principle I assessment).
- Commit after each user story phase (one journey = one commit), per the constitution's Commit Convention — e.g. `test: US1 auth journey e2e spec`.
- Stop at any checkpoint to validate a journey independently before continuing.
- Avoid: adding production `data-testid`/`id` attributes unless a selector genuinely proves non-resilient during implementation — research.md Decision 5 found none needed; if one is found necessary, keep it to the single minimal addition FR-017 allows and document it in this file's Notes.
