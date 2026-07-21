# Feature Specification: Sprints & Backlog

**Feature Branch**: `008-sprints-backlog`

**Created**: 2026-07-21

**Status**: Draft

**Visual Reference**: [visual-reference.png](./visual-reference.png) — Backlog view layout reference (sprint sections stacked above an unassigned Backlog section, translated into this product's existing design-system tokens rather than the reference's exact styling).

**Input**: User description: "Add sprints to each project — a Backlog view for planning which work happens when, and a sprint-scoped Board for running the active sprint. A sprint belongs to one project and has a name (unique per project), a start/end date, and a status (Planned, Active, Completed). A project may have at most one Active sprint at a time. Starting a sprint requires at least one item and no other Active sprint. Completing a sprint requires resolving its not-Done items (to the backlog or another Planned/Active sprint); Done items stay attached for history and completed sprints become read-only. Only Epics are excluded from sprint assignment. A new Backlog view (alongside Board/List/Tree) lists sprints soonest-first, each with their items, followed by an unscheduled Backlog section, with drag-and-drop to move items between sections using the same permission and optimistic-update rules as the existing Board. The Board gains an 'Active sprint' toggle mode. Both Backlog and sprint-scoped Board show a days-remaining/overdue indicator for the Active sprint. Existing all-items Board/List/Tree behavior must remain unchanged."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create a sprint (Priority: P1)

As a Manager or Admin, I can create a sprint for a project with a name and a date range, so I can plan upcoming work before deciding what belongs in it.

**Why this priority**: Nothing else in this feature is possible without a sprint existing first. It is the smallest independently shippable slice and unlocks every other story.

**Independent Test**: As a Manager/Admin, open a project, create a sprint named "Sprint 1" with a start and end date, and confirm it appears (Planned status) in the project's sprint list. Can be fully tested without any drag-and-drop or Board changes.

**Acceptance Scenarios**:

1. **Given** a project with no sprints, **When** a Manager/Admin creates a sprint with a unique name (2-50 chars) and an end date after the start date, **Then** the sprint is created with status Planned and zero items.
2. **Given** a project that already has a sprint named "Sprint 1", **When** a Manager/Admin tries to create another sprint named "Sprint 1" in the same project, **Then** the creation is rejected with a clear error.
3. **Given** a project, **When** a Manager/Admin tries to create a sprint whose end date is on or before its start date, **Then** the creation is rejected with a clear error.
4. **Given** a signed-in user without Manager/Admin role, **When** they attempt to create a sprint (including via a direct API call), **Then** the request is rejected server-side regardless of what the UI shows.

---

### User Story 2 - View the Backlog (Priority: P1)

As any signed-in user, I can open a project's Backlog view and see its sprints (soonest-start first), each listing their planned items, plus an unscheduled Backlog section below, so I can see everything not yet Done organized by when it's planned.

**Why this priority**: This is the primary value delivered by the feature — a planning view. Without it, sprints exist but nobody can see or reason about the plan. It's independently testable against seeded sprints/items and does not require drag-and-drop or lifecycle actions to be useful (read-only viewing already delivers value).

**Independent Test**: With a project that has one Planned sprint containing two items and three unscheduled items, open the Backlog view and confirm the sprint section (with name, date range, item count) appears above a "Backlog" section listing the three unscheduled items, filterable the same way as the List view.

**Acceptance Scenarios**:

1. **Given** a project with two Planned sprints starting on different dates, **When** a user opens the Backlog view, **Then** the sprints are listed with the soonest start date first.
2. **Given** a project with items that have no sprint assigned, **When** a user opens the Backlog view, **Then** those items appear in a "Backlog" section beneath all sprint sections.
3. **Given** the Backlog view, **When** a user applies a status, type, priority, assignee, or search filter, **Then** both sprint sections and the Backlog section are filtered consistently with how List view filtering behaves.
4. **Given** a project with an Epic that has no sprint, **When** a user opens the Backlog view, **Then** the Epic is visible for context but is not offered as something that can be assigned to a sprint.
5. **Given** a project with a Done item still attached to a Completed sprint, **When** a user opens the Backlog view, **Then** that item still appears under the Completed sprint's section (for history).
6. **Given** a Planned or Active sprint's section with zero items, **When** a user views it, **Then** an empty-state hint guides them to plan the sprint by moving items into it.
7. **Given** a user with item-creation rights, **When** they use the "Create" action on a sprint section or the Backlog section, **Then** the new item is created already assigned to that section (that sprint, or unassigned for the Backlog section).

---

### User Story 3 - Move items between Backlog and sprints (Priority: P2)

