# Implementation Plan: Kanban Board

**Branch**: `005-kanban-board` | **Date**: 2026-07-18 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/005-kanban-board/spec.md`

## Summary

Add a fourth work item status, **In Review** (To Do → In Progress → In
Review → Done — a pure C# enum addition, zero schema migration since Status
is already a string-converted column with no value constraint), and a new
**Board** view on the project detail page: drag-and-drop columns backed by
two new backend endpoints (`GET .../work-items/board` for the ordered
column list + card data, `PATCH .../work-items/{id}/status` for a safe,
field-scoped status change) and a new Angular CDK `drag-drop`-based
`BoardComponent`/`BoardCardComponent` pair built entirely from Feature 004's
existing design system (chips, avatars, tokens) — no new ad-hoc styling.

## Technical Context

**Language/Version**: C# 13 / .NET 10 (backend); TypeScript ~6.0.2 / Angular
22 (frontend), strict mode

**Primary Dependencies**: ASP.NET Core Web API, EF Core 10 (SQL Server
provider) — all existing, no new backend package. Angular CDK's
`@angular/cdk/drag-drop` (`DragDropModule`, `cdkDropList`, `cdkDrag`) —
already a project dependency (`@angular/cdk`, used for `BreakpointObserver`
in Feature 004) but its drag-drop module is a first-time consumer in this
feature; no new package needed.

**Storage**: SQL Server, via the existing `WorkItem` table — no new table,
no new column, no migration (see research.md #1). One enum member added to
`WorkItemStatus` in `backend/TaskFlow.Api/Data/Entities/WorkItem.cs`.

**Testing**: xUnit + real SQL Server test databases (backend, existing
pattern — `backend/TaskFlow.Api.Tests/TestSupport/SqlServerTestDatabase.cs`);
Vitest (frontend, existing pattern)

**Target Platform**: Browser (Angular SPA) + ASP.NET Core Web API, same as
all prior features

**Project Type**: Web application — this feature touches both `backend/`
and `frontend/` (unlike Feature 004, which was frontend-only)

**Performance Goals**: Board endpoint returns a project's full work item
set (up to 200 items, per FR-020) in one unpaginated response, in one query
pass plus one in-memory grouping pass — same order of work the existing
`GetTreeAsync` already does at this scale; no new performance goal beyond
matching that established pattern.

**Constraints**: No breaking changes to any existing endpoint or DTO (both
new endpoints are additive); the existing creator/assignee/Manager/Admin
edit-permission rule must be reused verbatim for status changes, not
reimplemented; drag-and-drop must not be the *only* way to change status
(FR-016 — the edit form remains a fully accessible fallback).

**Scale/Scope**: 2 new backend endpoints, 1 new DTO, 1 enum member, ~4-5 new
frontend files (board component, card component, permission helper, overdue
helper, CDK wiring) plus small edits to ~6 existing files (status arrays,
chip label map, design tokens, project-detail's view toggle). Comparable in
size to Feature 004; see Complexity Tracking.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Test-First Development**: PASS (with plan). Backend: service-level
  tests for the board query's child-count computation and ordering, and
  endpoint tests for both new routes (200/403/404), written before the
  implementation, following the same "Confirmed RED"/"Confirmed GREEN"
  discipline Feature 003 already established. Frontend: pure logic
  (`isOverdue()`, the shared `canEditWorkItem()` permission helper, and
  column-grouping) gets Vitest specs first; the drag-drop interaction
  itself is verified via component tests plus the manual quickstart
  walkthrough (CDK drag gestures are impractical to fully simulate in
  jsdom, same class of limitation as Feature 004's tablet-breakpoint
  resize check).
- **II. Secure by Default**: PASS. The new `PATCH .../work-items/{id}/status`
  endpoint reuses the *exact same* authorization rule already enforced by
  `WorkItemService.UpdateAsync` (creator, current assignee, Manager, or
  Admin) — factored into one shared private method both call, not
  reimplemented. The board's client-side drag-permission check (disabling
  drag for items the user can't edit) is explicitly documented as a UX
  convenience only; FR-015 requires the server to refuse an unauthorized
  status change regardless of what the UI allowed, and this plan tests
  that directly (an endpoint test calling the PATCH route as an
  unauthorized user, independent of any UI).
- **III. Clarity Over Cleverness**: PASS. `@angular/cdk/drag-drop` is the
  Angular ecosystem's built-in tool for exactly this interaction — not a
  new third-party dependency, not a hand-rolled drag implementation. The
  new PATCH endpoint is justified by data safety, not cleverness: reusing
  the existing full-record `PUT` for a drag would require the frontend to
  carry every field (including `description`/`parentWorkItemId`, which the
  board's card data doesn't need) just to avoid silently clobbering them on
  save — a field-scoped endpoint avoids that risk entirely. Extracting
  `canEditWorkItem()` into one shared function is a justified consolidation
  (this is its third call site: project-detail, work-item-detail, and now
  the board — see research.md #5), not premature abstraction.
- **IV. Consistent Code Quality & Review Gates**: PASS. New DTOs are
  records, mapped manually (no AutoMapper, matching existing convention);
  RESTful plural-noun routes matching `WorkItemsController`'s existing
  style; async I/O throughout; Angular strict mode, signals, no `any`; new
  `WorkItemStatus` TS union gets a 4th literal, and `StatusChipComponent`'s
  *exhaustive* switch means the compiler itself forces the new case to be
  added — a concrete, compiler-enforced check that FR-004's "every status
  surface handles In Review" requirement can't be silently missed for
  chips specifically.
- **V. API Contract Stability & Versioning**: PASS. Both new endpoints are
  purely additive (new routes, no changes to any existing route's request
  or response shape); the `InReview` enum member is additive to an
  already-unconstrained string column. No migration, no version bump,
  documented in `contracts/board-api.md`.
- **VI. Teach While Building**: Concepts you will learn in this feature —
  `PATCH` vs. `PUT` semantics and why a field-scoped update earns its own
  endpoint; reusing an in-memory `Dictionary`/`GroupBy` shape (already seen
  in `GetTreeAsync`) to compute per-item child-progress counts across a
  flat, non-hierarchical query; how a string-converted EF Core enum column
  means a new enum member is a zero-migration change; Angular CDK's
  `cdkDropListGroup`/`cdkDropList`/`cdkDrag` connected-list drag model.
- **VII. Incremental, Feature-by-Feature Delivery**: CONDITIONAL PASS —
  see Complexity Tracking. This feature spans both `backend/` and
  `frontend/` and touches several existing files (status arrays, chip
  labels, two components' permission logic) in addition to new ones,
  likely exceeding the ~15-file guideline; each individual change is small
  and mechanical.
- **VIII. Human in the Loop**: PASS — no auto-chaining; no
  `[NEEDS CLARIFICATION]` markers remain in the spec.

## Project Structure

### Documentation (this feature)

```text
specs/005-kanban-board/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/
│   └── board-api.md     # Phase 1 output (/speckit-plan command)
├── checklists/
│   └── requirements.md
├── visual-reference.png
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
backend/
├── TaskFlow.Api/
│   ├── Data/
│   │   └── Entities/
│   │       └── WorkItem.cs                  # MODIFIED — add InReview to WorkItemStatus enum
│   ├── Dtos/
│   │   ├── WorkItemBoardDto.cs              # NEW — { Columns: string[], Items: WorkItemBoardCardDto[] }
│   │   ├── WorkItemBoardCardDto.cs          # NEW — board card fields incl. child-progress counts
│   │   └── UpdateWorkItemStatusRequest.cs   # NEW — { Status: string }
│   ├── Services/
│   │   └── WorkItemService.cs               # MODIFIED — GetBoardAsync(), UpdateStatusAsync(),
│   │                                          shared EnsureCanEdit() extracted from UpdateAsync
│   └── Controllers/
│       └── WorkItemsController.cs           # MODIFIED — GET .../work-items/board,
│                                              PATCH api/work-items/{id}/status
└── TaskFlow.Api.Tests/
    ├── Services/WorkItemServiceTests.cs     # MODIFIED — GetBoardAsync, UpdateStatusAsync cases
    └── Integration/WorkItemsEndpointsTests.cs  # MODIFIED — new routes, 200/403/404 cases

