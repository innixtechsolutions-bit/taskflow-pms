# Phase 0 Research: Projects & Work Items

The source feature description (`feature-02-projects-workitems.md`, now
folded into `spec.md`) left no open product questions — every decision
below is a technical/design choice needed to implement an already-clear
spec, not a resolution of spec ambiguity.

## 1. Authorization: combining role checks with resource-ownership checks

**Decision**: Project create/edit/delete stays a simple attribute check —
`[Authorize(Roles = "Manager,Admin")]` on the controller action, identical
to how `UsersController` already gates its actions. Work-item edit/delete
cannot be expressed as an attribute, because "the caller is this item's
creator or current assignee" isn't a role — so `WorkItemService` accepts
the caller's user id and role, and decides allow/refuse itself, the same
shape as `UserService.ChangeRoleAsync`'s last-admin guard already
established in Feature 001.

**Rationale**: Keeps the authorization rule next to the business rule it
protects (in the service), rather than splitting "can they even call this"
(attribute) from "should this specific request succeed" (service) across
two unrelated mechanisms. Matches Feature 001's own precedent exactly.

**Alternatives considered**: A custom `IAuthorizationHandler`/policy-based
requirement — rejected as unnecessary ceremony for a single feature's rule
that only reads two foreign keys off the loaded entity; the constitution's
Clarity Over Cleverness principle favors the simpler, already-precedented
shape.

## 2. EF Core relationships, cascade delete, and SQL Server's cascade-path rule

**Decision**: `Project` → `WorkItem` (via `WorkItem.ProjectId`) is the only
relationship configured with `DeleteBehavior.Cascade` (deleting a project
deletes its work items, per FR-009). Every foreign key that points at
`User` — `Project.CreatedByUserId`, `WorkItem.CreatedByUserId`,
`WorkItem.AssigneeUserId` — is configured with `DeleteBehavior.Restrict`.