As any signed-in user with edit rights on an item, I can move a work item into a sprint or back to the backlog from the Backlog view (drag-and-drop, consistent with the Board's drag interaction), so planning is fast.

**Why this priority**: This turns the Backlog from a read-only report into a planning tool, but the view itself (US2) already delivers standalone value, so this layers on top.

**Independent Test**: With one Planned sprint and one unscheduled Story, drag the Story from the Backlog section into the sprint section and confirm its sprint assignment updates and persists on reload; drag it back out and confirm it returns to the Backlog section.

**Acceptance Scenarios**:

1. **Given** a Story with no sprint and edit rights on it, **When** the user drags it from the Backlog section into a Planned sprint's section, **Then** the item's sprint assignment updates immediately (optimistic) and persists.
2. **Given** an item assigned to a sprint, **When** the user drags it from that sprint's section into the Backlog section, **Then** its sprint assignment is cleared.
3. **Given** an item assigned to one Planned/Active sprint, **When** the user drags it into a different Planned/Active sprint's section, **Then** its sprint assignment updates to the new sprint.
4. **Given** a user without edit rights on an item, **When** they attempt to drag it, **Then** the move is not permitted and no change occurs.
5. **Given** a drag-and-drop move that fails server-side (e.g., network error, permission change mid-flight), **When** the failure response is received, **Then** the item visually reverts to its prior section (same revert behavior as the Board's drag).
6. **Given** an Epic shown in the Backlog view, **When** a user attempts to drag it into a sprint section, **Then** the drag is not permitted.
7. **Given** a Completed sprint's section, **When** a user attempts to drag an item into or out of it, **Then** the move is not permitted because the sprint is read-only.

---

### User Story 4 - Sprint lifecycle: start and complete (Priority: P2)

As a Manager or Admin, I can start a Planned sprint (making it Active) and later complete an Active sprint, choosing where its remaining open items go, so the sprint lifecycle progresses.

**Why this priority**: This is what makes "Active sprint" meaningful for the Board toggle (US5), but planning (US2/US3) can be demonstrated and is useful before any sprint is ever started.

**Independent Test**: With a Planned sprint containing one item, start it and confirm its status becomes Active; then complete it, choosing to move its remaining not-Done item to the backlog, and confirm the sprint becomes Completed and the item now has no sprint.

**Acceptance Scenarios**:

1. **Given** a Planned sprint with at least one item and no other Active sprint in the project, **When** a Manager/Admin starts it, **Then** its status becomes Active.
2. **Given** a Planned sprint with zero items, **When** a Manager/Admin attempts to start it, **Then** the action is rejected with a clear error.
3. **Given** a project that already has an Active sprint, **When** a Manager/Admin attempts to start a different Planned sprint, **Then** the action is rejected with an error naming the currently Active sprint.
4. **Given** an Active sprint with some Done and some not-Done items, **When** a Manager/Admin completes it and chooses "move to backlog" for the not-Done items, **Then** the sprint becomes Completed, its not-Done items are unassigned from any sprint, and its Done items remain attached to the Completed sprint.
5. **Given** an Active sprint with not-Done items, **When** a Manager/Admin completes it and chooses another Planned/Active sprint as the destination, **Then** those not-Done items are reassigned to the chosen sprint.
6. **Given** a Manager/Admin is completing a sprint, **When** the confirmation is shown, **Then** it states how many items are moving and to which destination before the action is confirmed.
7. **Given** an empty, never-started Planned sprint, **When** a Manager/Admin deletes it, **Then** it is removed.
8. **Given** a sprint that has been started (Active or Completed), **When** a Manager/Admin attempts to delete it, **Then** the deletion is rejected.

---

### User Story 5 - Sprint-scoped Board (Priority: P2)

As any signed-in user, I can switch the project's Board to show only the Active sprint's items (instead of everything), so my daily board matches what the team committed to.

**Why this priority**: This is the "running the sprint" half of the feature and depends on a sprint being startable (US4), but is a distinct, separately valuable UI capability.

**Independent Test**: With one Active sprint containing two items and three other items with no sprint, toggle the Board to "Active sprint" mode and confirm only the two sprint items appear with full drag/edit behavior; toggle back to "All items" and confirm all five reappear.

**Acceptance Scenarios**:

1. **Given** a project's Board, **When** a user toggles from "All items" to "Active sprint" while an Active sprint exists, **Then** only that sprint's items are shown, using the same columns, drag, and permission behavior as today.
2. **Given** a project with no Active sprint, **When** a user toggles the Board to "Active sprint" mode, **Then** a clear empty state explains no sprint is active and links to the Backlog to start one.
3. **Given** the Board in "All items" mode, **When** no toggle interaction occurs, **Then** its behavior is identical to the feature's pre-existing behavior.

---

### User Story 6 - Days remaining / overdue indicator (Priority: P3)

As any signed-in user, I can see at a glance how many days remain in the Active sprint (or that it's overdue) from both the Backlog and the sprint-scoped Board, so I know how the sprint's timeline is tracking.

**Why this priority**: Useful polish that improves the two views delivered by US2 and US5, but neither view is blocked on it — it's the smallest, most cosmetic addition.

**Independent Test**: With an Active sprint whose end date is 3 days from today, open the Backlog and confirm a "3 days remaining" indicator on its section; with an Active sprint whose end date is in the past, confirm an "overdue" indicator instead, and confirm both indicators also appear on the sprint-scoped Board.

**Acceptance Scenarios**:

1. **Given** an Active sprint whose end date is in the future, **When** a user views the Backlog or the sprint-scoped Board, **Then** the number of days remaining until the end date is shown.
2. **Given** an Active sprint whose end date is in the past, **When** a user views the Backlog or the sprint-scoped Board, **Then** an overdue indicator is shown instead of a days-remaining count.
3. **Given** a Planned or Completed sprint, **When** a user views the Backlog, **Then** no days-remaining/overdue indicator is shown for it (the indicator is Active-sprint-only).

---

### Edge Cases

- What happens if a Manager/Admin tries to create a sprint with a name that only differs from an existing sprint's name by case or whitespace within the same project? (Uniqueness check should treat these consistently with other unique-name checks already established in the system.)
- What happens when the last not-Done item is dragged out of an Active sprint manually before completion — does that block completion later? (No; completion still requires an explicit choice only when not-Done items exist at completion time.)
- What happens if two Manager/Admin users attempt to start two different Planned sprints in the same project at nearly the same time? (Server-side enforcement of "at most one Active sprint" must make one attempt fail regardless of UI timing.)
- What happens to an item that is deleted while assigned to a sprint? (Existing item-deletion rules apply; the sprint's item count simply reflects the remaining items.)
- What happens when a project has zero sprints? (Backlog view shows just the unscheduled Backlog section with no sprint sections.)
- What happens when a sprint's date range has already started or ended by the time it is started/completed relative to today's date? (Start/Complete are status transitions independent of the date range; the days-remaining/overdue indicator reflects the date range regardless of when the transition happened.)
- What happens when an Active sprint is completed while the Board is currently showing "Active sprint" mode for it? (The empty state defined in US5 Scenario 2 applies once there is no longer an Active sprint.)

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow Manager/Admin users to create a sprint for a project with a name (2-50 characters, unique within the project) and a start date and end date, where the end date is after the start date.
- **FR-002**: System MUST reject sprint creation, on the server, from any user who is not a Manager or Admin for that project, independent of what the client UI allows.
- **FR-003**: A newly created sprint MUST have status Planned and zero items.
- **FR-004**: System MUST allow Story, Task, and SubTask work items to be assigned to a sprint, and MUST NOT allow Epics to be assigned to a sprint.
- **FR-005**: System MUST enforce that a project has at most one sprint with status Active at any time.
- **FR-006**: System MUST allow a Manager/Admin to start a Planned sprint (transition to Active) only when it has at least one item and the project has no other Active sprint; otherwise the request MUST be rejected with an error, and when the rejection is due to an existing Active sprint, the error MUST name that sprint.
- **FR-007**: System MUST allow a Manager/Admin to complete an Active sprint (transition to Completed) only after resolving its not-Done items, choosing either "move to backlog" (clear sprint assignment) or "move to another Planned/Active sprint" as the destination for all of them.
- **FR-008**: System MUST leave a completed sprint's Done items attached to that sprint after completion, for history.
- **FR-009**: System MUST prevent further item-sprint-assignment changes via a Completed sprint (it is read-only from the sprint-relationship perspective) while still allowing normal item edits.
- **FR-010**: System MUST allow deletion of a sprint only when it is Planned, has never been started, and has zero items; sprints that have ever been Active MUST NOT be deletable.
- **FR-011**: System MUST provide a Backlog view per project, accessible alongside the existing Board/List/Tree views, that lists the project's sprints ordered by soonest start date first.
- **FR-012**: Each sprint section in the Backlog view MUST show the sprint's name, date range, item count, a days-remaining/overdue indicator when the sprint is Active, and a Start or Complete action appropriate to its current status (visible only to Manager/Admin).
- **FR-013**: The Backlog view MUST show an unscheduled "Backlog" section, below all sprint sections, listing items with no sprint assignment, and this section MUST support the same filters (status, type, priority, assignee, search) as the List view.
- **FR-014**: The Backlog view MUST render Epics for context but MUST NOT allow them to be dragged into any sprint section.
- **FR-015**: Users with edit rights on a work item MUST be able to drag it between the Backlog section and a Planned/Active sprint section, or between two Planned/Active sprint sections, to change its sprint assignment; users without edit rights MUST NOT be able to perform this action.
- **FR-016**: Sprint-assignment changes made via drag-and-drop in the Backlog view MUST apply an optimistic update that reverts on server rejection or failure, matching the existing Board's drag behavior.
- **FR-017**: The Board view MUST offer a toggle between "All items" (existing default behavior, unchanged) and "Active sprint" (showing only the project's current Active sprint's items, with the same columns, drag, and permission behavior as "All items" mode).
- **FR-018**: When a project has no Active sprint, the Board's "Active sprint" mode MUST show an empty state explaining that no sprint is active and linking to the Backlog view.
- **FR-019**: The sprint-scoped Board MUST show the same days-remaining/overdue indicator for the Active sprint as the Backlog view.
- **FR-020**: All existing Board/List/Tree behavior for "all items" (i.e., unaffected by this feature) MUST remain unchanged.
- **FR-021**: All sprint mutation endpoints (create, start, complete, delete) MUST enforce Manager/Admin authorization on the server; item sprint-assignment changes MUST follow the existing item-edit permission rules.
- **FR-022**: All sprint-related error responses MUST use the system's established ProblemDetails error shape.
- **FR-023**: Sprint status chips, ordering, and date presentation MUST follow the established design-system conventions used elsewhere in the product.
- **FR-024**: Each section in the Backlog view (a sprint section or the Backlog section) MUST offer a "Create" action that creates a new work item pre-assigned to that section (that sprint, or no sprint for the Backlog section), subject to the same item-creation rules and permissions used elsewhere in the product; Epics MUST NOT be creatable pre-assigned to a sprint section.
- **FR-025**: A sprint section with zero items MUST show an empty-state hint guiding the user to plan the sprint by moving items into it.
- **FR-026**: Each item row in the Backlog view MUST show its status, due date, and assignee inline, using the same chip and avatar conventions as the List view.

### Key Entities

- **Sprint**: Belongs to exactly one Project. Has a name (unique within its project), a start date, an end date (after start date), and a status (Planned, Active, or Completed). Has zero or more associated work items. At most one sprint per project may be Active at a time.
- **Work Item (existing entity, extended)**: Story, Task, and SubTask items gain an optional Sprint assignment (nullable — no sprint means "in the backlog"). Epics are never assigned to a sprint. An item's Done/not-Done state (existing status) determines its handling when its sprint is completed.
- **Project (existing entity)**: Owns zero or more Sprints, in addition to its existing work items.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A Manager/Admin can create a sprint, populate it with items from the backlog, and start it in a single Backlog-view session without navigating away from the view.
- **SC-002**: 100% of a project's not-Done items are assigned a valid destination (backlog or another sprint) whenever a sprint is completed — no item is ever left in an inconsistent or unresolved state.
- **SC-003**: At no point does a project have more than one Active sprint, verified across concurrent attempts to start a second sprint.
- **SC-004**: Users can identify, within a few seconds of opening either the Backlog or the sprint-scoped Board, how many days remain in the Active sprint or that it is overdue.
- **SC-005**: All pre-existing Board, List, and Tree view test scenarios (for "all items" behavior) continue to pass unmodified, confirming no regression from this feature.
- **SC-006**: Every drag-and-drop sprint-assignment change a user performs in the Backlog view either persists successfully or visibly reverts, with no case of a silently lost or duplicated assignment.

## Assumptions

- "Any signed-in user" for viewing the Backlog and switching Board modes means all authenticated roles (Developer, Manager, Admin) with existing project access — no new visibility restriction is introduced by this feature.
- Sprint name uniqueness is scoped per-project (the same sprint name may be reused across different projects).
- The Backlog view's filters (status, type, priority, assignee, search) reuse the same filtering behavior already established for the List view rather than introducing new filter types.
- "Started" for delete-eligibility purposes means the sprint has ever reached Active status, regardless of its current status — so a Completed sprint (which was necessarily Active at some point) is also non-deletable, consistent with "Completed sprints are kept for history."
- Editing a sprint's name or dates after creation is out of scope for this feature; a mis-created Planned sprint is deleted and recreated instead.
- The days-remaining/overdue calculation is based on the sprint's end date compared to the current date, consistent with day-granularity date handling used elsewhere in the product.
