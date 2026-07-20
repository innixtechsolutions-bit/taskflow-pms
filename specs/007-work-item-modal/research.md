# Research: Work Item Modal & Quick Creation

Each item: Decision, Rationale, Alternatives considered. Grounded in the actual
current implementation (`backend/TaskFlow.Api/`, `frontend/src/app/`) as of
Feature 006, not general best practice.

## 1. Modal mechanism: Angular Material `MatDialog`

**Decision**: Use `MatDialog`/`MatDialogRef`/`MAT_DIALOG_DATA` for the new
`WorkItemModalComponent`. No `MatDialog` usage exists anywhere in this codebase
today (grep across `frontend/src/app` for `MatDialog|@angular/material/dialog`
returns zero matches) — this is the first dialog in the app — but
`@angular/material` is already a dependency (`frontend/package.json` →
`"@angular/material": "^22.0.4"`), so no new package is added.

**Rationale**: The constitution's Technology Stack section names Angular
Material as the fixed UI library; `MatDialog` is its standard modal primitive
and integrates with the existing `MatFormFieldModule`/`MatSelectModule`/
`MatDatepickerModule` already used by `work-item-form.component.ts`, so the
modal's field controls carry over unchanged.

**Alternatives considered**: A hand-rolled overlay (CDK `Overlay` directly, no
`MatDialog`) was rejected as unjustified complexity — `MatDialog` already
provides focus trapping, Escape-to-close, and backdrop handling, which a
hand-rolled overlay would have to reimplement (constitution Principle III).

## 2. Confirm-discard tracking: a single `dirty` flag, not value diffing

**Decision**: The modal tracks one boolean signal, `dirty`, set to `true` on
the first change to any field (title, description, type, priority, status,
assignee, dates, labels) after the modal opens (or after load, in edit mode).
Escape/close checks this flag; `true` triggers the existing app-wide
`confirm()` pattern (native `window.confirm`, already used by
`workflow.component.ts`, `project-detail.component.ts`, and
`work-item-detail.component.ts` for delete confirmations — no new component).

**Rationale**: A value-by-value diff against the initial snapshot would
correctly avoid prompting when a user changes a field and changes it back, but
that precision buys nothing a daily-use quick-entry modal needs and adds a
second code path (snapshot capture + deep comparison) purely to handle an edge
case nobody is likely to hit (constitution Principle III — no cleverness
without a concrete need). It also lets the confirm dialog reuse the exact
`confirm()` pattern already established for destructive actions, rather than
introducing this feature's own `ConfirmDialogComponent` on top of introducing
`MatDialog` itself.

