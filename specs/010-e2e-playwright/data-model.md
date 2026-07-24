# Phase 1 Data Model: E2E Testing Foundation (Playwright)

This feature adds no application data model — no new database tables,
entities, or API shapes. What follows is the closest equivalent for a test
suite: the fixtures the suite provisions before tests run, and the Page
Object structure the 7 spec files are built on, both derived directly from
the spec's Key Entities section and Requirements.

## Fixture Entities

### Seeded Test Accounts

Three accounts must exist before any journey runs, per FR-007 and the
spec's Key Entities section.

| Field | Admin | Manager | Developer |
|---|---|---|---|
| Provisioning | Existing `AdminSeeder`, config-based (`Admin:Email` / `Admin:Password` user-secrets, unchanged) | `global-setup.ts`: register + promote via `PUT /api/users/{id}/role` (Decision 3, research.md) | `global-setup.ts`: register only, stays at the app's default role |
| Role at creation | `Admin` | `Developer` → promoted to `Manager` | `Developer` |
| Used by | Permission-boundary (as the promoter), navigation, role-change (as the actor changing another user's role) | Project/work-item, board drag, sprint journeys | Auth, permission-boundary, navigation, board drag (unauthorized-drag case), role-change (as the subject) |
| Identity stability | Fixed, config-driven | Fixed test email (e.g. `e2e-manager@taskflow.local`), re-provisioned idempotently each run (409-tolerant, see Decision 3) | Fixed test email (e.g. `e2e-developer@taskflow.local`), same idempotency |

Credentials are test-only literals scoped to the throwaway `TaskFlowDb_E2E`
database — never committed as production secrets, consistent with how
`TaskFlowApiFactory` in the existing xUnit suite already hardcodes its own
disposable test-database Admin credentials.

### Test Database

A single conceptual entity representing the dedicated environment (FR-003,
FR-004):

- **Name**: `TaskFlowDb_E2E` (convention; actual value comes from the
  connection string the backend is started with).
- **Schema source**: existing EF Core migrations, applied via
  `dotnet ef database update` (Decision 2, research.md) — never
  hand-created or `EnsureCreated`.
- **Reset boundary**: once per full local suite run, before the backend
  starts (documented prerequisite step, not run by the suite itself).
- **Journey-created data**: projects, work items, sprints, and any role
  changes made during a run are scoped to uniquely-named entities per
  journey (Decision 4) rather than relying on a full reset between tests.

## Page Object Model (`frontend/e2e/pages/`)

One class per page or reusable component, giving FR-008's resilient
selectors a single place to live per surface, and giving the 7 spec files
a shared, readable vocabulary. Each maps to the existing Angular component
identified during research:

| Page Object | Wraps (existing component) | Used by journeys |
|---|---|---|
| `login.page.ts` | `frontend/src/app/auth/login/login.component.ts` | Auth, Navigation, Board Drag, Sprint, Role Change, Permission-boundary |
| `register.page.ts` | `frontend/src/app/auth/register/register.component.ts` | Auth |
| `sidebar-nav.page.ts` | `frontend/src/app/shared/sidebar-nav/sidebar-nav.component.ts` | Navigation, Permission-boundary |
| `project-detail.page.ts` | `frontend/src/app/projects/project-detail/project-detail.component.ts` (covers `flat`/`tree`/`board`/`backlog` `?view=` modes) | Project/Work Item, Board Drag, Sprint |
| `work-item-modal.page.ts` | Work item create/edit modal (Feature 007) | Project/Work Item |
| `sprint-dialogs.page.ts` | `sprint-form.component.ts`, `complete-sprint-dialog.component.ts` | Sprint |
| `users.page.ts` | `frontend/src/app/users/...` (`UsersListComponent`) | Role Change, Permission-boundary |

Page Objects expose intention-revealing methods (e.g.
`projectDetailPage.dragCardToColumn(workItemId, columnName)`) built on the
selector priority order from Decision 5 (research.md) — they do not expose
raw locators to spec files, keeping each of the 7 spec files readable as a
sequence of user actions and assertions, matching the acceptance scenarios
in `spec.md` almost line-for-line.

## Relationships

```text
global-setup.ts
  └─ provisions ─> Seeded Test Accounts (Admin / Manager / Developer)
                       └─ used to authenticate ─> Page Objects (via auth.fixture.ts)
                                                       └─ drive ─> Angular components
                                                                      └─ call ─> existing API endpoints (contracts/api-usage.md)
                                                                                     └─ persist to ─> Test Database
```

No new persistent relationships are introduced; this diagram describes
test-run-time data flow only.
