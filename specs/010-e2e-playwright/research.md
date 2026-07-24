# Phase 0 Research: E2E Testing Foundation (Playwright)

All items below were resolvable from the existing codebase and the spec's
own explicit defaults — no `[NEEDS CLARIFICATION]` markers carried over from
`spec.md`, so this phase records *design* decisions rather than resolving
ambiguity.

## 1. Test runner, language, and location

**Decision**: `@playwright/test` with TypeScript, added as a devDependency
to `frontend/package.json`, with all suite code under a new `frontend/e2e/`
directory and a `frontend/playwright.config.ts`.

**Rationale**: Playwright's official test runner already provides
retries, trace/screenshot-on-failure, and parallel spec-file execution
(FR-009–FR-011) without extra tooling. Placing it inside `frontend/` keeps
it a single-devDependency addition (FR-017's framing) and TypeScript keeps
it consistent with the rest of the Angular codebase's tooling and the
constitution's "strict TypeScript, no `any`" rule (Principle IV). A
dedicated `e2e/` folder (sibling to `src/`) keeps Vitest (`ng test`, which
scopes to `src/`) from ever discovering or attempting to run `.spec.ts`
Playwright files, satisfying FR-014's separation requirement without any
Vitest config changes.

**Alternatives considered**:
- *Standalone top-level `e2e/` project with its own `package.json`* —
  rejected as unnecessary extra tooling (a second `node_modules`, a second
  lockfile) for no isolation benefit `frontend/e2e/` doesn't already give;
  conflicts with Clarity Over Cleverness (Principle III).
- *Cypress* — rejected; not mentioned by the spec, and Playwright's
  built-in trace viewer and multi-browser-engine architecture (even though
  only Chromium is used here per FR-018) is the better fit for a team that
  may want to revisit cross-browser later without switching tools.

## 2. Test database provisioning and reset

**Decision**: A dedicated SQL Server database, `TaskFlowDb_E2E` by
convention, brought to the latest schema with the project's existing EF
Core migrations (`dotnet ef database update`, run from
`backend/TaskFlow.Api/`) as a **documented manual prerequisite step**
before starting the backend against it — not automated inside Playwright's
`globalSetup`.

**Rationale**: The backend's `AdminSeeder` runs unconditionally at
`Program.cs` startup and needs the `Users` table to already exist, so
migrations must be applied *before* the backend process starts, not while
it's already holding open connections. A `dotnet ef database drop` while
the backend has live connections would fail (SQL Server refuses to drop a
database with active connections) or corrupt in-flight state — automating
that sequence from `globalSetup.ts` (which runs *after* Playwright's own
config assumes the backend is already reachable) would be fragile.
FR-013 and the spec's Assumptions explicitly leave "starting or assuming an
already-running backend and frontend... whichever this project's setup
makes simpler" to the implementer — a documented reset-then-start sequence
in `quickstart.md`/README is the simpler, more robust choice, consistent
with Clarity Over Cleverness (Principle III).

**Alternatives considered**:
- *`globalSetup.ts` shells out to `dotnet ef database drop --force` +
  `update` before tests run* — rejected per the live-connection problem
  above; also silently assumes `dotnet` CLI is on PATH inside a Node
  process, adding a hidden cross-toolchain dependency to what should be a
  simple "run Playwright" command.
- *Per-test database reset* — rejected as unnecessary; see Decision 5
  below (test-level isolation is achieved by test-created data, not
  full-database resets).

## 3. Manager/Developer account provisioning (no existing seeding mechanism)

**Decision**: `frontend/e2e/global-setup.ts` provisions the seeded Manager
and Developer accounts by calling the app's own existing public APIs, not
by adding new backend seeding code:
1. Log in as the config-seeded Admin (`POST /api/auth/login`) — this
   account already exists via the existing `AdminSeeder` +
   `Admin:Email`/`Admin:Password` user-secrets, unmodified.
2. Register a Manager-candidate and a Developer test user
   (`POST /api/auth/register` — both land as `Developer` per the app's
   existing default role, per Story 1's own acceptance scenario).
3. Promote the Manager-candidate using the Admin's token
   (`PUT /api/users/{id}/role` with `{"role": "Manager"}`) — the same
   endpoint exercised by the Role-change journey itself (Story 7).

**Rationale**: The codebase has no existing Manager/Developer seeding —
`AdminSeeder` (`backend/TaskFlow.Api/Startup/AdminSeeder.cs`) only ever
seeds a single Admin from config, and only if no Admin row exists yet
(idempotent, safe to rerun). Spec FR-007 requires seeded accounts "via the
same configuration-based seeding mechanism the app already uses... not a
special test-only backdoor." Since no such mechanism exists for
Manager/Developer, the closest-fit, zero-new-backend-code interpretation is
to provision them through the *same public APIs a real Admin would use* —
config-based seeding for Admin (unchanged), ordinary registration +
role-management for the other two. This adds no production code and
reuses exactly the surfaces the spec's own Story 7 already exercises.

**Alternatives considered**:
- *Add Manager/Developer seeding to `AdminSeeder`* — rejected: this is a
  production code change beyond the one narrow exception FR-017 allows
  (a selector hook), and FR-016 forbids adding application behavior for
  this feature.
- *A test-only seeding endpoint or backdoor* — explicitly rejected by
  FR-007's own wording.

**Idempotency note**: `global-setup.ts` first attempts registration and
treats a `409 Conflict` (`EmailAlreadyExistsException`, already used by
`AuthController.Register`) as "already provisioned, log in instead" —
so reruns against a database that wasn't fully reset still work.

## 4. Test isolation strategy (FR-005)

**Decision**: One full database reset per full local *run* of the suite
(Decision 2), not per individual test. Within a run, each journey creates
its own scoped data (its own project, its own work items, etc., using
unique-enough names such as a timestamp or random suffix) rather than
relying on a specific pre-existing row.

**Rationale**: A true per-test database reset would multiply run time far
past the "practical to run locally in a few minutes" target (FR-009,
SC-004) for only 7 journeys. Scoping each journey's test data to
freshly-created, uniquely-named entities gives the same "no cross-test
leftover data can cause a false pass/fail" guarantee FR-005 asks for,
without the cost of a reset between every test.

**Alternatives considered**:
- *Full DB reset before every spec file* — rejected as too slow relative
  to SC-004 for the value it adds, given unique-naming already prevents
  collisions.
- *Shared fixture data reused read-only across journeys* — rejected: it
  would make journeys depend on each other's ordering/state, which
  directly contradicts "each independently runnable" (spec's Suite
  Structure acceptance criteria and FR-002).

## 5. Selector strategy (FR-008)

**Decision**: Prefer, in order: (a) Playwright's role/label locators
(`getByRole`, `getByLabel`) — Angular Material's `mat-form-field` +
`mat-label` already produce accessible labels, so login/register forms
need no new markup; (b) existing stable, human-authored class names, ids,
and text content already present in the codebase (e.g. `.tree-view-toggle`,
`.flat-view-toggle`, `.active-sprint-toggle`, `id="tree-work-item-{id}"`,
and — confirmed by reading `board-card.component.html` — the `.card-key`
element, which already renders a unique `#{{ card().id }}` per card).

No new `data-testid` or `id` attribute is needed anywhere, including the
board: a specific card for the drag journey can be targeted with
`page.locator('.board-card').filter({ hasText: `#${workItemId}` })`, which
is exactly as resilient as a dedicated id and requires zero production
changes.

**Rationale**: A grep of `frontend/src` found zero existing `data-testid`
attributes, but reading the actual component templates (not just
guessing) showed the codebase already exposes what's needed: deliberately
named, stable CSS classes and ids for the tree view, and unique per-card
text (`#{{id}}`) for the board. FR-017 frames a selector-hook addition as
an *allowed exception if needed* — since it turns out not to be needed,
the simpler, fully-zero-production-change path wins outright (Clarity Over
Cleverness, Principle III, and FR-017's own preference for no production
changes when avoidable).

**Alternatives considered**:
- *Add `id="board-card-{id}"` to `board-card.component.html`* — this was
  the original plan-time assumption before the template was actually read;
  superseded once the existing `.card-key` `#{{id}}` text was confirmed to
  already serve the same purpose without touching production markup.
- *`data-testid` on every interactive element* — rejected as broader than
  the "kept minimal" instruction in FR-017; every element in scope already
  resolves cleanly via role/label or an existing class/id/text.
- *CSS structural selectors (nth-child, etc.)* — explicitly what FR-008
  rules out.

## 6. Failure artifacts and retry (FR-010, FR-011)

**Decision**: `playwright.config.ts` sets `retries: 1`, `trace:
'retain-on-failure'`, `screenshot: 'only-on-failure'`, output to the
default `test-results/` and `playwright-report/` directories (git-ignored).

**Rationale**: These are Playwright's built-in mechanisms for exactly the
behavior FR-010/FR-011 describe — no custom retry or artifact-capture code
needed (Clarity Over Cleverness).

## 7. Browser scope (FR-018)

**Decision**: `playwright.config.ts` defines a single project, Chromium,
via `devices['Desktop Chrome']`.

**Rationale**: Directly matches FR-018 and the spec's explicit
out-of-scope note on a cross-browser matrix.

## 8. npm script wiring (FR-013, FR-014)

**Decision**: Add `"e2e": "playwright test"` to `frontend/package.json`
`scripts`, alongside (not replacing) the existing `"test": "ng test"`.
`playwright.config.ts` sets `use.baseURL` to the frontend dev server
(`http://localhost:4300`, matching `angular.json`'s configured port) and
does **not** set a Playwright `webServer` entry — the backend and frontend
are documented prerequisites the developer starts first (see
`quickstart.md`), consistent with Decision 2's reasoning about DB-reset
sequencing.

**Rationale**: A single `npm run e2e` command satisfies FR-013's "single
command runs the full suite" while keeping it fully separate from
`npm test`/`ng test` and `dotnet test` per FR-014.

## 9. Direct API calls from within tests (FR-012)

**Decision**: Use Playwright's built-in `request` fixture (an
`APIRequestContext`) for the permission-boundary journey's direct
server-side check: log in as the seeded Developer via `POST /api/auth/login`
to obtain a JWT, then call `DELETE /api/projects/{id}` with that token and
assert a `403 Forbidden` response — exercising the same
`[Authorize(Roles = "Manager,Admin")]` guard documented on
`ProjectsController.Delete` (`backend/TaskFlow.Api/Controllers/ProjectsController.cs`).
The same `request` fixture is reused by `global-setup.ts` for account
provisioning (Decision 3).

**Rationale**: Playwright's `request` fixture needs no extra HTTP client
dependency and runs both browser-driven and direct-API assertions from the
same test framework, keeping the suite's dependency surface minimal.
