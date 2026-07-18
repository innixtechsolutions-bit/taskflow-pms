<!--
Sync Impact Report
- Version change: 1.0.0 → 1.1.0 (MINOR — new subsection added: Commit Convention,
  materially expanding Development Workflow & Quality Gates guidance)
- Modified principles: none
- Added principles: none
- Added sections: Development Workflow & Quality Gates → "Commit Convention"
  subsection (Conventional Commits format, feat/US-story referencing + test-count
  rule, one-logical-change-per-commit rule, commit-body-explains-why rule)
- Expanded sections: Development Workflow & Quality Gates
- Removed sections: none
- Templates requiring updates:
  - ✅ .specify/templates/tasks-template.md ("Commit after each task or logical
    group" line already present and compatible — no edit needed)
  - ✅ .specify/templates/plan-template.md (no commit-message-specific references)
  - ✅ .specify/templates/spec-template.md (no commit-message-specific references)
- Follow-up TODOs: none

---
Sync Impact Report (previous amendment, retained for history)
- Version change: 1.0.0 → 1.0.0 (kept unchanged per explicit maintainer instruction,
  even though this amendment adds substantial new material; note that under the
  versioning policy below this content would normally warrant a MINOR bump —
  flagged here rather than applied automatically)
- Modified principles:
  - II. Secure by Default (Authentication & Authorization) [refined: ASP.NET Core
    PasswordHasher, JWT bearer only, [Authorize(Roles=...)], EF Core parameterization]
  - III. Simplicity & YAGNI → III. Clarity Over Cleverness [renamed/refined per
    maintainer's stricter anti-pattern rules: no Repository/CQRS/UoW without
    justification; default Controller → Service → DbContext]
  - IV. Consistent Code Quality & Review Gates [refined: concrete C#/Angular/API
    design standards from maintainer's stack]
- Added principles: VI. Teach While Building (NON-NEGOTIABLE); VII. Incremental,
  Feature-by-Feature Delivery; VIII. Human in the Loop
- Added sections: Technology Stack (FIXED); Definition of Done (per feature)
- Expanded sections: Security & Data Protection Requirements; Development
  Workflow & Quality Gates; Governance (amendment procedure, "stop and ask"
  rule)
- Removed sections: none
- Source: merged from root-level constitution.md (maintainer-authored source
  copy), which has been deleted after this merge
- Templates requiring updates:
  - ✅ .specify/templates/plan-template.md (Constitution Check gate is generic/derived — no edit needed)
  - ✅ .specify/templates/spec-template.md (no constitution-specific references found)
  - ✅ .specify/templates/tasks-template.md (no constitution-specific references found)
  - ⚠ README.md — does not exist yet; principle VIII/Definition of Done reference
    a "What I learned" log in README that must be created before feature 1 is DONE
- Follow-up TODOs: none
-->

# TaskFlow PMS Constitution

<!--
This constitution governs all specs, plans, tasks, and implementations
generated for this project. Every /speckit-plan and /speckit-implement
run MUST comply with these principles. When a principle conflicts with
a "best practice" the agent knows, THIS FILE WINS.

Context: The maintainer is an experienced Angular developer who is
LEARNING .NET and SQL Server through this project. Code readability
and educational value are first-class requirements, not nice-to-haves.
-->

## Core Principles

### I. Test-First Development (NON-NEGOTIABLE)

Every feature MUST have automated tests written before its implementation
begins. Tests MUST be written, run to confirm they fail, and only then
followed by implementation code that makes them pass (Red-Green-Refactor). A
pull request MUST NOT merge with failing, skipped, or missing tests for the
behavior it introduces.

Concretely:
- Backend: xUnit. Every service class gets unit tests for its core logic. EF
  Core is tested against the SQL Server provider using a disposable test
  database — not the InMemory provider.
- Frontend: Vitest (Angular's default). Test signal logic and services; do not
  chase 100% coverage on templates.
- Each feature's `tasks.md` MUST include test tasks that are completed before
  the implementation is considered done.

**Rationale**: TaskFlow's core domain (roles, permissions, sprints, boards) is
security- and workflow-critical. Untested authorization and state-transition
logic is the most common source of privilege-escalation and data-integrity
bugs in collaborative systems, so tests must exist before code, not after.

### II. Secure by Default (Authentication & Authorization) (NON-NEGOTIABLE)

Every protected resource — UI route or API endpoint — MUST independently
verify identity and role on the server; Angular route guards are a UX
convenience only and MUST NEVER be relied upon as a security boundary.
Passwords MUST be hashed with ASP.NET Core's `PasswordHasher` (never plain
text, never homemade crypto) and MUST NEVER appear in logs, API responses, or
error messages. Authentication failures MUST return generic, non-enumerable
messages (e.g., one shared "Invalid email or password" for both a wrong email
and a wrong password).

Concretely:
- All non-auth endpoints require a valid JWT bearer token.
- Role checks use `[Authorize(Roles = ...)]` enforced server-side.
- EF Core queries use parameterization only; raw SQL requires explicit
  justification in the plan.

**Rationale**: TaskFlow is a multi-role collaboration tool (Developer,
Manager, Admin) where every other feature depends on correctly knowing who
the user is and what they may do. A single authorization bypass or credential
leak compromises the entire system, not just one feature.

### III. Clarity Over Cleverness (NON-NEGOTIABLE)

Generate simple, readable, well-commented code. Prefer explicit code over
abstractions — no clever one-liners, no premature generalization, no
speculative interfaces. Do NOT introduce a design pattern (Repository,
Mediator/CQRS, Unit of Work, etc.) unless the plan explains in plain language
WHY it is needed for this specific feature. Default to the simplest thing
that works: Controller → Service → EF Core DbContext. Any added complexity
MUST be justified by a concrete, currently-specified need and documented in
the plan's Complexity Tracking section.

**Rationale**: Early-stage TaskFlow features evolve quickly through
iteration, and the maintainer is learning the stack as they go. Premature
abstraction slows iteration and learning more than it saves, and unused
flexibility is a maintenance and comprehension liability.

### IV. Consistent Code Quality & Review Gates

All code MUST pass linting, type-checking, and the automated test suite in CI
before merge (C# builds with zero warnings — nullable reference types
enabled, warnings treated as errors). Every non-trivial change requires at
least one review before merging to the main branch. All API error responses
MUST follow a single, consistent shape (`ProblemDetails`) across the codebase.

Concretely — C#:
- Async all the way for I/O: every EF Core query and controller action that
  touches the database is async.
- DTOs for all API input/output; entities never leave the service layer. Use
  manual mapping (simple methods) — no AutoMapper.
- Validation with data annotations on request DTOs.
- File-scoped namespaces; one public type per file.

Concretely — Angular/TypeScript:
- Strict TypeScript. No `any`.
- Components stay small; data fetching lives in services.
- Signals (`signal`, `computed`, `resource`) for component and service state;
  RxJS only where streams are genuinely needed (e.g., SignalR events).
- Follow the official Angular style guide for naming and structure.

Concretely — API design:
- RESTful, plural nouns: `/api/projects`, `/api/projects/{id}/tasks`.
- Standard status codes: 200/201/204/400/401/403/404.
- Pagination on all list endpoints from day one (`page`, `pageSize`).

**Rationale**: A multi-contributor full-stack application needs enforced
consistency so the frontend and backend can rely on predictable contracts,
and so reviewers spend their attention on logic and design, not formatting or
one-off error shapes.

### V. API Contract Stability & Versioning

Breaking changes to API request/response shapes or database schemas MUST be
called out explicitly in the implementation plan and MUST include a migration
path; additive, backward-compatible changes are preferred over breaking ones.
Public API contracts consumed by the Angular frontend follow semantic
versioning discipline: a breaking change requires a major version bump and a
documented migration note. Database schema changes are made exclusively via
EF Core code-first migrations, never by hand-editing the database; every
migration is committed to git with a descriptive name (e.g.,
`AddTaskPriorityColumn`, not `Update1`).

**Rationale**: TaskFlow's frontend and backend are evolved and deployed as
separate units. Undocumented breaking changes between them — or undocumented
schema drift — fail silently in production rather than at build or review
time.

### VI. Teach While Building (NON-NEGOTIABLE)

The maintainer is new to .NET and SQL Server. Every generated C# file MUST
include brief explanatory comments for .NET-specific concepts the first time
they appear in the codebase, e.g.:
- dependency injection registration and lifetimes (Scoped/Singleton/Transient)
- async/await and why EF Core calls are awaited
- `DbContext`, `DbSet`, and change tracking
- middleware pipeline order in `Program.cs`
- model binding, DTOs vs entities, and why they are separated

Comments explain WHY, not just WHAT. One or two lines is enough — do not turn
files into essays. Each feature's `plan.md` MUST contain a short "Concepts you
will learn in this feature" section listing the .NET/SQL concepts involved.

**Rationale**: Code readability and educational value are first-class
requirements for this project, not nice-to-haves — the maintainer is learning
.NET and SQL Server through the act of building TaskFlow.

### VII. Incremental, Feature-by-Feature Delivery

One feature = one spec = one branch = one small, reviewable slice. Never
scaffold code for future features "while we're at it." Keep each feature's
implementation small enough to be read and understood in one sitting (target:
under ~15 new/changed files).

**Rationale**: Small, focused slices are easier to review, easier to learn
from, and reduce the risk of half-finished, hard-to-follow implementations.

### VIII. Human in the Loop

After `/speckit-implement`, the maintainer reviews all generated code before
the next feature begins. The agent MUST NOT chain into the next feature
automatically. If a requirement is ambiguous, mark it `[NEEDS CLARIFICATION]`
in the spec instead of guessing.

**Rationale**: Since the maintainer is learning the stack, understanding each
feature before moving to the next is part of the point — auto-chaining would
undermine the learning goal even if it were faster.

## Technology Stack (FIXED — do not substitute)

### Frontend
- Angular 22 (signal-first): signals for state, Signal Forms, standalone
  components, zoneless change detection, built-in control flow (`@if` /
  `@for`).
- No NgModules. No Zone.js. No third-party state library (no NgRx) — use
  signal-based services for state.
- Angular Material for UI components; Angular CDK for drag-and-drop.
- `HttpClient` with typed interfaces mirroring backend DTOs.

### Backend
- .NET 10 (LTS), ASP.NET Core Web API.
- Controllers (attribute-routed) — NOT Minimal APIs — because the controller
  style maps cleanly to Angular services and is what the maintainer will most
  often meet in industry codebases.
- Entity Framework Core 10 with SQL Server provider, code-first migrations.
- Authentication: JWT bearer tokens (no Identity UI scaffolding).
- Layering (keep it flat and obvious):
  - `Api/Controllers` — HTTP concerns only
  - `Api/Services` — business logic
  - `Api/Data` — DbContext, entities, migrations
  - `Api/Dtos` — request/response contracts
- No Clean Architecture / Onion / multiple projects until the app actually
  needs it. Single Web API project.

### Database
- SQL Server 2022 Developer Edition.
- Schema is defined via EF Core code-first migrations only. Never hand-edit
  the database.
- Every migration is committed to git. Migration names describe the change
  (`AddTaskPriorityColumn`, not `Update1`).
- Use `int` identity primary keys. Foreign keys and indexes must be explicit
  in the entity configuration, with a comment explaining each index's
  purpose.

## Security & Data Protection Requirements

- Session/auth tokens are JWT bearer tokens; Angular route guards are UX only
  and MUST NEVER be treated as a security control — every protected API
  endpoint independently verifies identity and role.
- Authentication endpoints MUST be rate-limited to mitigate brute-force
  attacks.
- Privileged mutations (e.g., role changes) MUST be restricted to the Admin
  role and MUST include safeguards against self-lockout (e.g., preventing
  removal of the last remaining Admin).
- Passwords are hashed with ASP.NET Core's `PasswordHasher` — never plain
  text, never homemade crypto.
- Connection strings and JWT secrets live in user-secrets / environment
  variables — never committed to git.
- EF Core parameterization only; raw SQL requires justification in the plan.

## Development Workflow & Quality Gates

- Every feature begins with a spec (`/speckit-specify`) and, when non-trivial,
  a plan (`/speckit-plan`) before implementation starts.
- Every protected page or endpoint's authorization logic MUST be covered by an
  integration test that verifies both the allowed path and the denied path.
- CI MUST run lint, type-check, and the full test suite on every pull request;
  merges are blocked on failure.
- After `/speckit-implement`, the maintainer reviews all generated code before
  the next feature begins (Principle VIII); the agent MUST NOT auto-chain into
  the next feature.

### Commit Convention

- All commits follow Conventional Commits: `type: summary` (max ~70 chars).
  Allowed types: `feat`, `fix`, `test`, `docs`, `chore`, `spec`, `refactor`.
- `feat` commits reference the user story (e.g. `feat: US4 edit/delete work
  item`) and include the passing test count when tests changed (e.g. "137/137
  backend tests pass").
- One logical change per commit — commit after each user story or logical
  group, never one giant batch per feature.
- The commit body, when used, explains WHY the change was made, not just what
  changed — the diff itself already shows what changed.

**Rationale**: Small, well-labeled commits make a feature's history reviewable
story-by-story rather than as one undifferentiated diff, and a consistent
`type:` prefix lets both the maintainer and tooling (e.g. changelog
generation) scan history at a glance.

## Definition of Done (per feature)

A feature is DONE only when:
1. Spec, plan, and tasks artifacts exist and are consistent (`/speckit-analyze`
   passes with no critical issues).
2. Code compiles with zero warnings; all tests pass.
3. Educational comments are present per Principle VI (Teach While Building).
4. The maintainer has read the generated code and can explain, in their own
   words, what each new file does. (Reviewer checklist item — the agent must
   remind the maintainer of this at the end of `/speckit-implement` output.)
5. README's "What I learned" log has a new entry for this feature.

## Governance

This constitution supersedes agent defaults, general best practices, and all
other development conventions for TaskFlow PMS. If the agent believes a
principle should be violated for a feature, it MUST stop and ask — never
silently deviate.

**Amendment procedure**: Amendments happen via a git commit that edits this
file with a one-line rationale in the commit message, together with an
updated version number per the versioning policy below and propagation of any
resulting changes to dependent templates (plan, spec, tasks) in the same
change.

**Versioning policy**:
- MAJOR: Backward-incompatible governance changes, or removal/redefinition of
  an existing principle.
- MINOR: A new principle added, or existing guidance materially expanded.
- PATCH: Clarifications, wording, and other non-semantic refinements.

All feature plans MUST include a Constitution Check confirming compliance
with these principles; unjustified deviations MUST be rejected at review or
the constitution MUST be amended first. Compliance is reviewed at each
`/speckit-plan` invocation via the plan template's Constitution Check section.

**Version**: 1.1.0 | **Ratified**: 2026-07-14 | **Last Amended**: 2026-07-17
