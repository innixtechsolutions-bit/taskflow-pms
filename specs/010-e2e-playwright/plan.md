# Implementation Plan: E2E Testing Foundation (Playwright)

**Branch**: `010-e2e-playwright` | **Date**: 2026-07-24 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/010-e2e-playwright/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Add a Playwright + TypeScript E2E suite under `frontend/e2e/` that drives the
real Angular app against the real ASP.NET Core backend and a dedicated
`TaskFlowDb_E2E` SQL Server database (schema via the existing EF Core
migrations), covering the seven curated risk journeys from the spec (auth,
permission boundaries, navigation, project/work-item CRUD, board drag,
sprint, role change). **Zero production code changes**: the codebase
already exposes enough stable selectors (accessible labels on Material
form fields, existing tree-view ids, and the board card's existing unique
`#{{id}}` text) that FR-017's allowed selector-hook exception turns out not
to be needed. Test accounts are provisioned each run via the app's own
existing APIs (config-seeded Admin + public register/role-change endpoints
for Manager and Developer), not a new seeding backdoor.

## Technical Context

**Language/Version**: TypeScript (Playwright test files), running on the
Node.js version already required by Angular 22 tooling in `frontend/`. No
new language introduced to the repo.

**Primary Dependencies**: `@playwright/test` (new frontend devDependency,
Chromium browser only). Exercises the existing Angular 22 frontend
(`frontend/`) and ASP.NET Core 10 Web API backend (`backend/TaskFlow.Api/`)
unmodified.

**Storage**: A dedicated SQL Server database (`TaskFlowDb_E2E` by
convention), schema brought up to date via the project's existing EF Core
migrations (`backend/TaskFlow.Api/Data/Migrations/`) — not `EnsureCreated`,
and not a new migration mechanism.

**Testing**: Playwright Test (`@playwright/test`), one spec file per journey
under `frontend/e2e/tests/`. Existing xUnit (`backend/TaskFlow.Api.Tests`)
and Vitest (`frontend` unit tests) suites are untouched and unaffected —
this is a third, independent test runner invoked by its own command.

**Target Platform**: Developer's local machine (Windows), against a running
`dotnet run` backend and a running Angular dev server (or built `dist`),
driving Chromium only (bundled by Playwright). No CI wiring in this feature.

**Project Type**: Web application (existing `frontend/` + `backend/`
structure). This feature adds a new `frontend/e2e/` directory and
`frontend/playwright.config.ts`; no new top-level project.

**Performance Goals**: Full suite completes in a few minutes locally
(SC-004); each of the 7 journeys is independently bounded so a single slow
journey cannot stall the whole run.

**Constraints**: No application/production behavior or markup changes
(FR-016; FR-017's selector-hook exception is available but, per
research.md Decision 5, turns out not to be needed); suite must exercise
the real backend/DB, never mocked APIs (FR-006); one automatic retry only,
no silent skips (FR-010); suite is fully separate from `dotnet test` /
`ng test` (FR-014).

**Scale/Scope**: 7 independently-runnable spec files, a curated smoke suite
of the highest-risk journeys — not exhaustive per-feature coverage.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Applies? | Assessment |
|---|---|---|
| I. Test-First Development | Indirectly | This feature's deliverable *is* tests; there is no separate "write a test for the test suite" step. Each of the 7 spec files is written directly from the spec's acceptance scenarios before any page-object/support code is filled in, preserving the red→green spirit at the suite level. |
| II. Secure by Default | Yes — reinforced | The permission-boundary journey (FR-012) makes at least one direct API call to prove server-side `[Authorize(Roles=...)]` enforcement, not just UI absence — this suite is partly a *test* of this principle elsewhere in the app, not a risk to it. No route guard or endpoint authorization logic is touched. |
| III. Clarity Over Cleverness | Yes | Plain Playwright Page Object files (one class per page/component), no custom test-framework abstraction layer, no retry/wait helpers beyond Playwright's built-ins. |
| IV. Consistent Code Quality & Review Gates | Yes | `frontend/e2e/**` is TypeScript, strict mode, no `any`, linted the same as the rest of `frontend/`. |
| V. API Contract Stability & Versioning | Yes — no changes | The suite only *consumes* existing endpoints (`/api/auth/register`, `/api/auth/login`, `/api/users/{id}/role`, `/api/projects/{id}`, etc., see [contracts/api-usage.md](./contracts/api-usage.md)); no request/response shape or migration is added or changed. |
| VI. Teach While Building | N/A | Applies to generated C# files; this feature adds no C# files, and adds no Angular markup changes either (see Summary). |
| VII. Incremental, Feature-by-Feature Delivery | Flagged | The file *count* (7 spec files + ~8 page objects + config/fixtures + README updates) will likely exceed the "~15 files" guideline. This is a scope property of the requirement (FR-002: one independently-runnable file per journey), not accidental scaffolding — see Complexity Tracking below. |
| VIII. Human in the Loop | Yes | No auto-chaining; maintainer reviews before Feature 011 begins, same as every prior feature. |

