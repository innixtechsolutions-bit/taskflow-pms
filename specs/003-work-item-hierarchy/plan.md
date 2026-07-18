# Implementation Plan: Work Item Hierarchy

**Branch**: `003-work-item-hierarchy` | **Date**: 2026-07-18 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/003-work-item-hierarchy/spec.md`

## Summary

Give Feature 002's flat `WorkItem` entity a self-referencing
`ParentWorkItemId` so items form a strict Epic → Story → Task → SubTask
chain, enforced entirely server-side in `WorkItemService`. Backend: one new
nullable FK column + migration on the existing `WorkItems` table, three new
lightweight DTOs (tree node, detail-view child, parent-candidate lookup),
and service logic for parent validation, type-change guards, and
application-level cascade delete (SQL Server disallows a cascading
self-referencing FK — research.md §1). Two new read endpoints (a full
project tree, and a parent-candidate lookup) sit alongside Feature 002's
unchanged paginated/filtered list endpoint. Frontend: the project detail
page gains a Tree/Flat toggle (tree is new; flat is Feature 002's existing
table, untouched), the work-item form gains a type-dependent parent
picker, and a new work-item detail page shows the parent link and direct
children — all reusing Feature 002's existing Material components and
`confirm()`-dialog delete pattern.

## Technical Context

**Language/Version**: C# / .NET 10 (LTS) for the backend; TypeScript
(strict) / Angular 22 for the frontend — unchanged from Features 001/002.

**Primary Dependencies**: ASP.NET Core Web API (attribute-routed
Controllers), EF Core 10 (SQL Server provider, code-first migrations) — no
new package references. Angular 22 (standalone components, signals, Signal
Forms, zoneless change detection) with Angular Material, continuing
Feature 002's styling pass; native `<select>` (not `mat-select`) remains
the convention for the new parent-type dropdowns, per Feature 002's
`[selected]`-per-`<option>` precedent (`002.../research.md` §6).

**Storage**: SQL Server 2022 Developer Edition via EF Core code-first
migration, adding one nullable `ParentWorkItemId` column and one index to
the existing `WorkItems` table — no new tables.

**Testing**: xUnit for the backend (service-level unit tests for parent
validation/type-change guards/cascade delete, plus integration tests for
the two new endpoints and the extended existing ones), using the same
`SqlServerTestDatabase`/`TaskFlowApiFactory` fixtures Features 001/002
already built. Vitest for the frontend.

**Target Platform**: Server-side ASP.NET Core Web API behind the existing
Angular SPA — no new deployment target.

**Project Type**: Web application (frontend + backend) — same single Web
API project as Features 001/002.

**Performance Goals**: No throughput target specified; same internal-tool
scale as Feature 002. The new tree endpoint returns a whole project's
items unpaginated (research.md §4) — acceptable at this feature's scale
(tens to low hundreds of items per project).

**Constraints**: The parent-type chain (data-model.md's Hierarchy rules
table) is enforced server-side regardless of client UI (FR-008); cascade
delete of a subtree happens in one operation via application code, not a
database `ON DELETE CASCADE`, because SQL Server rejects a cascading
self-referencing foreign key (research.md §1); all new error responses use
the existing `ProblemDetails` shape (FR-024).

**Scale/Scope**: Same internal-tool scale as Feature 002. This feature: 1
new entity column (`ParentWorkItemId`) + 1 new index, 4 new DTOs, 2 new API
endpoints (tree, parent-candidates) alongside 3 changed existing endpoints
(create/update/delete/get), 1 new frontend route (work item detail), 5
user stories.

## Concepts You Will Learn in This Feature

*(Required by constitution Principle VI — Teach While Building)*

- **Self-referencing foreign keys and why SQL Server won't let you cascade
  them**: `WorkItem.ParentWorkItemId` points at another row in the *same*
  table. Unlike Feature 002's `Project → WorkItem` cascade, SQL Server
  flatly refuses `ON DELETE CASCADE` on a self-join (error 1785) because it
  can't prove the cascade terminates — so subtree deletion has to be
  written explicitly in `WorkItemService`, which is also the only way to
  compute the descendant count the delete confirmation needs (research.md
  §1).
- **Why a "rank" argument can replace a graph algorithm**: instead of
  writing a visited-set/ancestor-walk to stop cycles, this feature proves
  (research.md §2) that a strictly-ordered type chain (Epic < Story < Task
  < SubTask, no skipped levels) makes cycles unreachable by construction —
  a small example of using a domain invariant to delete code you'd
  otherwise have to write and test.
- **Validating a change against *existing* state, not just the incoming
  request**: a `Type` change has to be checked against the item's current
  parent and current children — data that isn't in the request body at
  all, unlike the simpler "is this field's own value valid" checks Feature
  002's `Priority`/`Status` parsing did (research.md §3).
- **Recursive/tree-shaped API responses**: `WorkItemTreeNodeDto` nests
  itself (`children: WorkItemTreeNodeDto[]`) — this feature's first
  recursive DTO, and first endpoint that deliberately returns an entire
  unpaginated collection because the shape (a tree) doesn't compose with
  pagination (research.md §4).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Test-First Development | **PASS** | `tasks.md` (next phase) will include xUnit tests for parent-type validation, type-change guards, cascade delete, and both new endpoints, plus Vitest tests for the new parent picker and tree/detail components — all before their implementation tasks, per constitution and Features 001/002's precedent. |
| II. Secure by Default | **PASS** | All new/changed endpoints keep `[Authorize]`; the two new read endpoints require no additional role (any signed-in user, same as the existing list/get endpoints); hierarchy rules and delete-subtree authorization are enforced server-side in `WorkItemService` regardless of UI state, per FR-008. |
| III. Clarity Over Cleverness | **PASS** | Same flat Controller → Service → `AppDbContext` layering; no Repository/CQRS/Unit-of-Work; cycle prevention is explicitly *not* implemented as a graph algorithm because the type chain already makes it unnecessary (research.md §2) — the simpler-than-expected outcome this principle argues for. Subtree delete is a small explicit loop, not a recursive CTE or raw SQL. |
| IV. Consistent Code Quality & Review Gates | **PASS** | DTOs for all new I/O, no AutoMapper, `ProblemDetails` for all new error cases, nullable reference types + warnings-as-errors, file-scoped namespaces; Angular strict TypeScript, signals, Angular style guide — continuing Features 001/002's established conventions. |
| V. API Contract Stability & Versioning | **PASS** | `POST`/`PUT` request and response shapes gain one optional field (`parentWorkItemId`) — additive, not breaking. `GET /api/work-items/{id}`'s response shape grows (new fields only, nothing removed) — additive per this principle's preference. Migration named descriptively (`AddWorkItemHierarchy`) and committed to git. |
| VI. Teach While Building | **PASS** | "Concepts You Will Learn" section above; inline comments required per the concept list, especially the self-referencing-cascade gotcha and the rank-argument cycle proof. |
| VII. Incremental, Feature-by-Feature Delivery | **ACCEPTED DEVIATION** | Estimated ~14 backend + ~10 frontend changed/new files (~24 total) — over the "under ~15" target, for the same underlying reason Feature 002 accepted: one coherent hierarchy concept touches the entity, its migration, three new DTOs, service validation, and two new endpoints on the backend, and a parent picker, a new tree view, and a new detail page on the frontend — none of these is independently meaningful (a parent picker with no tree to show the result in, or a tree endpoint with no way to create a parented item, isn't a useful slice on its own). Mitigation: `tasks.md` will scope each user story as its own reviewable checkpoint (Principle VIII), and the backend/frontend split gives two natural review passes even within one feature. |
| VIII. Human in the Loop | **PASS** | No `[NEEDS CLARIFICATION]` markers remain in the spec; this plan does not chain into `/speckit-tasks` or implementation automatically. |

**Result**: No NON-NEGOTIABLE principle violations. One accepted deviation
from a non-mandatory target (Principle VII, above), on the same grounds
already accepted for Feature 002. Complexity Tracking table below is not
required — reserved for MUST-level violations, and Principle VII's
file-count guidance is a target, not a MUST.

## Project Structure

### Documentation (this feature)

```text
specs/003-work-item-hierarchy/
├── plan.md               # This file (/speckit-plan command output)
├── research.md           # Phase 0 output (/speckit-plan command)
├── data-model.md         # Phase 1 output (/speckit-plan command)
├── quickstart.md         # Phase 1 output (/speckit-plan command)
├── contracts/            # Phase 1 output (/speckit-plan command)
│   └── work-item-hierarchy-api.md
├── checklists/
│   └── requirements.md
└── tasks.md              # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
backend/
├── TaskFlow.Api/
│   ├── Controllers/
│   │   └── WorkItemsController.cs     # updated (Feature 002 file): +GetTree, +GetParentCandidates actions;
│   │                                   #   Get/Create/Update/Delete gain new error-case catch blocks
│   ├── Services/
│   │   ├── WorkItemService.cs         # updated (Feature 002 file): parent validation, type-change guards,
│   │   │                              #   recursive descendant collection, tree-building, parent-candidates query
│   │   └── WorkItemExceptions.cs      # updated (Feature 002 file): + hierarchy-specific exception types
│   ├── Data/
│   │   ├── AppDbContext.cs            # updated: WorkItem self-referencing relationship config (Restrict), new index
│   │   ├── Entities/
│   │   │   └── WorkItem.cs            # updated (Feature 002 file): + ParentWorkItemId, ParentWorkItem, Children
│   │   └── Migrations/                # new: AddWorkItemHierarchy
│   └── Dtos/
│       ├── WorkItemDto.cs             # updated (Feature 002 file): + ParentWorkItemId
│       ├── WorkItemRequest.cs         # updated (Feature 002 file): + ParentWorkItemId
│       ├── WorkItemDetailDto.cs       # new (data-model.md)
│       ├── WorkItemChildDto.cs        # new (data-model.md)
│       ├── WorkItemTreeNodeDto.cs     # new (data-model.md)
│       └── WorkItemLookupItemDto.cs   # new (data-model.md)
└── TaskFlow.Api.Tests/
    ├── Services/
    │   └── WorkItemServiceTests.cs    # updated (Feature 002 file): + parent-validation, type-change-guard,
    │                                   #   cascade-delete, tree-building test cases
    └── Integration/
        └── WorkItemsEndpointsTests.cs # updated (Feature 002 file): + tree/parent-candidates endpoint tests,
                                        #   + hierarchy error-case tests on existing endpoints

