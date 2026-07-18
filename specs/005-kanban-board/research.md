# Phase 0 Research: Kanban Board

No `[NEEDS CLARIFICATION]` markers remain in the spec — `feature-05-kanban-
board.md` resolved scope, permissions, and persistence questions up front.
This phase covers implementation-approach decisions, grounded in the actual
backend/frontend code (verified via two research passes during spec-writing
and planning), not assumptions.

## 1. Adding `InReview` requires no EF Core migration

**Finding**: `backend/TaskFlow.Api/Data/AppDbContext.cs`'s `WorkItem` entity
configuration has:
```csharp
entity.Property(w => w.Status).HasConversion<string>();
```
No `HasMaxLength`, no check constraint, nothing else touching `Status`
anywhere in the file. The column is a plain `nvarchar` accepting any string
the C# `WorkItemStatus` enum can produce.

**Decision**: Add `InReview` to the enum
(`backend/TaskFlow.Api/Data/Entities/WorkItem.cs`) between `InProgress` and
`Done`. No migration file is created — there is no schema change to
capture. This is called out explicitly (rather than silently skipped) to
satisfy Constitution Principle V's migration-discipline expectation: the
absence of a migration here is a verified fact about the existing schema,
not an oversight.

**Alternatives considered**: Storing status as an `int` with explicit
values and a migration to "insert" `InReview` at a specific ordinal —
moot, since the column is already string-converted; this concern (ordinal
shift affecting existing rows) simply doesn't apply here.

## 2. Board data: one new endpoint, not a reuse of the tree or flat-list endpoints

**Finding**: Neither existing read endpoint fits the board's needs.
`GET .../work-items` (flat, paginated) returns `WorkItemDto`, which lacks
child-progress counts. `GET .../work-items/tree` returns nested
`WorkItemTreeNodeDto` (which *does* have `DirectChildrenCount`/
`DirectChildrenDoneCount`) but lacks `DueDate`, and is hierarchically
nested rather than the flat, status-grouped shape the board needs (FR-008
requires every item, at every depth, to appear as its own card).

**Decision**: A new endpoint, `GET api/projects/{projectId}/work-items/
board`, returns `WorkItemBoardDto { Columns: BoardColumnDto[], Items:
WorkItemBoardCardDto[] }` — one query pass over all of a project's work
items (same unpaginated, whole-project-at-once approach `GetTreeAsync`
already uses at this scale, per that method's own research-backed
rationale), computing each item's direct-children-done count with the same
`Dictionary`/`GroupBy`-by-`ParentWorkItemId` shape `GetTreeAsync` already
uses — applied to every item, not just roots. `Columns` is the ordered
column list (`[{status: "ToDo", label: "To Do"}, ...]`) embedded in the
same response, since the board always needs both together in one request.

**Revised during triage — column labels ARE sent by the backend, not
derived client-side.** The original version of this decision had `Columns`
as a bare `string[]` of status names, with the frontend deriving each
column's header text from `StatusChipComponent`'s own status→label map.
That's wrong for this feature's own stated forward-compat requirement
(FR-006: "the rendering approach MUST NOT assume that list is permanently
fixed, since a future feature makes it configurable per project"). If the
board component computes column labels itself from a closed, hardcoded
map of the four known statuses, Feature 006 can't actually just "change
what the backend returns" — a project with a custom-named column (e.g.
"Backlog" or "QA") would have no entry in that map, forcing a board-
component code change exactly when the point of this architecture was to
avoid one. Sending `{status, label}` pairs from the backend now — even
though today's `label` values are just formatted enum names computed
server-side — means the board component only ever does "render whatever
columns the response contains, in that order," and Feature 006 becomes a
pure backend change (real per-project persistence + custom labels) with
zero board-component edits.

This does introduce a small, deliberate duplication: today, the backend's
board-column labels and the frontend's `StatusChipComponent` label map
say the same English words ("To Do", "In Progress", ...) in two places.
That's an accepted tradeoff, not an oversight — the two concepts are not
actually the same thing long-term. A status *chip* always represents one
of the fixed `WorkItemStatus` enum values and will always need an English
label for it, everywhere that status renders as a chip (tree rows, flat
rows, detail pages) — that's permanent, chip-specific display logic that
has nothing to do with board columns. A board *column*, per this
feature's own explicit premise, is the thing that becomes arbitrary and
per-project in Feature 006. Coupling column-label rendering to whatever
the backend returns now avoids a real, larger problem later; the minor
text duplication today is the cheaper side of that trade.

**Alternatives considered**: Extending `WorkItemDto`/the flat list endpoint
with child-progress counts and an "unpaginated" query flag — rejected;
Constitution Principle IV mandates pagination on list endpoints from day
one, and overloading one endpoint with a paginated/unpaginated mode is
more confusing than a second, purpose-built endpoint (the same reasoning
that already justifies the tree endpoint's existence alongside the flat
one). Flattening the tree endpoint's nested response client-side —
rejected, since it's still missing `DueDate` and requires the frontend to
walk a tree to reconstruct a flat list it didn't need in tree form.

## 3. Status changes: a new field-scoped `PATCH`, not a reuse of the full `PUT`