**Alternatives considered**: Full value-diff dirty-checking (rejected, above);
a dedicated `MatDialog`-based confirm component (rejected — `window.confirm()`
is the codebase's one existing confirmation pattern; introducing a second,
dialog-based one in the same feature that introduces `MatDialog` for the first
time adds a decision the spec doesn't ask for).

## 3. Label storage: explicit join entity, not EF Core's implicit many-to-many

**Decision**: Two new tables/entities — `Label` (`Id`, `ProjectId`, `Name`,
`CreatedAt`) and an explicit join entity `WorkItemLabel` (`Id`, `WorkItemId`,
`LabelId`) — configured via `HasOne`/`WithMany` pairs, not EF Core's
`UsingEntity<>()` implicit many-to-many.

**Rationale**: This is the first many-to-many relationship in this codebase
(grepping the whole backend for `ICollection`/`HasMany`/`WithMany` today turns
up only one-to-many: `Project.WorkItems`, `Project.WorkflowStatuses`,
`WorkItem.Children`). An explicit join entity keeps the pattern visible and
teachable (constitution Principle VI) and gives a natural place to hang the
`(WorkItemId, LabelId)` unique index that both prevents duplicate attachment
and makes the 0–5 cap a simple `Count()` query — EF Core's implicit join table
has no entity to attach that index's intent-explaining comment to.

**Alternatives considered**: EF Core implicit many-to-many (`UsingEntity<>()`)
— more concise, but hides the join table behind generated code with no
comment surface, working against Principle VI for what is, in this codebase,
a first-of-its-kind relationship worth making legible.

## 4. Label uniqueness: SQL Server default collation, same as every other name field

**Decision**: `Label` gets `entity.HasIndex(l => new { l.ProjectId, l.Name }).IsUnique()`
— same mechanism as `WorkflowStatus`'s `(ProjectId, Name)` unique index,
`Project.Name`, and `User.Email` — relying on SQL Server's default
case-insensitive collation (`SQL_Latin1_General_CP1_CI_AS`), not a separate
normalized/lowercase column.

**Rationale**: Every existing case-insensitive-uniqueness rule in this
codebase already uses this mechanism; introducing a normalized-column
approach just for labels would be an inconsistent, unjustified alternative to
a pattern that already works and is already documented in `data-model.md` for
prior features.

## 5. Label suggestions/filter options: query-time filter, no orphan-cleanup bookkeeping

**Decision**: `GET /api/projects/{projectId}/labels` returns only labels that
currently have at least one `WorkItemLabel` reference
(`Labels.Where(l => l.ProjectId == projectId && l.WorkItemLabels.Any())`),
ordered by name. No code path ever deletes a `Label` row.

**Rationale**: The spec states "an unused label simply stops appearing in
suggestions when no items reference it" and explicitly leaves exact cleanup
mechanics to planning. Filtering at query time makes this true by construction
with a single `.Any()` predicate, with no need to detect "did this
update/delete just orphan a label" on every `UpdateAsync`/`DeleteAsync` call
and no risk of a race between "check if orphaned" and "delete" reads/writes.
This is simpler and strictly safer than reference-counting-and-deleting
(constitution Principle III).

**Alternatives considered**: Delete-on-last-reference-removed (rejected —
requires new bookkeeping in `UpdateAsync` *and* `DeleteAsync`, purely to save
a handful of unused rows in a table with a per-project row count that will
never be large); a soft-delete/`IsActive` flag on `Label` (rejected — same
bookkeeping cost as hard delete, more schema for no behavioral gain over the
`.Any()` filter).

## 6. Label representation in DTOs: plain name strings, not id-bearing objects

**Decision**: Every work-item DTO that carries labels exposes them as
`List<string>` (label names), not `List<LabelDto>`/`List<int>` ids. The new
`GET /api/projects/{projectId}/labels` suggestions/filter endpoint likewise
returns `List<string>`.

**Rationale**: v1 has no per-label color, rename, or id-scoped operation
(spec's Out of Scope list) — nothing on the frontend ever needs a label's id,
only its display text and its identity-by-name for attach/detach and
filtering. Exposing ids the frontend never uses would be speculative surface
area (constitution Principle III).

## 7. Start-date validation: new exception, same shape as every other `WorkItemService` rule

**Decision**: Add `InvalidDateRangeException` to `Services/WorkItemExceptions.cs`
("Start date must be on or before the due date."), thrown from `CreateAsync`/
`UpdateAsync` when both dates are present and `StartDate > DueDate`, caught in
`WorkItemsController`'s `Create`/`Update` actions and turned into a 400
`ProblemDetails` — the exact pattern every existing validation rule in this
service already follows (e.g. `InvalidWorkItemTypeException`,
`AssigneeNotFoundException`).

**Rationale**: `WorkItem` has no date-range validation today (`DueDate` is
copied from the request with no check at all) — this is genuinely new
territory, but the *shape* of "dedicated exception → controller catch →
`Problem(400, message)`" is fully established and should be reused rather than
inventing a different validation-reporting mechanism for one field.

## 8. "Create another" field retention: matches the spec's explicit list exactly

**Decision**: On a successful create with "Create another" checked, the modal
clears `title`, `description`, and `labels`, and retains `type`, `statusId`,
`priority`, `assigneeUserId`, `parentWorkItemId`, `startDate`, and `dueDate`.

**Rationale**: The spec's User Story 4 explicitly lists what's retained —
"type, status, priority, assignee, parent, and dates" — and separately says
title and description clear. Labels aren't named in either list; since the
spec's stated motivation is fast entry of several *similar* items in a row
(same type/status/assignee/parent/timeframe, but each its own individual
title), and a label is closer in kind to "this specific item's tag" than to
"this batch's shared metadata," clearing labels alongside title/description is
the reading most consistent with the feature's stated intent. This is called
out here explicitly since it's the one field the spec leaves ambiguous.

## 9. View refresh during "Create another": a caller-supplied callback, not `afterClosed()` alone

**Decision**: `MAT_DIALOG_DATA` carries an `onSaved: () => void` callback
supplied by whichever view opened the modal (board, project-detail list/tree,
work-item detail). The modal invokes it after *every* successful create or
update — not only when the dialog finally closes. Each opener's `onSaved`
re-runs that view's own existing fetch (`getBoard()`, `getWorkItems()`,
`getWorkItemsTree()`, or `getWorkItemDetail()`).

**Rationale**: FR-005 requires the underlying view to refresh in place on
successful create — including every individual create during a "Create
another" batch, not just the last one before the dialog closes. Relying on
`MatDialogRef.afterClosed()` alone would only refresh once, after the whole
batch, leaving the board/list/tree visibly stale while a user logs several
items in a row. A callback invoked per-save keeps each view live throughout
the batch, matching "the created card shows the label chip and appears in the
right column without losing my board position" from the spec's Success Check
— which describes a single create, but the same live-refresh guarantee must
hold for each item in a "Create another" run.

## 10. Old route redirects: to the existing detail/project view, no auto-opened modal

**Decision**: The removed `.../work-items/new` and `.../work-items/:id/edit`
routes redirect (Angular route `redirectTo`) to `.../projects/:projectId` (create)
and `.../projects/:projectId/work-items/:id` (edit) respectively. The modal is
**not** auto-opened via a query parameter on arrival.

**Rationale**: These routes only matter for stale bookmarks/back-button/
external links now that every in-app entry point opens the modal directly
(FR-008 requires no dead page, not that the modal itself reopens). Auto-opening
the modal from a redirect target would need new query-param-driven "open on
load" plumbing solely to serve a legacy-link edge case that isn't part of any
user story — landing on a working, navigable page already satisfies FR-008
(constitution Principle III/VII — no speculative feature for a case the spec
doesn't ask for).

## 11. Labels endpoint placement: existing `WorkItemService`/`WorkItemsController`, no new service

**Decision**: `GetProjectLabelsAsync` (backing `GET /api/projects/{projectId}/labels`)
and the label-normalization/attach helper used by `CreateAsync`/`UpdateAsync`
live in the existing `WorkItemService`/`WorkItemsController`, not a new
`LabelService`/`LabelsController` pair.

**Rationale**: Feature 006 introduced `ProjectStatusService`/
`ProjectStatusesController` because `WorkflowStatus` has its own independent
lifecycle — add/rename/reorder/delete endpoints, its own permission rules
(Manager/Admin-gated mutations). `Label` has none of that in v1: no
create/rename/delete endpoint exists (spec: "no separate management screen in
v1"); a label's only two operations are "read the project's list" and
"attach/create inline while creating/editing a work item" — both already
naturally scoped to `WorkItemService`. Introducing a second service/controller
pair for an entity with no independent lifecycle would be exactly the
unjustified abstraction Principle III warns against.

## 12. API contract classification: additive, not breaking (Principle V)

**Decision**: `StartDate` and `Labels` are added as new optional fields to
`WorkItemRequest` and to the response DTOs that carry them (see
`data-model.md`); no existing field is renamed, retyped, or removed.

**Rationale**: Unlike Feature 006 (which had to replace a fixed enum with an
identity-based FK — a genuine breaking change requiring its own Complexity
Tracking justification), this feature only adds fields nothing previously
depended on. Existing callers that omit `startDate`/`labels` in a request
continue to work exactly as before (`StartDate` defaults to `null`, `Labels`
defaults to an empty list) — no migration path or coupled-deploy justification
is needed beyond noting the change is additive.
