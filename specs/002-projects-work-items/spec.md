# Feature Specification: Projects & Work Items

**Feature Branch**: `002-projects-work-items`

**Created**: 2026-07-17

**Status**: Draft

**Input**: User description: "Add projects and flat work items (tasks) to TaskFlow PMS so teams can organize and track their work. Projects are containers of work; work items (Epic/Story/Task/SubTask — a label only, no hierarchy yet) are the units of work inside them. Managers/Admins create and manage projects; any signed-in user can view projects, create work items, edit items they created/are assigned to (or any Manager/Admin can edit any item), and filter/search/paginate a project's item list. Full acceptance criteria supplied in feature-02-projects-workitems.md."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create a Project (Priority: P1)

As a Manager or Admin, I create a project with a name and description so my
team has a place to track work.

**Why this priority**: Nothing else in this feature is possible without a
project existing first — this is the foundation every other story builds on.

**Independent Test**: Sign in as a Manager, submit a unique project name and
description, and confirm the project appears in the project list showing its
name, creator, and creation date.

**Acceptance Scenarios**:

1. **Given** a signed-in Manager, **When** they submit a project with a
   unique 3–100 character name, **Then** the project is created, recorded
   with their identity and the current time as creator/created-date, and
   appears in the project list.
2. **Given** an existing project named "Website Redesign", **When** a
   Manager or Admin submits another project named "website redesign" (any
   case), **Then** the request is refused with "A project with this name
   already exists."
3. **Given** a signed-in Developer, **When** they attempt to create a
   project — whether through the UI or a direct API call — **Then** the
   attempt is refused.

---

### User Story 2 - View Projects and Their Work Items (Priority: P1)

As any signed-in user, I can see the list of all projects and open a
project to view its work items, so I know what work exists across the
company.

**Why this priority**: Paired with Story 1 as the minimum usable slice — a
project nobody can see or open delivers no value.

**Independent Test**: Sign in as any user (any role), view the project list,
and open a project to see its (possibly empty) work-item list.

**Acceptance Scenarios**:

1. **Given** at least one project exists, **When** any signed-in user views
   the project list, **Then** they see every project's name, creator,
   created date, and count of open (not-Done) work items, newest first,
   paginated.
2. **Given** a project with no work items, **When** any signed-in user opens
   it, **Then** they see an explicit "No work items yet" message.

---

### User Story 3 - Create a Work Item (Priority: P1)

As any signed-in user, I can create a work item inside a project — with
type, title, description, priority, status, optional assignee, and optional
due date — so the work is tracked.

**Why this priority**: This is the actual unit of work TaskFlow exists to
track; without it, projects are just empty containers.

**Independent Test**: Open an existing project and create a work item with a
title and a type, confirming it appears in that project's item list
immediately.

**Acceptance Scenarios**:

1. **Given** an open project, **When** any signed-in user submits a work
   item with a 3–200 character title and a type of Epic, Story, Task, or
   SubTask, **Then** the item is created with default priority Medium and
   default status ToDo if not specified, recorded with creator/created/
   updated timestamps, and appears in the project's item list right away.
2. **Given** a work item being created, **When** an assignee is specified,
   **Then** the system accepts only an existing registered user as the
   assignee.
3. **Given** a work item being created, **When** a due date in the past is
   specified, **Then** the system accepts it (imported/overdue work is
   legitimate).

---

### User Story 4 - Edit a Work Item and Update Its Status (Priority: P2)

As the creator or current assignee of a work item, or as a Manager/Admin, I
can edit its fields and change its status, so the item reflects reality.

**Why this priority**: Depends on Story 3's items existing; keeping status
current is essential but comes after the ability to create work at all.

**Independent Test**: As the item's creator, change its status from ToDo to
Done via the edit form and confirm the change is reflected in the item list.

**Acceptance Scenarios**:

1. **Given** a work item, **When** its creator, its current assignee, or a
   Manager/Admin edits any of its fields (including status), **Then** the
   change is saved and its last-updated timestamp advances.
2. **Given** a work item, **When** a signed-in user who is none of its
   creator, its current assignee, nor a Manager/Admin attempts to edit it —
   by UI or direct API call — **Then** the attempt is refused and the
   corresponding edit controls are not shown to them.
3. **Given** a work item, **When** anyone (its creator, assignee, or a
   Manager/Admin) attempts to change which project it belongs to, **Then**
   the system does not allow it — a work item's project is fixed at
   creation.

---

### User Story 5 - Edit or Delete a Project (Priority: P2)

As a Manager or Admin, I can edit or delete a project. Deleting a project
deletes its work items, after an explicit confirmation that states how many
items will be removed.