No unjustified violations. One guideline-level deviation (file count) is
recorded in Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/010-e2e-playwright/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
│   └── api-usage.md
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
# Existing web application structure (frontend + backend) — fully unmodified;
# see research.md Decision 5 for why no selector-hook markup change is needed.
backend/
├── TaskFlow.Api/                  # unchanged
└── TaskFlow.Api.Tests/            # unchanged (xUnit, untouched by this feature)

frontend/
├── src/                           # completely unchanged
├── playwright.config.ts           # NEW — Chromium-only project, trace/
│                                   # screenshot-on-failure, 1 retry
├── package.json                   # EDIT — add @playwright/test devDependency
│                                   # and an "e2e" script (separate from "test")
└── e2e/                           # NEW — the whole feature lives here
    ├── global-setup.ts            # provisions Manager/Developer test accounts
    │                               # via existing register/role-change APIs
    ├── fixtures/
    │   └── auth.fixture.ts        # logged-in-as-{role} Playwright fixtures
    ├── pages/                     # Page Object Model, one file per page/component
    │   ├── login.page.ts
    │   ├── register.page.ts
    │   ├── sidebar-nav.page.ts
    │   ├── project-detail.page.ts # covers list/tree/board/backlog view modes
    │   ├── work-item-modal.page.ts
    │   ├── sprint-dialogs.page.ts
    │   └── users.page.ts
    └── tests/                     # one spec file per journey (FR-002)
        ├── auth.spec.ts
        ├── permissions.spec.ts
        ├── navigation.spec.ts
        ├── project-work-items.spec.ts
        ├── board-drag.spec.ts
        ├── sprint.spec.ts
        └── role-change.spec.ts
```

**Structure Decision**: Web application structure already exists
(`backend/` + `frontend/`); this feature adds a self-contained `frontend/e2e/`
directory plus a root-level-of-`frontend` `playwright.config.ts`, keeping
Playwright a frontend devDependency (matching FR-017's "Playwright is a
devDependency" framing) while staying fully separate from `frontend/src`
(Vitest's scope) and `backend/TaskFlow.Api.Tests` (xUnit's scope), per
FR-014.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| File count likely exceeds the ~15-file incremental-delivery guideline (Principle VII) | FR-002 requires each of the 7 journeys to be its own independently-runnable spec file, and FR-008's resilient-selector requirement is best met with one Page Object file per page/component rather than inline selectors repeated across specs | Collapsing journeys into fewer spec files would violate FR-002 directly; collapsing page objects into one shared file would produce a large, low-cohesion file that makes selectors harder to keep resilient (violates FR-008) and harder to review than several small, single-purpose ones — each individual file stays small and easy to read in one sitting, which is the underlying intent of Principle VII even though the file *count* is higher than typical |

## Post-Design Constitution Check

*Re-evaluated after Phase 1 (data-model.md, contracts/api-usage.md, quickstart.md).*

No new violations introduced by the design artifacts:

- **II. Secure by Default**: `contracts/api-usage.md` confirms the only
  privileged endpoint the suite calls directly (`PUT /api/users/{id}/role`)
  is called with the seeded Admin's own token, exactly as a real Admin
  would; the permission-boundary direct call (`DELETE /api/projects/{id}`
  as Developer) exists specifically to *prove* the guard, not bypass it.
- **III. Clarity Over Cleverness**: `data-model.md`'s Page Object table maps
  one file to one existing Angular component each — no shared "god object"
  introduced during design.
- **V. API Contract Stability**: `contracts/api-usage.md` lists only
  pre-existing, unmodified endpoints.
- **VII. Incremental Delivery**: the Phase 1 artifacts didn't add scope
  beyond what was already flagged in Complexity Tracking above — still one
  Page Object per component, one spec file per journey, no extra layers.

Gate: **PASS**. Ready for `/speckit-tasks`.