frontend/
├── src/
│   ├── design-tokens.scss                   # MODIFIED — --color-status-inreview-{bg,text}
│   └── app/
│       ├── projects/
│       │   ├── work-items.service.ts        # MODIFIED — WorkItemStatus union +'InReview',
│       │   │                                  WorkItemBoard types, getBoard(), updateStatus()
│       │   ├── work-item-permissions.ts     # NEW — shared canEditWorkItem() pure function
│       │   ├── work-item-permissions.spec.ts  # NEW
│       │   ├── work-item-form/
│       │   │   └── work-item-form.component.ts/.html  # MODIFIED — STATUSES += 'InReview'
│       │   ├── project-detail/
│       │   │   ├── project-detail.component.ts/.html  # MODIFIED — viewMode 'board' option,
│       │   │   │                                        STATUSES += 'InReview', use shared
│       │   │   │                                        canEditWorkItem() instead of private copy
│       │   │   └── project-detail.component.spec.ts   # MODIFIED — board toggle tests
│       │   ├── work-item-detail/
│       │   │   └── work-item-detail.component.ts       # MODIFIED — use shared canEditWorkItem()
│       │   └── board/
│       │       ├── board.component.ts/.html/.css       # NEW — columns, CDK drag-drop wiring,
│       │       │                                          optimistic move + revert
│       │       ├── board.component.spec.ts              # NEW
│       │       ├── board-card.component.ts/.html/.css   # NEW — card content (title, type,
│       │       │                                          priority chip, avatar/unassigned,
│       │       │                                          friendly+overdue due date, n/m done)
│       │       ├── board-card.component.spec.ts         # NEW
│       │       ├── overdue.ts                            # NEW — isOverdue() pure function
│       │       └── overdue.spec.ts                       # NEW
│       └── shared/
│           └── status-chip/
│               └── status-chip.component.ts             # MODIFIED — InReview label + color case
```

**Structure Decision**: Existing two-project layout (`backend/TaskFlow.Api`
+ `frontend/`), unchanged. New backend logic lives in the existing
`WorkItemService`/`WorkItemsController` (no new controller — board and
status-update are work-item concerns, matching where tree/parent-candidates
already live). New frontend pieces live in a new `frontend/src/app/projects/
board/` directory alongside the existing `project-detail/`,
`work-item-detail/`, etc. siblings — consistent with the flat,
feature-grouped layout already used under `projects/`. `work-item-
permissions.ts` is placed directly under `projects/` (not `shared/`)
because it is specific to work-item edit semantics, not a general design-
system piece.

## Complexity Tracking

> Fill ONLY if Constitution Check has violations that must be justified

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|---------------------------------------|
| Changed/new file count likely exceeds the ~15-file guideline in Principle VII, and spans both `backend/` and `frontend/` in one feature | The board is not meaningfully shippable without both halves: a board with no backend-supplied column list and no safe status-update path is not the feature described (FR-006, FR-013-015), and In Review must exist everywhere status already appears (FR-004) before the board can show four real columns. Splitting into "backend In Review + status endpoint" and "frontend board" across two separate features/branches was considered, but the first half alone ships no user-visible value and the second half cannot be built or tested without the first — they are one coherent, sequentially-dependent unit of work, delivered here as separate commits per user story (Setup/Foundational → US1 In Review → US2 View board → US3 Drag → US4/US5), not one undifferentiated diff. |
