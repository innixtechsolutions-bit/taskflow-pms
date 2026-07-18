# TaskFlow Design System

Introduced by Feature 004 (App Shell & Design System). Everything here lives
under `frontend/src/app/shared/`. For the full input/output contract of each
piece, see
[`specs/004-app-shell-design-system/contracts/design-system-components.md`](../../../../specs/004-app-shell-design-system/contracts/design-system-components.md).

## Tokens

One file, `frontend/src/design-tokens.scss`, is the single source of truth
for every color, spacing, radius, and layout value the app uses. It defines
CSS custom properties on `:root` (consumable as `var(--token-name)` from any
component's CSS or from `style` bindings) plus one Sass variable,
`$breakpoint-tablet-px`, for the one place a value has to be a compile-time
number (a `@media` query condition can't read a CSS custom property).

Categories: primary/brand, surface, sidebar, status color (one pair of
bg/text per `WorkItemStatus` value), priority color (one pair per
`WorkItemPriority` value), an 8-color avatar accent palette, a spacing scale
(`--space-1` through `--space-6`), corner radius, and layout (content
max-width, sidebar width, tablet breakpoint).

**Rule**: no component hard-codes a color, spacing, or radius value for
these concepts — if you need one, add or reuse a token instead.

## Shared components

| Component / service / pipe | What it's for |
|---|---|
| `<app-shell>` | The persistent sidebar + content-area wrapper every authenticated page renders inside. Used once, in `app.html`. |
| `<app-sidebar-nav>` | The sidebar's nav links, user block, and logout menu. Used inside `<app-shell>` only. |
| `<app-page-header>` | Title + optional subtitle + optional right-aligned action button, for a page's top section. |
| `<app-status-chip>` | A colored, non-interactive label for a `WorkItemStatus` value. |
| `<app-priority-chip>` | Same, for a `WorkItemPriority` value. |
| `<app-user-avatar>` | A circular initials badge with a deterministic per-name color, optionally with the name shown alongside. |
| `<app-empty-state>` | Icon + message + optional action, for any list/tree with nothing to show. |
| `NotificationService` | Injectable service; `.success(message)` / `.error(message)` show a toast. Use instead of silent navigation after create/edit/delete. |
| `FriendlyDatePipe` | `{{ date \| friendlyDate }}` — always "Jul 17, 2026"-style, never a raw ISO string, with a `—` placeholder for null/undefined. |

## When to use a shared component instead of ad-hoc markup

If a screen displays a work item's status or priority, an assignee's name,
an empty list/tree, a page title, or any date — use the matching piece
above. Writing your own `<span>` with inline status/priority colors, your
own initials-avatar, or interpolating a raw date field directly defeats the
whole point of this feature (FR-008/FR-009/FR-010/FR-011): one place per
concept, so a color or format change happens once instead of N times.

## Two implementation gotchas worth knowing before you add a new slot

1. **Angular's NG8011**: content projected via an attribute-selector
   `<ng-content select="[foo]">` silently fails to project if it sits inside
   an `@if` block with more than one root node. Wrap multi-node content in a
   single `<ng-container foo>` inside the `@if`.
2. **Attribute selector casing**: use kebab-case (`page-header-actions`,
   `empty-state-action`), never camelCase — HTML lowercases attribute names
   on parse, so a camelCase `select` never matches.
