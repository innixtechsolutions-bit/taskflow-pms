# Phase 1 Data Model: App Shell & Design System

This feature is presentation-only — it introduces no database entities, no EF
Core migrations, and no new DTOs from the backend. "Data model" here means the
frontend-only presentation-layer types and value shapes the shared design
system components are built around, derived from the spec's Key Entities
section.

## Design Token Set

Not a runtime object — a fixed set of CSS custom properties defined once in
`frontend/src/design-tokens.scss` (see research.md #3) and consumed via
`var(--token-name)`. Documented here as the "shape" every component agrees on:

| Token category | Example names | Values |
|---|---|---|
| Primary/brand | `--color-primary`, `--color-primary-contrast` | Purple accent + active-state color from the visual reference |
| Surface | `--color-surface`, `--color-surface-alt`, `--color-sidebar-bg` | White cards, light content background, dark navy sidebar |
| Status color (per `WorkItemStatus`) | `--color-status-todo`, `--color-status-inprogress`, `--color-status-done` | Gray, blue, green |
| Priority color (per `WorkItemPriority`) | `--color-priority-low`, `--color-priority-medium`, `--color-priority-high`, `--color-priority-critical` | Gray, amber, orange/red, red |
| Avatar accent palette | `--color-avatar-1` … `--color-avatar-8` | Fixed rotation used by `avatarColorFor()` |
| Spacing scale | `--space-1` (4px) … `--space-6` (32px) | 4/8/12/16/24/32 |
| Radius | `--radius-card`, `--radius-chip` | Rounded corners per the visual reference |
| Layout | `--content-max-width`, `--sidebar-width`, `--sidebar-width-collapsed`, `--breakpoint-tablet` | ~1440px, ~240px, ~72px, ~1024px |

**Validation rule**: every status value in `WorkItemStatus` and every priority
value in `WorkItemPriority` MUST have a corresponding color token — enforced
by the chip components exhaustively switching over the union type (a TS
compile error results if a new enum value is added without a matching
token/case, satisfying the spec's edge case about undefined chip colors).

## Status / Priority Types (frontend)

New string-literal union types, introduced to replace the current untyped
`string` fields used for these concepts in `work-items.service.ts`:

```ts
type WorkItemStatus = 'ToDo' | 'InProgress' | 'Done';
type WorkItemPriority = 'Low' | 'Medium' | 'High' | 'Critical';
```

These mirror the backend's `WorkItemStatus`/`WorkItemPriority` C# enums
(`backend/TaskFlow.Api/Data/Entities/WorkItem.cs`) exactly by value name — no
new values, no renaming, no backend change. `WorkItem`, `WorkItemDetail`, and
`WorkItemTreeNode` interfaces narrow their `status`/`priority` fields from
`string` to these unions as part of the retrofit.

## Navigation Item

A frontend-only, code-owned config shape (not persisted):

| Field | Type | Notes |
|---|---|---|
| `label` | `string` | Display text, e.g. "Projects" |
| `icon` | `string` | Material icon name |
| `route` | `string` | Router path, e.g. `/projects` |
| `visibleTo` | `'all' \| UserRole[]` | `'all'`, or a specific role list (e.g. `['Admin']` for Users) |

Filtered at render time against `authService.currentRole()`
(`frontend/src/app/auth/auth.service.ts`) — display-only; the
`adminGuard`/API-level role checks remain the actual security boundary
(FR-015).

## Avatar Identity

Derived, not stored — computed from data already present on the current user
/ any user/assignee reference returned by existing APIs:

| Input | Derivation | Output |
|---|---|---|
| `fullName: string` | first-letter of first two space-separated words, uppercased | Initials, e.g. "Uma Kannan" → "UK" |
| `fullName: string` | hashed into an index over the avatar accent palette (research.md #5, revised) | Deterministic background color, stable across sessions and screens |

**Correction from the original plan**: research.md #5 originally proposed
hashing on the user's numeric `id`. Implementation found that the tree
endpoint's DTO (`WorkItemTreeNodeDto`, `backend/TaskFlow.Api/Dtos/`) only
returns `AssigneeName` — no `AssigneeUserId` — while the flat list/detail
DTOs do include an id. Hashing on different keys in different views would
make the *same* assignee render a *different* avatar color in the tree vs.
the flat/detail view, directly failing FR-010/US3's "same user → same color,
everywhere" requirement — worse than the collision risk it was meant to
avoid. `fullName` is the one identifier available in every context (tree,
flat list, detail, sidebar, Users list), so both initials and color now
derive from it. Two users who happen to share an exact display name will
share an avatar color — an accepted, low-probability cosmetic tradeoff,
consistent with adding no new backend fields (FR-015/spec Assumptions).

No new fields are required on any existing user/assignee DTO — `id` and
`fullName` (or equivalent `assigneeName`/`assigneeId`-style fields) already
exist wherever an assignee or the current user is returned today.

## Toast Notification

Ephemeral UI state, not persisted — a call shape on the new
`NotificationService`:

| Method | Params | Effect |
|---|---|---|
| `success(message)` | `message: string` | Shows a `MatSnackBar` toast with the success token styling, short auto-dismiss |
| `error(message)` | `message: string` | Shows a `MatSnackBar` toast with the error token styling, short auto-dismiss |

## Friendly Date

A pure display transform, not a stored value — `FriendlyDatePipe` takes any
existing `Date`/ISO-string field already on current DTOs (`createdAt`,
`updatedAt`, project `startDate`/`endDate`, etc.) and renders `'MMM d, y'`
(e.g. "Jul 17, 2026"), or a placeholder (`"—"`) when the value is
null/undefined. No new date fields are introduced; no change to how dates are
stored or sent to the API.
