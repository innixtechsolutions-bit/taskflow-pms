# Feature Specification: App Shell & Design System

**Feature Branch**: `004-app-shell-design-system`

**Created**: 2026-07-18

**Status**: Draft

**Input**: User description: "Transform TaskFlow's UI from 'styled forms' into a designed product: a persistent app shell with sidebar navigation, and a reusable design system that every current and future screen uses. Retrofit Projects, project detail (tree + flat), work item forms/detail, Users, and a Dashboard placeholder onto shared page header, cards, status/priority chips, and avatars with identical colors everywhere. No backend changes, no functional/permission changes. Plus two QA additions: (1) all dates must render in a friendly format like 'Jul 17, 2026', never raw ISO timestamps; (2) list pages must use the full available content width sensibly, not hug the left side of the viewport."

**Visual Reference**: [`visual-reference.png`](./visual-reference.png) (in this
directory) — a reference mockup showing the intended sidebar treatment
(dark navy sidebar, purple active/primary accent, product logo mark at
top), card style (white surface, soft shadow, rounded corners), chip
color language (green = complete/done, blue = in progress, amber/orange
= medium priority or pending, red = high priority/overdue, gray = low/
not started), avatar stacks and single avatars with initials, table-style
list rows with inline progress bars, and typography scale (bold page
titles, muted subtitles, small-caps-free labels). This feature's
functional requirements describe *what* the shell and components must
do; this image is the source of truth for exact visual styling to be
translated into concrete design tokens during `/speckit-plan`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - One consistent application shell (Priority: P1)

As a signed-in user, every authenticated page I visit lives inside the same
shell — dark sidebar on the left, light content area on the right with a
consistent page header — so the product feels like one application instead
of a set of separate, differently-styled screens.

**Why this priority**: This is the structural foundation every other story
depends on. Without the shell existing and being applied everywhere, chips,
avatars, and cards have nowhere consistent to live.

**Independent Test**: Log in and visit Dashboard, Projects, a project
detail page, a work item detail page, and Users (as Admin). Each renders
inside the same sidebar + header layout, with consistent spacing and a
bounded content max-width, whether that page has a lot or a little content.

**Acceptance Scenarios**:

1. **Given** a signed-in user, **When** they log in, **Then** they land
   inside the shell (sidebar + content area), not a bare unstyled page.
2. **Given** a signed-in user on any authenticated page, **When** the page
   loads, **Then** a page header shows a title, a short subtitle, and (if
   the page has one) a right-aligned primary action button.
3. **Given** a user on the login or register page, **When** the page
   renders, **Then** it is NOT wrapped in the sidebar shell — it stays
   centered/standalone as today.
