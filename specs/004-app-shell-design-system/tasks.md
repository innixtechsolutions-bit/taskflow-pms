# Tasks: App Shell & Design System

**Input**: Design documents from `D:\Projects\taskflow-pms\specs\004-app-shell-design-system\`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/design-system-components.md, quickstart.md

**Tests**: Included and REQUIRED — Constitution Principle I (Test-First Development, NON-NEGOTIABLE) mandates tests before implementation. Per the constitution's own carve-out, pure logic (chip color mapping, avatar hashing, date formatting, nav filtering, notification service) gets full Vitest specs written first; template-heavy retrofits are verified by updating the existing co-located component specs for new selectors/content, not by chasing 100% template coverage.

**Organization**: Tasks are grouped by user story (spec.md priorities P1–P3) so each story is independently implementable, testable, and demoable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1/US2/US3/US4, matching spec.md
- All paths are relative to `D:\Projects\taskflow-pms\`

## Path Conventions

Frontend-only feature. All paths are under `frontend/src/app/` (Angular 22,
standalone components) or `frontend/src/`. No `backend/` files are touched.

---

## Phase 1: Setup

**Purpose**: Nothing new to scaffold — this feature adds files to the existing
`frontend/` Angular app (Features 001-003 already established it). No new
project, package, or build config is needed.

- [ ] T001 Confirm `frontend/` builds and existing tests pass before starting (`cd frontend && npm test`), to have a clean baseline for FR-018/SC-008 regression comparison

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The single shared design-token source every user story's
components consume. No user story component work should start until tokens
exist.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T002 [P] Create `frontend/src/design-tokens.scss` defining CSS custom properties on `:root` per data-model.md's Design Token Set table: primary/surface/sidebar colors, one color token per `WorkItemStatus` value (ToDo/InProgress/Done), one per `WorkItemPriority` value (Low/Medium/High/Critical), an 8-color avatar accent palette, a spacing scale (`--space-1` 4px … `--space-6` 32px), `--radius-card`/`--radius-chip`, and layout tokens `--content-max-width` (~1440px), `--sidebar-width` (~240px), `--sidebar-width-collapsed` (~72px), `--breakpoint-tablet` (1024px) — values sourced from `visual-reference.png`
- [ ] T003 Import `design-tokens.scss` into `frontend/src/styles.css` (single `@use`/`@import` at the top) — depends on T002
- [ ] T004 [P] Align `frontend/src/material-theme.scss`'s `mat.theme()` primary palette so Material's own primary color matches `--color-primary` from `design-tokens.scss` — depends on T002

**Checkpoint**: Design tokens exist and are globally loaded. All user story phases can now begin (US1 and US2 can run in parallel; US3 and US4 depend on shared components that are easiest to build after the shell exists, but their own chip/avatar/pipe work is independent — see Dependencies section).

---

## Phase 3: User Story 1 - One consistent application shell (Priority: P1) 🎯 MVP

**Goal**: Every authenticated page renders inside one shell (dark sidebar area
+ light content area with a page header); login/register stay outside it;
list pages use the full available content width instead of the current
960px-capped, left-hugging layout.

**Independent Test**: Log in, visit Dashboard/Projects/a project
detail/a work item detail/Users — all share the same shell and header
pattern; visit `/login` directly — it is not wrapped in the shell; view
Projects list at a wide viewport — content fills the available width.

### Tests for User Story 1

- [ ] T005 [P] [US1] Write `ShellComponent` spec asserting it renders a sidenav region, a content region, and applies the shell's content-width token to that region, in `frontend/src/app/shared/shell/shell.component.spec.ts` (must fail before T009 exists)
- [ ] T006 [P] [US1] Write `PageHeaderComponent` spec asserting `title`/`subtitle` inputs render and projected `actions` content appears only when provided, in `frontend/src/app/shared/page-header/page-header.component.spec.ts` (must fail before T010 exists)

### Implementation for User Story 1

- [ ] T007 [P] [US1] Create `PageHeaderComponent` (`title: string`, `subtitle?: string` inputs; `actions` content-projection slot) at `frontend/src/app/shared/page-header/page-header.component.ts` (+ `.html` + `.css`), styled from `design-tokens.scss` spacing/typography — makes T006 pass
- [ ] T008 [US1] Create `AppShellComponent` using `MatSidenavModule` (`mat-sidenav-container` + `mat-sidenav` in `mode="side"`, permanently open), a content region bound to `--content-max-width`, and a `<ng-content>` slot for the routed page, at `frontend/src/app/shared/shell/shell.component.ts` (+ `.html` + `.css`) — makes T005 pass (sidebar-nav is wired in during US2; leave a placeholder `<ng-content select="[sidebarNav]">` or similar slot for now)
- [ ] T009 [US1] Rewrite `frontend/src/app/app.html` / `frontend/src/app/app.css`: when `authService.isAuthenticated()`, render `<router-outlet>` inside `<app-shell>`; when not authenticated, render `<router-outlet>` inside a minimal centered wrapper (preserving today's login/register centering); remove the old `<app-header>` usage and the global `.app-content { max-width: 960px; margin: 0 auto; }` rule (superseded by the shell's own `--content-max-width`-driven region) — depends on T008
- [ ] T010 [P] [US1] Delete `frontend/src/app/shared/header/` (`header.component.ts`/`.html`/`.css`/`.spec.ts`) — its nav+user-menu+logout responsibility is fully absorbed by the shell (finished in US2); depends on T009
- [ ] T011 [P] [US1] Retrofit `frontend/src/app/home/home.component.html` to render `<app-page-header title="Dashboard" subtitle="...">` in place of any ad-hoc heading — depends on T007
- [ ] T012 [P] [US1] Retrofit `frontend/src/app/projects/projects-list/projects-list.component.html`: use `<app-page-header>` with the "New Project" button projected into `actions`, and change the list's container CSS so its card/table content fills the shell's content width (no inner fixed-width wrapper) — depends on T007, T009
- [ ] T013 [P] [US1] Retrofit `frontend/src/app/projects/project-detail/project-detail.component.html`: use `<app-page-header>` for the project title/subtitle, and widen the tree/flat view containers to fill the shell's content width — depends on T007, T009
- [ ] T014 [P] [US1] Retrofit `frontend/src/app/users/users-list/users-list.component.html`: use `<app-page-header>`, and widen the users table to fill the shell's content width — depends on T007, T009
- [ ] T015 [US1] Update `frontend/src/app/projects/projects-list/projects-list.component.spec.ts`, `frontend/src/app/projects/project-detail/project-detail.component.spec.ts`, and `frontend/src/app/users/users-list/users-list.component.spec.ts` for the new `app-page-header` selector/content structure — depends on T011, T012, T013, T014

**Checkpoint**: Every authenticated page renders inside the shell with a
consistent header and full-width content; login/register are unaffected;
User Story 1 is independently testable via quickstart.md sections 1 and 5.

---

## Phase 4: User Story 2 - Sidebar navigation with active state and sign-out (Priority: P1)

**Goal**: A real sidebar — product name, icon+label nav links with active-route
highlighting, a bottom user block (avatar/name/role) with a logout menu,
collapsing to an icon rail below the tablet breakpoint.

**Independent Test**: Click each nav link and see the active state follow;
open the user menu and log out; resize to tablet width and confirm the
icon-rail collapse; as a non-Admin, confirm the Users link is absent.

### Tests for User Story 2

- [ ] T016 [P] [US2] Write a unit test for the nav item list's role-based visibility filtering (`'all'` vs role-restricted entries, e.g. Users is `['Admin']`-only) in `frontend/src/app/shared/sidebar-nav/nav-items.spec.ts` (must fail before T017 exists)
- [ ] T017 [P] [US2] Write `SidebarNavComponent` spec asserting: the nav item matching the current route gets the active class, the user block shows avatar/name/role from `AuthService`, choosing "Logout" from the user menu emits a `logout` output, **the Users link is present when `currentRole()` is `'Admin'` and absent for `'Developer'`/`'Manager'`, and the Projects link is present for all three roles** (this replaces the equivalent coverage in `header.component.spec.ts`, deleted in T010 — see spec.md US2 Acceptance Scenario 3), in `frontend/src/app/shared/sidebar-nav/sidebar-nav.component.spec.ts` (must fail before T019 exists)

### Implementation for User Story 2

- [ ] T018 [P] [US2] Define the nav item config array (`label`, `icon`, `route`, `visibleTo: 'all' | UserRole[]`) per data-model.md's Navigation Item shape — Dashboard/Projects (`'all'`), Users (`['Admin']`) — in `frontend/src/app/shared/sidebar-nav/nav-items.ts` — makes T016 pass
- [ ] T019 [US2] Create `SidebarNavComponent`: renders nav items filtered by `authService.currentRole()` against `visibleTo`, highlights the active route (`routerLinkActive`), renders a bottom user block (`<app-user-avatar>` once available in US3 — use plain initials markup for now if US3 isn't done yet, or sequence US3's avatar component before this task, see Dependencies) with name/role and a `MatMenu`-based logout trigger that emits a `logout` output, and — using CDK `BreakpointObserver` on `--breakpoint-tablet` — switches to icon-only items with `matTooltip` labels below the breakpoint, at `frontend/src/app/shared/sidebar-nav/sidebar-nav.component.ts` (+ `.html` + `.css`) — depends on T018; makes T017 pass
- [ ] T020 [US2] Wire `<app-sidebar-nav>` into `AppShellComponent`'s template (`frontend/src/app/shared/shell/shell.component.html`), handling its `(logout)` output by calling `authService.logout()` and navigating to `/login` — depends on T008 (US1), T019
- [ ] T021 [US2] Update `frontend/src/app/shared/shell/shell.component.spec.ts` to assert `<app-sidebar-nav>` is present and its `logout` output triggers navigation to `/login` — depends on T020

**Checkpoint**: The shell is fully navigable end-to-end (US1 + US2 together
give a complete, navigable, role-aware shell) — quickstart.md sections 2 and
6 pass.

---

## Phase 5: User Story 3 - Consistent status, priority, and assignee at a glance (Priority: P2)

**Goal**: Status/priority render as identically-colored chips everywhere;
assignees render as deterministic-color initials avatars everywhere; every
displayed date renders in friendly short form, never raw ISO.

**Independent Test**: Compare the same work item's status/priority chip and
assignee avatar across tree view, flat view, and detail page — identical in
all three; confirm every visible date reads like "Jul 17, 2026".

### Tests for User Story 3

- [ ] T022 [P] [US3] Write `StatusChipComponent` spec asserting each `WorkItemStatus` value (`ToDo`/`InProgress`/`Done`) renders its documented label and a distinct token-driven color class, in `frontend/src/app/shared/status-chip/status-chip.component.spec.ts` (must fail before T026 exists)
- [ ] T023 [P] [US3] Write `PriorityChipComponent` spec, same pattern for `WorkItemPriority` (`Low`/`Medium`/`High`/`Critical`), in `frontend/src/app/shared/priority-chip/priority-chip.component.spec.ts` (must fail before T027 exists)
- [ ] T024 [P] [US3] Write `UserAvatarComponent` spec asserting: the same `userId` always yields the same background color across separate render instances, initials are derived correctly from `fullName` (e.g. "Uma Kannan" → "UK"), and `showName` toggles the adjacent name text, in `frontend/src/app/shared/user-avatar/user-avatar.component.spec.ts` (must fail before T028 exists)
- [ ] T025 [P] [US3] Write `FriendlyDatePipe` spec asserting `'MMM d, y'` output (e.g. "Jul 17, 2026") for a known date and a placeholder (`"—"`) for `null`/`undefined`, in `frontend/src/app/shared/friendly-date.pipe.spec.ts` (must fail before T029 exists)

### Implementation for User Story 3

- [ ] T026 [P] [US3] Add `WorkItemStatus`/`WorkItemPriority` string-literal union types and narrow the `status`/`priority` fields on the `WorkItem`, `WorkItemDetail`, and `WorkItemTreeNode` interfaces (currently untyped `string`) in `frontend/src/app/projects/work-items.service.ts`
- [ ] T027 [P] [US3] Create `StatusChipComponent` (`status: WorkItemStatus` input, exhaustive switch over the union to a token color class — a compile error if a status value is ever added without a matching case) at `frontend/src/app/shared/status-chip/status-chip.component.ts` (+ `.html` + `.css`) — depends on T026; makes T022 pass
- [ ] T028 [P] [US3] Create `PriorityChipComponent` (same pattern for `WorkItemPriority`) at `frontend/src/app/shared/priority-chip/priority-chip.component.ts` (+ `.html` + `.css`) — depends on T026; makes T023 pass
- [ ] T029 [P] [US3] Create `UserAvatarComponent` (`userId: number`, `fullName: string`, `showName?: boolean` inputs; `avatarColorFor(id)` hashing into the avatar palette tokens + `initialsFor(fullName)` helpers) at `frontend/src/app/shared/user-avatar/user-avatar.component.ts` (+ `.html` + `.css`) — makes T024 pass
- [ ] T030 [P] [US3] Create `FriendlyDatePipe` (standalone pipe named `friendlyDate`, wraps Angular's built-in `DatePipe` with format `'MMM d, y'`, `en-US` locale, renders `"—"` for `null`/`undefined`) at `frontend/src/app/shared/friendly-date.pipe.ts` — makes T025 pass
- [ ] T031 [US3] Retrofit `frontend/src/app/projects/project-detail/project-detail.component.html`: wrap the tree view in a card container (per FR-014, preserving existing indentation/expand-collapse behavior unchanged), and in both tree rows and the flat table replace plain status/priority text with `<app-status-chip>`/`<app-priority-chip>`, plain assignee text with `<app-user-avatar>`, and the raw `createdAt` interpolation with `| friendlyDate` — depends on T027, T028, T029, T030
- [ ] T032 [P] [US3] Retrofit `frontend/src/app/projects/work-item-detail/work-item-detail.component.html` to use `<app-status-chip>`, `<app-priority-chip>`, and `<app-user-avatar>` for the work item's status/priority/assignee fields — depends on T027, T028, T029
- [ ] T033 [P] [US3] Retrofit `frontend/src/app/projects/projects-list/projects-list.component.html`'s `createdAt` interpolation to use `| friendlyDate` — depends on T030
- [ ] T034 [P] [US3] Retrofit `frontend/src/app/users/users-list/users-list.component.html` to use `<app-user-avatar>` next to each user's name and `| friendlyDate` for `createdAt` — depends on T029, T030
- [ ] T035 [US3] Also complete `SidebarNavComponent`'s bottom user block (from T019) to use `<app-user-avatar>` for the signed-in user instead of any placeholder initials markup, in `frontend/src/app/shared/sidebar-nav/sidebar-nav.component.html` — depends on T019, T029
- [ ] T036 [US3] Update `frontend/src/app/projects/project-detail/project-detail.component.spec.ts`, `frontend/src/app/projects/work-item-detail/work-item-detail.component.spec.ts`, `frontend/src/app/projects/projects-list/projects-list.component.spec.ts`, and `frontend/src/app/users/users-list/users-list.component.spec.ts` for the new chip/avatar/`friendlyDate` selectors and formatted-text assertions (asserting "Jul 17, 2026"-style output, not raw ISO) — depends on T031, T032, T033, T034

**Checkpoint**: Status/priority/assignee are visually identical everywhere
and no raw ISO date is visible anywhere — quickstart.md sections 3 and 4
pass.

---

## Phase 6: User Story 4 - Feedback and empty states (Priority: P3)

**Goal**: Create/edit/delete actions show a success/failure toast instead of
silent navigation; empty lists show a friendly icon+message(+action) instead
of plain text.

**Independent Test**: Create, edit, and delete a project/work item and see a
toast each time; open a project with zero work items and see the empty-state
component instead of "No work items yet".

### Tests for User Story 4

- [ ] T037 [P] [US4] Write `NotificationService` spec asserting `success(message)`/`error(message)` call `MatSnackBar.open` with the message and the correct token-driven `panelClass`, in `frontend/src/app/shared/notification.service.spec.ts` (must fail before T039 exists)
- [ ] T038 [P] [US4] Write `EmptyStateComponent` spec asserting `icon`/`message` inputs render and the projected `action` slot appears only when content is provided, in `frontend/src/app/shared/empty-state/empty-state.component.spec.ts` (must fail before T040 exists)

### Implementation for User Story 4

- [ ] T039 [P] [US4] Create `NotificationService` (`success(message: string)`, `error(message: string)`, wrapping `MatSnackBar` with token-driven styling and a short auto-dismiss duration) at `frontend/src/app/shared/notification.service.ts` — makes T037 pass
- [ ] T040 [P] [US4] Create `EmptyStateComponent` (`icon: string`, `message: string` inputs; `action` content-projection slot) at `frontend/src/app/shared/empty-state/empty-state.component.ts` (+ `.html` + `.css`) — makes T038 pass
- [ ] T041 [US4] Call `NotificationService.success`/`.error` from `onSubmit` in `frontend/src/app/projects/project-form/project-form.component.ts` (create/edit project) — depends on T039
- [ ] T042 [US4] Call `NotificationService.success`/`.error` from `onSubmit` in `frontend/src/app/projects/work-item-form/work-item-form.component.ts` (create/edit work item) — depends on T039
- [ ] T043 [US4] Call `NotificationService.success`/`.error` from `onDelete`/`onDeleteProject` in `frontend/src/app/projects/project-detail/project-detail.component.ts` (delete work item, delete project) — depends on T039
- [ ] T044 [US4] Call `NotificationService.success`/`.error` from `onDelete` in `frontend/src/app/projects/work-item-detail/work-item-detail.component.ts` — depends on T039
- [ ] T045 [P] [US4] Replace the "No work items yet" (tree and flat views) and "No children yet" plain-text empty states with `<app-empty-state>` (+ an "Add work item" action where applicable) in `frontend/src/app/projects/project-detail/project-detail.component.html` and `frontend/src/app/projects/work-item-detail/work-item-detail.component.html` — depends on T040 (the "No items match your filters." case keeps its own message text but also moves to `<app-empty-state>`, with no action projected)
- [ ] T046 [P] [US4] Add a matching `<app-empty-state>` for the Users list's zero-results case in `frontend/src/app/users/users-list/users-list.component.html` — depends on T040
- [ ] T047 [US4] Update `frontend/src/app/projects/project-form/project-form.component.spec.ts`, `frontend/src/app/projects/work-item-form/work-item-form.component.spec.ts`, `frontend/src/app/projects/project-detail/project-detail.component.spec.ts`, and `frontend/src/app/projects/work-item-detail/work-item-detail.component.spec.ts` to assert the notification service is invoked on success/failure and that empty states render `<app-empty-state>` — depends on T041, T042, T043, T044, T045, T046

**Checkpoint**: All four user stories complete — the feature matches
spec.md's Success Check in full.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Documentation and final regression validation across all
stories.

- [ ] T048 [P] Write the brief design-system usage doc (which tokens exist, which shared components exist, when to use them — FR-017) at `frontend/src/app/shared/README.md`, linking to `contracts/design-system-components.md` in the spec directory for the full contract. **Acceptance criteria**: a reader new to this codebase, given only this doc, can (a) name where the token file lives and list its categories (color/spacing/radius/layout), (b) list all seven shared components/services/pipe by selector or import path and one-line purpose each, and (c) state the rule for when to use a shared component instead of ad-hoc markup — without opening any component source file
- [ ] T049 Run the full backend suite (`cd backend/TaskFlow.Api && dotnet test`) and confirm 100% pass with no changes needed (this feature touches no backend files) — SC-008
- [ ] T050 Run the full frontend suite (`cd frontend && npm test`) and confirm 100% pass, with any reduced test count limited to selector updates explicitly made in T015/T021/T036/T047 — SC-008
- [ ] T051 Walk through `quickstart.md` sections 1-9 manually in a running app (`ng serve` + backend running) and confirm every scenario passes, including the keyboard-focus check (FR-016), the tablet-breakpoint resize check (SC-004), and the Feature 003 tree-view/"Add child" pre-select regression checks folded into quickstart.md section 3
- [ ] T052 Single cross-cutting checkpoint: with the app running, open the same status value and the same priority value side-by-side (or in quick succession) across project-detail tree view, project-detail flat view, and work-item-detail — confirm pixel-identical chip color/label in all three for every status and every priority value (not just the one instance sampled in quickstart.md section 3) — SC-002
- [ ] T053 Add a "Feature 004: App Shell & Design System" entry to README.md's "What I learned" log (matching the style of the Feature 001-003 entries already there), per the constitution's Definition of Done item 5

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — run first
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Story 1 (Phase 3)**: Depends on Foundational only
- **User Story 2 (Phase 4)**: Depends on Foundational + US1's `AppShellComponent` existing (T008) to wire the sidebar into
- **User Story 3 (Phase 5)**: Depends on Foundational only for its own components (chips/avatar/pipe are independent of the shell); T035 (sidebar user-block avatar) additionally depends on US2's T019
- **User Story 4 (Phase 6)**: Depends on Foundational only
- **Polish (Phase 7)**: Depends on all four user stories being complete

### User Story Dependencies (spec.md priorities)

- **US1 (P1)**: No dependency on other stories — the true MVP slice
- **US2 (P1)**: Needs `AppShellComponent` (US1/T008) to exist as a mounting point, but its own nav/logout/breakpoint logic is independently built and tested
- **US3 (P2)**: Its chip/avatar/pipe components (T022-T030) are independently buildable in parallel with US1/US2; only the *retrofit* tasks (T031-T034) benefit from US1's page-header retrofits already being in place, and T035 needs US2's sidebar to exist
- **US4 (P3)**: Fully independent of US1/US2/US3's components — `NotificationService` and `EmptyStateComponent` need only Foundational tokens; wiring into project/work-item forms and detail pages can happen any time after those files exist (which they already do, pre-feature)

### Within Each User Story

- Tests written and confirmed failing before their corresponding implementation task (Constitution Principle I)
- Shared components before the page retrofits that consume them
- Page retrofits before the spec-file updates that assert on their new markup

### Parallel Opportunities

- T002 and (after it) T003/T004 have a strict one-then-two ordering; everything else marked [P] within a phase touches a different file and can run concurrently
- Once Foundational (Phase 2) is done, US1, US3, and US4's component-creation tasks (T007-T008, T022-T030, T037-T040) can all proceed in parallel by different contributors; US2 only needs to wait on US1's T008
- Within US3, all four new component/pipe creation tasks (T027-T030) are parallel; within US4, both new pieces (T039-T040) are parallel

---

## Parallel Example: User Story 3

```bash
# Tests (after Foundational is done, all independent files):
Task: "StatusChipComponent spec in frontend/src/app/shared/status-chip/status-chip.component.spec.ts"
Task: "PriorityChipComponent spec in frontend/src/app/shared/priority-chip/priority-chip.component.spec.ts"
Task: "UserAvatarComponent spec in frontend/src/app/shared/user-avatar/user-avatar.component.spec.ts"
Task: "FriendlyDatePipe spec in frontend/src/app/shared/friendly-date.pipe.spec.ts"

