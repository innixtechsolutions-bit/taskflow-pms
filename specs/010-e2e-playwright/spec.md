# Feature Specification: E2E Testing Foundation (Playwright)

**Feature Branch**: `010-e2e-playwright`

**Created**: 2026-07-24

**Status**: Draft

**Input**: User description: "Add an end-to-end test suite (Playwright) that exercises TaskFlow through a real browser against the real backend and database — covering the critical user journeys that unit and integration tests cannot: full page navigation, routing, guards, and cross-page state, exactly the category of bug that has twice slipped past our test suites so far (a routing regression in Feature 004, a flaky Material harness timing issue in Feature 009)."

## User Scenarios & Testing *(mandatory)*

<!--
  This feature is testing infrastructure, not a user-facing feature. The
  "user stories" below describe the end-to-end journeys the suite must
  verify, written from the perspective of the person actually using
  TaskFlow through a browser. Acceptance scenarios describe the app
  behavior the suite proves still works, not the suite's own mechanics
  (those live in Requirements).
-->

### User Story 1 - Auth journey (Priority: P1)

A person can register a new TaskFlow account, land on their dashboard,
log out, log back in, and land back inside the app rather than being
stuck on the login screen. Someone who is not logged in and tries to
visit a page that requires authentication is sent to the login screen
and, after logging in, is returned to the page they originally asked
for.

**Why this priority**: Nothing else in the app is reachable without a
working login flow. Authentication is also the single most
security-sensitive path in the system — a broken or bypassable login
puts every other feature at risk.

**Independent Test**: Can be fully run on its own by registering a
fresh account, logging out, logging back in, and attempting one
protected URL while logged out — delivers confidence that the front
door of the app works before any other journey is trusted.

**Acceptance Scenarios**:

1. **Given** no account exists for a given email, **When** a visitor
   registers with valid details, **Then** they land on their dashboard
   as a Developer.
2. **Given** a logged-in user, **When** they log out and then log back
   in with the same credentials, **Then** they land inside the app
   again, not stuck on the login screen.
3. **Given** no one is logged in, **When** a visitor navigates directly
   to a protected page URL, **Then** they are redirected to the login
   screen, and logging in from there returns them to the page they
   originally requested.

---

### User Story 2 - Permission-boundary journey (Priority: P1)

A Developer cannot create, edit, or delete a project, cannot manage
workflow columns, and cannot manage users — neither the option to do so
appears in the interface, nor does a direct request to perform the
action succeed if attempted anyway.

**Why this priority**: Authorization is the second pillar the rest of
the app depends on. A permission boundary that only exists in the UI
and not on the server is a real security bypass, so this journey
carries the same criticality as login itself.

**Independent Test**: Can be fully run on its own by logging in as a
seeded Developer, confirming the relevant controls are absent from the
interface, and separately issuing at least one direct request for a
restricted action to confirm it is rejected server-side.

**Acceptance Scenarios**:

1. **Given** a logged-in Developer, **When** they view the project list
   or a project's settings, **Then** no controls to create, edit, or
   delete a project or manage its workflow columns are visible.
2. **Given** a logged-in Developer, **When** they view the app
   navigation, **Then** no user-management option is visible.
3. **Given** a logged-in Developer, **When** a restricted action (e.g.
   deleting a project) is attempted directly against the backend rather
   than through the interface, **Then** the action is refused.

---

### User Story 3 - Navigation journey (Priority: P2)

After logging in, a user can reach every part of the app they're
allowed to see through the sidebar, and the sidebar itself reflects
what they're allowed to see: an Admin sees a Users link, a Developer
does not, and a Developer who navigates straight to the Users URL is
blocked rather than shown the page.

**Why this priority**: A routing regression has already slipped past
existing test suites once (Feature 004); this journey exists
specifically to catch that class of bug going forward. It depends on a
working login (Story 1) but not on any of the CRUD or board journeys.

**Independent Test**: Can be fully run on its own by logging in as each
of an Admin and a Developer and walking every sidebar item, plus one
direct URL visit to a restricted page as the Developer.

**Acceptance Scenarios**:

1. **Given** a logged-in user, **When** they select each item available
   in the sidebar, **Then** each one navigates to the correct page.
2. **Given** a logged-in Admin, **When** they view the sidebar,
   **Then** a Users item is present and navigates correctly.
3. **Given** a logged-in Developer, **When** they view the sidebar,
   **Then** no Users item is present, and a direct visit to the Users
   URL is blocked.

---

### User Story 4 - Project and work item journey (Priority: P2)

A Manager or Admin can create a project, add a Story to it, add a Task
as a child of that Story, and see both items — along with their
parent-child relationship — correctly reflected in both the project's
List view and its Tree view.

**Why this priority**: Projects and work items are the core data
objects the rest of the app (boards, sprints) is built on top of; this
journey validates the foundational create-and-view path.

