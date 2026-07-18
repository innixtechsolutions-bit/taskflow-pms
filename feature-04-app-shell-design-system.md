# Feature 04 — App Shell & Design System (paste this into /speckit-specify)

Transform TaskFlow's UI from "styled forms" into a designed product:
a persistent app shell with sidebar navigation, and a reusable design
system that every current and future screen uses.

## Why (context)

Features 001–003 delivered working, Material-styled screens, but each
page stands alone — there is no navigation shell, no shared visual
language, and no reusable presentation components. Upcoming features
(Kanban, timesheets, dashboard, reports) are UI-heavy; building them
without a design foundation would produce an inconsistent product and
repeated styling work. This feature builds that foundation and
retrofits the existing screens onto it. The visual direction is a
modern SaaS project-management look: dark sidebar, light content area,
card-based layouts, colored status/priority chips, and avatar-marked
assignees.

## Design direction (reference description)

- **Layout**: fixed dark sidebar (~240px) on the left with the product
  name/logo at top, icon+label navigation links, and the signed-in
  user (avatar, name, role) pinned at the bottom with a menu (logout).
  Light content area on the right with a consistent page header
  (title, short subtitle, and a right-aligned primary action button
  where the page has one). Content max-width with comfortable padding;
  the shell is responsive down to tablet width (sidebar collapses to
  icons-only below a breakpoint).
- **Navigation items (current scope)**: Dashboard (placeholder page
  for now — real dashboard is a later feature), Projects, Users
  (Admin only). Future features add their own items.
- **Cards**: white surface, subtle border/shadow, rounded corners —
  the default container for content blocks.
- **Chips/badges**: Status (To Do / In Progress / Done) and Priority
  (Low / Medium / High / Critical) render as small colored chips with
  a consistent, documented color per value — the same colors
  everywhere they appear (lists, trees, details).
- **Avatars**: users render as circular initial-avatars (deterministic
  background color derived from the user) next to their name;
  assignee columns and detail views use them.
- **Empty states**: a friendly icon + message + (where relevant) a
  primary action, replacing bare text like "No work items yet."
- **Feedback**: success/failure of create/edit/delete actions shows a
  brief toast/snackbar instead of silent navigation.

## User stories

1. As a signed-in user, every page I visit lives inside the same shell
   — sidebar, header pattern, spacing — so the product feels like one
   application instead of separate screens.
2. As a signed-in user, I can navigate between sections from the
   sidebar, see which section I'm currently in (active state), and
   sign out from the user area at the bottom.
3. As a user scanning lists and trees, I can read status, priority,
   and assignee at a glance through consistent chips and avatars
   instead of plain text.
4. As a user performing actions, I get immediate visual confirmation
   (toasts) and helpful empty states, so the app never feels dead or
   ambiguous.
5. As a Developer on the team (role), I do not see navigation entries
   I cannot use (Users stays Admin-only, as in Feature 001).

## Acceptance criteria

### Shell & navigation
- Auth pages (login/register) remain outside the shell, centered as
  today; every authenticated page renders inside the shell.
- Sidebar: product name, nav links with icons, active-route
  highlighting, user block at bottom (avatar, name, role, logout).
- Users nav item visible to Admins only (server security unchanged —
  this is display; Feature 001's guards and 403s still apply).
- Below the tablet breakpoint the sidebar collapses to icons with
  accessible labels (tooltips or expanded overlay) — no horizontal
  scrolling of the shell at typical laptop widths.
- Page header pattern used on Projects, project detail, Users, and
  the Dashboard placeholder: title, subtitle, optional primary action.

### Design tokens & components
- A single documented set of theme values (primary color, surface
  colors, status colors, priority colors, spacing scale, corner
  radius) defined once and consumed everywhere — no per-component
  hard-coded colors for these concepts.
- Reusable presentation pieces exist and are used by all current
  screens: status chip, priority chip, user avatar (+ optional name),
  empty state, page header.
- Status/priority colors are identical in every location they appear.

### Retrofit of existing screens
- Projects list, project detail (tree + flat views from Feature 003),
  work item forms and detail, Users list, home/dashboard placeholder —
  all restyled onto the shell, cards, chips, and avatars.
- The Feature 003 tree view renders inside a card with chips/avatars
  per row; indentation and expand/collapse behavior unchanged.
- No functional behavior changes anywhere: routes, guards, API calls,
  validation, and permissions are untouched. All existing backend and
  frontend tests must remain green (frontend test selectors may be
  updated only where template structure changes).

### Non-functional
- The design system is documented briefly in the repo (which tokens
  exist, which shared components exist, when to use them) so future
  features follow it without guesswork.
- No new backend endpoints; this is a frontend-only feature except
  where an existing response already provides needed data.
- Keyboard focus states remain visible on all interactive elements.

## Out of scope (do NOT include in this feature)

- The real Dashboard content (KPI cards with live numbers, charts) —
  a later feature; only the placeholder page inside the shell ships now
- Dark mode / theme switching
- Kanban board, calendar, timesheet screens (later features — they
  will consume this design system)
- Notifications bell, global search in the header (later)
- Mobile-phone-width layouts (tablet and up is the target for now)
- Backend changes of any kind

## Success check

Feature is complete when: logging in lands inside the shell with the
sidebar showing Dashboard/Projects (and Users for Admins only) with
correct active states; the Projects, project detail (tree), work item
detail, and Users screens all use the shared page header, cards,
status/priority chips, and avatars with identical colors everywhere;
deleting a work item shows a toast; an empty project shows the new
empty state; logout works from the sidebar user menu; resizing to
tablet width collapses the sidebar to icons; and the full existing
test suites still pass.