**Finding**: The existing `PUT api/work-items/{id}` (`WorkItemService.
UpdateAsync`) is a full-record update — it expects type, title,
description, priority, status, assignee, due date, and parent all
together. `WorkItemBoardCardDto` (see #2) deliberately does not carry
`Description`/`ParentWorkItemId`, since the card never displays them.

**Decision**: A new endpoint, `PATCH api/work-items/{id}/status`, accepting
only `{ Status: string }`. `WorkItemService` gets a new `UpdateStatusAsync`
method that reuses the *same* authorization check `UpdateAsync` already
runs (creator, current assignee, Manager, or Admin) — extracted into one
shared private method (`EnsureCanEdit`) both call, so the rule is written
once, not duplicated between the two update paths.

**Rationale**: If the board instead reused the full `PUT` endpoint, the
frontend would have to submit a complete `WorkItemRequest` reconstructed
from the board card's partial data on every drag — omitting
`description`/`parentWorkItemId` in that request would silently clobber
them on save (they'd be sent as unset and overwrite the existing values).
A field-scoped `PATCH` makes that class of bug structurally impossible
instead of relying on the frontend to remember to carry every field
forward correctly.

**Alternatives considered**: Fetching the full `WorkItemDetail` immediately
before submitting a `PUT` on every drag (extra round trip per drag,
following the existing "fetch fresh right before a mutating action"
pattern used elsewhere for delete confirmations) — rejected as slower and
still more request payload than necessary for what is conceptually a
single-field change.

## 4. `@angular/cdk/drag-drop` for the board's drag interaction

**Decision**: Use `DragDropModule` (`cdkDropListGroup` around the board,
one `cdkDropList` per column, `cdkDrag` per card, handling the move in
`(cdkDropListDropped)`). Confirmed unused anywhere in the frontend today —
this feature is its first consumer — but it ships as part of `@angular/
cdk`, already a project dependency since Feature 004 (used there for
`BreakpointObserver`), so no new package is added.

**Rationale**: This is the Angular ecosystem's purpose-built tool for
exactly this interaction (connected drop lists, built-in keyboard support,
touch support), consistent with the constitution's "Angular Material/CDK
for drag-and-drop" line in the fixed Technology Stack section. Building a
custom drag implementation would be reinventing what CDK already solves
correctly.

**Alternatives considered**: A third-party Kanban/drag library —
rejected, unnecessary new dependency when CDK already covers this and is
already in the dependency tree for exactly this purpose per the
constitution's own stack notes.

## 5. Optimistic move + revert-on-failure, and where drag permission is enforced

**Decision**: On drop, the frontend immediately moves the card to the
target column's local list (optimistic), then calls the new
`updateStatus()` service method. On success, nothing further happens (the
optimistic state is now correct). On failure, the card moves back to its
original column using already-known client state — no refetch is needed
to know where it came from, since the move only ever happens within data
the board already loaded.

Separately, and *before* any drag can start: a card whose item the current
user cannot edit (per the shared `canEditWorkItem()` check) has dragging
disabled entirely (CDK's `cdkDragDisabled`), with a title/tooltip
explaining why. This satisfies the spec's "the attempt reverts with a
clear message" requirement more cleanly than allowing an ultimately-doomed
drag to start and then snapping back — there is nothing to revert if the
drag never begins. The revert-on-failure path (above) still exists
separately for the case a permitted drag's save genuinely fails (network
error, a concurrent server-side rejection, etc.) — that path is exercised
by a real failed `PATCH` regardless of the client-side permission gate.

**`canEditWorkItem()` extraction**: The creator/assignee/Manager/Admin
rule already exists as a private method in both `project-detail.component.
ts` (`canEdit()`) and `work-item-detail.component.ts` (`canEdit()`). The
board is a third call site for the identical rule. Extracting it now to
`frontend/src/app/projects/work-item-permissions.ts` as a plain, unit-
tested function — and updating the two existing components to call it
instead of keeping their own copies — is a justified consolidation at its
third use, not premature abstraction (Constitution Principle III).

**Alternatives considered**: Leaving the rule duplicated a third time —
rejected; three independently-maintained copies of a security-relevant
permission rule is a real drift risk, exactly the kind of duplication
Principle III's "no premature abstraction" carve-out isn't meant to
protect once a rule is genuinely reused, not just similar-looking.

## 6. Existing status arrays and the design system's exhaustive chip switch

**Decision**: `work-item-form.component.ts` and `project-detail.
component.ts` each keep their own local `STATUSES` constant (used for the
create/edit form's dropdown and the flat-view filter dropdown,
respectively) — both simply gain `'InReview'` in the correct position.
These do **not** fetch from the new board endpoint; they are unrelated,
already-existing, project-independent status lists, and fetching from a
project-scoped board endpoint just to populate a generic filter dropdown
would be new indirection with no benefit.

`StatusChipComponent`'s status→color mapping is an *exhaustive* TypeScript
`switch` over the `WorkItemStatus` union (see `frontend/src/app/shared/
status-chip/status-chip.component.ts`). Adding `'InReview'` to the union
type without adding a matching `case` is a compile error, not a runtime
gap — this is the concrete mechanism that makes FR-004's "every status
surface handles In Review correctly" verifiable for chips specifically,
beyond just careful review.

**New design token**: `--color-status-inreview-bg` / `--color-status-
inreview-text` added to `design-tokens.scss`, from the purple/violet
family already used for `--color-primary` (per the spec's explicit
"distinct hue... not reusing blue/green" direction) — a lighter tint than
the primary accent so it reads as a status color, not an action color.

## 7. "Open work item" count needs no code change

**Finding**: `ProjectService.cs`'s open-count computation is `p.WorkItems.
Count(w => w.Status != WorkItemStatus.Done)`. This already includes any
status other than `Done`, so `InReview` items are automatically counted as
open the moment the enum member exists — verified, not assumed.

**Decision**: No change to `ProjectService.cs`. A backend test is added
asserting an `InReview` item is counted as open, to guard this behavior
as a regression check (FR-004/SC-007), not because the production code
needs to change.