**Why this priority**: Full project lifecycle management, needed for
ongoing use but less urgent than the create → view → work loop above.

**Independent Test**: As a Manager, delete a project containing several work
items, confirm the count shown in the confirmation matches, and verify both
the project and its items are gone afterward.

**Acceptance Scenarios**:

1. **Given** a project with 12 work items, **When** a Manager or Admin
   requests to delete it, **Then** they see a confirmation stating "This
   will also delete 12 work items."
2. **Given** that confirmation, **When** the Manager or Admin confirms,
   **Then** the project and all 12 of its work items are removed, and the
   project no longer appears in the project list.
3. **Given** a signed-in Developer, **When** they attempt to edit or delete
   a project — by UI or direct API call — **Then** the attempt is refused.

---

### User Story 6 - Filter, Search, and Page Through Work Items (Priority: P3)

As any signed-in user, I can filter and search a project's work items (by
status, type, priority, assignee, and title text) and page through long
lists, so I can find things quickly.

**Why this priority**: An efficiency/discoverability enhancement on top of
the baseline viewing capability from Story 2 — valuable once item volume
grows, but not blocking core value.

**Independent Test**: In a project with items of mixed priority, filter to
"High" priority and confirm only High-priority items appear; then search a
title substring and confirm only matching items appear.

**Acceptance Scenarios**:

1. **Given** a project with items of varying status, type, priority, and
   assignee, **When** a user applies any combination of those filters,
   **Then** only items matching every applied filter are shown.
2. **Given** a project's work items, **When** a user searches a substring of
   a title (any case), **Then** only items whose title contains that
   substring are shown.
3. **Given** a filter or search that matches nothing, **When** it is
   applied, **Then** the system shows "No items match your filters."
4. **Given** a project with more items than fit on one page, **When** a user
   pages through the list (default page size 20), **Then** each page shows
   the correct slice, sorted by most recently updated first by default.

---

### Edge Cases

- What happens when two people edit the same work item at nearly the same
  time? The later save wins; this feature does not detect or warn about the
  conflict (no optimistic concurrency in this slice).
- What happens when a `pageSize` beyond the maximum (100) is requested? The
  system clamps it to 100 rather than rejecting the request.
- What happens when someone tries to view, edit, or delete a project or
  work item that no longer exists (e.g., deleted by someone else
  moments earlier)? The request is refused as not found.
- What happens when a work item's current assignee is also its creator, and
  a third-party Manager reassigns it to someone else? The original
  creator retains edit rights (as creator); the new assignee gains them too.

## Requirements *(mandatory)*

### Functional Requirements

#### Projects

- **FR-001**: System MUST allow a Manager or Admin to create a project with
  a name (required, 3–100 characters) and an optional description (up to
  2000 characters).
- **FR-002**: System MUST reject a project name that duplicates an existing
  project's name case-insensitively, with the message "A project with this
  name already exists."
- **FR-003**: System MUST record the creator and creation timestamp for
  every project.
- **FR-004**: System MUST refuse project creation, edit, and deletion for
  any user who is not a Manager or Admin, regardless of whether the attempt
  is made through the UI or a direct API call.
- **FR-005**: System MUST allow any authenticated user to view the list of
  all projects and open any project to view its work items.
- **FR-006**: System MUST show, for each project in the list, its name,
  creator, created date, and a count of its open (not-Done) work items.
- **FR-007**: System MUST sort the project list by most recently created
  first, and MUST paginate it.
- **FR-008**: System MUST require an explicit confirmation before deleting a
  project, and that confirmation MUST state the number of work items that
  will be deleted along with it.
- **FR-009**: System MUST delete all of a project's work items when the
  project itself is deleted.

#### Work Items

- **FR-010**: System MUST allow any authenticated user to create a work item
  within an existing project, specifying: type (Epic, Story, Task, or
  SubTask), title (required, 3–200 characters), description (optional, up
  to 5000 characters), priority (Low, Medium, High, or Critical; default
  Medium), status (ToDo, InProgress, or Done; default ToDo), an optional
  assignee, and an optional due date.
- **FR-011**: System MUST accept only an existing registered user as a work
  item's assignee, when one is set.
- **FR-012**: System MUST accept any due date, including dates in the past.
- **FR-013**: System MUST record, for every work item, its owning project,
  its creator, its creation timestamp, and its last-updated timestamp.
- **FR-014**: System MUST NOT allow a work item's project to change after
  creation.
- **FR-015**: System MUST allow a work item's creator, its current assignee,
  or any Manager or Admin to edit any of its fields, including its status.