# Implementation (after T026 union types land):
Task: "StatusChipComponent in frontend/src/app/shared/status-chip/status-chip.component.ts"
Task: "PriorityChipComponent in frontend/src/app/shared/priority-chip/priority-chip.component.ts"
Task: "UserAvatarComponent in frontend/src/app/shared/user-avatar/user-avatar.component.ts"
Task: "FriendlyDatePipe in frontend/src/app/shared/friendly-date.pipe.ts"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (design tokens — blocks everything)
3. Complete Phase 3: User Story 1 (shell + page header + full-width fix)
4. **STOP and VALIDATE**: quickstart.md sections 1 and 5 — shell applies
   everywhere, list pages use full width
5. Review with the maintainer before continuing (Constitution Principle VIII)

### Incremental Delivery

1. Setup + Foundational → tokens ready
2. US1 → shell exists everywhere → demo (MVP)
3. US2 → navigable, role-aware sidebar → demo
4. US3 → consistent chips/avatars/dates → demo
5. US4 → toasts + empty states → demo (feature complete, matches spec.md's
   Success Check)
6. Phase 7 → docs + full regression + quickstart walkthrough

### Commit Convention

Per the constitution's Commit Convention: one commit per user story (or
logical sub-group within a large story), `feat: US<N> <summary>` referencing
the passing test count, e.g. `feat: US3 status/priority chips, avatars,
friendly dates (187/187 frontend tests pass)`. Foundational and Polish
phases commit as `chore:`/`docs:` per the same convention.

---

## Notes

- [P] tasks touch different files and have no incomplete-task dependency
- Every retrofit task specifies the exact template file being changed
- Tests must be written and observed failing before their implementation task
  (Constitution Principle I) — this applies to the pure-logic specs (chip
  color mapping, avatar hashing, date formatting, nav filtering, notification
  service); template-heavy component specs are updated alongside their
  retrofit task per the constitution's "do not chase 100% template coverage"
  carve-out
- Commit after each user story (or logical sub-group) completes
- Stop at any checkpoint to validate that story independently before
  continuing
- No task in this file touches `backend/` — FR-015/FR-018 require zero
  backend changes and zero regressions in the existing backend test suite