**Independent Test**: Can be fully run on its own by logging in as a
Manager, creating one project, one Story, and one child Task, and
inspecting both views — delivers confidence in core CRUD and hierarchy
display independent of drag or sprint behavior.

**Acceptance Scenarios**:

1. **Given** a logged-in Manager or Admin, **When** they create a new
   project, **Then** the project exists and is reachable.
2. **Given** an existing project, **When** a Story is created and then
   a Task is created as its child, **Then** both appear in the
   project's List view and Tree view.
3. **Given** a Story with a child Task, **When** the Tree view is
   opened, **Then** the parent-child relationship is visibly correct.

---

### User Story 5 - Board drag journey (Priority: P2)

On a project's Board, a permitted user can drag a work item card from
one column to another and have that status stick — reloading the page
still shows the card in its new column. If a user without permission to
change status attempts the same drag, the card visibly reverts to its
original column rather than silently failing or appearing to succeed.

**Why this priority**: Drag interaction is the app's most complex piece
of client-side state and was the source of a flaky test in Feature 009;
this journey exists to catch both drag-persistence bugs and permission
bypasses in the board specifically.

**Independent Test**: Can be fully run on its own against a project
that already has at least one work item, by dragging a card, reloading,
and confirming persistence, then separately attempting the same drag as
an unauthorized user.

**Acceptance Scenarios**:

1. **Given** a work item on a project Board, **When** an authorized
   user drags its card to a different column, **Then** the card's new
   status is still shown after the page is reloaded.
2. **Given** the same setup, **When** a user without status-change
   permission attempts the same drag, **Then** the card visibly
   reverts to its original column.

---

### User Story 6 - Sprint journey (Priority: P3)

A Manager or Admin can create a sprint, drag a work item into it from
the Backlog, start the sprint, and see the Board switch into an "Active
sprint" mode that shows only that sprint's items. Completing the sprint
prompts for a destination for any items still open, and honors the
choice made.

**Why this priority**: Sprints build on top of the board and backlog
mechanics already covered by Stories 4 and 5, so this journey is lower
risk in isolation but still covers a full, multi-step business
workflow.

**Independent Test**: Can be fully run on its own by creating a sprint,
moving one backlog item into it, starting it, confirming the Board's
active-sprint filtering, then completing the sprint and picking a
destination for any item left open.

**Acceptance Scenarios**:

1. **Given** a project with backlog items, **When** a sprint is created
   and a work item is dragged into it from the Backlog, **Then** the
   item is associated with that sprint.
2. **Given** a sprint containing at least one item, **When** the sprint
   is started, **Then** the Board's active-sprint mode shows only that
   sprint's items.
3. **Given** a started sprint with an item still open, **When** the
   sprint is completed and a destination is chosen for that item,
   **Then** the item ends up at the chosen destination.

---

### User Story 7 - Role-change journey (Priority: P3)

An Admin changes another user's role, and the next time that user logs
in, their permissions and visible navigation reflect the new role.

**Why this priority**: This is a narrow, single-action journey that
depends on both auth (Story 1) and permission boundaries (Story 2)
already working, so it is the last journey layered on top of the
foundation the others establish.

**Independent Test**: Can be fully run on its own by having an Admin
change a seeded user's role, then logging in as that user and
confirming the new role's permissions and navigation apply.

**Acceptance Scenarios**:

1. **Given** a logged-in Admin, **When** they change another user's
   role, **Then** the change is saved.
2. **Given** a user whose role was just changed, **When** that user
   next logs in, **Then** their navigation and permissions match the
   new role, not the old one.

### Edge Cases

- What happens when the suite is run against a test database that still
  has leftover data from a previous run or a previous test file? Each
  test or test file MUST establish its own known starting state so
  leftover data cannot make a later test pass or fail incorrectly.
- What happens when a drag-and-drop action is interrupted or dropped
  outside a valid column? The card MUST remain in (or return to) a
  valid, visibly correct column state.
- What happens when a test fails once but passes on Playwright's single
  automatic retry? It MUST be reported as passing, but a failure that
  persists through the retry MUST be reported as a real failure, never
  silently skipped or marked as an expected failure.
- What happens when the backend or frontend is not running when the
  suite is started? The suite MUST fail fast with a clear error rather
  than hanging or producing misleading test failures.
- What happens when an unauthorized drag or direct API call is
  attempted? The system MUST reject the change server-side regardless
  of what the UI allowed the test to attempt.

## Requirements *(mandatory)*

<!--
  This feature is testing infrastructure: these requirements describe
  what the E2E suite itself must do, not new application behavior. No
  application behavior changes as a result of this feature.
-->

### Functional Requirements

**Coverage**

- **FR-001**: The suite MUST cover all seven journeys described in User
  Scenarios & Testing: auth, permission boundaries, navigation, project
  and work item CRUD, board drag, sprint, and role change.
