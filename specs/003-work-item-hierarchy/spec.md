# Feature Specification: Work Item Hierarchy

**Feature Branch**: `003-work-item-hierarchy`

**Created**: 2026-07-18

**Status**: Draft

**Input**: User description: "Give work items a parent-child hierarchy (Epic → Story → Task → Sub-task) so large work can be broken down and tracked as a tree. Feature 002 gave work items a Type label but no relationships between them. This feature adds parent/child structure and the rules that keep it sane, and is the foundation for the task-details view (subtasks tab), boards, and sprint planning in later features. The chain is strict and skips no levels: Epic is top-level only (never a parent of itself, never has a parent); Story's parent must be an Epic; Task's parent is optional but if set must be a Story (standalone Tasks remain legal); SubTask's parent is required and must be a Task. Parent and child must belong to the same project, an item cannot be its own ancestor, and an item's type cannot change if doing so would invalidate its existing parent or children. Deleting an item with children requires a confirmation stating how many descendants will be deleted, and deletes the whole subtree. Users need a parent picker limited to valid candidates, the ability to change/clear an existing parent, an indented tree view with expand/collapse and per-parent done/total counts on the project view, a work item detail view showing the parent as a link and direct children with status, and continued access to the flat filtered/searched list from Feature 002 regardless of tree position."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create a work item with a valid parent (Priority: P1)

As any signed-in user, when creating a work item I can pick a parent from a
list of valid candidates only — the form never offers an illegal parent
(wrong type for the chain, a different project, or an item that would create
a cycle) — so the hierarchy is correct by construction from the moment an
item is created.

**Why this priority**: This is the foundational capability the whole feature
exists to deliver. Without correct-by-construction parent assignment, no
other part of the hierarchy (tree view, detail navigation, cascade delete)
has trustworthy data to work with.

**Independent Test**: Can be fully tested by creating an Epic, then a Story
under it, then a Task under the Story, then a SubTask under the Task —
verifying each parent picker only lists legal candidates — and delivers a
working, valid four-level chain even with no tree visualization yet.

**Acceptance Scenarios**:

1. **Given** an existing Epic in a project, **When** a user creates a new
   Story and opens the parent picker, **Then** only Epics in that same
   project are offered as candidates.
2. **Given** an existing Story, **When** a user creates a new Task, **Then**
   the parent field is optional and, if used, only Stories in the same
   project are offered.
3. **Given** an existing Task, **When** a user creates a new SubTask,
   **Then** a parent is required and only Tasks in the same project are
   offered.
4. **Given** a user attempts to set an invalid parent by calling the API
   directly (wrong type, different project, or a cycle), **When** the
   request is submitted, **Then** it is refused with a clear error naming
   the specific rule that was violated.
5. **Given** work items created under Feature 002 before this feature
   shipped (all without a parent), **When** the system is queried after
   this feature ships, **Then** those items remain valid with no data
   migration required beyond adding the new parent field.

---

### User Story 2 - View the project's work items as an indented hierarchy tree (Priority: P1)

As any signed-in user viewing a project's work items, I can see the
hierarchy at a glance — children indented under their parents, with
expand/collapse per parent — while standalone items (no parent, no
children) list normally alongside them.

**Why this priority**: Creating relationships has no visible value until
users can see the structure they built. This is the other half of the MVP:
data integrity (User Story 1) plus visibility (this story) together make the
hierarchy usable and demonstrable.

**Independent Test**: Can be fully tested by opening a project that already
contains an Epic/Story/Task/SubTask chain (seeded via User Story 1 or the
API) and confirming the tree renders indentation, expand/collapse controls,
and correct child counts without needing any new items to be created during
the test.

**Acceptance Scenarios**:

1. **Given** a project with an Epic that has two Stories, **When** the
   project view loads, **Then** the Stories render indented beneath the
   Epic with an expand/collapse control on the Epic's row.
2. **Given** a parent with 5 direct children of which 3 are Done, **When**
   its row renders, **Then** it displays a count such as "3/5 done".
3. **Given** a project containing both hierarchical items and standalone
   items with no parent, **When** the tree view loads, **Then** standalone
   items list normally without being nested under anything.
4. **Given** a user collapses a parent row and reloads the page, **When**
   the tree view re-renders, **Then** the row may render expanded again
   (collapse state is not required to persist across reloads).
5. **Given** items at the same level under the same parent, **When** they
   render, **Then** they are ordered most-recently-updated first,
   consistent with Feature 002's existing ordering.

---

### User Story 3 - Navigate the tree from a work item's detail view (Priority: P2)

As any signed-in user, opening a work item I can see its parent (as a
navigable link, when one exists) and a list of its direct children with
their title, type, status, and assignee, so I can navigate the tree in both
directions from wherever I am.

**Why this priority**: This extends visibility (User Story 2) to the
single-item context and enables navigation, but the feature already
delivers value without it — it is a usability enhancement on top of the
tree view, not a precondition for it.

