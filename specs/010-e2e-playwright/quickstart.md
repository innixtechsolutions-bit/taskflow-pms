# Quickstart: Running the E2E Suite

This validates the feature end-to-end: a dedicated test database, a running
instance of the app, and all 7 journeys passing via one command. See
[data-model.md](./data-model.md) for what gets provisioned and
[contracts/api-usage.md](./contracts/api-usage.md) for the endpoints the
suite calls directly.

## Prerequisites

- SQL Server 2022 reachable at `localhost` (same instance developers already
  use, per `backend/TaskFlow.Api.Tests`' fixtures).
- `dotnet` and `node`/`npm` on PATH.
- Backend user-secrets configured (`Admin:Email`, `Admin:Password`,
  `Jwt:*`) as already required to run the app locally — see
  `specs/001-user-auth/quickstart.md` if these aren't set up yet.

## 1. Reset the E2E test database

From `backend/TaskFlow.Api/`, point the connection string at a dedicated
database and apply migrations:

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=localhost;Database=TaskFlowDb_E2E;Trusted_Connection=True;TrustServerCertificate=True"
dotnet ef database drop --force --connection $env:ConnectionStrings__DefaultConnection
dotnet ef database update --connection $env:ConnectionStrings__DefaultConnection
```

This gives every full suite run a known-empty schema (spec's "fresh
database per run" option for FR-005) — do this before starting the backend
in step 2, not while it's already running (see research.md Decision 2 for
why).

## 2. Start the backend against the E2E database

Still in `backend/TaskFlow.Api/`, with the same `ConnectionStrings__DefaultConnection`
env var set from step 1:

```powershell
dotnet run --launch-profile http
```

This triggers `AdminSeeder` against the now-empty `TaskFlowDb_E2E`,
creating the seeded Admin from `Admin:Email`/`Admin:Password`. Confirm it's
listening on `http://localhost:5146` (matches `frontend/proxy.conf.json`
and CORS policy).

## 3. Start the frontend

In `frontend/`:

```powershell
npm start
```

Confirm it's serving on `http://localhost:4300` (per `angular.json`).

## 4. Run the suite

In `frontend/`:

```powershell
npm run e2e
```

This runs `playwright test`, which first executes `e2e/global-setup.ts`
(provisions the Manager and Developer test accounts per research.md
Decision 3), then all 7 spec files in `e2e/tests/`.

## Expected outcome

- All 7 journeys pass (`auth`, `permissions`, `navigation`,
  `project-work-items`, `board-drag`, `sprint`, `role-change`).
- Run again (without repeating steps 1–3) to confirm reruns are stable —
  `global-setup.ts`'s idempotent account provisioning means only steps 1–3
  need repeating for a fully clean slate, not every single run.
- Repeat 3 consecutive times per the spec's Success Criteria (SC-002).

## Verifying the suite actually catches regressions (SC-003)

1. Temporarily comment out the `authGuard` (or `adminGuard`) on one route in
   `frontend/src/app/app.routes.ts`.
2. Re-run `npm run e2e`.
3. Confirm the navigation or permission-boundary journey fails — this
   proves the suite exercises the risk it claims to (a routing/guard
   regression like the one that shipped in Feature 004).
4. Revert the change. No application behavior change should ship as part
   of this feature (FR-016) — this step is a one-time verification, not a
   permanent test fixture.

## On failure

Check `frontend/playwright-report/` (HTML report) and
`frontend/test-results/` (per-test screenshot + trace) for the failing
step — no re-run needed to start debugging (FR-011). Open a trace with:

```powershell
npx playwright show-trace test-results/<failing-test-folder>/trace.zip
```
