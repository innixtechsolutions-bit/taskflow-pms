# Design System Component Contracts

This feature has no new backend API surface. Its external interface is the
**public API of the new shared Angular components/services/pipe** — this is
what future features (Kanban, timesheets, dashboard, reports — per the spec's
"Why") are expected to import and consume, so it is documented here as a
contract in the same spirit as an API contract: a promise about inputs,
outputs, and behavior that should not break without a plan-level note (per
Constitution Principle V's spirit, applied to the frontend).

All components are standalone, importable individually (no barrel-module
requirement), and live under `frontend/src/app/shared/`.

---

## `<app-shell>` — `shared/shell/shell.component.ts`

Top-level layout wrapper. Rendered once in `app.html` for authenticated
routes only; never rendered on `/login` or `/register`.

- **Inputs**: none — reads the current user from `AuthService` internally.
- **Outputs**: none.
- **Content projection**: `<router-outlet>` (or equivalent) renders inside its
  content area.
- **Behavior**: renders `<app-sidebar-nav>` + a content region using the
  `--content-max-width` token; collapses the sidebar to icon rail below
  `--breakpoint-tablet` (delegated to `SidebarNavComponent`).

## `<app-sidebar-nav>` — `shared/sidebar-nav/sidebar-nav.component.ts`

- **Inputs**: none — reads nav item config internally, filters by
  `authService.currentRole()`.
- **Outputs**: `logout` — emitted when the user confirms logout from the
  bottom user menu (parent, or the component itself, calls
  `authService.logout()` and navigates to `/login`).
- **Behavior**: highlights the nav item matching the current route (via
  `Router.url` / `routerLinkActive`); below the tablet breakpoint, renders
  icon-only items with a tooltip carrying the label (`matTooltip` or
  equivalent) instead of removing the label from the accessibility tree.

## `<app-page-header>` — `shared/page-header/page-header.component.ts`

- **Inputs**:
  - `title: string` (required)
  - `subtitle?: string` (optional)
- **Content projection**: an `actions` slot (e.g. `<ng-content
  select="[page-header-actions]">`) for an optional right-aligned primary
  action button — pages without an action simply project nothing.
- **Outputs**: none.

## `<app-status-chip>` — `shared/status-chip/status-chip.component.ts`

- **Inputs**: `status: WorkItemStatus` (required; see data-model.md) — a
  compile-time union, not a free-form string.
- **Outputs**: none — purely presentational, non-interactive (`role`
  omitted/neutral, no click handler).
- **Guarantee**: identical color/label for a given `status` value everywhere
  it is used (FR-009/SC-002) because it is the *only* place status→color
  mapping is implemented.

## `<app-priority-chip>` — `shared/priority-chip/priority-chip.component.ts`

- **Inputs**: `priority: WorkItemPriority` (required).
- **Outputs**: none.
- **Guarantee**: same as `<app-status-chip>`, for priority.

## `<app-user-avatar>` — `shared/user-avatar/user-avatar.component.ts`

- **Inputs**:
  - `userId: number` (required — drives the deterministic color)
  - `fullName: string` (required — drives the initials)
  - `showName?: boolean` (default `false`) — when `true`, renders the name
    text adjacent to the circular avatar.
- **Outputs**: none.
- **Guarantee**: same `userId` always renders the same background color,
  anywhere in the app (FR-010).

## `<app-empty-state>` — `shared/empty-state/empty-state.component.ts`

- **Inputs**:
  - `icon: string` (Material icon name)
  - `message: string`
- **Content projection**: an `action` slot for an optional primary-action
  button (e.g. "Add work item"); omitted entirely when there is nothing
  actionable (e.g. a filtered-to-nothing state).

## `NotificationService` — `shared/notification.service.ts`

- **Methods**:
  - `success(message: string): void`
  - `error(message: string): void`
- **Behavior**: shows a `MatSnackBar` toast styled via design tokens, short
  auto-dismiss duration, does not block navigation or require user
  dismissal.

## `FriendlyDatePipe` — `shared/friendly-date.pipe.ts`

- **Pipe name**: `friendlyDate`
- **Usage**: `{{ someDate | friendlyDate }}`
- **Input type**: `Date | string | null | undefined`
- **Output**: `'MMM d, y'`-formatted string (e.g. `"Jul 17, 2026"`), or a
  placeholder string (`"—"`) for `null`/`undefined`.
- **Guarantee**: never emits a raw ISO 8601 string (FR-011/SC-006).

---

## Non-goals of this contract

- No new HTTP endpoints, DTOs, or request/response shapes — nothing here
  changes what the Angular app sends to or receives from
  `backend/TaskFlow.Api`.
- No component in this contract enforces authorization — visibility filtering
  (e.g. hiding the Users nav item) is a UX convenience only, per FR-015; the
  server remains the sole enforcement point.
