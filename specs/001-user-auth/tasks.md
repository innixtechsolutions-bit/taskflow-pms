---

description: "Task list for User Registration, Login & Role-Based Access"
---

# Tasks: User Registration, Login & Role-Based Access

**Input**: Design documents from `/specs/001-user-auth/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md (all present)

**Tests**: Included and REQUIRED — the project constitution (Principle I, NON-NEGOTIABLE) mandates tests written before implementation for every feature, and the Development Workflow section requires every protected endpoint's authorization logic to be covered by an integration test for both the allowed and denied path. Test tasks below MUST be completed, and MUST fail, before their corresponding implementation tasks.

**Organization**: Tasks are grouped by user story (from spec.md) to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3, US4)
- File paths are exact, per `plan.md`'s Project Structure

## Path Conventions

Web application split per `plan.md`: `backend/TaskFlow.Api/` (+ `backend/TaskFlow.Api.Tests/`) and `frontend/src/app/`.

<!-- Sample tasks from the template have been replaced with the actual tasks for this feature. -->

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Scaffold both projects from scratch (repo currently has no `backend/` or `frontend/`)

- [X] T001 Create the backend solution: `dotnet new webapi -o backend/TaskFlow.Api` (.NET 10), `dotnet new xunit -o backend/TaskFlow.Api.Tests`, a `TaskFlow.sln` referencing both, and a project reference from the test project to `TaskFlow.Api` (note: `.NET 10 SDK` generated `TaskFlow.slnx`, the newer XML solution format, instead of `.sln` — functionally equivalent, used throughout)
- [X] T002 [P] Add NuGet packages to `backend/TaskFlow.Api/TaskFlow.Api.csproj`: `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Design`, `Microsoft.AspNetCore.Authentication.JwtBearer` (note: `Microsoft.AspNetCore.Identity` package was added then removed — `PasswordHasher<T>` ships in the ASP.NET Core shared framework already referenced by the Web SDK, and the explicit package reference triggered an NU1510 "will not be pruned" build error under `TreatWarningsAsErrors`)
- [X] T003 [P] Create the Angular 22 workspace in `frontend/` (standalone APIs, zoneless, signals) via `ng new frontend --standalone --style=css --routing`, then add Angular Material and Angular CDK (`ng add @angular/material`) (note: `provideZonelessChangeDetection()` added explicitly to `app.config.ts` — the generated project ships with no `zone.js` dependency but doesn't wire the provider by default)
- [X] T004 [P] Enable `<Nullable>enable</Nullable>` and `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in `backend/TaskFlow.Api/TaskFlow.Api.csproj`; enable `"strict": true` in `frontend/tsconfig.json` (note: added `<WarningsNotAsErrors>NU1903</WarningsNotAsErrors>` with an inline comment — the default webapi template's transitive `Microsoft.OpenApi` 2.0.0 has an unrelated advisory with no compatible non-breaking fix; kept visible, not silenced)
- [X] T005 [P] Run `dotnet user-secrets init --project backend/TaskFlow.Api` and document the required configuration keys (`Admin:Email`, `Admin:Password`, `Jwt:SigningKey`, `Jwt:Issuer`, `Jwt:Audience`) as placeholder entries in `backend/TaskFlow.Api/appsettings.Development.json` (values left empty, real values only ever in user-secrets/env vars — never committed) (also set `ConnectionStrings:DefaultConnection` for local SQL Server via Windows Auth, and real dev values for all keys in user-secrets)

**Checkpoint**: Both projects build (even if empty) before foundational work begins.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared infrastructure every user story depends on — the `User` entity/table, JWT auth wiring, error handling, and the frontend auth-state skeleton

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T006 Create the `User` entity with a `Role` enum (`Developer`, `Manager`, `Admin`) in `backend/TaskFlow.Api/Data/Entities/User.cs` per `data-model.md` (Id, FullName, Email, PasswordHash, Role, CreatedAt) — include a one-line comment on the enum explaining why `Role` is a fixed enum, not a table (Clarity Over Cleverness)
- [X] T007 Create `AppDbContext` in `backend/TaskFlow.Api/Data/AppDbContext.cs` with a `Users` `DbSet<User>`, a unique index on `Email`, and `Role` configured via `HasConversion<string>()` — include a short comment on what `DbSet`/change tracking is (constitution Principle VI)
- [X] T008 [P] Generate the initial EF Core migration `AddUsersTable` in `backend/TaskFlow.Api/Data/Migrations/` via `dotnet ef migrations add AddUsersTable --project backend/TaskFlow.Api` (executed after T009's `AppDbContext` registration in `Program.cs`, since EF's design-time tooling needs a way to construct the context; applied via `dotnet ef database update` against the real local SQL Server — `TaskFlowDb` created and verified)
- [X] T009 [P] Configure JWT bearer authentication and authorization services in `backend/TaskFlow.Api/Program.cs` (signing key/issuer/audience from configuration, `ValidateLifetime = true`), with a short comment on why the middleware order matters
- [X] T010 [P] Configure global `ProblemDetails` error handling (`AddProblemDetails()` + exception-handling middleware) in `backend/TaskFlow.Api/Program.cs` so every error response shares one shape (FR-020)
- [X] T011 [P] Unit tests for the `AdminSeeder` fail-fast/seed logic in `backend/TaskFlow.Api.Tests/Startup/AdminSeederTests.cs`: throws when `Admin:Email` is missing/empty, throws when `Admin:Password` is missing/empty, seeds an Admin with a hashed password when both are present, does not reseed when an Admin already exists — write FIRST, confirm it FAILS (constitution Principle I; no Admin-seeding logic exists yet to satisfy it). **Confirmed RED**: compile error (`AdminSeeder`/`Startup` namespace didn't exist) — also caught a real bug in the new `SqlServerTestDatabase` test fixture (xUnit v2's `IAsyncLifetime` expects `Task`, not `ValueTask`), fixed before proceeding.
- [X] T012 Implement the fail-fast `AdminSeeder` in `backend/TaskFlow.Api/Startup/AdminSeeder.cs`, invoked from `Program.cs` before `app.Run()`: if no `Admin` user exists and `Admin:Email`/`Admin:Password` are missing or empty, throw immediately with a clear message (FR-018a); otherwise seed the Admin using `PasswordHasher<User>` (FR-018) — depends on T006, T007, T011 (make the T011 tests pass). **Confirmed GREEN**: 4/4 `AdminSeederTests` pass; also smoke-tested by running the app against the real dev DB — Admin row seeded and verified via `sqlcmd`.
- [X] T013 [P] Create shared DTOs `AuthResponse` and `PagedResult<T>` in `backend/TaskFlow.Api/Dtos/`
- [X] T014 [P] Unit tests for the Angular `AuthService` state skeleton in `frontend/src/app/auth/auth.service.ts` (spec file `frontend/src/app/auth/auth.service.spec.ts`): `signal<AuthState | null>` initializes from an existing `localStorage` value, `computed` signals (`isAuthenticated`, `currentRole`) reflect state correctly, setting state persists to `localStorage`, clearing state removes it — write FIRST, confirm it FAILS (constitution Principle I; `AuthService` doesn't exist yet). **Confirmed RED**: `Could not resolve "./auth.service"` build error.
- [X] T015 [P] Create the Angular `AuthService` skeleton in `frontend/src/app/auth/auth.service.ts`: a root-provided `signal<AuthState | null>`, `computed` signals (`isAuthenticated`, `currentRole`), and `localStorage` read/write for persistence across refresh (no register/login calls yet — those land in US1/US2) — depends on T014 (make the T014 tests pass). **Confirmed GREEN**: 5/5 new tests pass (7/7 total including the baseline `app.spec.ts`).
- [X] T016 [P] Create `frontend/src/app/auth/auth.interceptor.ts` (attaches `Authorization: Bearer <token>` from `AuthService`, and on a `401` response clears state) and register it in `frontend/src/app/app.config.ts` — depends on T015 (also added `provideHttpClient(withInterceptors(...))` to `app.config.ts`, which wasn't present by default)
- [X] T016a *(small addition, post-checkpoint)* Add an interactive API UI for development: `Scalar.AspNetCore` package, `app.MapScalarApiReference()` mapped alongside `app.MapOpenApi()` inside the existing `IsDevelopment()` block in `backend/TaskFlow.Api/Program.cs`, with an inline comment distinguishing the OpenAPI *document* (`MapOpenApi()`, machine-readable JSON at `/openapi/v1.json`) from the Scalar *UI* (`MapScalarApiReference()`, human-browsable page at `/scalar` that renders that document). Verified: `dotnet build` still shows only the 4 pre-existing `NU1903` warnings (0 new), all 15 backend tests still pass, and `/scalar`/`/openapi/v1.json` both confirmed live against the running dev server via curl.

**Checkpoint**: Foundation ready — `AppDbContext`/`User` exist, `AdminSeeder` and `AuthService` are both implemented against tests written first, auth middleware is wired. User story implementation can now begin.

---

## Phase 3: User Story 1 - Register for a TaskFlow Account (Priority: P1) 🎯 MVP

**Goal**: A visitor can register with full name, email, and password, and is immediately signed in as a Developer.

**Independent Test**: Submit the registration form with valid, unique details and confirm the visitor lands on the home page, signed in with the Developer role.

### Tests for User Story 1 ⚠️

> Write these tests FIRST; confirm they FAIL before implementation (constitution Principle I)

- [X] T017 [P] [US1] Unit tests for `AuthService.RegisterAsync` in `backend/TaskFlow.Api.Tests/Services/AuthServiceTests.cs`: creates account with Developer role by default, rejects duplicate (case-insensitive) email, hashes the password via `PasswordHasher`, rejects a password that fails the rules. **Confirmed RED**: compile errors (`AuthService`/`RegisterRequest` didn't exist).
- [X] T018 [P] [US1] Integration tests for `POST /api/auth/register` in `backend/TaskFlow.Api.Tests/Integration/AuthEndpointsTests.cs`: `201` on success with token+Developer role, `409` on duplicate email, `400` on invalid name/email/password (allowed + denied paths). **Confirmed RED**: same compile errors, plus a real bug in the new `TaskFlowApiFactory` test fixture (missing `using Microsoft.AspNetCore.Hosting;` for `IWebHostBuilder`), fixed before proceeding. Required adding a new `Microsoft.AspNetCore.Mvc.Testing` package reference and marking `Program` as `public partial class Program;` so `WebApplicationFactory<Program>` can target it.
- [X] T019 [P] [US1] Vitest tests for the registration flow in `frontend/src/app/auth/register/register.component.spec.ts`: password rules shown before submit, duplicate-email error displayed, successful submit redirects home. **Confirmed RED**: `Cannot find module './register.component'` build error.

### Implementation for User Story 1

- [X] T020 [US1] Create `RegisterRequest` DTO with data-annotation validation (FullName 2-100 chars, valid email, password ≥8 chars/1 letter/1 digit) in `backend/TaskFlow.Api/Dtos/RegisterRequest.cs`
- [X] T021 [US1] Implement `AuthService.RegisterAsync` in `backend/TaskFlow.Api/Services/AuthService.cs` (uniqueness check, `PasswordHasher`, default `Developer` role, JWT issuance with 8h `exp`) — depends on T006-T013, T020 (also added `EmailAlreadyExistsException`/`InvalidPasswordException` in `Services/AuthExceptions.cs`, and registered `AuthService` as Scoped in `Program.cs`)
- [X] T022 [US1] Implement `AuthController.Register` (`POST /api/auth/register`) in `backend/TaskFlow.Api/Controllers/AuthController.cs` — depends on T021. **Confirmed GREEN**: all 11 backend US1 tests pass (15/15 including Foundational's `AdminSeederTests`).
- [X] T023 [US1] Build the Angular registration page (`frontend/src/app/auth/register/register.component.ts` + template) showing password rules before submission and inline validation errors — depends on T015 (used Angular 22's Signal Forms — `form()`, `[formField]`, `required`/`email`/`minLength`/`maxLength`/`pattern` validators — confirmed against the actually-installed `@angular/forms/signals` API surface, per the constitution's fixed stack)
- [X] T024 [US1] Add `AuthService.register()` calling `POST /api/auth/register`, storing token/state on success, and navigating to the home route — depends on T015, T022, T023. **Confirmed GREEN**: 10/10 frontend tests pass (3 new `RegisterComponent` tests + 7 from Foundational).

**Checkpoint**: User Story 1 is fully functional and independently testable — a visitor can register and land on the home page as a Developer. **Verified end-to-end** with the real API against the real dev database (not just test doubles): `POST /api/auth/register` returns `201` with a valid JWT and `Developer` role; a duplicate email returns `409` with the exact `ProblemDetails` message; an invalid password returns `400` with `ValidationProblemDetails`; no password or hash appears in any response body.

---

## Phase 4: User Story 2 - Log In and Stay Signed In (Priority: P1)

**Goal**: A returning user can sign in, stay signed in across refreshes for 8 hours, sign out, be redirected back to their originally requested page, and be rate-limited after repeated failures.

**Independent Test**: Sign in with a known account's credentials, refresh the browser to confirm persistence, and sign out to confirm the session ends.

### Tests for User Story 2 ⚠️

- [ ] T025 [P] [US2] Unit tests for `AuthService.LoginAsync` and `LoginAttemptTracker` in `backend/TaskFlow.Api.Tests/Services/AuthServiceTests.cs` and `backend/TaskFlow.Api.Tests/Services/LoginAttemptTrackerTests.cs`: same generic error for wrong email vs. wrong password, 8h token expiry set correctly, 6th attempt within 15 minutes blocked, counter clears on success
- [ ] T026 [P] [US2] Integration tests for `POST /api/auth/login`, `POST /api/auth/logout`, `GET /api/auth/me` in `backend/TaskFlow.Api.Tests/Integration/AuthEndpointsTests.cs`: `200` on success, `401` generic message for bad credentials, `429` after rate limit, `me` returns `401` for missing/expired token and `200` for a valid one (allowed + denied paths)
- [ ] T027 [US2] Integration test asserting the `ProblemDetails` response shape (`type`/`title`/`status`/`detail` present and populated) on a representative error response — the `401` from `POST /api/auth/login` with bad credentials — in `backend/TaskFlow.Api.Tests/Integration/AuthEndpointsTests.cs` (same file as T026; covers FR-020's "consistent error shape" requirement directly, not just incidentally)
- [ ] T028 [P] [US2] Vitest tests for the login component and route guard in `frontend/src/app/auth/login/login.component.spec.ts` and `frontend/src/app/auth/auth.guard.spec.ts`: generic error message rendering; guard redirects unauthenticated access to `/login?returnUrl=...` and returns there post-login; **and** an expired-token scenario asserts the interceptor/guard redirect to `/login` with the exact message "Your session has expired." (FR-010), distinct from the plain "not logged in" redirect case

### Implementation for User Story 2

- [ ] T029 [US2] Implement `LoginAttemptTracker` (`IMemoryCache`-backed, per-email 15-minute window) in `backend/TaskFlow.Api/Services/LoginAttemptTracker.cs`
- [ ] T030 [US2] Create `LoginRequest` DTO in `backend/TaskFlow.Api/Dtos/LoginRequest.cs`
- [ ] T031 [US2] Implement `AuthService.LoginAsync` (rate-limit check via T029, generic invalid-credentials error, JWT issuance) in `backend/TaskFlow.Api/Services/AuthService.cs` — depends on T029, T030
- [ ] T032 [US2] Implement `AuthController.Login`, `Logout`, `Me` actions in `backend/TaskFlow.Api/Controllers/AuthController.cs` — depends on T031
- [ ] T033 [US2] Build the Angular login page (`frontend/src/app/auth/login/login.component.ts` + template) with inline validation and the generic error message — depends on T015
- [ ] T034 [US2] Implement `frontend/src/app/auth/auth.guard.ts` (functional `CanActivateFn`, redirect to `/login?returnUrl=`) and apply it to protected routes in `frontend/src/app/app.routes.ts` — depends on T015
- [ ] T035 [US2] Wire session-expiry handling: interceptor/guard react to an expired token by clearing `AuthService` state and redirecting to login with "Your session has expired.", and add the logout call/state-clear to `AuthService` — depends on T015, T016, T032

**Checkpoint**: User Stories 1 AND 2 both work independently — the full register → login → persist → expire/logout loop is functional.

---

## Phase 5: User Story 3 - See My Identity in the App (Priority: P2)

**Goal**: A signed-in person sees their name and role in the header on every page, with logout available from there.

**Independent Test**: Sign in and confirm name/role are visible in the header on multiple pages, with a working logout control.

### Tests for User Story 3 ⚠️

- [ ] T036 [P] [US3] Vitest tests for the header component in `frontend/src/app/shared/header/header.component.spec.ts`: displays the signed-in name and role from `AuthService`, logout button ends the session

### Implementation for User Story 3

- [ ] T037 [US3] Build the `header` component reading `AuthService` signals for name/role, with a logout button, in `frontend/src/app/shared/header/header.component.ts` (+ template) — depends on T015, T035
- [ ] T038 [US3] Add the header to the app's shell/layout so it renders on every authenticated page in `frontend/src/app/app.ts` (or the equivalent root layout component) — depends on T037

**Checkpoint**: User Stories 1, 2, AND 3 all work independently — identity is visible everywhere a signed-in person goes.

---

## Phase 6: User Story 4 - Admin Manages User Roles (Priority: P3)

**Goal**: An Admin can list every user and change roles, guarded against locking out the last Admin; non-Admins are refused by any route.

**Independent Test**: Sign in as an Admin, open the Users page, change another person's role, and confirm the change takes effect.

### Tests for User Story 4 ⚠️

- [ ] T039 [P] [US4] Unit tests for `UserService` in `backend/TaskFlow.Api.Tests/Services/UserServiceTests.cs`: pagination shape, role change takes effect, last-remaining-Admin self-demotion is rejected
- [ ] T040 [P] [US4] Integration tests for `GET /api/users` and `PUT /api/users/{id}/role` in `backend/TaskFlow.Api.Tests/Integration/UsersEndpointsTests.cs`: `200` + paginated list and successful role change for an Admin caller, `403` for a non-Admin caller on both endpoints, `400` for the last-Admin guard, `404` for an unknown user id (allowed + denied paths)
- [ ] T041 [P] [US4] Vitest tests for the users-list component in `frontend/src/app/users/users-list/users-list.component.spec.ts`: renders the paginated list, triggers a role change, is not reachable/rendered for a non-Admin

### Implementation for User Story 4

- [ ] T042 [US4] Create `UserListItemDto` and `ChangeRoleRequest` DTOs in `backend/TaskFlow.Api/Dtos/`
- [ ] T043 [US4] Implement `UserService` (paginated list, role change with the last-Admin guard evaluated immediately before commit) in `backend/TaskFlow.Api/Services/UserService.cs` — depends on T006, T007, T042
- [ ] T044 [US4] Implement `UsersController` (`GET /api/users`, `PUT /api/users/{id}/role`) with `[Authorize(Roles = "Admin")]` on both actions in `backend/TaskFlow.Api/Controllers/UsersController.cs` — depends on T043
- [ ] T045 [US4] Build `frontend/src/app/users/users.service.ts` calling the Users API — depends on T016
- [ ] T046 [US4] Build the `users-list` component (paginated table, role-change control) in `frontend/src/app/users/users-list/` (+ template) — depends on T045
- [ ] T047 [US4] Add an Admin-only route guard (extend/compose with `auth.guard.ts`) for the Users route in `frontend/src/app/app.routes.ts`, and add the "Users" navigation entry only for Admins in the header — depends on T034, T037, T046

**Checkpoint**: All four user stories are independently functional — the feature described in `spec.md` is complete end-to-end.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Feature-wide verification against the constitution's Definition of Done

- [ ] T048 [P] Verify `dotnet ef database update` applies cleanly to a fresh database and the app fails to start (per T012) when `Admin:Password` is unset — validates quickstart.md scenario 7
- [ ] T049 [P] Password-non-exposure audit (validates SC-005 and constitution Principle II directly, not just incidentally): integration test(s) in `backend/TaskFlow.Api.Tests/Integration/` asserting the response bodies of `POST /api/auth/register`, `POST /api/auth/login`, `GET /api/auth/me`, and `GET /api/users` never contain a `password` or `passwordHash` property (serialize each response and assert the property is absent, not merely null); plus a manual review confirming no logging statement in `AuthService`, `UserService`, or `AdminSeeder` includes a raw password or password hash
- [ ] T050 [P] Review all new C# files for the constitution's required educational comments (DI lifetimes, async/await, DbContext/change tracking, middleware order, DTOs vs. entities — see plan.md "Concepts You Will Learn")
- [ ] T051 Add a "What I learned" entry for this feature to the project `README.md` (constitution Definition of Done item 5) — create `README.md` if it does not yet exist
- [ ] T052 Run all `quickstart.md` validation scenarios end-to-end manually
- [ ] T053 Confirm zero build warnings (`dotnet build`) and all tests pass (`dotnet test`, and the frontend Vitest suite) — this includes T049's password-non-exposure audit

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Story 1 (Phase 3)**: Depends only on Foundational
- **User Story 2 (Phase 4)**: Depends only on Foundational (not on US1's registration code, though it shares `AuthService`/`AuthController` files — see note below)
- **User Story 3 (Phase 5)**: Depends on Foundational; its "logged-in" precondition is easiest to exercise once US2 exists, but the header component itself only reads `AuthService` state
- **User Story 4 (Phase 6)**: Depends on Foundational; easiest to exercise once US1/US2 exist (need a way to sign in as Admin), but its DTOs/service/controller/component files are independent of US1-3's files
- **Polish (Phase 7)**: Depends on all four user stories being complete (T049 specifically needs the register/login/me/users endpoints from US1, US2, and US4 to all exist)

**Note on shared files**: US1 and US2 both add methods to `AuthService.cs` and `AuthController.cs` (Register in US1; Login/Logout/Me in US2). These are different methods in the same file, so US1 and US2 are functionally independent (each can be demoed on its own once its methods exist) but not strictly parallel-safe at the file level — sequence T021→T031 and T022→T032 rather than editing both simultaneously.

### Within Each User Story

- Tests are written and confirmed failing before implementation tasks (constitution Principle I) — this now also applies to the Foundational phase's `AdminSeeder` (T011→T012) and `AuthService` skeleton (T014→T015)
- DTOs/entities before services; services before controllers/components; core logic before UI wiring

### Parallel Opportunities

- Setup: T002, T003, T004, T005 in parallel after T001
- Foundational: T008, T009, T010, T011, T013, T014 in parallel after T006-T007; T012 follows T011 (test-first); T015 follows T014 (test-first); T016 follows T015
- Tests within a story: T017-T019 (US1) in parallel; T025, T026, T028 (US2) in parallel — note T027 follows T026 in the same file (`AuthEndpointsTests.cs`) rather than running parallel to it; T039-T041 (US4) in parallel
- Once Foundational is done, US1 and US2 backend/frontend work can proceed in parallel by different developers (mind the shared-file note above); US3 and US4 similarly once their dependencies land

---

## Parallel Example: User Story 1

```bash
# Tests for User Story 1 together:
Task: "Unit tests for AuthService.RegisterAsync in backend/TaskFlow.Api.Tests/Services/AuthServiceTests.cs"
Task: "Integration tests for POST /api/auth/register in backend/TaskFlow.Api.Tests/Integration/AuthEndpointsTests.cs"
Task: "Vitest tests for registration flow in frontend/src/app/auth/register/register.component.spec.ts"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: register a visitor, confirm Developer role + home page
5. Per constitution Principle VIII (Human in the Loop): pause here for maintainer review before continuing

### Incremental Delivery

1. Setup + Foundational → foundation ready
2. User Story 1 → validate → review checkpoint (MVP)
3. User Story 2 → validate → review checkpoint (full auth loop)
4. User Story 3 → validate → review checkpoint (identity visible everywhere)
5. User Story 4 → validate → review checkpoint (admin role management)
6. Polish (Phase 7)

**Note on feature size**: this feature's total new/changed file count (~32 across backend + frontend + tests) exceeds the constitution's "~15 files, readable in one sitting" guideline for a single slice (Principle VII — a target, not a NON-NEGOTIABLE rule). The feature was scoped as one unit in `spec.md` because registration, login, and role-based access are inseparable as a first feature; `plan.md`'s Constitution Check documents this as an accepted deviation rather than a false PASS. To keep each increment reviewable, treat each user-story checkpoint above (not just the final one) as a natural pause-and-review point, per Principle VIII.

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps each task to its user story for traceability
- Tests MUST be written first and MUST fail before their implementation tasks (constitution Principle I — not optional for this project, and applies to Foundational-phase services too, not only user-story phases)
- Commit after each task or logical group
- Stop at any checkpoint to validate a story independently, per the constitution's Human-in-the-Loop principle
