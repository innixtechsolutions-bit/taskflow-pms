# Implementation Plan: App Shell & Design System

**Branch**: `004-app-shell-design-system` | **Date**: 2026-07-18 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/004-app-shell-design-system/spec.md`

## Summary

Replace the current bare top-bar (`HeaderComponent`) + unstyled-page layout with a
persistent Angular Material `mat-sidenav` shell (dark sidebar, active-route nav,
bottom user menu) and a small set of reusable, token-driven presentation
components (status chip, priority chip, avatar, empty state, page header) plus a
`MatSnackBar`-backed toast service and a friendly-date pipe. Every existing
authenticated screen (Dashboard placeholder, Projects list, project detail
tree/flat, work item detail, Users list) is retrofitted onto these pieces with
**no change to routes, guards, API calls, or the 960px `.app-content` wrapper's
job being replaced by a wider token-driven max-width**. This is a frontend-only,
purely presentational feature — no backend or database changes.

## Technical Context

**Language/Version**: TypeScript ~6.0.2 (Angular 22, strict mode)

**Primary Dependencies**: Angular 22 (standalone components, zoneless change
detection, signals), Angular Material 22 (`MatSidenavModule`, `MatSnackBarModule`,
`MatMenuModule`, `MatToolbarModule`, `MatIconModule` already available as
dependencies but not all yet used), Angular CDK 22 (`BreakpointObserver` for the
tablet-breakpoint collapse)

**Storage**: N/A — no persistence changes; reads existing data already returned
by current API responses (`fullName`, `role`, `status`, `priority`, `createdAt`,
`updatedAt`, `dueDate` on existing DTOs)

**Testing**: Vitest via `@angular/build:unit-test` (`ng test` / `npm test`),
co-located `*.spec.ts` per component, `TestBed` + `provideRouter([])` pattern
already used throughout `frontend/src/app/**/*.spec.ts`

**Target Platform**: Browser (Angular SPA), tablet width (~768px) and up per
spec's explicit scope

**Project Type**: Web application — this feature touches `frontend/` only;
`backend/` is unmodified

**Performance Goals**: No new network calls; shell/nav render and breakpoint
collapse must not introduce visible layout jank (single reflow on resize, no
content-visibility flicker)

**Constraints**: No new backend endpoints or DTO fields (per spec Assumptions);
all existing backend and frontend automated tests must continue to pass (FR-018,
SC-008); no `any` in new TypeScript; keyboard focus must remain visible on all
interactive elements (FR-016)

**Scale/Scope**: ~5 retrofitted screens (Dashboard placeholder, Projects list,
project detail, work item detail, Users list) + 1 shell component + 5 shared
presentation components/services (status chip, priority chip, avatar, empty
state, page header, toast service, friendly-date pipe) + 1 design-token
stylesheet; target under ~15 new/changed files is unrealistic to hold strictly
here given the number of screens retrofitted, but each individual change stays
small and mechanical (swap markup for a shared component) — see Complexity
Tracking below for the explicit justification required by Principle VII.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Test-First Development**: PASS (with plan). New pure logic (avatar color
  hash, friendly-date pipe, breakpoint-driven nav collapse state, nav-item
  visibility-by-role) gets Vitest unit tests written first. Template-heavy
  retrofits (swapping plain text for a chip/avatar component) are verified via
  existing/updated component specs asserting the new component selector and
  content are present — not chasing template coverage for its own sake, per the
  constitution's explicit carve-out.
- **II. Secure by Default**: PASS, no gate concerns. FR-015 explicitly forbids
  touching guards/API calls/permissions. The Users nav item's visibility is
  display-only; `adminGuard` on the `/users` route (server-verified role via the
  existing JWT-backed API) remains the only real boundary, unchanged.
- **III. Clarity Over Cleverness**: PASS. No new design patterns (no state
  library, no repository layer — this is presentation-only). Chips/avatar/empty
  state/page header are plain standalone components with `input()`s, not a
  generic "widget framework." Design tokens are static CSS custom properties, not
  a runtime theming service. See research.md for the specific "why not X"
  reasoning behind each of these choices.
- **IV. Consistent Code Quality & Review Gates**: PASS. Strict TypeScript, no
  `any`; signals for the shell's collapse state; Angular Material for the
  sidenav/snackbar/menu (per the fixed Technology Stack); follows existing
  co-located spec-file and service-based-data-fetching conventions already in
  the codebase.
- **V. API Contract Stability & Versioning**: PASS, not applicable — no API
  changes.
- **VI. Teach While Building**: N/A for this feature in the .NET/SQL sense (no
  backend files touched), but the equivalent Angular-side teaching moments
  (zoneless signals driving a CDK `BreakpointObserver` subscription, a custom
  Angular pipe, `MatSnackBar` injection) get the same brief "why" comments on
  first appearance, consistent with the spirit of the principle.
- **VII. Incremental, Feature-by-Feature Delivery**: CONDITIONAL PASS — see
  Complexity Tracking. Five screens must be retrofitted for the feature to be
  coherent (a half-retrofitted shell would violate FR-009/SC-002's "identical
  everywhere" requirement), which pushes changed-file count past the ~15 file
  guideline. Each individual file's change is small and mechanical.
- **VIII. Human in the Loop**: PASS — no auto-chaining; `/speckit-implement`
  stops for review as usual, and no `[NEEDS CLARIFICATION]` markers remain in
  the spec.

## Project Structure

### Documentation (this feature)

```text
specs/004-app-shell-design-system/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md         # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
│   └── design-system-components.md
├── checklists/
│   └── requirements.md
├── visual-reference.png
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
frontend/
├── src/
│   ├── design-tokens.scss          # NEW — CSS custom properties: color
│   │                                 (primary/surface/status/priority),
│   │                                 spacing scale, radius, content max-width,
│   │                                 tablet breakpoint constant
│   ├── styles.css                  # MODIFIED — import design-tokens.scss;
│   │                                 remove/relax the 960px .app-content cap
│   │                                 (superseded by the shell's own content
│   │                                 width, see research.md #7)
│   ├── material-theme.scss         # MODIFIED — align Material theme palette
│   │                                 with design-tokens.scss primary color
│   └── app/
│       ├── app.routes.ts           # UNCHANGED — no route/guard edits
│       ├── app.html / app.css      # MODIFIED — render <app-shell> instead of
│       │                             <app-header> + bare <main class="app-content">
│       │                             for authenticated routes; auth pages
│       │                             (login/register) stay outside it
│       ├── shared/
│       │   ├── header/             # REMOVED — superseded by shell/sidebar-nav
│       │   ├── shell/              # NEW — AppShellComponent (mat-sidenav-container)
│       │   ├── sidebar-nav/        # NEW — SidebarNavComponent (nav items, active
│       │   │                         state, user block, logout menu, icon-rail
│       │   │                         collapse via CDK BreakpointObserver)
│       │   ├── page-header/        # NEW — PageHeaderComponent (title, subtitle,
│       │   │                         optional action content projection)
│       │   ├── status-chip/        # NEW — StatusChipComponent
│       │   ├── priority-chip/      # NEW — PriorityChipComponent
│       │   ├── user-avatar/        # NEW — UserAvatarComponent (initials + hashed
│       │   │                         color, optional adjacent name)
│       │   ├── empty-state/        # NEW — EmptyStateComponent (icon, message,
│       │   │                         optional primary-action content projection)
│       │   ├── notification.service.ts   # NEW — thin MatSnackBar wrapper
│       │   │                               (success/error toasts)
│       │   └── friendly-date.pipe.ts     # NEW — 'Jul 17, 2026' formatting +
│       │                                   null placeholder
│       ├── home/                          # MODIFIED — page-header retrofit
│       ├── projects/
│       │   ├── projects-list/             # MODIFIED — page-header, cards/table
│       │   │                                full-width, friendly dates, toasts
│       │   ├── project-detail/            # MODIFIED — page-header, tree view in
│       │   │                                a card w/ chips+avatars, flat view
│       │   │                                full-width, empty states, toasts
│       │   ├── work-item-detail/          # MODIFIED — chips, avatar, friendly
│       │   │                                dates, "No children yet" empty state
│       │   └── work-item-form/            # MODIFIED — toasts on save; no field
│       │                                    logic changes
│       └── users/
│           └── users-list/                # MODIFIED — page-header, avatar,
│                                             friendly dates, full-width table,
│                                             empty state
└── (backend/ — UNCHANGED, no files touched)
```

**Structure Decision**: Single existing `frontend/` Angular app (Option 1-style,
already established by Features 001-003). No new projects, no lazy-loaded
feature modules, no NgModules — all new pieces are standalone components/
services/pipes added under `frontend/src/app/shared/`, following the existing
flat-and-obvious layering already used by `auth/`, `projects/`, and `users/`.
The `shared/header/` directory is removed as part of the retrofit since its
responsibility (nav + user menu + logout) is fully absorbed by the new
`shared/shell/` + `shared/sidebar-nav/` pair.

## Complexity Tracking

> Fill ONLY if Constitution Check has violations that must be justified

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|---------------------------------------|
| Changed-file count likely exceeds the ~15-file guideline in Principle VII | FR-009/SC-002 require status and priority chips to render with *identical* color/label everywhere, and FR-001/SC-001 require *every* authenticated page to sit inside the same shell. Retrofitting only some of the five existing screens would leave the app in a visibly inconsistent state that fails the feature's own acceptance criteria and success checks. | Splitting this into multiple mini-features (e.g., "shell only" then "chips only" per screen) was considered, but each slice would ship a visibly half-styled app in between (some screens on the old header, some on the new shell), which is a worse user- and reviewer-facing state than one coherent, mechanically-repetitive change reviewed as a single story-grouped diff (per the Commit Convention, still committed as separate logical commits per user story, just within one feature/branch). |
