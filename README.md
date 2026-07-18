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
