# Phase 0 Research: App Shell & Design System

No `[NEEDS CLARIFICATION]` markers remain in the spec, and the Technical
Context in plan.md was filled directly from the existing codebase (Angular 22,
Angular Material 22, CDK, Vitest — all already in `frontend/package.json`), so
this phase focuses on **implementation-approach decisions** for the pieces the
spec deliberately leaves at the "what" level.

## 1. Shell layout mechanism

**Decision**: Use Angular Material's `MatSidenavModule`
(`mat-sidenav-container` / `mat-sidenav` in `mode="side"`, permanently open
above the tablet breakpoint) as the new `AppShellComponent`, replacing the
current `shared/header/header.component.ts` top bar entirely.

**Rationale**: This is exactly the layout Material's sidenav primitive is
built for (persistent side navigation + content area), and the project's
constitution fixes Angular Material as the UI component library. Using it
avoids hand-rolling positioning, focus-trapping, and ARIA landmark behavior
that `mat-sidenav` already provides correctly.

**Alternatives considered**: A custom CSS grid/flex sidebar with manual
`position: fixed` and manual ARIA roles — rejected as reinventing behavior
Material already solves, adding maintenance surface for no benefit (violates
Principle III, Clarity Over Cleverness).

## 2. Icons-only collapse at the tablet breakpoint

**Decision**: Use Angular CDK's `BreakpointObserver` (already a transitive
dependency via `@angular/cdk`) inside `SidebarNavComponent`, observing a
single custom breakpoint constant (`(max-width: 1024px)`, stored once in
`design-tokens.scss` as `--breakpoint-tablet` and mirrored as a TS constant
for the `BreakpointObserver` query) to toggle an `isCollapsed` signal that
switches nav item rendering between icon+label and icon-only-with-tooltip.

**Rationale**: `BreakpointObserver` is the idiomatic, already-available CDK
tool for this; a signal driven by its observable fits the project's
zoneless/signals-first architecture cleanly (subscribe once in the
component's constructor via `toSignal`, no manual `ngOnDestroy` cleanup
needed).

**Alternatives considered**: Pure CSS media queries with `display` toggling —
rejected because the icons-only rail needs a *different template structure*
(tooltips appear only in collapsed mode), not just different styling, so a
signal-driven `@if` is simpler than fighting CSS-only visibility toggling for
structurally different markup.

## 3. Design tokens

**Decision**: A single new `frontend/src/design-tokens.scss` file defining
CSS custom properties on `:root` — primary/surface colors, one color per
status value, one color per priority value, a spacing scale (4/8/12/16/24/32
px steps), corner radius, the content max-width, and the tablet breakpoint —
imported once in `styles.css`. Components reference `var(--token-name)` in
their own scoped CSS; nothing hard-codes hex values.

**Rationale**: Tokens here are static design-time values, not runtime state,
so CSS custom properties are the right layer — no Angular service, no
injected "theme" object, and they're consumable both from component CSS and
from `style.background-color` bindings (needed for the avatar's per-user hash
color) without importing TypeScript constants into every component.

**Alternatives considered**: A TypeScript `DESIGN_TOKENS` constants object —
rejected because it would need to be threaded through every component's CSS
some other way (inline styles everywhere) or duplicated into SCSS anyway;
CSS custom properties are consumable from both worlds with one source.

## 4. Status & priority chips

**Decision**: Two small standalone presentational components,
`StatusChipComponent` and `PriorityChipComponent`, each taking a typed input
(`status: WorkItemStatus` / `priority: WorkItemPriority`, new string-literal
union types introduced alongside the existing untyped `string` fields on
`WorkItem`/`WorkItemDetail`/`WorkItemTreeNode`) and rendering a `<span>` with
a token-driven background/text color class per value — not Angular Material's
`mat-chip`.

**Rationale**: `mat-chip` (used inside `mat-chip-set`/`mat-chip-listbox`)
carries interactive/selectable semantics — `role="option"`, ripple, focus
management for selection — that are wrong for a static, read-only status
label. A plain component keeps the DOM and ARIA semantics honest for
something that is not interactive, while still consuming the same design
tokens and living in `shared/` for reuse (per FR-008).

**Alternatives considered**: Styling `<mat-chip>` to suppress its interactive
affordances — rejected, fights the component's default behavior and role
semantics rather than using the right tool for a static label.

## 5. Deterministic avatar color

