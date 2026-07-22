# Feature 09 — Project Summary Dashboard (paste into /speckit-specify)

Add a per-project Summary view — stat cards, status/priority
breakdowns, team workload, and recent activity — so anyone opening a
project gets an at-a-glance picture instead of having to read the
full item list.

## Why (context)

TaskFlow now has Board, Backlog, List, and Tree views inside a project
— all detail-level. Nothing answers "how's this project doing?" at a
glance. This feature adds that: a Summary tab (first tab, the new
default landing when opening a project) with the stat cards, charts,
and activity feed shown in the visual reference (saved Jira Summary
screenshots). This feature also introduces an **Activity Log** —
recording who changed what and when — because the "recent activity"
card needs real data to show, and no such record exists yet anywhere
in the system.

## New concept: Activity Log

An **activity entry** is recorded whenever a work item is created, or
one of these fields changes: Status, Priority, Assignee, Sprint. Each
entry captures: the work item (id + title, so it still means something
if later deleted), who made the change, what changed (field name,
old value, new value — using display names, e.g. status names not
IDs), and when. Entries are never edited or deleted by users; they are
an append-only audit trail scoped to a project (derived from the work
item's project at the time of the change). Project, work item create/
edit/delete of *other* kinds (title, description, dates, labels,
hierarchy) do not generate entries in v1 — only the four fields above,
because those are what the reference's activity feed shows changing.

## User stories

1. As any signed-in user, opening a project lands me on its Summary
   tab by default (Board/Backlog/List/Tree remain available as before,
   now alongside Summary), so I immediately see project health.
2. As any signed-in user, I see stat cards for the project: total work
   items, completed count and percent, in-progress count, and a
   due-soon count (due within the next 7 days, not yet Done), each
   reflecting live data.
3. As any signed-in user, I see a status breakdown (a donut/ring chart
   using the project's own workflow columns and their chip colors)
   and a priority breakdown (a bar chart across Low/Medium/High/
   Critical), so I can see where work is concentrated.
4. As any signed-in user, I see a team workload panel — each assignee
   with a count or proportion of open (not-Done) items assigned to
   them, including an "Unassigned" row when applicable — so workload
   imbalance is visible.
5. As any signed-in user, I see a recent activity feed — the latest
   entries from the Activity Log for this project, newest first, in
   readable sentences ("Jane changed Task 'Fix login' status from To
   Do to In Progress — 6 minutes ago"), so I can see what's been
   happening without digging through items.
6. As any signed-in user opening a work item's detail page, I can see
   that item's own activity history (a filtered view of the same log),
   so I can see its change history specifically.

## Acceptance criteria

### Layout & tabs
- Tab order becomes Summary | Board | Backlog | List | Tree; Summary
  is the default when a project is opened without an explicit view in
  the URL. Existing explicit-view links/behavior (e.g. Board's
  ?view=board) are unchanged.
- Summary uses the design system's card layout; charts and counts
  refresh when the tab is revisited (not necessarily live-pushed —
  polling or refresh-on-navigation is sufficient; real-time is a later
  phase).

### Stat cards
- Total items, Completed (count + %), In Progress (count), Due soon
  (count, next 7 days, excludes Done) — each computed from current
  data, category-aware (Done = the project's Done-category statuses,
  per Feature 006), Epics included in totals.

### Status & priority breakdowns
- Status breakdown reflects the project's actual workflow columns
  (names, order, colors) — a project with custom columns (Feature 006)
  shows its own columns here, not a fixed set.
- Priority breakdown shows all four levels even at zero count (so the
  shape is comparable project to project).

### Team workload
- One row per user who is either assigned at least one open item in
  this project or is a Manager/Admin of the system (so managers can
  see a team member with zero load); "Unassigned" appears if any open
  item has no assignee.
- Sorted by open-item count descending.

### Activity log & feed
- Entries created automatically by existing create/edit flows (no new
  UI needed to create them) for: work item creation, and changes to
  Status, Priority, Assignee, Sprint.
- Project Summary feed: latest N entries (paginated or "load more"),
  newest first, each rendered as a one-line human-readable sentence
  with a relative timestamp ("6 minutes ago") and the actor's name.
- Work item detail's activity section: same rendering, filtered to
  that item, oldest-to-newest or newest-first consistently with the
  project feed's order.
- Entries are immutable and never exposed for editing/deleting via any
  API.
- If a referenced work item is later deleted, its activity entries
  remain (using the captured title snapshot) rather than disappearing
  or erroring.

### Non-functional
- All Summary data is scoped to the requesting user's visibility
  (same project access as every other view — no new permission model).
- Activity entries are written as part of the same transaction as the
  change they record (no change is saved without its entry, and vice
  versa).
- Pure logic (percent calculations, due-soon windowing, relative-time
  formatting, activity-sentence construction) is test-first.
- Existing Board/Backlog/List/Tree behavior is unchanged; this feature
  only adds the Summary tab, the activity-log write path hooked into
  existing mutations, and the Summary/detail read surfaces.

## Out of scope (do NOT include in this feature)

- Editable/configurable dashboards, widget rearranging
- Cross-project ("all my projects") dashboard — this is per-project
- Burndown/velocity charts (sprint-specific analytics — later)
- Activity entries for label, description, title, date, or hierarchy
  changes (only the four fields listed above, in v1)
- Real-time push updates to the Summary or activity feed (SignalR
  phase later)
- Exporting or filtering the activity log

## Success check

Feature is complete when: opening a project lands on Summary by
default; stat cards show correct live totals/completed/in-progress/
due-soon; the status donut matches the project's own workflow columns
and colors; the priority bar chart shows all four levels; team
workload lists assignees sorted by open-item count with an Unassigned
row when relevant; changing a work item's status, priority, assignee,
or sprint produces a new activity entry visible in both the project
feed and that item's own detail activity section, in a readable
sentence with a relative time; a second project's Summary reflects
only its own data; and all prior views and tests remain unaffected.
