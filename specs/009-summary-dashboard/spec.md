# Feature Specification: Project Summary Dashboard & Activity Log

**Feature Branch**: `009-summary-dashboard`

**Created**: 2026-07-22

**Status**: Planned

**Input**: User description: "Add a per-project Summary view — stat cards, status/priority breakdowns, team workload, and recent activity — so anyone opening a project gets an at-a-glance picture instead of having to read the full item list. This feature also introduces an Activity Log — recording who changed what and when — because the recent-activity card needs real data to show, and no such record exists yet anywhere in the system. Activity entries are recorded when a work item is created, or when its Status, Priority, Assignee, or Sprint changes; entries are append-only and never edited or deleted. Summary becomes the new default tab (Board/Backlog/List/Tree remain available). Stat cards: total items, completed (count + %), in progress, due soon (7 days, excludes Done). Status breakdown uses the project's own workflow columns and colors; priority breakdown shows all four levels even at zero. Team workload lists assignees (plus zero-load Managers/Admins and an Unassigned row when applicable) sorted by open-item count descending. A project-level activity feed and a work-item-level activity history both render the same log as readable, relative-timestamped sentences."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Land on a project's Summary with live stat cards (Priority: P1)

As any signed-in user, when I open a project I land on its Summary tab by default and immediately see stat cards — total work items, completed (count and percent), in-progress count, and due-soon count — so I get an at-a-glance health check without reading the full item list.

**Why this priority**: This is the entry point and the smallest slice that delivers the feature's core promise ("how's this project doing, at a glance"). Without it, nothing else in the feature is reachable, and it is fully testable against existing work item data alone (no new write path required).

**Independent Test**: Open a project with a known mix of items (some Done, some in progress, one due in 3 days). Confirm the Summary tab is shown without any explicit `?view=` in the URL, and that the four stat cards show the correct total, completed count/percent, in-progress count, and due-soon count.

**Acceptance Scenarios**:

1. **Given** a project, **When** a signed-in user opens it without an explicit view in the URL, **Then** the Summary tab is shown, and the tab order is Summary | Board | Backlog | List | Tree.
2. **Given** a project reached via an explicit view link (e.g. `?view=board`), **When** the user opens that link, **Then** the existing explicit view is shown unchanged (Summary does not override an explicit view selection).
3. **Given** a project with 10 items where 4 are in a Done-category status, **When** the Summary tab is shown, **Then** the Completed stat card shows "4" and "40%".
4. **Given** a project with items in non-Done-category statuses, **When** the Summary tab is shown, **Then** the In Progress stat card counts every item in an Open-category status — i.e., every item not yet Done, per the project's own workflow column categories (the workflow model has only Open/Done categories; there is no separate "not yet started" category to exclude).
5. **Given** a project with an item due in 5 days (not Done) and another due in 10 days, **When** the Summary tab is shown, **Then** the Due Soon count includes only the item due within 7 days and excludes any Done item regardless of due date.
6. **Given** a project with Epics among its items, **When** stat cards are computed, **Then** Epics are included in the Total and other applicable counts.
7. **Given** a user revisits the Summary tab after data has changed elsewhere, **When** they return to the tab, **Then** the stat cards reflect current data (refresh-on-navigation or polling is sufficient; no real-time push is required).

---

### User Story 2 - See status and priority breakdowns (Priority: P2)