**Decision (revised during implementation — see data-model.md's "Correction
from the original plan")**: A pure function (`avatarColorFor(fullName:
string): string`, co-located with `UserAvatarComponent`) that hashes the
user's `fullName` into an index over a fixed palette of ~8 accent tokens
defined in `design-tokens.scss`, plus a small initials helper
(`initialsFor(fullName: string)`, e.g. "Uma Kannan" → "UK").

**Rationale**: The original plan hashed on the user's numeric `id`, reasoned
to be safer than a name (no duplicate-name collisions). Implementation found
that `WorkItemTreeNodeDto` (`backend/TaskFlow.Api/Dtos/`) — the tree view's
data source — only returns `AssigneeName`, not an id, while the flat
list/detail DTOs do have one. Hashing on `id` where available and `fullName`
elsewhere would make the same assignee render two *different* avatar colors
depending on which view they're seen in, directly breaking FR-010/US3's
"same user → same color, everywhere" — a worse, and directly testable,
failure than the name-collision risk. `fullName` is hashed everywhere
instead, since it is the one field present in every context (tree, flat
list, detail, sidebar, Users list) — no new backend field, per FR-015.

**Alternatives considered**: Hashing on `id` where available, falling back
to `fullName` in the tree view — rejected because it produces exactly the
inconsistency described above (same person, different color, depending on
which screen). A hosted avatar/gravatar-style service — rejected, out of
scope (no external services) and unnecessary for initials-only avatars.

## 6. Toasts

**Decision**: A thin `NotificationService` (`shared/notification.service.ts`)
wrapping Angular Material's `MatSnackBar` — already a project dependency,
currently unused anywhere in the codebase — with two convenience methods,
`success(message: string)` and `error(message: string)`, each applying a
token-driven `panelClass` and a short auto-dismiss duration. Existing
create/edit/delete flows call this service instead of relying on silent
navigation (and instead of the native `confirm()` used today for some
actions, which is unaffected by this change unless it currently stands in for
a missing success/failure signal).

**Rationale**: `MatSnackBar` already provides accessible, queued,
auto-dismissing toast behavior — reusing it (per the fixed "Angular Material
for UI components" stack rule) is strictly simpler than building a custom
toast component and avoids adding a new dependency.

**Alternatives considered**: A custom toast/snackbar component with its own
stacking and timing logic — rejected as reinventing what `MatSnackBar`
already does correctly.

## 7. Friendly date formatting

**Decision**: A new standalone Angular pipe, `FriendlyDatePipe`
(`shared/friendly-date.pipe.ts`), wrapping Angular's built-in `DatePipe` with
the format string `'MMM d, y'` (e.g. "Jul 17, 2026") under the `en-US`
locale, with explicit null/undefined handling that renders a placeholder
(e.g. `"—"`) instead of an empty or malformed cell. Applied everywhere a raw
date is currently interpolated directly in a template.

**Rationale**: Angular's built-in `DatePipe` already produces this exact
format given the right format string; wrapping it in a named pipe
(`| friendlyDate`) makes the intent self-documenting at each call site and
centralizes the null-placeholder behavior in one place instead of repeating
`?? '—'` everywhere.

**Alternatives considered**: A third-party date library (date-fns, dayjs) —
rejected as an unnecessary new dependency for a single fixed format string
Angular's own `DatePipe` already covers.

**Known call sites to update** (from codebase survey): `createdAt` in
`projects-list.component.html`, `project-detail.component.html`, and
`users-list.component.html` (all currently raw ISO interpolation). Work item
`dueDate`/`updatedAt`/`createdAt` are not currently rendered anywhere in the
UI (they exist only as an unrendered form field or DTO fields) — per FR-015
("no functional behavior changes"), this feature applies `FriendlyDatePipe`
everywhere a date is **already shown**, and does not add new date displays
that don't exist today; adding a due-date display to the work item detail
page would be new scope, not a retrofit, so it is left for a future feature
unless the maintainer says otherwise.

## 8. Full-width list pages (QA fix)

**Decision**: Replace the single global `.app-content { max-width: 960px;
margin: 0 auto; }` rule in `frontend/src/app/app.css` — which today caps
*every* authenticated page, list or not, at a fairly narrow 960px — with the
new shell's own content wrapper using a wider `--content-max-width` token
(e.g. ~1440px, matching the visual reference's proportions), and ensure list-
page inner containers (tables, card grids) use `width: 100%` / CSS grid so
they actually fill that wrapper instead of shrinking to content size.

**Rationale**: The 960px cap is the confirmed root cause of the "hugging the
left side" QA symptom — on any viewport wider than ~1000px, up to ~40% of the
horizontal space sits unused as side margins, and this rule currently applies
uniformly to narrow forms and wide lists alike. Giving the shell's content
area a single wider token (still bounded, per FR-006's "within the shell's
max-width," not edge-to-edge) fixes list pages while forms (which already
constrain themselves further via `.form-page`'s own 400-500px `mat-card`
max-width) are unaffected.

**Alternatives considered**: Leaving `.app-content` alone and adding a
page-specific override only on list pages — rejected as more fragile (every
future list page would need to remember the override) versus fixing the
shared wrapper once, which is exactly what a shell + design tokens is for.

## 9. Navigation item configuration

**Decision**: A static, readonly array of nav item descriptors (label, icon
name, route, `visibleTo: UserRole[] | 'all'`) defined once in
`SidebarNavComponent` (or a tiny co-located `nav-items.ts` if the list grows),
filtered against `authService.currentRole()` — no backend endpoint, no new
DTO.

**Rationale**: Matches the spec's explicit assumption that no new backend
endpoints are needed; the nav item set is small, static, and changes only
when a future feature adds a section (already anticipated in the spec's "Why"
section).

**Alternatives considered**: Deriving nav items from a backend-provided
navigation/permissions endpoint — rejected as unnecessary indirection for a
fixed, small, code-owned list (Principle III).
