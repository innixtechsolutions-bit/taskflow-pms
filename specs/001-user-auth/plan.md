# Implementation Plan: User Registration, Login & Role-Based Access

**Branch**: `001-user-auth` | **Date**: 2026-07-15 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-user-auth/spec.md`

## Summary

Build registration, login, session persistence (8-hour JWT), logout, and
Admin-only user/role management for TaskFlow PMS, seeded with an initial
Admin account from configuration. Backend: ASP.NET Core Web API (Controllers)
issuing JWT bearer tokens, EF Core + SQL Server for a single `User` entity
with an enum `Role`, `PasswordHasher` for credentials, an in-memory
per-email login-attempt tracker for rate limiting. Frontend: Angular 22
signal-based `AuthService` + `HttpInterceptor` + functional route guard,
storing the token in `localStorage`, with a header component showing
identity/role and an Admin-only Users page with pagination and role changes.

## Technical Context

**Language/Version**: C# / .NET 10 (LTS) for the backend; TypeScript
(strict) / Angular 22 for the frontend — both fixed by the project
constitution.

**Primary Dependencies**: ASP.NET Core Web API (attribute-routed
Controllers), EF Core 10 (SQL Server provider, code-first migrations),
`Microsoft.AspNetCore.Authentication.JwtBearer`,
`Microsoft.AspNetCore.Identity`'s `PasswordHasher<User>` (used standalone,
not full Identity); Angular 22 (standalone components, signals, Signal
Forms, zoneless change detection, built-in `@if`/`@for`), Angular Material,
Angular CDK (not needed by this feature specifically, but present per stack).

**Storage**: SQL Server 2022 Developer Edition via EF Core code-first
migrations. Single `Users` table (see `data-model.md`); no separate session
store — sessions are represented by the JWT itself.

**Testing**: xUnit for the backend (service-level unit tests + integration
tests against a disposable SQL Server test database, not InMemory); Vitest
for the frontend (signal/service logic).

**Target Platform**: Server-side ASP.NET Core Web API (cross-platform host)
behind a browser-based Angular SPA.

**Project Type**: Web application (frontend + backend), single Web API
project — no Clean Architecture / multi-project split (constitution
Principle III / Technology Stack).

**Performance Goals**: No throughput target is specified by the feature; a
small internal team tool. Reasonable default: interactive API responses
(sub-second) under normal load, sufficient to meet SC-001 (<1 min end-to-end
registration) and SC-004 (<30s to find and change a role) with substantial
margin.

**Constraints**: 8-hour absolute (non-sliding) session lifetime (FR-009); 5
failed logins / 15 minutes per email triggers a 15-minute block (FR-019);
passwords never logged or returned (FR-006); application MUST fail to start
if no Admin exists and seed Admin credentials are missing/empty (FR-018a);
all error responses share one consistent shape (FR-020); frontend route
protection is UX-only — every protected endpoint independently verifies
identity/role server-side (FR-021).

**Scale/Scope**: Internal tool for software teams (tens to low hundreds of
users per organization), not internet-scale. This feature: 1 entity (`User`
+ `Role` enum), 6 API endpoints, 4 user stories.

## Concepts You Will Learn in This Feature

*(Required by constitution Principle VI — Teach While Building)*

- **Dependency injection & lifetimes**: registering `AppDbContext` (Scoped),
  `PasswordHasher<User>` and `ILoginAttemptTracker` (Singleton, since it
  wraps a shared `IMemoryCache`) in `Program.cs`, and why the lifetime choice
  matters for each.
- **async/await with EF Core**: why every controller action and EF Core
  query in this feature is `async`, and what awaiting actually does.
- **`DbContext` & `DbSet`**: how `AppDbContext` maps the `User` entity to the
  `Users` table, and how EF Core tracks changes for `SaveChangesAsync`.
- **Middleware pipeline order**: where JWT bearer authentication,
  authorization, and exception/`ProblemDetails` handling sit in
  `Program.cs`'s pipeline, and why order matters.
- **Model binding & DTOs vs. entities**: why `RegisterRequest`/`LoginRequest`
  DTOs exist separately from the `User` entity, and how ASP.NET Core binds
  a JSON request body to a DTO with data-annotation validation.
- **JWT bearer authentication**: how a signed token's claims (identity,
  role, `exp`) let the API authenticate and authorize a request without a
  server-side session store, and the trade-off that implies for logout.
- **Angular signals for auth state**: how a root-provided `signal` +
  `computed` pair models "who is logged in," and how a functional
  `CanActivateFn` guard and an `HttpInterceptor` both read from it.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Test-First Development | **PASS** | `tasks.md` (next phase) will include xUnit service/integration tests and Vitest tests as prerequisite tasks before each implementation task, per constitution. |
| II. Secure by Default | **PASS** | JWT bearer + server-side `[Authorize(Roles=...)]` on every protected endpoint (`contracts/`); `PasswordHasher` only; generic auth-failure message (FR-008); Angular guard explicitly documented as UX-only (research.md §9). |
| III. Clarity Over Cleverness | **PASS** | Flat Controller → Service → `AppDbContext` layering (Project Structure below); no Repository/CQRS/Unit-of-Work introduced; `Role` kept as an enum, not a speculative `Roles` table (data-model.md). |
| IV. Consistent Code Quality & Review Gates | **PASS** | DTOs for all I/O, no AutoMapper (manual mapping), `ProblemDetails` for all errors, nullable reference types + warnings-as-errors, file-scoped namespaces; Angular strict TypeScript, signals, Angular style guide. |
| V. API Contract Stability & Versioning | **PASS** | Greenfield feature — no existing contract to break. Migrations will be named descriptively (e.g. `AddUsersTable`) and committed to git. |
| VI. Teach While Building | **PASS** | "Concepts You Will Learn" section above; inline comments required in generated C# per the concept list. |
| VII. Incremental, Feature-by-Feature Delivery | **ACCEPTED DEVIATION** | Single feature, single branch; no scaffolding for future features (projects/sprints/boards) included — but the feature's total new/changed file count (~32, see `tasks.md`) exceeds Principle VII's "~15 files, one sitting" target. Registration, login, session handling, and role-based access are inseparable as a first feature (nothing else in TaskFlow is reachable without auth), so the feature was intentionally scoped as one unit rather than split. Mitigation: `tasks.md` treats each user-story checkpoint (not just the final one) as a review pause, per Principle VIII. |
| VIII. Human in the Loop | **PASS** | No further `[NEEDS CLARIFICATION]` markers remain in the spec; plan does not chain into implementation or the next feature automatically. |

**Result**: No NON-NEGOTIABLE principle violations. One accepted deviation from a non-mandatory target (Principle VII, above). Complexity Tracking table below is not required — that table is reserved for violations of a MUST-level principle (e.g., introducing a disallowed design pattern), and Principle VII's file-count guidance is a target, not a MUST.

## Project Structure

### Documentation (this feature)

```text
specs/001-user-auth/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md         # Phase 1 output (/speckit-plan command)
├── quickstart.md         # Phase 1 output (/speckit-plan command)
├── contracts/            # Phase 1 output (/speckit-plan command)
│   ├── auth-api.md
│   └── users-api.md
└── tasks.md              # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
backend/
├── TaskFlow.Api/
│   ├── Controllers/
│   │   ├── AuthController.cs        # register, login, logout, me
│   │   └── UsersController.cs       # list users, change role (Admin only)
│   ├── Services/
│   │   ├── AuthService.cs           # register/login logic, token issuance
│   │   ├── UserService.cs           # list/paginate, role-change + last-admin guard
│   │   └── LoginAttemptTracker.cs   # IMemoryCache-backed rate limiter
│   ├── Data/
│   │   ├── AppDbContext.cs
│   │   ├── Entities/
│   │   │   └── User.cs              # includes Role enum
│   │   └── Migrations/              # EF Core code-first migrations
│   ├── Dtos/
│   │   ├── RegisterRequest.cs
│   │   ├── LoginRequest.cs
│   │   ├── AuthResponse.cs
│   │   ├── UserListItemDto.cs
│   │   ├── PagedResult.cs
│   │   └── ChangeRoleRequest.cs
│   ├── Startup/
│   │   └── AdminSeeder.cs           # startup fail-fast seed logic (research.md §5)
│   └── Program.cs
└── TaskFlow.Api.Tests/
    ├── Services/
    │   ├── AuthServiceTests.cs
    │   ├── UserServiceTests.cs
    │   └── LoginAttemptTrackerTests.cs
    └── Integration/
        ├── AuthEndpointsTests.cs    # covers allowed + denied paths
        └── UsersEndpointsTests.cs   # covers allowed + denied paths, incl. last-admin guard

frontend/
├── src/app/
│   ├── auth/
│   │   ├── login/                   # login page component
│   │   ├── register/                # registration page component
│   │   ├── auth.service.ts          # signal-based auth state, localStorage sync
│   │   ├── auth.guard.ts            # functional CanActivateFn
│   │   ├── auth.interceptor.ts      # attaches Authorization header
│   │   └── auth.service.spec.ts
│   ├── users/
│   │   ├── users-list/              # Admin-only Users page
│   │   ├── users.service.ts
│   │   └── users.service.spec.ts
│   ├── shared/
│   │   └── header/                  # name/role display + logout control
│   └── app.routes.ts
└── (Vitest specs colocated with each unit above, per Angular convention)
```

**Structure Decision**: Web application split (Option 2: frontend + backend),
matching the constitution's fixed stack. Backend is a single ASP.NET Core
Web API project with flat `Controllers/Services/Data/Dtos` layering — no
additional projects or architectural layers, per Principle III and the
constitution's explicit "single Web API project" directive.

## Complexity Tracking

*No Constitution Check violations — table intentionally omitted.*
