# Phase 0 Research: Work Item Hierarchy

## §1 Self-referencing foreign key and cascade delete

**Decision**: Add `WorkItem.ParentWorkItemId` (`int?`, FK → `WorkItem.Id`, same table)
with `DeleteBehavior.Restrict` at the database level. Subtree deletion
(FR-020/FR-021) is implemented in `WorkItemService`, not via a database
cascade.

**Rationale**: SQL Server refuses to create a self-referencing foreign key
with `ON DELETE CASCADE` — it raises error 1785 ("may cause cycles or
multiple cascade paths") even for a single self-join, because the engine
cannot prove a cascade chain through the same table terminates. This is the
same class of constraint Feature 002 already hit with `Project → WorkItem`
vs. the two `WorkItem → User` foreign keys (`data-model.md` §"Authorization
derived..."), just triggered by a table referencing itself instead of two
paths converging on `User`. The fix is the same shape: keep the FK
`Restrict`, and perform the cascade in application code, where it can also
report the count the confirmation dialog needs (FR-020) — something a raw
DB cascade could never surface to the UI beforehand anyway.

**Alternatives considered**:
- Raw SQL recursive CTE (`WITH cte AS (...)`) to fetch descendants in one
  round trip. Rejected: constitution requires justification for raw SQL,
  and at this feature's scale (an internal PM tool, trees a handful of
  levels deep, tens of items per project) a small loop of EF Core queries
  is simpler to read and test, matching Principle III.
- `DeleteBehavior.ClientCascade`: makes EF Core load and delete the graph
  client-side automatically. Rejected: it requires the full descendant
  graph to already be loaded/tracked, and doesn't give an explicit point to
  compute the pre-delete confirmation count — an explicit service method
  reads clearer and does both jobs (research.md's Teach-While-Building
  angle: the maintainer sees the recursion instead of implicit framework
  behavior).

## §2 Cycle prevention is a free consequence of the strict type chain, not a separate algorithm

**Decision**: Do not implement a graph/ancestor-walk cycle check. Enforcing
FR-001–FR-004 (the strict Epic→Story→Task→SubTask parent-type rule, no
skipped levels) is sufficient by itself to make cycles structurally
impossible, which also satisfies FR-006 (no cycles) and the "not the item
itself or its own descendants" part of FR-010 with zero extra code.

**Rationale**: Assign each type a fixed rank — Epic=0, Story=1, Task=2,
SubTask=3. The chain rule requires every item's parent to be of the type
ranked exactly one below the item's own type (Epic itself has no parent at
all). Walking from any item up through parent references therefore
produces a strictly decreasing sequence of ranks, bounded below at 0
(Epic). A cycle would require the sequence to return to a rank it already
visited, which is impossible for a strictly decreasing bounded integer
sequence. The same argument covers "cannot be its own ancestor" (an item
can never share its own type with anything in its required-parent chain)
and "cannot pick a descendant as its parent" (a descendant's type is always
ranked at or below the item's own rank, never one rank above it, which is
the only rank a valid parent can have). Concretely: the parent-candidate
query (`type == requiredParentType AND projectId == item.ProjectId`) never
needs an "exclude self and descendants" clause — the type filter alone
already excludes every possible cycle.

**Alternatives considered**:
- Explicit ancestor-walk / visited-set check on every parent assignment.
  Rejected: redundant given the proof above, and constitution Principle III
  (Clarity Over Cleverness / avoid speculative generality) argues against
  writing and testing a traversal algorithm the type system already makes
  unreachable. If a future feature ever allows skipping levels or custom
  hierarchies, this proof breaks and the check would need to be added then
  — not before.

## §3 Type-change validation against existing relationships (FR-007)

**Decision**: When a `PUT` changes an item's `Type`, `WorkItemService`
checks two things before applying the change, in addition to the existing
parent/child rules:
1. If the item currently has a parent, the parent's actual type must still
   equal the *new* type's required parent type (or the item must have no
   parent, if the new type is Epic).
2. If the item currently has any children, every child's required-parent
   type must still equal the *new* type.

If either check fails, the update is refused with a `400` naming the
conflict (e.g., "Cannot change type: this item has SubTask children, which
require a Task parent.") — the same request that would otherwise silently
orphan a relationship instead does nothing.

**Rationale**: This is the one place the type chain's rank argument (§2)
doesn't self-enforce, because it's evaluated against relationships that
*already exist* under the *old* type, not new ones being created. It is a
direct implementation of FR-007 and the spec's own example (a Task with
SubTask children cannot become a Story).