**Independent Test**: Can be fully tested by opening the detail page of an
item that has both a parent and children and confirming the parent link
navigates correctly and each child row links to that child's own detail
page.

**Acceptance Scenarios**:

1. **Given** a work item with a parent, **When** its detail view loads,
   **Then** the parent's title renders as a link that navigates to the
   parent's own detail view.
2. **Given** a work item with no parent, **When** its detail view loads,
   **Then** no parent link is shown.
3. **Given** a work item with direct children, **When** its detail view
   loads, **Then** each child is listed with title, type, status, and
   assignee, and each links to that child's detail view.
4. **Given** a work item whose type can legally have children (Epic,
   Story, or Task), **When** a user starts creating a child from that
   item's detail view, **Then** the new item's form pre-selects this item
   as the parent.

---

### User Story 4 - Reassign or clear an existing item's parent (Priority: P2)

As any signed-in user, I can change or clear an existing item's parent,
subject to the same hierarchy rules and the same edit permissions as
Feature 002, so I can reorganize work as plans change.

**Why this priority**: Reorganization is valuable but secondary to getting
the initial structure right (User Story 1) and being able to see it (User
Story 2) — teams can operate for a while by deleting and recreating items
before this becomes a blocker.

**Independent Test**: Can be fully tested by taking an existing Task with a
Story parent, changing its parent to a different Story in the same project
(should succeed), and attempting to change it to an Epic (should be
refused) — independent of any other feature capability.

**Acceptance Scenarios**:

1. **Given** a Task with a Story parent, **When** a user changes its
   parent to a different Story in the same project, **Then** the change
   succeeds and the tree reflects the new position.
2. **Given** a Task with a Story parent, **When** a user attempts to
   change its parent to an Epic, **Then** the change is refused with a
   clear error.
3. **Given** a Task with no parent, **When** a user clears the parent
   field on a form that already has none set, **Then** no error occurs and
   the item remains standalone.
4. **Given** an item whose type change would invalidate its existing
   parent or children (e.g., a Task with SubTask children being changed to
   a type that cannot have SubTask children), **When** the type change is
   attempted, **Then** it is refused with a clear error.
5. **Given** Feature 002's edit permission rules (creator / Manager /
   Admin), **When** a user without edit permission attempts to change an
   item's parent, **Then** the request is refused exactly as it would be
   for any other field edit.

---

### User Story 5 - Use the flat filtered list regardless of tree position (Priority: P3)

As any signed-in user, I can still use the flat filtered list from Feature
002 when I want it — filtering and searching return matching items
regardless of their position in the tree — so the hierarchy is additive and
never blocks existing workflows.

**Why this priority**: This is a non-regression guarantee rather than new
functionality — it matters, but it is the lowest-risk story since it
requires no new UI, only continued correctness of existing behavior.

**Independent Test**: Can be fully tested by running Feature 002's existing
filter/search scenarios against a project that now contains hierarchical
items and confirming results are unchanged in correctness, independent of
whether the tree view is used at all.

**Acceptance Scenarios**:

1. **Given** a project with a mix of parented and standalone items,
   **When** a user applies a status or type filter, **Then** matching
   items are returned regardless of their depth or position in the tree.
2. **Given** a search term that matches a child item's title, **When** the
   search runs, **Then** the child item appears in the flat results even
   though its parent doesn't match.
3. **Given** the flat filtered view is displayed, **When** it renders,
   **Then** it lists items without tree indentation, exactly as in Feature
   002.

### Edge Cases

- What happens when a user attempts to set an item as its own parent, or
  creates a longer cycle (A → B → A) across a chain of edits? The system
  MUST refuse the change and name the cycle as the violated rule.
- What happens when a user tries to change an Epic's type to Story after it
  already has Story children? The system MUST refuse the type change
  because it would invalidate the existing children relationship.
- What happens when a user deletes a Story that has a Task, and that Task
  has two SubTasks? The confirmation MUST state the total descendant count
  across all levels (e.g., "This will also delete 3 nested items"), and
  deleting MUST remove the entire subtree.
- What happens when a user without delete permission on a parent item
  attempts to delete it? The request MUST be refused per Feature 002's
  existing permission rules, independent of how many descendants exist.
- What happens to a pre-existing Feature 002 work item (created before this
  feature shipped, with no parent field) when this feature ships? It MUST
  remain valid and usable with no required data migration beyond adding the
  new column.
- What happens when a user tries to set a parent belonging to a different
  project? The system MUST refuse the assignment.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST treat Epic as a top-level type only — an Epic can
  never have a parent.
- **FR-002**: System MUST require a Story's parent, when set, to be an
  Epic.
- **FR-003**: System MUST treat a Task's parent as optional; when a parent
  is set, it MUST be a Story. Standalone Tasks with no parent remain valid.