- **FR-002**: Each journey MUST be organized as its own independently
  runnable spec file, so any single journey can be executed and
  understood without running the others.

**Environment and isolation**

- **FR-003**: The suite MUST run against a dedicated test database that
  is distinct from any developer's working database, and MUST never run
  against a database containing real work.
- **FR-004**: The test database MUST be creatable and resettable using
  the application's existing EF Core migrations, not a separate schema
  mechanism.
- **FR-005**: Each test or spec file MUST start from a known state —
  either a fresh database per run, or explicit setup/teardown — such
  that no cross-test leftover data can make a later test pass or fail
  incorrectly.
- **FR-006**: The suite MUST exercise the real backend and the real
  built frontend; it MUST NOT mock or stub API responses.
- **FR-007**: At the start of each run, a seeded Admin account and at
  least one seeded Manager account and one seeded Developer account
  MUST exist, created via the same configuration-based seeding
  mechanism the application already uses for this purpose, not a
  special test-only backdoor.

**Reliability and selectors**

- **FR-008**: Selectors used by the suite MUST be resilient — based on
  role, label, or a stable test hook — rather than brittle CSS
  structure, so cosmetic UI changes elsewhere do not break these tests.
- **FR-009**: Each journey MUST complete within a bounded time, and the
  full suite MUST be practical to run locally in a few minutes.
- **FR-010**: A failing test MAY retry automatically once (the standard
  Playwright retry behavior); a test that still fails after that retry
  MUST be reported as a failure, never silently skipped or marked as an
  expected failure.
- **FR-011**: On failure, the suite MUST produce a screenshot and a
  trace for the failing step, saved locally, sufficient to debug the
  failure without re-running the suite.
- **FR-012**: The permission-boundary journey (User Story 2) MUST
  include at least one direct API call made from within the test — not
  only a check of UI absence — to prove the restriction is enforced by
  the server and not only hidden in the interface.

**Developer experience**

- **FR-013**: A single documented command MUST run the full E2E suite
  locally, against an already-running (or suite-started) backend and
  frontend.
- **FR-014**: Running the E2E suite MUST be separate from the existing
  `dotnet test` and `ng test` commands; it MUST NOT run as part of
  either and is not required to.
- **FR-015**: The project's README MUST document how to run the E2E
  suite.

**Non-functional constraints**

- **FR-016**: This feature MUST NOT change, fix, or add any application
  behavior — it adds tests only. If a real bug is uncovered while
  building the suite, it MUST be reported separately rather than fixed
  as part of this feature.
- **FR-017**: This feature MUST require no production code changes,
  with one narrow exception: adding a stable selector hook (such as a
  test-id attribute) to existing markup where a resilient selector
  would not otherwise be possible. Any such addition MUST be kept
  minimal and documented.
- **FR-018**: The suite MUST run against a single browser engine
  (Chromium) for this version; a cross-browser matrix is out of scope.

### Key Entities

- **Seeded Test Accounts**: The Admin, Manager, and Developer accounts
  present at the start of every run, created through the app's existing
  configuration-based seeding mechanism, used as the identities the
  journeys log in as.
- **Test Database**: A dedicated database instance, separate from any
  developer's working database, brought to a known schema state via the
  application's existing EF Core migrations before each run.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A single documented command runs all seven journeys
  against a dedicated test database and a running instance of the app.
- **SC-002**: All seven journeys pass reliably across three consecutive
  local runs.
- **SC-003**: A deliberately introduced routing/guard regression (e.g.
  temporarily removing a route guard) is caught — causes at least one
  journey to fail — proving the suite actually exercises the risk it
  claims to cover.
- **SC-004**: The full suite completes in a few minutes when run
  locally against an already-running app.
- **SC-005**: Zero existing xUnit or Vitest tests are modified, and no
  application behavior changes as a result of this feature.
- **SC-006**: The README documents how to run the suite, verified by a
  person unfamiliar with the suite being able to run it from those
  instructions alone.

## Assumptions

- The application already has a configuration-based mechanism for
  seeding an Admin, Manager, and Developer account; this feature reuses
  it rather than building a new one.
- The test database is a separate SQL Server database (matching the
  project's existing database technology) reachable with its own
  connection string, brought to schema by the app's existing EF Core
  migrations.
- "Starting or assuming an already-running backend and frontend" is
  left to whichever approach is simpler to document and maintain; the
  suite is not required to manage the app's lifecycle itself.
- Playwright is added as a devDependency of the frontend project and
  introduces no new runtime dependency for the shipped application.
- Continuous integration wiring, visual regression testing, load/
  performance testing, a cross-browser matrix, and mobile-viewport
  testing are explicitly out of scope for this feature, per the source
  description.
- This is a curated smoke suite of the highest-risk journeys, not
  exhaustive coverage of every feature; unit and integration tests
  remain the primary coverage mechanism for logic-level correctness.