As any signed-in user, I see a status breakdown (using the project's own workflow columns and their chip colors) and a priority breakdown (Low/Medium/High/Critical) on the Summary tab, so I can see where work is concentrated.

**Why this priority**: Builds directly on the stat cards (US1) with the same underlying data, but is a distinct, separately testable visual — a project can ship US1 without it and still deliver value.

**Independent Test**: Open a project with custom workflow columns (per Feature 006) and items spread across three of its five statuses; confirm the status breakdown shows exactly those five columns, in the project's column order, using each column's configured color, with correct counts. Separately confirm the priority breakdown shows all four priority levels even when one has zero items.

**Acceptance Scenarios**:

1. **Given** a project with custom workflow columns (names, order, colors) distinct from another project's, **When** its Summary tab is shown, **Then** the status breakdown reflects that project's own columns — not a fixed global set — in that project's configured order and colors.
2. **Given** a project where no item currently has Critical priority, **When** the priority breakdown is shown, **Then** Critical still appears with a zero count (all four levels are always shown).
3. **Given** two different projects, **When** each Summary tab is viewed, **Then** each shows only its own status/priority breakdown data.

---

### User Story 3 - See team workload (Priority: P2)

As any signed-in user, I see a team workload panel listing each relevant user with their count of open (not-Done) items in this project, including an "Unassigned" row when applicable, so workload imbalance is visible.

**Why this priority**: Independently valuable and testable against existing assignment data, but secondary to the headline stat cards and breakdowns.

**Independent Test**: In a project with three open items assigned to two different users and one unassigned open item, plus a Manager with zero assigned items in this project, open the Summary tab and confirm all three users appear (including the zero-load Manager) plus an "Unassigned" row, sorted by open-item count descending.

**Acceptance Scenarios**:

1. **Given** a project with open items assigned to User A (3), User B (1), and one open item unassigned, **When** the workload panel is shown, **Then** rows appear in the order A (3), B (1), Unassigned (1), sorted by count descending.
2. **Given** a system Manager or Admin with zero assigned open items in this project, **When** the workload panel is shown, **Then** that Manager/Admin still appears with a zero count.
3. **Given** a project where every open item has an assignee, **When** the workload panel is shown, **Then** no "Unassigned" row appears.
4. **Given** the workload panel, **When** counting open items per user, **Then** only items not in a Done-category status are counted.

---

### User Story 4 - Activity log captures changes and feeds the project Summary (Priority: P1)

As any signed-in user, I see a recent activity feed on the Summary tab — the latest entries from a newly introduced project Activity Log, newest first, in readable sentences with a relative timestamp ("Jane changed Task 'Fix login' status from To Do to In Progress — 6 minutes ago") — so I can see what's been happening without digging through items. Entries are created automatically whenever a work item is created or one of Status, Priority, Assignee, or Sprint changes.

**Why this priority**: This is the feature's namesake new capability — without an Activity Log, "recent activity" has no real data. It is independently testable: perform a status change, then confirm an entry appears in the feed, without needing the stat cards, charts, or workload panel to exist.

**Independent Test**: Change a work item's status via the existing edit flow. Confirm a new activity entry is created in the same transaction as the change and appears at the top of the project's Summary activity feed as a readable sentence with a relative timestamp and the actor's name, without requiring any new UI to create the entry itself.

**Acceptance Scenarios**:

1. **Given** a work item is created, **When** the creation succeeds, **Then** a new activity entry is recorded capturing the item (id + title), the actor, and the creation event.
2. **Given** an existing work item, **When** its Status, Priority, Assignee, or Sprint changes, **Then** a new activity entry is recorded capturing the field name, the old value, and the new value using display names (e.g. status names, not internal IDs).
3. **Given** an existing work item, **When** a field other than Status/Priority/Assignee/Sprint changes (e.g. title, description, dates, labels, hierarchy), **Then** no activity entry is recorded for that change.
4. **Given** a work item update that changes both a tracked field and a non-tracked field in one save, **When** the update succeeds, **Then** only the tracked field change produces an activity entry.
5. **Given** a change to a tracked field, **When** the underlying save fails, **Then** no activity entry is recorded (the entry and its change are written in the same transaction — no orphaned entries, no un-logged changes).
6. **Given** a project's Summary tab, **When** the activity feed is shown, **Then** it lists the latest entries for that project only, newest first, each as a one-line human-readable sentence with a relative timestamp and the actor's name.
7. **Given** more entries exist than fit on one page, **When** the user requests more, **Then** additional older entries load (pagination or "load more"), still newest first.
8. **Given** a work item is later deleted, **When** its prior activity entries are viewed, **Then** they remain visible using the captured title snapshot rather than erroring or disappearing.
9. **Given** any activity entry, **When** any API is used, **Then** the entry cannot be edited or deleted (no such capability is exposed).
10. **Given** two different projects, **When** each Summary tab's feed is viewed, **Then** each shows only entries for work items that belonged to its own project at the time of the change.

---

### User Story 5 - See a work item's own activity history (Priority: P3)

As any signed-in user viewing a work item's detail page, I can see that item's own activity history — a filtered view of the same Activity Log — so I can see its change history specifically.

**Why this priority**: Reuses the log and rendering built for US4; it is a smaller, additive surface and reasonably deferred without blocking the Summary tab's core value.

**Independent Test**: Make two tracked changes to one work item (e.g. change assignee, then priority). Open that item's detail page and confirm both entries appear in its activity section, filtered to that item only, in the same relative-timestamped sentence format and consistent ordering as the project feed.

**Acceptance Scenarios**:

1. **Given** a work item with three tracked changes in its history, **When** its detail page is opened, **Then** its activity section shows exactly those three entries, rendered the same way as the project feed (readable sentence, relative timestamp, actor name).
2. **Given** a work item's activity section and the project Summary feed, **When** both are compared, **Then** they use a consistent ordering (both newest-first).
3. **Given** two different work items in the same project, **When** each item's activity section is viewed, **Then** each shows only entries for that specific item.

---

### Edge Cases

- A brand-new project with zero work items: stat cards show zeros, breakdown charts show all-zero/empty states, workload panel shows only zero-load Managers/Admins (or an empty state), and the activity feed shows an empty state — none of these error.
- An item's due date is exactly at the 7-day boundary: it is treated as within the due-soon window (inclusive).
- An item becomes Done on the same day it was due soon: it is excluded from the Due Soon count and from open-item workload counts from that point on.
- A work item is moved between projects (if supported elsewhere in the system): activity entries already recorded keep the project they were scoped to at the time of the change; they do not retroactively move.
- A Sprint field changes to "no sprint" (removed from a sprint): the entry records the prior sprint's name as the old value and a clear "None"/"Backlog" as the new value (and vice versa when assigned into a sprint from none).
- A user who is the actor of a change is later deactivated or deleted (if applicable elsewhere in the system): their name snapshot in past entries remains readable rather than breaking the sentence.
- Two tracked-field changes happen in rapid succession on the same item: both produce distinct, correctly ordered entries.
- A project with only one workflow column (e.g. everything in one status): the status breakdown still renders correctly with a single segment.

## Requirements *(mandatory)*

### Functional Requirements

**Layout & navigation**
- **FR-001**: System MUST add a Summary tab to every project's view, positioned first, ahead of Board, Backlog, List, and Tree.
- **FR-002**: System MUST show the Summary tab by default when a project is opened without an explicit view specified; opening a project via an explicit view link MUST continue to show that explicit view unchanged.
- **FR-003**: System MUST refresh Summary data at least on tab (re)navigation; real-time/live-push updates are not required.

**Stat cards**
- **FR-004**: System MUST compute, per project, from current data: Total work items, Completed count and percent, In Progress count, and Due Soon count (due within the next 7 days, inclusive, and not in a Done-category status).
- **FR-005**: Completed/Done determination MUST use the project's own workflow column categories (per Feature 006), not a fixed status name match.
- **FR-006**: Stat card totals MUST include Epics.

**Status & priority breakdowns**
- **FR-007**: System MUST render a status breakdown using the requesting project's own workflow columns, in that project's configured order, using each column's configured chip color, with a count per column.
- **FR-008**: System MUST render a priority breakdown across all four levels (Low, Medium, High, Critical), always showing all four even when a level's count is zero.

**Team workload**
- **FR-009**: System MUST list, per project, one row per user who either has at least one open (not-Done) item assigned in that project, or holds the Manager or Admin role (shown even with zero assigned open items).
- **FR-010**: System MUST include an "Unassigned" row when at least one open item in the project has no assignee.
- **FR-011**: Workload rows MUST be sorted by open-item count descending.

**Activity Log (new capability)**
- **FR-012**: System MUST record a new, append-only activity entry whenever a work item is created, capturing the item's id and title, the actor, and a creation marker, with a timestamp.
- **FR-013**: System MUST record a new activity entry whenever an existing work item's Status, Priority, Assignee, or Sprint changes, capturing: the work item (id + title), the actor, the changed field name, the old value, the new value (old/new expressed using display names, not internal IDs), and a timestamp.
- **FR-014**: System MUST NOT record an activity entry for changes to any other work item field (title, description, dates, labels, hierarchy) or for creation/edit/deletion of a Project entity itself.
- **FR-015**: Each activity entry MUST be scoped to a project, derived from the work item's project at the time of the change.
- **FR-016**: Writing an activity entry MUST occur in the same transaction as the change it records: a change is never persisted without its corresponding entry, and an entry is never persisted without its corresponding change succeeding.
- **FR-017**: Activity entries MUST be immutable: no API MUST expose the ability to edit or delete an existing entry.
- **FR-018**: If a work item is later deleted, its previously recorded activity entries MUST remain accessible, displaying the captured title snapshot rather than erroring or disappearing.

**Activity feed & history surfaces**
- **FR-019**: The project Summary tab MUST show the latest activity entries for that project, newest first, each rendered as a one-line human-readable sentence including the actor's name, what changed, and a relative timestamp (e.g. "6 minutes ago").
- **FR-020**: The project Summary activity feed MUST support viewing more than the initial page of entries (pagination or "load more"), preserving newest-first order.
- **FR-021**: A work item's detail page MUST show that item's own activity history, filtered to entries for that item, rendered with the same sentence format and consistent ordering (newest-first) as the project feed.
- **FR-022**: All Summary and activity data MUST be scoped to the requesting user's existing project visibility/access — no new permission model is introduced by this feature.

### Key Entities *(include if feature involves data)*

- **Activity Log Entry**: An immutable, append-only record of one tracked change. Attributes: the work item it refers to (captured id and title snapshot, independent of later deletion/rename), the project it is scoped to, the actor (user who made the change), the kind of event (creation, or a specific field change), for field changes: the field name plus old and new display values, and a timestamp. Entries are never updated or removed once written.
- **Project Summary** (derived/computed view, not a stored entity): The aggregation, per project, of stat-card totals, status breakdown, priority breakdown, team workload, and the latest activity entries — computed from existing Work Item, Workflow Column, and User data plus the new Activity Log.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user opening any project sees its health summary (stat cards, breakdowns, workload, recent activity) without navigating away from the default landing tab.
- **SC-002**: Stat card figures (total, completed count/percent, in-progress, due-soon) match a manual count of the same project's current data in 100% of spot checks.
- **SC-003**: The status breakdown visually matches the project's own configured workflow columns (names, order, and colors) for 100% of projects checked, including projects with customized columns.
- **SC-004**: The priority breakdown always displays all four priority levels, regardless of whether any items exist at a given level.
- **SC-005**: Team workload correctly surfaces workload imbalance — the highest-loaded assignee always appears first, and a zero-load Manager/Admin or an "Unassigned" row is visible whenever applicable.
- **SC-006**: Every tracked change (creation, or a Status/Priority/Assignee/Sprint edit) made through existing flows results in a new, correctly worded activity entry visible within one tab refresh, with zero entries lost or duplicated across 50 consecutive tracked changes in a spot check.
- **SC-007**: A work item's own activity section always shows a subset of the same entries visible in its project's feed, filtered correctly to that item, with no cross-item leakage.
- **SC-008**: A second, unrelated project's Summary never shows another project's stat cards, breakdowns, workload, or activity entries.
- **SC-009**: All previously existing Board, Backlog, List, and Tree behavior (including explicit `?view=` links) remains unchanged after this feature ships.

## Assumptions

- The "recent activity" feed's page size (the "latest N entries") defaults to a reasonable count (e.g. 10-20) for the first page, with "load more"/pagination for older entries; the exact number is a presentation detail left to planning, not a behavioral requirement.
- Team workload ties (equal open-item counts) may break ties by any stable, deterministic order (e.g. name); no specific tie-break rule is required by the business.
- "Open" item, for workload and due-soon purposes, means any item not in a Done-category status for its project's workflow, consistent with the Completed calculation elsewhere in this feature and with Feature 006's status-category model.
- The Activity Log's write path is added to the existing work item creation and update flows (service-layer), not exposed as a separate user-facing "log an activity" action — there is no new UI for creating entries, per the feature description.
- "Due soon" windowing uses calendar days from the current server date; no user-configurable timezone handling beyond what the rest of the system already does for due dates.
- No new database-level permission model is introduced: Activity Log read access follows the same project-membership/visibility rules already enforced for Board/List/Tree/Backlog.
- A visual reference (Jira-style Summary tab screenshots) exists outside this spec and informs layout during planning/implementation, but this specification intentionally describes behavior, not pixel-level styling, per this product's own design system (consistent with how Feature 008 treated its visual reference).
- Sprint-specific analytics (burndown/velocity), cross-project dashboards, configurable/rearrangeable widgets, real-time push updates, and activity-log export/filtering are explicitly out of scope for this feature (see feature description) and are not covered by the requirements above.
