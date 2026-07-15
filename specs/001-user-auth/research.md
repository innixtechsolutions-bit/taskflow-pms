# Research: User Registration, Login & Role-Based Access

Most technical decisions for this feature are pre-fixed by the project
constitution (`.specify/memory/constitution.md`): .NET 10 ASP.NET Core Web
API with Controllers, EF Core 10 + SQL Server, JWT bearer auth, Angular 22
(signals, standalone, Material). What follows resolves the decisions the
constitution leaves open.

## 1. Token delivery & client-side persistence

**Decision**: JWT access token issued on register/login, returned in the
response body, stored in the browser's `localStorage` by an Angular
signal-based `AuthService`, and attached to every API request via an
`HttpInterceptor` (`Authorization: Bearer <token>`).

**Rationale**: The constitution specifies "JWT bearer tokens" (an
`Authorization` header scheme), and the spec requires the session to survive
a browser refresh (User Story 2, FR-009) without a refresh-token mechanism
(explicitly out of scope). `localStorage` is the simplest way to persist a
single, short-lived (8h) bearer token across refreshes without introducing
cookie/CORS/CSRF concerns, matching Principle III (Clarity Over Cleverness)
and keeping the concept count low for Principle VI (Teach While Building —
interceptors and signals are enough new ground for this feature).

**Alternatives considered**: An httpOnly, Secure cookie is more resistant to
XSS token theft, but it requires CORS `credentials` configuration and a CSRF
mitigation strategy to be safe — meaningful added complexity for a first
feature whose explicit brief is registration/login, not hardened session
architecture. Rejected for this feature; worth revisiting in a future
security-hardening feature if TaskFlow's threat model calls for it.

## 2. Session expiry mechanism

**Decision**: The JWT's standard `exp` claim is set to issued-at + 8 hours.
The ASP.NET Core JWT bearer handler validates lifetime server-side on every
request (`ValidateLifetime = true`, zero clock skew tolerance beyond the
library default). The Angular `AuthService` also stores the expiry timestamp
alongside the token so the UI can proactively show "Your session has
expired" and redirect on the next navigation, rather than waiting for a 401.

**Rationale**: A single `exp` claim gives an absolute, non-renewing 8-hour
window with no extra moving parts — matching the spec's explicit exclusion
of refresh-token rotation and sliding sessions.

**Alternatives considered**: Sliding expiration (extend on each request) was
rejected — the spec calls for a fixed 8-hour window, and sliding expiration
would silently change that behavior.

## 3. Login rate limiting (5 attempts / 15 minutes per email)

**Decision**: A small `ILoginAttemptTracker` service backed by
`IMemoryCache`, keyed by normalized (lowercased) email, incrementing a
failed-attempt counter with a 15-minute sliding cache expiration. Checked
before credential validation; incremented only on a failed attempt; cleared
on a successful login.

**Rationale**: The requirement keys on **email**, not IP or connection, which
doesn't map cleanly onto ASP.NET Core's built-in `RateLimiting` middleware
(its partitioning is designed around connection/IP/user, and keying off a
POST body requires enabling request-body buffering — extra ceremony with no
teaching payoff for this feature). A small dedicated service is easier to
read, test, and explain (Principle III, VI).

**Alternatives considered**: `Microsoft.AspNetCore.RateLimiting` middleware
with a custom partition key reader — rejected for the body-buffering
complexity noted above. A persisted (DB) attempt counter was rejected as
unnecessary durability for a 15-minute window; in-memory is sufficient and
simpler.

## 4. Password hashing

**Decision**: Use `Microsoft.AspNetCore.Identity`'s `PasswordHasher<User>`
directly (constructed and called from `AuthService`), without adopting the
rest of ASP.NET Core Identity (no `UserManager`, no Identity `DbContext`, no
Identity UI).

**Rationale**: The constitution explicitly names `PasswordHasher` while
explicitly excluding "Identity UI scaffolding." Using the hasher type in
isolation satisfies both.

**Alternatives considered**: A third-party hashing library (e.g., BCrypt.Net)
was rejected — `PasswordHasher` is already an audited, built-in option and
the constitution names it specifically.

## 5. Seed Admin at startup

**Decision**: A single async method invoked from `Program.cs` after the app
is built but before `app.Run()`. It opens a scoped `DbContext`, checks
whether any `Admin`-role user exists; if not, reads `Admin:Email` and
`Admin:Password` from configuration (`IConfiguration`, backed by user-secrets
in development / environment variables elsewhere per FR-018). If either value
is missing or empty, it throws immediately, which stops the host from
starting and surfaces the exception message (FR-018a). Otherwise it creates
the seed Admin using the same `PasswordHasher` as normal registration.

**Rationale**: This is a one-shot startup check, not an ongoing background
process, so a plain startup method is simpler than an `IHostedService`
(Principle III — avoid ceremony without a concrete need).

**Alternatives considered**: An `IHostedService`/`BackgroundService` was
rejected as unnecessary abstraction for a task that runs once and must
complete (or fail) before the app accepts traffic.

## 6. Logout semantics for a stateless bearer token

**Decision**: `POST /api/auth/logout` is a thin, mostly symmetry-preserving
endpoint (204 No Content) that exists so the frontend has one consistent
place to call. The actual "session end" (FR-011) is achieved client-side:
the Angular `AuthService` clears the token from `localStorage` and its
signal state, and the interceptor/guard stop sending/accepting it. No
server-side token blacklist is introduced.

**Rationale**: Without refresh tokens (explicitly out of scope), the JWT is
a short-lived (≤8h), stateless bearer credential. Building server-side
revocation (a blacklist or a persisted session table) to force instant
server-side invalidation would add real infrastructure for a benefit the
spec doesn't ask for. The accepted trade-off — documented here rather than
left implicit — is that a captured token technically remains valid until its
natural expiry even after "logout"; this matches the feature's simplicity
mandate (Principle III) and its explicit non-goal of refresh-token rotation.

**Alternatives considered**: A server-side revocation list keyed by token ID
(`jti`) — rejected as disproportionate infrastructure for a first feature
that explicitly excludes refresh-token machinery.

## 7. Pagination shape for the Users list

**Decision**: `GET /api/users?page={1-based}&pageSize={n}` returning
`{ items: [...], page, pageSize, totalCount }`.

**Rationale**: Matches the constitution's "pagination on all list endpoints
from day one" with the simplest common shape.

## 8. Error response shape

**Decision**: All API error responses use ASP.NET Core's built-in
`ProblemDetails` (RFC 7807) — `ValidationProblemDetails` for model-validation
failures, and `ProblemDetails` with an appropriate `status`/`title`/`detail`
for auth failures (401), authorization failures (403), and rate-limiting
(429).

**Rationale**: Directly satisfies the constitution's "all API error
responses MUST follow a single, consistent shape" and FR-020, using a
built-in ASP.NET Core convention rather than a custom envelope.

## 9. Angular route guarding & auth state

**Decision**: A functional `CanActivateFn` (Angular's standalone-era guard
style) reads the signal-based `AuthService` state; on failure it redirects to
`/login?returnUrl=<attempted-url>` (supporting FR-012). `AuthService` exposes
a root-provided `signal<AuthState | null>` plus `computed` signals
(`isAuthenticated`, `currentRole`) consumed by the guard, the header
component, and an `HttpInterceptor`.

**Rationale**: Functional guards and signals are the constitution-mandated,
current Angular idiom (no NgModules, signal-first state).