**Alternatives considered**: Silently clearing the invalidated parent or
children on a type change. Rejected: the spec's edge case is explicit that
the change should be *refused*, not auto-repaired — auto-clearing would
silently destroy the user's existing structure, which is a worse outcome
than an error asking them to reorganize first.

## §4 Tree endpoint is a separate, unpaginated read model — not the existing paginated list

**Decision**: Add one new endpoint, `GET
/api/projects/{projectId}/work-items/tree`, returning the *entire*
project's hierarchy as nested nodes (top-level = items with no parent).
The existing paginated/filtered `GET /api/projects/{projectId}/work-items`
list endpoint (Feature 002) is unchanged and continues to back the flat
view (User Story 5).

**Rationale**: A tree and a page are different shapes of the same data —
pagination assumes a flat, independently-orderable sequence, which doesn't
compose cleanly with "show me this node's children indented beneath it."
At this feature's internal-tool scale (tens to low hundreds of items per
project, per Feature 002's plan.md Scale/Scope, unchanged here), returning
the whole tree in one response is simpler and cheaper to reason about than
inventing tree-aware pagination, consistent with Principle III (Clarity
Over Cleverness / YAGNI) — constitution's "pagination on all list
endpoints" guidance is aimed at endpoints that return a flat, growing
collection, which this tree endpoint deliberately isn't.

**Alternatives considered**: Reuse the flat list endpoint and build the
tree client-side from a fully-fetched, unpaginated page. Rejected: it
would require the frontend to special-case "fetch every page" just for the
tree view, and per-parent done/total counts (FR-014) are cheaper to compute
once, server-side, over the whole project than to reconstruct client-side
from a possibly-partial fetch.

## §5 Parent picker candidates are a small, dedicated lookup endpoint

**Decision**: Add `GET
/api/projects/{projectId}/work-items/parent-candidates?type=Story`,
returning only `{ id, title }` pairs for items of the required parent type
in that project. The frontend calls it whenever the create/edit form's
`Type` field changes, mirroring Feature 002's existing
`UserLookupItemDto`/non-Admin lookup pattern (`002.../research.md` §9) for
the assignee picker.

**Rationale**: Same shape as an already-accepted precedent in this
codebase — a minimal, role-agnostic lookup DTO for populating a picker,
instead of overloading the full work-item list/detail response for this
purpose. Because of §2's proof, this query is just
`type == requiredParentType(selectedType) AND projectId == projectId` —
no exclusion logic needed.

## §6 Detail view composition: one enriched DTO for `GET /api/work-items/{id}`

**Decision**: Introduce `WorkItemDetailDto`, used only by `GET
/api/work-items/{id}`, adding four fields on top of the existing
`WorkItemDto` shape: `parentWorkItemId`, `parentTitle`,
`totalDescendantCount`, and `children` (a list of lightweight
`{id, title, type, status, assigneeName}` rows). `POST`/`PUT`'s response
and the flat list/tree endpoints keep using the existing, slimmer
`WorkItemDto` (extended only with `parentWorkItemId`, needed so the create/
edit form and tree-building logic know an item's parent).

**Rationale**: Directly mirrors Feature 002's own
`ProjectListItemDto`/`ProjectDetailDto` split — a slim shape for
listings, a richer shape for the single-item view that needs to support a
confirmation dialog (`totalDescendantCount`, exactly analogous to
`ProjectDetailDto.TotalWorkItemCount` for the project-delete confirmation)
and cross-navigation (`children`, `parentTitle`). Computing
`totalDescendantCount` here (rather than a separate endpoint) means the
detail page already has the number it needs before the user clicks delete,
with no extra round trip — the same "fetch what you'll need to confirm
with, up front" pattern Feature 002 established for project deletion.

**Alternatives considered**: A dedicated `GET
/api/work-items/{id}/descendant-count` endpoint, called right before
showing the delete confirmation. Rejected: an extra round trip for a value
cheaply computed alongside data the detail page loads anyway.

## §7 Ordering within tree levels

**Decision**: Both the tree endpoint's children arrays and the unchanged
flat list are ordered by `UpdatedAt` descending, matching Feature 002's
existing flat-list order (FR-015).

**Rationale**: One consistent rule, no new user-facing sort concept to
document or test.
