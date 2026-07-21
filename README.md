# TaskFlow PMS

A task/project management system built feature-by-feature, following the
project constitution at `.specify/memory/constitution.md`. The maintainer is
an experienced Angular developer learning .NET and SQL Server through the
act of building this app — see "What I learned" below for the running log
that principle requires.

## Stack

- **Frontend**: Angular 22 (signals, standalone components, zoneless, Signal
  Forms), Angular Material/CDK.
- **Backend**: .NET 10, ASP.NET Core Web API (Controllers), EF Core 10 with
  SQL Server, JWT bearer authentication.
- **Database**: SQL Server 2022 Developer Edition, code-first migrations.

## Running locally

See `specs/001-user-auth/quickstart.md` for setup and validation steps.

## What I learned

### Feature 001: User Registration, Login & Role-Based Access

- **DI lifetimes aren't just a formality.** `AppDbContext` and anything that
  depends on it (`AuthService`, `UserService`, `AdminSeeder`) has to be
  Scoped — one instance per request — because EF Core's change tracking
  assumes that. `ILoginAttemptTracker` has to be the opposite: Singleton,
  because it wraps one shared `IMemoryCache` that needs to accumulate failed
  login attempts *across* requests. Getting either backwards either breaks
  (Scoped-into-Singleton won't even resolve) or silently does nothing
  (a Singleton-lifetime cache that gets recreated per request never
  remembers anything).
- **`async`/`await` isn't decoration.** Every EF Core call and controller
  action in this feature is `async` so a thread isn't blocked waiting on
  SQL Server I/O — under load, that's the difference between a thread pool
  that keeps serving other requests and one that stalls.
- **The most useful bug of this feature wasn't in application logic — it was
  in *when* configuration gets read.** `Program.cs` read `Jwt:SigningKey`
  into a local variable before `builder.Build()`, then closed over that
  variable when configuring JWT bearer validation. That's fine when nothing
  changes the configuration after that point — which is true for a normal
  `dotnet run`. But `WebApplicationFactory`-based integration tests layer in
  a *different* signing key via `ConfigureWebHost`, and that layering
  finishes *after* the point where the closure had already captured its
  value. The result: tokens were *issued* with the test key (a live
  `IConfiguration` read inside `AuthService`, evaluated per request) but
  *validated* against whatever the earlier snapshot had captured — every
  protected endpoint failed with "the signature key was not found," and
  every test up to that point had been exercising only the unauthenticated
  paths (register, and login's own response body), so nothing had actually
  round-tripped a token through `[Authorize]` yet. The fix was small (read
  configuration *inside* the lazily-evaluated `AddJwtBearer` options
  delegate instead of a captured variable) but the lesson was bigger: a
  value captured in a closure is frozen at the moment the closure is
  *defined*, not at the moment it *runs* — and those two moments can be
  further apart than they look, especially across a host-startup boundary.
- **JWT bearer auth trades a server-side session store for a stateless
  signed token.** The whole session lives in the token's claims (identity,
  role, `exp`) and a shared signing key — which is simple and needs no
  session storage, but also means "logout" has nothing to actually revoke
  server-side; it's the client discarding the token that ends the session,
  not the server. `POST /api/auth/logout` exists for the shape of the API
  and for `[Authorize]` to still gate it, not because it does anything a
  client-side `clearAuth()` doesn't already do alone.
- **Angular's functional guards compose like plain functions, because
  that's what they are.** `adminGuard` calls `authGuard` directly inside
  itself rather than duplicating its session/expiry logic — the only
  subtlety is that both still have to run inside the router's injection
  context for their `inject()` calls to resolve, which works here because
  `adminGuard` is itself invoked by the router in that same context, and
  the call to `authGuard` happens synchronously within it.
- **A route guard is a UX nicety, full stop.** Every guard added in this
  feature (`authGuard`, `adminGuard`) exists so the *user* doesn't see a
  flash of a page they can't use — it does nothing to actually secure
  anything. `UsersController`'s `[Authorize(Roles = "Admin")]` is what a
  non-Admin actually can't get past, verified by an integration test that
  calls the endpoint directly with a non-Admin token, bypassing the
  frontend entirely.

### Feature 002: Projects & Work Items

- **`AddProblemDetails()` alone doesn't touch every error response.**
  Registering it in `Program.cs` is enough for unhandled *exceptions* and
  for `[ApiController]`'s own validation failures, but a bare
  `[Authorize(Roles = "Manager,Admin")]` rejection is handled entirely by
  the authorization *middleware*, before any controller action (or its
  `Problem(...)` calls) ever runs — and that middleware, on its own, returns
  a 403 with a completely empty body. A Polish-phase test written to check
  "every error response is a real `ProblemDetails`" caught this by actually
  parsing the response body and finding nothing there to parse. The fix
  was one line, `app.UseExceptionHandler()` followed by
  `app.UseStatusCodePages()` — the second half of the pair, which converts
  any already-error-coded-but-bodyless response into a `ProblemDetails`
  through the same registration. It quietly fixed the identical gap in
  Feature 001's own `[Authorize(Roles = "Admin")]` endpoints too, which had
  never been tested against this specific angle (only their status codes,
  never their body shape).
- **SQL Server's "multiple cascade paths" rule forces a design decision, not
  just a workaround.** `WorkItem` has two foreign keys into `User`
  (creator, assignee) and one into `Project`, which itself has its own
  foreign key into `User`. Making all of the `User`-pointing foreign keys
  cascade would mean deleting a `User` could reach `WorkItem` two different
  ways — directly, and indirectly through `Project` — which SQL Server
  refuses to allow as a schema at all. The fix (`DeleteBehavior.Restrict`
  on every foreign key that points at `User`) isn't a workaround for a
  database limitation; it's the correct modeling choice regardless, since
  this app has no user-deletion feature yet for those paths to protect
  against.
- **A task breakdown can miss a dependency that only becomes visible during
  implementation.** `tasks.md` scheduled work-item edit/delete UI in one
  phase and the actual itemized work-item list (with its filter/search/
  pagination) in a later one — reasonable on paper, since each looked like
  an independent slice. But an edit/delete *control* has nothing to attach
  to without rows to render it next to, so the earlier phase silently
  depended on output the later phase hadn't produced yet. Discovering this
  while implementing (not while planning) meant pulling the listing
  endpoint and a basic unfiltered rendering of it forward, then having the
  later phase extend rather than originate the list — documented inline in
  `tasks.md` rather than silently reordered, so the plan stayed honest
  about what actually happened and why.

### Feature 003: Work Item Hierarchy

- **A table can't cascade-delete into itself.** `WorkItem.ParentWorkItemId`
  is the first foreign key in this codebase that points back at its own
  table. SQL Server refuses `ON DELETE CASCADE` on a self-referencing
  foreign key outright (error 1785) — not because of a "multiple paths"
  conflict like Feature 002's `User` foreign keys, but because the engine
  can't prove a self-referential cascade chain ever terminates. The fix is
  the same shape as Feature 002's (`DeleteBehavior.Restrict`), but the
  reason is different, and it means "delete this item's whole subtree" has
  to be application code (`WorkItemService.DeleteAsync` walking descendant
  ids level by level) rather than something the database does for free.
- **Proving a case is impossible can delete more code than handling it
  would have needed.** The spec calls for refusing cycles and
  self/descendant parent selection. The instinct is to write an
  ancestor-walk with a visited set. But the hierarchy's own rule — a valid
  parent is always exactly one rank below its child (Epic < Story < Task <
  SubTask), and Epic can never have a parent at all — means a rank can only
  ever strictly decrease while walking up any parent chain, which makes
  returning to a rank already visited mathematically impossible. The
  ordinary "is this the required type, in the same project" check that the
  feature needs anyway already enforces this, for free. No traversal
  algorithm was written, tested, or is now sitting in the codebase waiting
  to have a bug in it.
- **A test can share a fixture's flaw with the code it's testing.** The
  type-change guard has two independent checks — does the item's *existing
  parent* still fit the new type, and do its *existing children* still fit
  it — and a test meant to isolate the children-check used a setup where
  the item's *own* parent was also incompatible with the new type. The
  parent-check fired first, and the test failed asserting the wrong
  exception type. The fix was to make the fixture data actually isolate
  the condition under test (a parentless item), not to reorder the
  production code's checks. Separately, a first full test-suite run that
  same session reported 31 failures that a second, unchanged run didn't
  reproduce — SQL-Server-backed tests each provision and drop their own
  database, and running many of them in parallel intermittently exhausted
  something at the database-server level. Telling that apart from a real
  regression meant re-running before trusting either number.

### Feature 004: App Shell & Design System

- **A DTO's actual shape beats a plan's assumption about it, every time.**
  research.md's original plan hashed a user's avatar color off their
  numeric id — safer than a name, in theory, since two people can share a
  display name but never an id. Implementation found that
  `WorkItemTreeNodeDto` (the tree view's data source) only returns
  `AssigneeName`, no id, while the flat-list/detail DTOs do have one.
  Hashing on whichever key happened to be available per view would have
  made the same assignee render two *different* avatar colors depending on
  which screen they were seen on — a visible, directly-testable failure of
  the feature's own "same user, same color, everywhere" requirement, and a
  worse outcome than the name-collision risk the id was chosen to avoid.
  The fix was to hash on `fullName` everywhere, accepting the (much
  smaller, cosmetic-only) risk instead. The lesson isn't "the plan was
  wrong" — it's that a plan's data assumptions need checking against the
  actual API responses before code gets written against them, not after.
- **Angular's content projection has a sharp edge around control flow.**
  `<ng-content select="[foo]">` silently fails to project an element that
  matches `[foo]` if that element sits inside an `@if` block with more
  than one root node — Angular's own NG8011 diagnostic names this exactly,
  but only as a build warning, not an error, so it's easy to miss. Two
  sibling buttons (Edit/Delete) inside one `@if` inside `<app-page-header>`
  rendered as a completely empty actions slot, with no runtime error at
  all — five component-spec assertions just started reporting `null` where
  they expected a button, and the actual cause only showed up on close
  reading of the build's warning output, not the test failures themselves.
  The fix (wrap multi-node conditional content in a single `<ng-container
  foo>`) is simple once you know to look for it.
- **HTML lowercases attribute names before Angular ever sees them.**
  `<ng-content select="[pageHeaderActions]">` paired with a
  template-authored `<a pageHeaderActions>` looks like it should match —
  the strings are identical in the source. It doesn't, because the browser
  (and jsdom, in tests) parses HTML attribute names as case-insensitive and
  normalizes them to lowercase before Angular's selector matching ever
  runs; `pageHeaderActions` becomes `pageheaderactions` on the DOM, and
  `[pageHeaderActions]` as a selector never matches that. Kebab-case
  (`page-header-actions`) sidesteps the whole problem by construction, and
  is what every custom attribute selector in this codebase uses now.
- **A design system's promise ("identical everywhere") is a testable
  claim, not just an aesthetic one.** Because `StatusChipComponent` and
  `PriorityChipComponent` are each the *only* place a status/priority
  value maps to a color, "the same status renders the same color in the
  tree, the flat list, and the detail page" isn't something that needed
  its own cross-view integration test — it's true by construction, and
  each component's own unit test (one assertion per enum value) is
  sufficient. Centralizing a mapping is a way of making a consistency
  requirement literally impossible to violate, not just less likely.

### Feature 005: Kanban Board

- **A DTO field's *shape*, not just its presence, decides whether a future
  feature is a one-file change or a cross-cutting one.** The original plan
  had the board endpoint return `columns: string[]` (bare status names),
  with the frontend deriving each column's display label from
  `StatusChipComponent`'s own map — reasonable, since that map already
  existed and was the single source of truth for status labels elsewhere.
  Caught before implementation: a future per-project custom column (Feature
  006) wouldn't have a matching frontend label, because the frontend would
  still be guessing labels from a fixed enum instead of reading whatever
  the backend actually sent. The fix was to change the field to
  `{status, label}[]`, computed server-side — a few lines of backend code,
  but it changes what "adding a column" means for every future feature:
  purely a backend change, never a matching frontend edit. The lesson: when
  a field's data clearly *could* vary later, check whether its current
  shape encodes an assumption (here, "the frontend already knows every
  possible status") that the variability would break.
- **Comparing dates as strings sidesteps a real UTC/local timezone bug.**
  The obvious way to check "is this due date before today" is
  `new Date(dueDate) < new Date()` — but due dates arrive from the API as
  UTC-midnight ISO strings, and constructing a `Date` from one and reading
  its local getters (`.getDate()`, etc.) silently applies a UTC→local
  conversion that can shift the calendar date by a day for any user behind
  UTC. A due date of "2026-07-19" can read back as July 18 in local time,
  making an item overdue a day early. The fix never constructs a `Date`
  from the due date at all: it reads the first 10 characters of the ISO
  string (`YYYY-MM-DD`) and string-compares that against a `today` string
  built from `Date.prototype`'s local getters on the *current* moment only
  (which has no timezone ambiguity, since "today" is being asked, not
  parsed). Two different techniques for two different purposes — parsing
  a stored date vs. reading the current moment — quietly need two different
  approaches to stay correct.
- **A browser click survives being wrapped in a drag directive, because
  "click" and "drag" are already distinguished upstream.** Making a Kanban
  card both draggable (`cdkDrag`) and clickable-to-detail (`routerLink`)
  looked like it might need an explicit gesture check — some way to tell
  "the user picked this up and dropped it three pixels later" apart from
  "the user clicked it." It doesn't: `cdkDrag` only initiates a drag once
  pointer movement crosses its own threshold, so a genuine click (press and
  release with no meaningful movement) is never intercepted and reaches
  the nested `routerLink` as an ordinary click event. No custom
  click-vs-drag disambiguation code was needed — nor, per the same
  reasoning, was any needed to keep a disabled-drag card's link clickable.

### Feature 006: Custom Workflow Columns

- **Replacing a system-wide fixed enum with a per-project entity touches
  nearly every existing status-aware surface by definition, so the honest
  move was to size the work accordingly rather than pretend it was small.**
  `WorkItemStatus` (a 4-value enum) became `WorkflowStatus` (a per-project
  table with `Position`/`Category`/`ColorKey`) — a change with no partial
  version, since every query, DTO, and UI element that referenced the enum
  had to move to an id-based reference in the same coupled deploy (a status
  can be renamed at any time, so a name-keyed reference would silently
  break the moment a Manager renamed a column). This was the largest
  feature so far by file count, and the constitution's own Complexity
  Tracking section exists precisely to let a feature admit that up front
  instead of hiding it behind an artificially small task list.
- **An EF Core migration's auto-scaffolded operation order can silently
  destroy data, and the only way to catch it is to test the migration
  itself, not just the model it produces.** The scaffolded migration
  dropped the old `Status` column and defaulted the new
  `WorkflowStatusId` to 0 *before* any backfill ran — invisible in every
  ordinary service test, because those all build a fresh schema straight
  from the current EF model (`EnsureCreatedAsync`) and never see the
  in-between state a real upgrade passes through. The fix was twofold:
  hand-reorder the migration's `Up()` (create table → nullable column →
  raw-SQL backfill while the old column still exists → `NOT NULL` → drop
  old column), and add a dedicated migration-replay test that uses
  `IMigrator.MigrateAsync("<name>")` to stop a real database at the
  pre-migration schema, seed it with raw ADO.NET (the current C# model can
  no longer represent the old shape), then migrate forward and assert on
  the result. A green test suite built entirely on today's model can still
  hide a data-loss bug in the path that gets you there.
- **A "closed set" and a "no-longer-closed set" can sit right next to each
  other in the same concept, and treating them differently is what keeps
  compile-time safety.** Status *names* went from fixed to per-project and
  arbitrary — but status *colors* stayed a fixed, curated 10-member
  `ChipColor` palette, and status *category* stayed a fixed `Open`/`Done`
  pair. Re-keying `StatusChipComponent`'s exhaustive switch from the
  now-open status name to the still-closed `ChipColor` preserved the
  compiler's ability to catch a missing case, instead of quietly losing
  that guarantee everywhere status was displayed. The lesson generalizes:
  when a feature "opens up" one closed set, check its neighbors
  individually rather than assuming they all opened up together.
- **A read endpoint and its own management screen can have different
  authorization rules, and conflating them nearly shipped a regression.**
  The natural instinct was to gate the whole `api/projects/{id}/statuses`
  read the same way as the new add/rename/reorder/delete actions
  (Manager/Admin only) — it's the same controller, after all. But the
  board, status dropdowns, and filters every role already depends on
  (Feature 001-005) all read from that same endpoint, so restricting it
  would have silently broken every non-Manager's board. Caught during
  `/speckit-analyze`: only the *mutating* actions are role-restricted; the
  read stays open to any authenticated user, and a dedicated cross-cutting
  test now asserts both halves of that boundary in one place so a future
  change can't loosen or tighten either side unnoticed.

### Feature 007: Work Item Modal & Quick Creation

- **Every route eagerly importing its component into the main chunk is
  invisible until someone actually measures the bundle.** `ng build` had been
  quietly failing its own budget (1.09MB initial vs. a 1MB limit) because
  `app.routes.ts` used `component:` instead of `loadComponent:` everywhere,
  and the work item modal's own heavy dependencies (`MatDatepicker`, Signal
  Forms) shipped in the main bundle even on pages that merely *could* open it.
  Converting every route to `loadComponent` and dynamically importing the
  modal itself (shared by board/work-item-detail/project-detail via one
  `openWorkItemModal` helper) dropped the initial bundle to 551KB with
  headroom to spare — the fix was mechanical once found, but nothing in the
  test suite would ever have caught the regression, since Vitest never builds
  a production bundle.
- **A PUT that "replaces the whole resource" means every field, including
  ones the caller didn't think about touching.** The label attach/detach
  design reuses this codebase's existing "PUT replaces everything" contract
  (same as every other `WorkItemRequest` field) rather than diffing an
  attach/detach list — which meant the frontend had to be deliberate about
  always sending the current `labels` array, even when empty, on every save.
  Omitting the key on an edit (the natural instinct for an "unchanged, so
  don't send it" field) would have silently wiped every label the item
  already had, since the backend's normalization helper treats a missing
  `Labels` the same as an explicit empty list.
- **The same many-to-many cascade conflict from Feature 006 reappeared here,
  which suggests it's a pattern in this schema, not a one-off.** `Label →
  WorkItemLabel` needed `DeleteBehavior.Restrict` instead of the originally
  planned `Cascade` — SQL Server rejects it as a multiple-cascade-paths error
  (1785), because `WorkItemLabel` is reachable from `Project` two ways
  (`Project → Label → WorkItemLabel` and `Project → WorkItem →
  WorkItemLabel`). Any future many-to-many hanging off an entity that's
  already cascade-deleted from a shared ancestor should expect the same
  conflict and plan for `Restrict` on one side up front, rather than
  discovering it from a failed migration.