- **FR-004**: System MUST require a SubTask to have a parent, and that
  parent MUST be a Task.
- **FR-005**: System MUST require a parent and child to belong to the same
  project.
- **FR-006**: System MUST refuse any parent assignment that would make an
  item its own ancestor (directly or transitively).
- **FR-007**: System MUST refuse a work item type change if the change
  would make its existing parent or existing children invalid under the
  hierarchy rules.
- **FR-008**: The parent picker's client-side candidate filtering (FR-010)
  MUST NOT be relied upon as the sole enforcement of FR-001 through FR-007
  — every hierarchy rule MUST be re-validated independently server-side on
  every request, so a request bypassing the picker entirely (e.g., a direct
  API call) is held to the same rules.
- **FR-009**: System MUST refuse an invalid parent assignment attempted via
  direct API call with an error response that names the specific rule
  violated, consistent with the project's existing error response format.
- **FR-010**: The work item creation and edit forms MUST offer a parent
  picker limited to valid candidates only: correct type for the chain, same
  project, and excluding the item itself and any of its own descendants.
- **FR-011**: Users MUST be able to change or clear an existing item's
  parent, subject to the same hierarchy rules as creation and the same edit
  permissions as Feature 002.
- **FR-012**: Pre-existing work items created before this feature (all
  without a parent) MUST remain valid without requiring any data migration
  beyond the addition of the new parent field.
- **FR-013**: The project work items view MUST render children indented
  beneath their parent, with an expand/collapse control per parent with
  children; collapse state is not required to persist across page reloads.
- **FR-014**: Each parent row in the tree view MUST display a count of its
  direct children and how many of them are in Done status (e.g., "3/5
  done").
- **FR-015**: Items within the same level of the tree MUST be ordered by
  most recently updated first, consistent with Feature 002's existing flat
  list ordering.
- **FR-016**: Standalone items (no parent, no children) MUST list normally
  in the project view alongside hierarchical items.
- **FR-017**: The work item detail view MUST show the item's parent as a
  navigable link when a parent exists, and show nothing in its place when
  it does not.
- **FR-018**: The work item detail view MUST list the item's direct
  children with title, type, status, and assignee, and each child MUST
  link to its own detail view.
- **FR-019**: Creating a new work item from a detail view MUST pre-select
  the current item as the parent when the current item's type can legally
  have children.
- **FR-020**: Deleting a work item that has one or more descendants MUST
  require a confirmation that states the total number of descendants
  (across all levels) that will also be deleted.
- **FR-021**: Deleting a work item with descendants MUST delete the entire
  subtree in one operation.
- **FR-022**: Delete permission checks (creator / Manager / Admin, per
  Feature 002) MUST apply to the item being deleted; the subtree is removed
  together with it under that same authorization check.
- **FR-023**: The flat filtered/searched work item list from Feature 002
  MUST continue to return matching items regardless of their position in
  the hierarchy.
- **FR-024**: All error responses introduced by this feature MUST use the
  same error response shape already established by Feature 002.

### Key Entities

- **Work Item**: The existing Feature 002 entity (Epic / Story / Task /
  SubTask), extended with a single optional reference to another work item
  in the same project representing its parent. A work item may have zero or
  more direct children. The parent reference is absent by default for all
  items created before this feature.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can build a complete four-level chain (Epic → Story →
  Task → SubTask) and see it rendered as a correctly indented tree with
  accurate child counts, with no manual data correction needed.
- **SC-002**: 100% of invalid parent-assignment attempts (wrong type for
  the chain, cross-project, or cycle-forming), whether from the guided
  picker or a direct API call, are rejected with an error identifying the
  violated rule.
- **SC-003**: A user can determine how many of an item's direct children
  are Done without opening any child item, directly from the parent's row
  in the tree view.
- **SC-004**: Every deletion of an item with descendants presents the exact
  descendant count before the deletion occurs, with zero instances of
  silent, uncounted data loss.
- **SC-005**: 100% of Feature 002's existing flat-list filter and search
  scenarios continue to pass unchanged after this feature ships.
- **SC-006**: Existing work items created prior to this feature remain
  fully usable immediately after deployment, with no manual migration step
  required from users or operators beyond the schema change itself.

## Assumptions

- The hierarchy has exactly four fixed levels (Epic, Story, Task, SubTask)
  matching Feature 002's existing type values; introducing custom levels or
  renaming types is out of scope.
- Reordering siblings, manual sort order, and moving an item between
  projects are out of scope for this feature.
- Progress roll-up beyond a parent's direct-children "n/m done" count
  (e.g., a recursive completion percentage on an Epic) is out of scope.
- Drag-and-drop tree manipulation is out of scope; parent changes happen
  through the existing edit form.
- Edit and delete permissions for a work item are unchanged from Feature
  002 (creator / Manager / Admin) and are not expanded or narrowed by the
  introduction of parent/child relationships.