- **FR-016**: System MUST refuse an edit attempt by any user who is not the
  work item's creator, its current assignee, or a Manager/Admin — both at
  the API and by hiding or disabling the corresponding controls in the UI.
- **FR-017**: System MUST allow a work item's creator, or any Manager or
  Admin, to delete it, after a confirmation step.
- **FR-018**: System MUST refuse a delete attempt by any user who is not the
  work item's creator or a Manager/Admin.

#### Listing, Filtering, and Search

- **FR-019**: System MUST paginate a project's work-item list, defaulting to
  a page size of 20 and never exceeding a page size of 100.
- **FR-020**: System MUST allow filtering a project's work items by status,
  type, priority, and assignee, individually or in any combination.
- **FR-021**: System MUST allow a case-insensitive substring search of a
  project's work items by title.
- **FR-022**: System MUST sort a project's work-item list by most recently
  updated first by default.
- **FR-023**: System MUST show an explicit "No work items yet" message for a
  project with no items, and a distinct "No items match your filters."
  message when a filter or search yields no matches.

#### Non-Functional

- **FR-024**: System MUST require authentication for every endpoint
  introduced by this feature.
- **FR-025**: System MUST enforce every role-based rule in this feature on
  the server, independent of any client-side restriction.
- **FR-026**: System MUST present every error response introduced by this
  feature in the same consistent shape already established by the system.
- **FR-027**: System MUST reflect a create, edit, or delete action in the
  lists and counts a user sees immediately afterward, without requiring a
  manual refresh.

*Deferred to a future feature (explicitly out of scope for this feature)*:
parent/child hierarchy between work items and its validation rules; sprints,
backlog, and story points; a Kanban board with drag-and-drop (status changes
are form-based in this feature); comments, attachments, and an activity log;
project membership/teams and per-project permissions; moving a work item to
a different project; project editing/deletion by Developers; and real-time
(live-updating) views.

### Key Entities

- **Project**: A named container of work. Attributes: name (unique,
  case-insensitive), description, creator, created date. Owns a collection
  of Work Items; deleting a Project deletes all of its Work Items.
- **Work Item**: A single unit of trackable work belonging to exactly one
  Project. Attributes: type (Epic/Story/Task/SubTask — a label only, no
  hierarchy), title, description, priority, status, assignee (an existing
  User, optional), due date (optional), creator, created date, last-updated
  date. Belongs to exactly one Project for its entire lifetime.
- **User** *(from Feature 001, not redefined here)*: Referenced as a work
  item's creator or assignee, and as the actor whose role (Developer /
  Manager / Admin) determines project- and work-item-level permissions.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A Manager can create a project and see it at the top of the
  project list in under 1 minute.
- **SC-002**: A user can create a work item in an existing project and see
  it appear in that project's item list immediately, with no manual page
  refresh.
- **SC-003**: 100% of project and work-item create/edit/delete attempts by a
  user without the required role or relationship (creator/assignee/
  Manager/Admin, as applicable) are refused, whether attempted through the
  UI or a direct API call.
- **SC-004**: Filtering or searching a project's work items returns only
  matching items, with zero false positives or false negatives, across
  every supported filter and combination of filters.
- **SC-005**: Deleting a project always shows an accurate item count in its
  confirmation, and after confirming, 100% of that project's work items are
  removed along with it.
- **SC-006**: Every project and work-item list a user views reflects the
  current state of the system immediately after any create, edit, or delete
  action they perform — no stale data is shown without a manual refresh.

## Assumptions

- Deleting a project is a permanent (hard) delete, not a soft-delete or
  archive — no feature to date has introduced soft-delete/archive
  conventions, and this feature's own acceptance criteria describe items
  being irreversibly "gone" after deletion.
- There is no conflict detection for two people editing the same work item
  at once (no optimistic concurrency) — this feature's scope is deliberately
  flat and simple; conflict handling can be added later if it proves
  necessary.
- "An existing, current user" for assignment purposes means any registered
  account, of any role — Feature 001 has no concept of deactivating or
  soft-deleting a user, so every registered account is inherently current.
- Company-wide visibility (no project membership or per-project permissions)
  is intentional for this feature, per the source requirements — a future
  feature may add team/membership scoping.
- This feature reuses Feature 001's authentication (JWT bearer) and
  role model (Developer/Manager/Admin) rather than introducing any new
  identity or permission concept.
- "Any signed-in user" creating or viewing work items includes Developers,
  Managers, and Admins alike — only project management (create/edit/delete)
  and the narrower work-item edit/delete rules are role- or
  relationship-restricted.