**Rationale**: SQL Server refuses to create a schema where a single delete
could cascade to the same table through two different paths (a real
`CREATE TABLE`/migration-time error, "may cause cycles or multiple cascade
paths"). If `WorkItem.CreatedByUserId` also cascaded from `User`, and
`Project.CreatedByUserId` also cascaded from `User`, then deleting a `User`
could reach `WorkItem` two ways: directly, and indirectly through their
`Project`s. Restricting all `User`-pointing foreign keys sidesteps this
entirely — and is the semantically safer default anyway, since Feature 001
has no user-deletion feature at all yet; nothing today ever exercises these
paths, but the schema needs to be valid regardless.

**Alternatives considered**: Cascade on every foreign key — rejected;
fails at migration time for the reason above. `DeleteBehavior.SetNull` on
the `User` foreign keys — rejected for `CreatedByUserId` (a work item or
project must always have a creator; the column isn't nullable), viable in
principle for the optional `AssigneeUserId` but unnecessary until user
deletion exists as a feature at all.

## 3. Enum storage convention

**Decision**: `WorkItemType`, `WorkItemPriority`, and `WorkItemStatus` are
plain C# enums, each configured with EF Core's `HasConversion<string>()`
and colocated in `WorkItem.cs` — the same pattern (and the same file
placement precedent) as Feature 001's `Role` enum living alongside `User`
in `User.cs`.

**Rationale**: Consistency with an already-established, working pattern.
Storing as `string` (not the default `int`) keeps the database
human-readable when inspected directly, matching Feature 001's rationale
for `Role`.

**Alternatives considered**: A separate lookup table per enum — rejected
per the constitution's Clarity Over Cleverness principle and Feature 001's
identical reasoning for not doing this for `Role`: none of these sets are
admin-configurable in this feature, so a fixed enum is simpler and just as
correct.

## 4. Pagination and filtering shape

**Decision**: Both `GET /api/projects` and `GET /api/projects/{id}/work-items`
return the existing generic `PagedResult<T>` (`Items`, `Page`, `PageSize`,
`TotalCount`) already defined in `Dtos/PagedResult.cs` — no new pagination
envelope. Work-item filtering builds its `IQueryable` by appending
`.Where(...)` only for each filter parameter the caller actually supplied
(status, type, priority, assignee, title search), rather than one large
conditional expression.

**Rationale**: `PagedResult<T>` already exists and fits both list shapes
exactly; reusing it is the simplest option, and matches the constitution's
"pagination on all list endpoints" rule with the shape it already
established. Conditionally appending `.Where()` clauses is standard EF
Core practice — each `.Where()` call only adds a predicate to the query
expression tree; nothing executes against the database until the query is
enumerated (e.g., by `.ToListAsync()`), so five conditionally-appended
`.Where()` calls still produce one SQL query with up to five `AND`
conditions, not five round-trips.

**Alternatives considered**: A dedicated `WorkItemFilterRequest`
value object with its own validation — considered, but the four filter
values map directly to existing enums/an int id with no extra validation
needed beyond "does this string parse as the enum," so a request DTO with
plain optional query parameters is simpler and sufficient.

## 5. Delete confirmation: client-side, not a dedicated endpoint

**Decision**: The work-item count shown in a project's delete confirmation
("This will also delete 12 work items") comes from `totalWorkItemCount` on
`GET /api/projects/{id}` (`ProjectDetailDto`) — already fetched to render
the project detail page — rather than a separate preview/confirm endpoint.
`DELETE /api/projects/{id}` and `DELETE /api/work-items/{id}` are plain,
unconditional deletes once called; the confirmation step itself lives
entirely in the frontend.

**Rationale**: Avoids inventing a second endpoint whose only job is to
answer a question the frontend can already answer from data it fetched to
render the page anyway (Clarity Over Cleverness). Note this is a
*different* count than `ProjectListItemDto.openWorkItemCount` (FR-006, only
counts not-Done items, shown in the project list) — the delete
confirmation needs the *total* count, since deleting a project removes
every work item regardless of status.

**Alternatives considered**: A `DELETE .../projects/{id}?confirm=true`
two-step API — rejected as unnecessary indirection; the frontend already
has the count without asking the server a second time.

## 6. Frontend: continuing the plain-HTML pattern, applying Feature 001's `<select>` lesson

**Decision**: This feature's Angular components continue Feature 001's
established style — native HTML forms/tables/`<select>` elements, no
Angular Material components — for consistency, even though Angular
Material is present in the fixed stack and unused so far. Every `<select>`
introduced in this feature (status, type, priority, assignee dropdowns in
the work-item form and filter bar) uses `[selected]` on each `<option>`
from the start, not `[value]` on the `<select>` itself.

**Rationale**: Feature 001's Phase 7 polish pass found a real bug this
exact way: `[value]` on a `<select>` is written by Angular before its
`@for`-rendered `<option>` children exist in the DOM, so the browser has
nothing yet to match against and silently defaults to the first option —
every non-default role silently displayed wrong on first page load. This
feature has *more* `<select>` usage than Feature 001 (status, type,
priority, and assignee, each in both a create/edit form and a filter bar),
so applying the fix's pattern from the first line written avoids
rediscovering the same bug class four more times.

**Alternatives considered**: Introducing Angular Material's form controls
now, which sidestep this native-`<select>` quirk entirely — reasonable
for a future feature, but switching UI approach mid-project without a
concrete driver (this feature's acceptance criteria don't require it)
would be a bigger, less incremental change than the constitution's
Principle VII favors.

## 7. API URL shape

**Decision**: Work-item creation and listing are nested under their
project (`POST`/`GET /api/projects/{projectId}/work-items`), since a work
item cannot exist without a project and its list is always scoped to one.
Getting, editing, and deleting a single work item are flat
(`GET`/`PUT`/`DELETE /api/work-items/{id}`), since a work item's project
never changes after creation (FR-014) and its own id is sufficient to
address it.

**Rationale**: Matches the constitution's "RESTful, plural nouns" rule and
common REST convention: nest a collection under its parent when the
collection is always scoped to one parent; address an individual resource
by its own id once you have it.

**Alternatives considered**: Fully nesting every work-item route under its
project (`GET/PUT/DELETE /api/projects/{projectId}/work-items/{id}`) —
rejected as redundant, since `projectId` never changes and never
disambiguates anything once the work item's own id is known.
