---

description: "Task list for Project Summary Dashboard & Activity Log"
---

# Tasks: Project Summary Dashboard & Activity Log

**Input**: Design documents from `/specs/009-summary-dashboard/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/summary-and-activity-api.md, quickstart.md

**Tests**: Included and REQUIRED — constitution Principle I (Test-First Development) is NON-NEGOTIABLE for this project. Every test task below must be written and confirmed failing before its paired implementation task.

**Organization**: Tasks are grouped by user story (spec.md's own priorities: US1 P1, US4 P1, US2 P2, US3 P2, US5 P3). Commit order follows plan.md's Constitution Check: Setup/Foundational → US1 → US4 → US2 → US3 → US5.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: Which user story this task belongs to (US1/US2/US3/US4/US5)
- File paths are exact and repository-relative

## Path Conventions

Existing two-project layout: `backend/TaskFlow.Api/` + `backend/TaskFlow.Api.Tests/` (C#/.NET), `frontend/src/app/` (Angular/TypeScript). No new top-level directories.

---

## Phase 1: Setup

**Purpose**: Schema foundation for the new Activity Log table

- [X] T001 Create `ActivityLogEntry` entity + `ActivityEventType` (`Created`, `FieldChanged`) and `ActivityField` (`Status`, `Priority`, `Assignee`, `Sprint`) enums in `backend/TaskFlow.Api/Data/Entities/ActivityLogEntry.cs` (data-model.md's field table; `WorkItemId` is a plain `int`, deliberately not a navigation property — research.md #1)
- [X] T002 Add `ActivityLogEntries` DbSet and entity configuration in `backend/TaskFlow.Api/Data/AppDbContext.cs`: `ProjectId` FK → `Project`, `Cascade` (research.md #2); `ActorUserId` FK → `User`, `Restrict` (research.md #3); **no** relationship configured for `WorkItemId` (research.md #1); indexes on `ProjectId` and `WorkItemId`; `EventType`/`Field` via `.HasConversion<string>()` (depends on T001)
- [X] T003 Generate the EF Core migration `AddActivityLog` (`dotnet ef migrations add AddActivityLog` from `backend/TaskFlow.Api/`) and commit the generated file under `backend/TaskFlow.Api/Data/Migrations/` (depends on T002)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Wire the new `ActivityLogService` dependency through `WorkItemService`/`WorkItemsController` before any user story adds behavior to either class, so no story collides with another over the same constructor signature

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T004 [P] Create `ActivityLogService` skeleton — constructor takes `AppDbContext`; stub `RecordCreated`/`RecordFieldChange` methods that only call `dbContext.ActivityLogEntries.Add(...)`, no `SaveChangesAsync()` of their own (research.md #6) — in `backend/TaskFlow.Api/Services/ActivityLogService.cs` (depends on T001)
- [X] T005 [P] Register `ActivityLogService` in the DI container (`builder.Services.AddScoped<ActivityLogService>();`) in `backend/TaskFlow.Api/Program.cs` (depends on T004)
- [X] T006 Add an `ActivityLogService` constructor dependency to `WorkItemService` (primary constructor, no behavior change yet) in `backend/TaskFlow.Api/Services/WorkItemService.cs` (depends on T004)
- [X] T007 Add an `ActivityLogService` constructor dependency to `WorkItemsController` (no new routes yet) in `backend/TaskFlow.Api/Controllers/WorkItemsController.cs` (depends on T004)
- [X] T008 Update `WorkItemServiceTests.CreateSut()` to construct `WorkItemService` with a `new ActivityLogService(Db)` in `backend/TaskFlow.Api.Tests/Services/WorkItemServiceTests.cs` (depends on T006) — this is the only test call site in the repo constructing `WorkItemService` directly

**Checkpoint**: Schema exists; both services compile against the new dependency; full solution build is green; no observable behavior has changed yet.

---

## Phase 3: User Story 1 - Land on a project's Summary with live stat cards (Priority: P1) 🎯 MVP

**Goal**: Opening a project shows its Summary tab by default, with correct live stat cards (Total, Completed count/%, In Progress, Due Soon).

**Independent Test**: Open a project with a known mix of items; confirm Summary is shown with no `?view=` in the URL and the four stat cards are numerically correct; confirm an explicit `?view=board` link still shows Board.

### Tests for User Story 1 ⚠️

> Write these first; confirm they FAIL before implementing.

- [X] T009 [P] [US1] `WorkItemServiceTests`: `GetSummaryAsync` — stat cards (total/completed/percent/inProgress/dueSoon, including the exactly-7-days-out boundary, a zero-item project avoiding divide-by-zero, and an Epic among the items still counting toward `Total`/its category bucket per FR-006 — Epics are not filtered out), status breakdown (every project column present, including zero-count ones), priority breakdown (all 4 levels present, including zero-count ones), workload (assignee counts, zero-load Manager/Admin inclusion, Unassigned row, descending sort) in `backend/TaskFlow.Api.Tests/Services/WorkItemServiceTests.cs`
- [X] T010 [P] [US1] `WorkItemsEndpointsTests`: `GET api/projects/{projectId}/summary` returns 200 with the correct shape for a seeded project and 404 for an unknown project in `backend/TaskFlow.Api.Tests/Integration/WorkItemsEndpointsTests.cs`
- [X] T011 [P] [US1] `project-detail.component.spec.ts`: default `viewMode` is `'summary'` when no `?view=` query param is present; an explicit `?view=board`/`backlog`/`flat`/`tree` still wins in `frontend/src/app/projects/project-detail/project-detail.component.spec.ts`
- [X] T012 [P] [US1] `summary.component.spec.ts`: fetches `getProjectSummary()` on init and renders the four stat cards with the returned values in `frontend/src/app/projects/summary/summary.component.spec.ts`

### Implementation for User Story 1

- [X] T013 [P] [US1] Create `ProjectSummaryDto`, `StatCardsDto`, `StatusBreakdownItemDto`, `PriorityBreakdownItemDto`, `WorkloadRowDto` records in `backend/TaskFlow.Api/Dtos/ProjectSummaryDto.cs`
- [X] T014 [US1] Implement `WorkItemService.GetSummaryAsync(int projectId)` — full computation per data-model.md: stat cards, status breakdown (project's `WorkflowStatuses` ordered by `Position` + live counts), priority breakdown (all 4 `WorkItemPriority` members), workload (open-item counts per assignee merged with every system Manager/Admin, plus a synthetic Unassigned row — research.md #10) in `backend/TaskFlow.Api/Services/WorkItemService.cs` (depends on T013; makes T009 pass)
- [X] T015 [US1] Add `GET api/projects/{projectId}/summary` route (404 on `ProjectNotFoundException`) in `backend/TaskFlow.Api/Controllers/WorkItemsController.cs` (depends on T014; makes T010 pass)
- [X] T016 [P] [US1] Add `ProjectSummary`/`StatCards`/`StatusBreakdownItem`/`PriorityBreakdownItem`/`WorkloadRow` TypeScript interfaces and a `getProjectSummary(projectId)` method in `frontend/src/app/projects/work-items.service.ts`
- [X] T017 [US1] Create `SummaryComponent` — fetches `getProjectSummary()` on init, renders only the four stat cards for now (donut/bar/workload sections are added in US2/US3) in `frontend/src/app/projects/summary/summary.component.ts` / `.html` / `.css` (depends on T016; makes T012 pass)
- [X] T018 [US1] Add `'summary'` as the fifth view mode in `ProjectDetailComponent` — default when no `?view=` query param, tab order **Summary | Board | Backlog | List | Tree**, `<app-summary>` embed in `frontend/src/app/projects/project-detail/project-detail.component.ts` / `.html` (depends on T017; makes T011 pass)

**Checkpoint**: Summary is the default tab; stat cards show correct, live data. Independently demoable.

---

## Phase 4: User Story 4 - Activity log captures changes and feeds the project Summary (Priority: P1)

**Goal**: Work item creation and Status/Priority/Assignee/Sprint changes automatically write an activity entry, visible in the project's Summary feed.

**Independent Test**: Change a work item's Status via the existing edit flow; confirm a new entry is written in the same transaction as the change and appears newest-first in the project feed as a readable, relative-timestamped sentence — without needing US1's stat cards to exist (though the feed's UI home is the `SummaryComponent` US1 already built).

### Tests for User Story 4 ⚠️

> Write these first; confirm they FAIL before implementing.

- [X] T019 [P] [US4] `ActivityLogServiceTests` (new file): `RecordCreated`/`RecordFieldChange` persist correct rows; `GetProjectFeedAsync` pagination/newest-first ordering/project-scoping; `GetWorkItemHistoryAsync` per-item filtering; entries keep their `WorkItemTitle`/`WorkItemType` snapshot readable after the referenced `WorkItem` row is deleted in `backend/TaskFlow.Api.Tests/Services/ActivityLogServiceTests.cs`
- [X] T020 [P] [US4] `WorkItemServiceTests`: `CreateAsync` writes one `Created` entry; `UpdateAsync` writes one `FieldChanged` entry per actually-changed tracked field (Status/Priority/Assignee/Sprint) with correct display names, and none when only untracked fields change or when a "changed" field's resolved value is unchanged; `UpdateStatusAsync`/`UpdateSprintAsync` each write 0-or-1 entry correctly in `backend/TaskFlow.Api.Tests/Services/WorkItemServiceTests.cs`
- [X] T021 [P] [US4] `WorkItemsEndpointsTests`: `GET api/projects/{projectId}/activity` (paginated, newest-first, 404 unknown project) and `GET api/work-items/{id}/activity` (filtered to one item, 404 unknown item) in `backend/TaskFlow.Api.Tests/Integration/WorkItemsEndpointsTests.cs`
- [X] T022 [P] [US4] `build-activity-sentence.spec.ts`: correct sentence for `Created` and for each `FieldChanged` field, including Sprint's null-to-"Backlog" display case in `frontend/src/app/projects/activity-feed/build-activity-sentence.spec.ts`
- [X] T023 [P] [US4] `relative-time.pipe.spec.ts`: "just now" / "N minutes ago" / "N hours ago" / "N days ago" / falls back to `friendlyDate` beyond ~7 days in `frontend/src/app/shared/relative-time.pipe.spec.ts`
- [X] T024 [P] [US4] `activity-feed.component.spec.ts`: renders a list of entries via `buildActivitySentence()` + `RelativeTimePipe`, newest-first in `frontend/src/app/projects/activity-feed/activity-feed.component.spec.ts`

### Implementation for User Story 4

- [X] T025 [P] [US4] Add `ActivityEntryDto` record in `backend/TaskFlow.Api/Dtos/ActivityEntryDto.cs`
- [X] T026 [US4] Implement `ActivityLogService.RecordCreated`/`RecordFieldChange`/`GetProjectFeedAsync`/`GetWorkItemHistoryAsync` in `backend/TaskFlow.Api/Services/ActivityLogService.cs` (depends on T025; makes T019 pass)
- [X] T027 [US4] Wire `WorkItemService.CreateAsync`: wrap in one explicit `IDbContextTransaction`, save the work item first (assigning its real `Id`), then call `RecordCreated` and save again (research.md #6) in `backend/TaskFlow.Api/Services/WorkItemService.cs` (depends on T026)
- [X] T028 [US4] Wire `WorkItemService.UpdateAsync`: fetch old display values (status name, assignee name, sprint name) before mutation, call `RecordFieldChange` once per actually-changed tracked field (research.md #5, #7) in `backend/TaskFlow.Api/Services/WorkItemService.cs` (depends on T026)
- [X] T029 [US4] Wire `WorkItemService.UpdateStatusAsync`: fetch old status name before mutation, call `RecordFieldChange` only when it changed in `backend/TaskFlow.Api/Services/WorkItemService.cs` (depends on T026)
- [X] T030 [US4] Wire `WorkItemService.UpdateSprintAsync`: fetch old sprint name ("Backlog" when null) before mutation, call `RecordFieldChange` only when it changed in `backend/TaskFlow.Api/Services/WorkItemService.cs` (depends on T026; T027-T030 together make T020 pass)
- [X] T031 [US4] Add `GET api/projects/{projectId}/activity` and `GET api/work-items/{id}/activity` routes in `backend/TaskFlow.Api/Controllers/WorkItemsController.cs` (depends on T026; makes T021 pass)
- [X] T032 [P] [US4] Add `ActivityEntry` TypeScript interface and `getProjectActivity(projectId, page, pageSize)` / `getWorkItemActivity(workItemId)` methods in `frontend/src/app/projects/work-items.service.ts`
- [X] T033 [P] [US4] Create `buildActivitySentence()` pure function in `frontend/src/app/projects/activity-feed/build-activity-sentence.ts` (makes T022 pass)
- [X] T034 [P] [US4] Create `RelativeTimePipe` in `frontend/src/app/shared/relative-time.pipe.ts` (makes T023 pass)
- [X] T035 [US4] Create `ActivityFeedComponent` rendering entries via `buildActivitySentence()` + `RelativeTimePipe` in `frontend/src/app/projects/activity-feed/activity-feed.component.ts` / `.html` / `.css` (depends on T033, T034; makes T024 pass)
- [X] T036 [US4] Wire the paginated project activity feed ("load more") into `SummaryComponent` via `<app-activity-feed>` in `frontend/src/app/projects/summary/summary.component.ts` / `.html` (depends on T035, T032, and T017 from US1)

**Checkpoint**: Activity entries are recorded automatically by existing flows and visible in the project's Summary feed. Independently demoable via direct API calls even without US1's UI.

---

## Phase 5: User Story 2 - See status and priority breakdowns (Priority: P2)

**Goal**: The Summary tab shows a status donut (project's own columns/colors) and a priority bar chart (all 4 levels, zero-count included).

**Independent Test**: Open a project with custom workflow columns and confirm the donut shows exactly those columns, in order, in their configured colors; confirm the priority bar chart always shows all 4 levels.

**Note**: `GetSummaryAsync` already computes `statusBreakdown`/`priorityBreakdown` correctly as of US1 (T014) — this story is frontend-only.

### Tests for User Story 2 ⚠️

- [X] T037 [P] [US2] `donut-segments.spec.ts`: segment percentages always sum to 100%, a zero-item project produces an empty/neutral state, a single-status project produces one full segment in `frontend/src/app/projects/summary/donut-segments.spec.ts`
- [X] T038 [P] [US2] `status-donut-chart.component.spec.ts`: segments match the project's own column names, order, and colors in `frontend/src/app/projects/summary/status-donut-chart.component.spec.ts`
- [X] T039 [P] [US2] `priority-bar-chart.component.spec.ts`: always renders all 4 priority levels, including any at zero count in `frontend/src/app/projects/summary/priority-bar-chart.component.spec.ts`

### Implementation for User Story 2

- [X] T040 [P] [US2] Create `donutSegments()` pure function (turns `{count, colorVar}[]` into `{colorVar, dashArray, dashOffset}[]`) in `frontend/src/app/projects/summary/donut-segments.ts` (makes T037 pass)
- [X] T041 [US2] Create `StatusDonutChartComponent` — stacked SVG `<circle>` arcs via `donutSegments()`, each colored by its status's `--color-chip-{key}-text` token (research.md #14) in `frontend/src/app/projects/summary/status-donut-chart.component.ts` / `.html` / `.css` (depends on T040; makes T038 pass)
- [X] T042 [US2] Create `PriorityBarChartComponent` — flexbox bars with `width: {percent}%`, each colored by its level's `--color-priority-{level}-text` token in `frontend/src/app/projects/summary/priority-bar-chart.component.ts` / `.html` / `.css` (makes T039 pass)
- [X] T043 [US2] Wire both charts into `SummaryComponent`'s template in `frontend/src/app/projects/summary/summary.component.html` (depends on T041, T042)

**Checkpoint**: Status/priority breakdowns render correctly per project.

---

## Phase 6: User Story 3 - See team workload (Priority: P2)

**Goal**: The Summary tab shows a team workload panel — assignees plus zero-load Managers/Admins plus an Unassigned row, sorted by open-item count descending.

**Independent Test**: In a project with items split across two assignees, one unassigned open item, and a zero-load Manager, confirm all rows appear correctly sorted.

**Note**: `GetSummaryAsync` already computes `workload` correctly as of US1 (T014) — this story is frontend-only.

### Tests for User Story 3 ⚠️

- [X] T044 [P] [US3] `team-workload.component.spec.ts`: rows sorted by count descending, an Unassigned row appears only when applicable, a zero-load Manager/Admin row still renders in `frontend/src/app/projects/summary/team-workload.component.spec.ts`

### Implementation for User Story 3

- [X] T045 [US3] Create `TeamWorkloadComponent` — row list with a proportion bar per row, reusing `UserAvatarComponent` in `frontend/src/app/projects/summary/team-workload.component.ts` / `.html` / `.css` (makes T044 pass)
- [X] T046 [US3] Wire `TeamWorkloadComponent` into `SummaryComponent`'s template in `frontend/src/app/projects/summary/summary.component.html` (depends on T045)

**Checkpoint**: Team workload panel renders correctly, sorted, with Unassigned/zero-load rows as applicable.

---

## Phase 7: User Story 5 - See a work item's own activity history (Priority: P3)

**Goal**: A work item's detail page shows its own activity history, filtered from the same log, rendered identically to the project feed.

**Independent Test**: Make two tracked changes to one item; confirm its detail page shows exactly those two entries, same sentence format and ordering as the project feed.

### Tests for User Story 5 ⚠️

- [ ] T047 [P] [US5] `work-item-detail.component.spec.ts`: fetches `getWorkItemActivity()` and renders it via `<app-activity-feed>`, filtered to just that item's entries in `frontend/src/app/projects/work-item-detail/work-item-detail.component.spec.ts`

### Implementation for User Story 5

- [ ] T048 [US5] Wire `WorkItemDetailComponent` to fetch `getWorkItemActivity()` and render `<app-activity-feed>` in a new Activity section in `frontend/src/app/projects/work-item-detail/work-item-detail.component.ts` / `.html` (depends on T035 from US4; makes T047 pass)

**Checkpoint**: All five user stories are independently functional and demoable.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [ ] T049 [P] Run the full backend suite (`dotnet test` from `backend/`) and confirm all pre-existing tests plus every new test above pass
- [ ] T050 [P] Run the full frontend suite (`npm test` from `frontend/`) and confirm all pre-existing tests plus every new test above pass
- [ ] T051 Walk through every scenario in `quickstart.md` end-to-end against a running instance
- [ ] T052 Add a "What I learned" entry for this feature to `README.md` (constitution Definition of Done, Principle VIII)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately.
- **Foundational (Phase 2)**: Depends on Setup (T001) — BLOCKS all user stories.
- **US1 (Phase 3)**: Depends on Foundational completion. No dependency on any other story.
- **US4 (Phase 4)**: Depends on Foundational completion. Backend tasks (T019-T031) have no dependency on US1; the frontend feed-wiring task (T036) depends on US1's `SummaryComponent` (T017) already existing.
- **US2 (Phase 5)**: Depends on US1's `GetSummaryAsync` (T014) already returning correct `statusBreakdown`/`priorityBreakdown`, and US1's `SummaryComponent` (T017) existing to embed the charts into.
- **US3 (Phase 6)**: Depends on US1's `GetSummaryAsync` (T014) already returning correct `workload`, and US1's `SummaryComponent` (T017).
- **US5 (Phase 7)**: Depends on US4's `ActivityFeedComponent` (T035) and `getWorkItemActivity()` (T032).
- **Polish (Phase 8)**: Depends on all desired user stories being complete.

### Within Each User Story

- Tests are written and confirmed failing before their paired implementation task (constitution Principle I).
- DTOs/entities before service methods; service methods before controller routes; backend routes before the frontend service methods that call them; pure functions before the components that use them.
- Story complete (checkpoint) before moving to the next priority.

### Parallel Opportunities

- T004/T005 (Foundational) can run in parallel with each other, sequentially before T006-T008.
- Within each story's Tests block, every `[P]`-marked task targets a different file and can run in parallel.
- T013/T016 (US1), T025/T032/T033/T034 (US4), T040 (US2) are `[P]` — different files from their neighbors.
- US2 and US3 touch different new files from each other (only `summary.component.html` is a shared edit point, done in each story's final wiring task) — a second developer could start US3 immediately after US1's checkpoint without waiting for US2.

---

## Parallel Example: User Story 1

```bash
# Tests (different files, run together):
Task: "WorkItemServiceTests: GetSummaryAsync stat cards/breakdowns/workload in backend/TaskFlow.Api.Tests/Services/WorkItemServiceTests.cs"
Task: "WorkItemsEndpointsTests: GET .../summary in backend/TaskFlow.Api.Tests/Integration/WorkItemsEndpointsTests.cs"
Task: "project-detail.component.spec.ts: default view mode in frontend/src/app/projects/project-detail/project-detail.component.spec.ts"
Task: "summary.component.spec.ts: stat card rendering in frontend/src/app/projects/summary/summary.component.spec.ts"

