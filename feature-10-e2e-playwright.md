# Feature 10 — E2E Testing Foundation (Playwright) (paste into /speckit-specify)

Add an end-to-end test suite (Playwright) that exercises TaskFlow through
a real browser against the real backend and database — covering the
critical user journeys that unit and integration tests cannot: full
page navigation, routing, guards, and cross-page state, exactly the
category of bug that has twice slipped past our test suites so far
(a routing regression in Feature 004, a flaky Material harness timing
issue in Feature 009).

## Why (context)

TaskFlow has ~700 unit/integration tests (xUnit + Vitest) covering
services, components, and API contracts in isolation. Nothing today
walks through the app the way a person actually would: log in, click
around, create something, see it appear. This feature adds that layer
— a small, deliberately curated set of E2E smoke tests covering the
riskiest cross-cutting journeys (auth, navigation, core CRUD, drag
interaction, permission boundaries), run against a dedicated test
database so they never touch real data.

This is testing infrastructure, not a user-facing feature — "user
stories" below describe what the suite must verify, and acceptance
criteria describe the suite's own behavior and reliability.

## Scope: the journeys to cover

1. **Auth journey**: register a new account, land on the dashboard as
   Developer, log out, log back in, land back inside the app (not
   redirected to login), and confirm an unauthenticated visit to a
   protected URL redirects to login with a working returnUrl.
2. **Navigation journey**: after login, every sidebar item (Dashboard,
   Projects, Users for Admin) navigates correctly; the Users link is
   absent for a Developer and a direct visit to its URL as a Developer
   is blocked.
3. **Project and work item journey**: as Manager/Admin, create a
   project, create a Story, create a Task as its child, confirm both
   appear in the project's List and Tree views, and confirm the
   parent-child relationship shows correctly.
4. **Board drag journey**: on a project's Board, drag a card from one
   column to another, confirm the card's status persists after a page
   reload, and confirm an unauthorized user's drag attempt is rejected
   with a visible revert.
5. **Sprint journey**: create a sprint, drag a work item into it from
   the Backlog, start the sprint, confirm the Board's "Active sprint"
   mode shows only that item, then complete the sprint and choose a
   destination for any open items.
6. **Permission-boundary journey**: confirm a Developer cannot create,
   edit, or delete a project or manage workflow columns or users,
   verified both by UI absence and by at least one direct API call
   made from within a test, proving server-side enforcement rather
   than hidden UI alone.
7. **Role-change journey**: an Admin changes another user's role, and
   that user's next login reflects the new role and permissions.

## Acceptance criteria (the suite itself)

### Environment and isolation
- E2E tests run against a dedicated test database, not the developer's
  working database, created or reset via the existing EF Core
  migrations, and never run against a database containing real work.
- Each test or test file starts from a known state, either a fresh
  database per run or explicit setup and teardown that leaves no
  cross-test leftover data that could make a later test pass or fail
  incorrectly.
- Tests run against the real backend and the real built frontend, not
  mocked APIs, since the goal is to catch what unit tests miss.
- A seeded Admin account, and at least one seeded Manager and one
  seeded Developer, exist at the start of each run via the same
  configuration-based seeding mechanism the app already uses, not a
  special test-only backdoor.

### Suite structure and reliability
- Organized as one spec file per journey, auth, navigation, project
  and work item, board drag, sprint, permissions, and role change,
  each independently runnable.
- Selectors are resilient, role or label or test-id based rather than
  brittle CSS chains, so UI polish elsewhere does not break these
  tests for cosmetic reasons alone.
- Each journey completes in a bounded time, and the whole suite is
  practical to run locally in a few minutes.
- Flakiness budget: a test may retry once automatically on failure,
  the standard Playwright retry, but a test that still fails on retry
  is a real signal, not noise. No test is allowed to be silently
  skipped or marked expected to fail.
- Failures produce a screenshot and trace for the failing step, saved
  locally, to make debugging fast without re-running.

### Developer experience
- A single command runs the full E2E suite locally, starting or
  assuming an already-running backend and frontend, documented in a
  short README addition, whichever this project's setup makes simpler.
- The suite is separate from dotnet test and ng test. It does not run
  as part of those and does not need to for this feature. CI wiring,
  if any, is a future concern, not required here.

### Non-functional
- No production code changes are required to support this feature,
  since Playwright is a devDependency and a separate tool. If a
  selector needs a stable hook, such as a data-testid, added to
  existing markup, that is the one kind of production change allowed,
  kept minimal and documented.
- This feature does not change, fix, or add any application behavior.
  It only adds tests. If it uncovers a real bug, that is reported and
  fixed as its own separate, explicitly called-out change, not folded
  in silently.

## Out of scope, do NOT include in this feature

- Continuous integration pipeline or GitHub Actions wiring
- Visual regression testing, pixel diffing
- Load or performance testing
- Cross-browser matrix; one browser, Chromium, is sufficient for v1
- Mobile-viewport E2E testing
- Testing every feature exhaustively; this is a curated smoke suite of
  the highest-risk journeys, not a replacement for unit and
  integration coverage

## Success check

Feature is complete when the documented command runs all seven
journeys against a dedicated test database and a running instance of
the app, all seven pass reliably across three consecutive local runs,
a deliberately introduced routing break, such as temporarily removing
a guard, is caught by the suite, proving it actually exercises the
risk it claims to, and the README documents how to run it. No existing
xUnit or Vitest test changes; no application behavior changes.