frontend/
├── src/app/
│   ├── projects/
│   │   ├── project-detail/
│   │   │   ├── project-detail.component.ts    # updated (Feature 002 file): Tree/Flat toggle, tree rendering,
│   │   │   │                                  #   expand/collapse state, child-count display
│   │   │   └── project-detail.component.html  # updated
│   │   ├── work-item-form/
│   │   │   ├── work-item-form.component.ts    # updated (Feature 002 file): type-dependent parent picker
│   │   │   └── work-item-form.component.html  # updated
│   │   ├── work-item-detail/                  # new
│   │   │   ├── work-item-detail.component.ts
│   │   │   └── work-item-detail.component.html
│   │   └── work-items.service.ts              # updated (Feature 002 file): + getTree, getParentCandidates,
│   │                                          #   getWorkItemDetail; WorkItem interface + parentWorkItemId
│   └── app.routes.ts                          # updated: + projects/:projectId/work-items/:id (detail route)
└── (Vitest specs colocated with each unit above, per Angular convention)
```

**Structure Decision**: Continues Features 001/002's web application split
(frontend + backend) and single-Web-API-project layout — no new projects,
no new architectural layers, no new top-level directories. Every backend
change lands in the existing flat `Controllers/Services/Data/Dtos`
structure; every frontend change lands in the existing `projects/` feature
folder, plus one new sibling component (`work-item-detail/`) following the
same pattern as `project-form/`/`work-item-form/`.

## Complexity Tracking

*No Constitution Check violations — table intentionally omitted (see the accepted Principle VII deviation above, which is not a violation requiring justification here).*