4. **Given** a list page (Projects, Users, work item lists), **When** it
   renders on a typical laptop/desktop width, **Then** its content area
   uses the full available content width sensibly (cards/table/columns
   expand to fill the space within the shell's max-width and padding)
   rather than rendering a narrow column hugging the left edge.

---

### User Story 2 - Sidebar navigation with active state and sign-out (Priority: P1)

As a signed-in user, I can navigate between sections from the sidebar, see
which section I'm currently in, and sign out from a user menu pinned at the
bottom of the sidebar.

**Why this priority**: Navigation is the core interaction the shell exists
to provide; without it the shell is just a wrapper with no function.

**Independent Test**: From any authenticated page, click each visible nav
link and confirm navigation occurs and the clicked item shows an active
state; open the bottom user menu and confirm logout signs the user out and
returns them to the login page.

**Acceptance Scenarios**:

1. **Given** a signed-in user, **When** they view the sidebar, **Then**
   they see the product name/logo, icon+label links for Dashboard and
   Projects (and Users if Admin), and a user block at the bottom showing
   their avatar, name, and role.
2. **Given** a user on a given section, **When** the sidebar renders,
   **Then** the nav link for that section is visually marked as active and
   no other link is.
3. **Given** a non-Admin user (e.g. Developer), **When** the sidebar
   renders, **Then** the Users link is not present (existing server-side
   403 guard on the Users endpoints is unchanged and still enforced
   independently of this display-only rule).
4. **Given** a signed-in user, **When** they open the user menu and choose
   logout, **Then** their session ends and they are returned to the login
   page.

---

### User Story 3 - Consistent status, priority, and assignee at a glance (Priority: P2)

As a user scanning lists and trees, I can read status, priority, and
assignee at a glance through consistent colored chips and initial-avatars
instead of plain text, and those colors/styles are identical everywhere
they appear.

**Why this priority**: This is the highest-value visual/scanability
improvement for day-to-day use of the existing screens, but it depends on
the shell (Story 1) existing first.

**Independent Test**: View the same work item's status and priority on the
Projects tree view, the flat filtered list, and the work item detail page;
confirm the chip color and label are pixel-for-pixel the same styling
convention in all three places. View an assignee's avatar in a list and in
the detail view; confirm same initials/color.

**Acceptance Scenarios**:

1. **Given** a work item with a given status, **When** it is shown in the
   tree view, the flat list, and the detail page, **Then** the status chip
   uses the same color and label in all three places.
2. **Given** a work item with a given priority, **When** it is shown
   anywhere in the app, **Then** the priority chip uses the same color and
   label everywhere.
3. **Given** a work item with an assignee, **When** the assignee is shown
   in a list, tree row, or detail view, **Then** a circular initials-avatar
   with a deterministic background color (same user → same color, every
   time) renders next to their name.
4. **Given** a date value shown anywhere in the UI (e.g. project dates,
   work item created/updated/due dates), **When** it is rendered,
   **Then** it displays in a short, human-friendly format such as
   "Jul 17, 2026" — never a raw ISO 8601 timestamp or unformatted date
   string.

---

### User Story 4 - Feedback and empty states (Priority: P3)

As a user performing create/edit/delete actions, I get immediate visual
confirmation (a toast/snackbar) rather than silent navigation, and when a
list has no data I see a friendly empty state instead of bare text.

**Why this priority**: Polish that meaningfully improves perceived quality
and reduces "did that work?" confusion, but the app is fully usable without
it while Stories 1-3 land first.

**Independent Test**: Create, edit, and delete a work item or project and
confirm a toast appears for each outcome (success and failure). View a
project with no work items and confirm a friendly empty state (icon +
message + relevant primary action) instead of plain "No work items yet"
text.

**Acceptance Scenarios**:

1. **Given** a user creates, edits, or deletes a project or work item,
   **When** the action completes, **Then** a brief toast confirms success
   or explains failure.
2. **Given** a project with no work items, **When** its detail page loads,
   **Then** an empty state with icon, message, and (where applicable) a
   primary action (e.g. "Add work item") is shown instead of plain text.
3. **Given** the Users list has no users to show under active filters,
   **When** the page loads, **Then** a matching friendly empty state
   renders.

---

### Edge Cases

- What happens on a very long project name, work item title, or user name
  in a chip/header/avatar-adjacent label? Text truncates with an ellipsis
  and full text available via tooltip/title attribute rather than breaking
  the layout.
- What happens when the viewport is resized to tablet width mid-session?
  The sidebar collapses to icons-only without a page reload, and expands
  back above the breakpoint; no horizontal scroll of the shell appears at
  typical laptop widths or at the tablet breakpoint.
- What happens when a list page has very few rows (e.g. one project)?
  The content area still uses the full sensible width for its layout
  (e.g. card grid, table) rather than shrinking to fit the row count.
- What happens when a status or priority value is encountered that has no
  defined chip color (should not occur given the fixed enums, but the
  system must not silently render an uncolored/unstyled chip)?
- What happens to a date field that is null/not-yet-set (e.g. no due date)?
  It renders as a clear placeholder (e.g. "—" or "No due date"), not a
  formatted zero-date or blank cell that looks broken.
- What happens on the Dashboard placeholder page — is it just an empty
  shell page? Yes; it shows the standard page header and an empty/
  "coming soon" state, per Out of Scope.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST render every authenticated page inside a
  persistent shell consisting of a fixed dark sidebar (~240px wide) and a
  light content area, with the login and register pages excluded from the
  shell.
- **FR-002**: The sidebar MUST show the product name/logo, icon+label
  navigation links for Dashboard and Projects (all authenticated users) and
  Users (Admins only, matching Feature 001's existing role model), and a
  user block pinned at the bottom showing the signed-in user's avatar,
  name, and role with a menu offering logout.
- **FR-003**: The sidebar MUST visually indicate which navigation item
  corresponds to the current route (active state), updating as the user
  navigates.
- **FR-004**: The content area MUST use a consistent page header pattern
  (title, short subtitle, optional right-aligned primary action button) on
  the Dashboard placeholder, Projects list, project detail, and Users list
  pages.
- **FR-005**: Below a defined tablet breakpoint, the sidebar MUST collapse
  to an icons-only rail with accessible labels (e.g. tooltips) while
  remaining fully navigable; no horizontal scrolling of the shell chrome
  MUST occur at typical laptop widths or at the tablet breakpoint.
- **FR-006**: List and index pages (Projects list, project detail tree/flat
  views, Users list, and any other tabular/card-grid list) MUST use the
  full available content width within the shell's max-width and padding —
  content MUST NOT render as a narrow column hugging the left edge when
  more horizontal space is available.
- **FR-007**: The system MUST define a single, documented set of design
  tokens (primary color, surface colors, status colors, priority colors,
  spacing scale, corner radius) that all components consume; no component
  MUST hard-code its own color values for these concepts. Token values
  MUST follow the visual reference (`visual-reference.png`): a dark
  navy sidebar with a purple primary/active accent, white cards with a
  soft shadow and rounded corners, and a status/priority color language
  of green (complete/done), blue (in progress), amber/orange (medium
  priority or pending), red (high/critical priority or overdue), and
  gray (low priority or not started).
- **FR-008**: The system MUST provide reusable presentation components —
  status chip, priority chip, user avatar (with optional adjacent name),
  empty state, and page header — and every current screen that displays
  status, priority, assignee, empty lists, or a page title MUST use them
  instead of ad-hoc markup.
- **FR-009**: A given status value MUST render with the same color and
  label everywhere it appears in the application (lists, trees, detail
  views); the same rule applies to priority values.
- **FR-010**: A user's avatar MUST render as a circular initials badge
  with a background color deterministically derived from that user
  (the same user always produces the same color), used consistently in
  assignee columns, tree rows, and detail views.
- **FR-011**: All user-facing date values (including but not limited to
  project start/end dates and work item created/updated/due dates) MUST be
  formatted as a short, human-readable date (e.g. "Jul 17, 2026") wherever
  they are displayed; the system MUST NOT display raw ISO 8601 timestamps
  or unformatted date strings anywhere in the UI. A missing/null date MUST
  render as a clear placeholder rather than an empty or malformed value.
- **FR-012**: The system MUST show a brief toast/snackbar confirming
  success or failure after create, edit, and delete actions on projects
  and work items, replacing any current silent navigation on those
  actions.
- **FR-013**: Lists and trees that currently show plain empty-state text
  (e.g. "No work items yet") MUST instead show the shared empty-state
  component (icon, message, and a primary action where one is
  applicable).
- **FR-014**: The Feature 003 tree view MUST render inside a card
  container with status/priority chips and assignee avatars per row,
  preserving its existing indentation and expand/collapse behavior
  unchanged.
- **FR-015**: This feature MUST NOT change any route guard, API call,
  validation rule, or permission check; the Users nav item's visibility is
  a display-only convenience and the server-side Admin-only enforcement
  from Feature 001 remains the sole security boundary.
- **FR-016**: All interactive elements (nav links, buttons, menu triggers)
  MUST retain a visible keyboard focus indicator.
- **FR-017**: The design system's tokens, shared components, and usage
  guidance MUST be documented briefly in the repository so future features
  can follow it without guesswork.
- **FR-018**: All existing backend and frontend automated tests MUST
  continue to pass; frontend test selectors MAY be updated only where
  template structure changed as a result of this feature.

### Key Entities

- **Design Token Set**: the documented, single-source values for color
  (primary, surface, status-by-value, priority-by-value), spacing scale,
  and corner radius that all UI components reference; not a data entity,
  but a shared contract every screen depends on.
- **Navigation Item**: a sidebar entry with an icon, label, target route,
  and a visibility rule (all authenticated users, or Admin-only); driven by
  the existing role data from Feature 001, no new persistence.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of authenticated pages (Dashboard, Projects, project
  detail, work item detail, Users) render inside the shared shell with a
  consistent page header; 0 authenticated pages render outside it.
- **SC-002**: A given status or priority value renders with identical
  color and label in every location it appears (list, tree, detail) —
  verified with 0 discrepancies across the retrofitted screens.
- **SC-003**: Users can identify a work item's status, priority, and
  assignee without reading text labels, purely from chip color and avatar,
  in under 2 seconds of visual scanning per row.
- **SC-004**: Resizing the browser to tablet width collapses the sidebar
  to icons-only with no horizontal scrollbar appearing on the shell at any
  width from tablet up through common desktop/laptop widths.
- **SC-005**: Every create, edit, and delete action on a project or work
  item produces a visible toast within 1 second of the action completing,
  for both success and failure outcomes.
- **SC-006**: 100% of date values displayed anywhere in the retrofitted
  screens use the friendly short format (e.g. "Jul 17, 2026"); 0 raw ISO
  timestamps are visible to users.
- **SC-007**: On common laptop/desktop viewport widths, list pages (Projects,
  Users, work item tree/flat views) visibly use the full available content
  width within the shell's max-width — no list page renders its primary
  content narrower than roughly 90% of the available content-area width.
- **SC-008**: 100% of pre-existing backend and frontend automated tests
  pass after the retrofit, with no reduction in test count other than
  selector updates explicitly tied to template structure changes.

## Assumptions

- "Tablet breakpoint" follows common responsive convention (~768–1024px);
  exact pixel value is a design-token/implementation detail decided during
  planning, not fixed here.
- The friendly date format is date-only (no time-of-day) unless a specific
  screen's existing behavior already relies on time granularity (e.g. an
  audit/history timestamp); this feature does not introduce new
  time-of-day displays.
- "Full content width sensibly" means content fills the available width
  within the shell's overall content max-width and padding (not edge-to-
  edge browser width, and not an artificially narrow fixed-width column
  inside that area).
- No new backend endpoints or DTO fields are required; all data needed for
  chips, avatars, and dates already exists in current API responses.
- Dark mode, the real Dashboard content, Kanban/calendar/timesheet screens,
  notifications, global search, and phone-width layouts are explicitly out
  of scope, per the feature description.
- Role model and permissions are unchanged from Feature 001; this feature
  only changes how the Users nav entry is displayed, not who can access
  the Users feature.
