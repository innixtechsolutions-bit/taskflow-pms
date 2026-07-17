# Implementation Plan: Projects & Work Items

**Branch**: `002-projects-work-items` | **Date**: 2026-07-17 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-projects-work-items/spec.md`

## Summary

Add two entities to TaskFlow: **Project** (a named container of work,
created/edited/deleted by Manager/Admin only) and **Work Item** (a flat
unit of work — type is a label only, no parent/child hierarchy yet —
belonging to exactly one Project, creatable by any signed-in user, editable
by its creator/current assignee/any Manager or Admin). Backend: two new EF
Core entities and a code-first migration on top of Feature 001's existing
`AppDbContext`/`User`, two Controller→Service pairs following the same flat
layering as `AuthController`/`AuthService`, reusing the existing generic
`PagedResult<T>` for both project and work-item listing, with combined
role-and-ownership authorization checks in the service layer (extending the
last-admin-guard pattern from `UserService`). Frontend: Angular components
for a project list, a shared project create/edit form, a project detail
view (work-item list with filter/search/pagination), and a shared work-item
create/edit form — continuing Feature 001's plain-HTML-forms pattern (no
Angular Material components introduced) and applying the `[selected]`-per-
`<option>` lesson from Feature 001's Phase 7 bug fix from the start for
every dropdown.

## Technical Context

**Language/Version**: C# / .NET 10 (LTS) for the backend; TypeScript
(strict) / Angular 22 for the frontend — both fixed by the project
constitution and already established in Feature 001.

**Primary Dependencies**: ASP.NET Core Web API (attribute-routed
Controllers), EF Core 10 (SQL Server provider, code-first migrations) —
same packages already referenced by `TaskFlow.Api`, no new package
references needed. Angular 22 (standalone components, signals, Signal
Forms, zoneless change detection, built-in `@if`/`@for`) — same as Feature
001; Angular Material remains unused by choice (see research.md §6).

**Storage**: SQL Server 2022 Developer Edition via EF Core code-first
migrations, extending the existing `TaskFlowDb` database with two new
tables (`Projects`, `WorkItems`) and their foreign keys to the existing
`Users` table.

**Testing**: xUnit for the backend (service-level unit tests + integration
tests against a disposable SQL Server test database, same
`SqlServerTestDatabase`/`TaskFlowApiFactory` fixtures Feature 001 already
built); Vitest for the frontend.

**Target Platform**: Server-side ASP.NET Core Web API behind the existing
Angular SPA — no new deployment target.

**Project Type**: Web application (frontend + backend) — same single Web
API project as Feature 001, no new project/architecture layer.

**Performance Goals**: No throughput target specified; same internal-tool
scale as Feature 001. Reasonable default: interactive (sub-second) API
responses under normal load, sufficient to meet SC-001/SC-002's
near-immediate list-update expectations.

**Constraints**: A project name must be unique case-insensitively (FR-002);
deleting a project cascades to delete all its work items (FR-009); a work
item's project is immutable after creation (FR-014); every role/ownership
rule is enforced server-side regardless of client UI (FR-025); all error
responses share Feature 001's existing `ProblemDetails` shape (FR-026);
lists must reflect the current state immediately after a mutation, without
a manual refresh (FR-027, SC-006).

**Scale/Scope**: Same internal-tool scale as Feature 001 (tens to low
hundreds of users per organization). This feature: 2 new entities (`Project`,
`WorkItem`, plus 3 new enums), 9 new API endpoints, 6 user stories.

## Concepts You Will Learn in This Feature

*(Required by constitution Principle VI — Teach While Building)*

- **EF Core relationships & foreign keys**: this is TaskFlow's first
  one-to-many relationship (`Project` → `WorkItem`) and first optional
  foreign key (`WorkItem.AssigneeUserId`) — how EF Core's code-first
  conventions infer these from navigation properties, and how cascade
  delete actually happens at the database level.
- **`DeleteBehavior` and SQL Server's multiple-cascade-paths rule**: why
  `Project` → `WorkItem` cascades on delete (FR-009) but every foreign key
  pointing at `User` (creator/assignee) must NOT — SQL Server refuses to
  create a schema where the same child row could be cascade-deleted via two
  different paths, and mixing these up produces a real migration error, not
  just a style preference.
- **Authorization beyond `[Authorize(Roles=...)]`**: work-item edit/delete
  rules depend on *who the caller is relative to the resource* (its
  creator or current assignee), not just their role — this can't be
  expressed as a routing attribute and has to be a check inside the service
  layer, extending the same pattern `UserService`'s last-admin guard already
  established.
- **Composable, conditional `IQueryable` filtering**: building a work-item
  search/filter query by appending `.Where(...)` only for the filters a
  caller actually supplied, instead of one large conditional expression —
  and why this still translates to a single efficient SQL query (EF Core
  defers execution until the query is enumerated).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Test-First Development | **PASS** | `tasks.md` (next phase) will include xUnit service/integration tests and Vitest tests as prerequisite tasks before each implementation task, per constitution and Feature 001's precedent. |
| II. Secure by Default | **PASS** | Every new endpoint requires `[Authorize]` at minimum; project mutation endpoints add `[Authorize(Roles = "Manager,Admin")]`; work-item edit/delete ownership checks happen server-side in the service layer regardless of UI state (contracts/). |
| III. Clarity Over Cleverness | **PASS** | Same flat Controller → Service → `AppDbContext` layering as Feature 001; no Repository/CQRS/Unit-of-Work; work-item `Type` stays a flat enum with no hierarchy (per spec's explicit scope); reuses the existing generic `PagedResult<T>` rather than inventing a second pagination shape; delete confirmation is a client-side UX step using data already fetched via `GET`, not a dedicated preview endpoint. |
| IV. Consistent Code Quality & Review Gates | **PASS** | DTOs for all I/O, no AutoMapper, `ProblemDetails` for all errors, nullable reference types + warnings-as-errors, file-scoped namespaces; Angular strict TypeScript, signals, Angular style guide — all continuing Feature 001's established conventions. |
| V. API Contract Stability & Versioning | **PASS** | All new endpoints — no existing contract changes. Migration named descriptively (`AddProjectsAndWorkItems`) and committed to git. |
| VI. Teach While Building | **PASS** | "Concepts You Will Learn" section above; inline comments required in generated C# per the concept list, especially the cascade-path gotcha. |
| VII. Incremental, Feature-by-Feature Delivery | **ACCEPTED DEVIATION** | Estimated ~25 new/changed backend files + ~25 new/changed frontend files (~50 total, see Project Structure below) — larger than Feature 001's own deviation, for the same underlying reason plus one new one: two small, additive touches to Feature 001 files turned out to be genuine prerequisites (exposing the caller's own id per research.md §8, and a non-Admin user-lookup endpoint per §9) rather than something a tighter scope could avoid. Projects and Work Items are scoped together deliberately (per the source feature description): a project with nothing to put in it, or work items with no container, are not independently meaningful slices. Mitigation: `tasks.md` will again treat each user-story checkpoint as its own review pause (Principle VIII), not just the final one, and the two Feature-001-touching changes are called out as their own explicit tasks so they're easy to review in isolation. |
| VIII. Human in the Loop | **PASS** | No `[NEEDS CLARIFICATION]` markers remain in the spec; this plan does not chain into `/speckit-tasks` or implementation automatically. |

**Result**: No NON-NEGOTIABLE principle violations. One accepted deviation from a non-mandatory target (Principle VII, above), on the same grounds already accepted for Feature 001. Complexity Tracking table below is not required — reserved for MUST-level violations, and Principle VII's file-count guidance is a target, not a MUST.

## Project Structure

### Documentation (this feature)

```text
specs/002-projects-work-items/
├── plan.md               # This file (/speckit-plan command output)
├── research.md           # Phase 0 output (/speckit-plan command)
├── data-model.md         # Phase 1 output (/speckit-plan command)
├── quickstart.md         # Phase 1 output (/speckit-plan command)
├── contracts/            # Phase 1 output (/speckit-plan command)
│   ├── projects-api.md
│   └── work-items-api.md
└── tasks.md              # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
backend/
├── TaskFlow.Api/
│   ├── Controllers/
│   │   ├── ProjectsController.cs      # create/list/get/edit/delete a project
│   │   ├── WorkItemsController.cs     # create/list (per project), get/edit/delete a work item
│   │   ├── UsersController.cs         # updated (Feature 001 file): class-level [Authorize(Roles="Admin")]
│   │   │                              #   moves to its two existing actions individually; new GetLookup
│   │   │                              #   action allows any role (research.md §9)
│   │   └── AuthController.cs          # updated (Feature 001 file): Me() also returns the caller's Id
│   ├── Services/
│   │   ├── ProjectService.cs          # CRUD + duplicate-name guard + total/open item counts
│   │   ├── WorkItemService.cs         # CRUD + creator/assignee/role authorization + filter/search
│   │   ├── UserService.cs             # updated (Feature 001 file): + GetAssignableUsersAsync (id+fullName only)
│   │   └── AuthService.cs             # updated (Feature 001 file): IssueToken includes Id (research.md §8)
│   ├── Data/
│   │   ├── AppDbContext.cs            # updated: Projects/WorkItems DbSets, relationships, indexes
│   │   ├── Entities/
│   │   │   ├── Project.cs
│   │   │   └── WorkItem.cs            # includes WorkItemType/Priority/Status enums (see data-model.md)
│   │   └── Migrations/                # new: AddProjectsAndWorkItems
│   ├── Dtos/
│   │   ├── ProjectRequest.cs          # POST and PUT bodies are identically shaped — one DTO, not two
│   │   ├── ProjectListItemDto.cs      # for GET /api/projects (open item count)
│   │   ├── ProjectDetailDto.cs        # for GET/POST/PUT .../{id} (total item count, for delete confirm)
│   │   ├── WorkItemRequest.cs         # POST and PUT bodies are identically shaped — one DTO, not two
│   │   ├── WorkItemDto.cs
│   │   ├── UserLookupItemDto.cs       # id + fullName only (research.md §9)
│   │   ├── AuthResponse.cs            # updated (Feature 001 file): + Id
│   │   └── MeResponse.cs              # updated (Feature 001 file): + Id
│   └── Program.cs                     # updated: register ProjectService, WorkItemService (Scoped)
└── TaskFlow.Api.Tests/
    ├── Services/
    │   ├── ProjectServiceTests.cs
    │   ├── WorkItemServiceTests.cs
    │   └── UserServiceTests.cs        # updated (Feature 001 file): + GetAssignableUsersAsync tests
    └── Integration/
        ├── ProjectsEndpointsTests.cs   # covers allowed + denied paths
        ├── WorkItemsEndpointsTests.cs  # covers allowed + denied paths, incl. creator/assignee edit rules
        └── UsersEndpointsTests.cs      # updated (Feature 001 file): + lookup-endpoint allowed-for-any-role test

frontend/
├── src/app/
│   ├── auth/
│   │   └── auth.service.ts            # updated (Feature 001 file): AuthState/AuthApiResponse + id (research.md §8)
│   ├── projects/
│   │   ├── projects-list/             # project list + "New Project" (Manager/Admin only)
│   │   ├── project-form/              # shared create + edit project form
│   │   ├── project-detail/            # project header + work-item list/filter/search/pagination
│   │   ├── work-item-form/            # shared create + edit work-item form
│   │   ├── projects.service.ts
│   │   └── work-items.service.ts
│   ├── shared/header/                 # updated: "Projects" nav entry for every signed-in user
│   └── app.routes.ts                  # updated: /projects, /projects/:id, work-item routes
└── (Vitest specs colocated with each unit above, per Angular convention)
```

**Structure Decision**: Continues Feature 001's web application split
(Option 2: frontend + backend) and single-Web-API-project layout — no new
projects, no new architectural layers. New backend code follows the exact
same flat `Controllers/Services/Data/Dtos` structure already established.

## Complexity Tracking

*No Constitution Check violations — table intentionally omitted (see the accepted Principle VII deviation above, which is not a violation requiring justification here).*