# Implementation (independent files, run together):
Task: "ProjectSummaryDto + nested records in backend/TaskFlow.Api/Dtos/ProjectSummaryDto.cs"
Task: "ProjectSummary TS interfaces + getProjectSummary() in frontend/src/app/projects/work-items.service.ts"
```

---

## Implementation Strategy

### MVP First (User Stories 1 + 4 — both P1)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (blocks everything)
3. Complete Phase 3: US1 — **STOP and VALIDATE**: Summary tab defaults correctly, stat cards are accurate
4. Complete Phase 4: US4 — **STOP and VALIDATE**: activity entries appear automatically in the feed
5. Together, US1 + US4 are the feature's stated MVP (spec.md gives both P1 — "at a glance" and "recent activity" are equally the feature's core promise)

### Incremental Delivery

1. Setup + Foundational → foundation ready
2. US1 → stat cards + default tab → demo
3. US4 → activity log + feed → demo (MVP complete)
4. US2 → charts → demo
5. US3 → workload → demo
6. US5 → item detail history → demo
7. Polish → full regression + quickstart walkthrough

### Parallel Team Strategy

With two developers after Foundational: Developer A takes US1 → US4 (the log write-path and its own feed UI are naturally sequential); Developer B can start US2/US3 as soon as US1's `GetSummaryAsync` (T014) and `SummaryComponent` (T017) checkpoint lands, since both are frontend-only additions to an already-correct backend response.

---

## Notes

- `[P]` tasks touch different files with no unmet dependency.
- `[Story]` labels map every user-story-phase task back to spec.md for traceability.
- Every test task must fail before its paired implementation task is written (constitution Principle I, NON-NEGOTIABLE).
- Commit after each user story phase (or logical group within a large phase like US4), per constitution's Commit Convention — e.g. `feat: US1 Summary tab stat cards (NNN/NNN backend, NNN/NNN frontend tests pass)`.
- Avoid: vague tasks, two tasks editing the same file marked `[P]`, cross-story dependencies that would break a story's independent testability.
