# Data Model: User Registration, Login & Role-Based Access

## Entity: User

Represents a registered person (spec: "User Account").

| Field | Type | Rules |
|---|---|---|
| `Id` | `int` (identity PK) | Auto-generated. Per constitution: `int` identity primary keys. |
| `FullName` | `string` | Required, 2–100 characters (FR-001). |
| `Email` | `string` | Required, valid email format, unique (FR-002). Unique index relies on SQL Server's default case-insensitive collation (`SQL_Latin1_General_CP1_CI_AS`) so uniqueness is case-insensitive without a separate normalized column — documented assumption; if the database is ever configured with a case-sensitive collation, this index must add an explicit `COLLATE` clause. |
| `PasswordHash` | `string` | Required. Produced by `PasswordHasher<User>`; never null, never serialized in any DTO (FR-006). |
| `Role` | `Role` enum (`Developer` \| `Manager` \| `Admin`) | Required, defaults to `Developer` on creation (FR-004). Stored as `nvarchar` via EF Core's `HasConversion<string>()` so the raw table data is human-readable — a small, deliberate teaching touch (Principle VI) with no added runtime complexity. |
| `CreatedAt` | `DateTime` (UTC) | Set once at creation; displayed as "registration date" on the Users page (FR-014). |

**Why no separate `Role` table**: only three fixed, spec-defined roles exist
and nothing in this feature (or the roadmap described in the spec) calls for
admin-configurable roles. A `Role` table would be speculative generality the
constitution's Clarity Over Cleverness principle explicitly disallows;
revisit only if a future feature needs dynamic roles.

**Validation** (enforced in the service layer via DTO validation, not only
DB constraints):
- `FullName`: 2–100 chars, required.
- `Email`: required, RFC-valid format, uniqueness checked before insert.
- Raw password (never persisted): ≥8 characters, ≥1 letter, ≥1 digit
  (FR-003) — validated on the `RegisterRequest` DTO, not on the entity.

**State transitions**:
- `Role` changes Developer ↔ Manager ↔ Admin, triggered only by an Admin via
  `PUT /api/users/{id}/role` (FR-015).
- Guard: a change is rejected if the target user is the only remaining
  `Admin` and the new role is not `Admin` (FR-016). This is a business rule
  evaluated inside the same transaction as the update (count remaining
  Admins excluding the target user, immediately before committing the
  change) — it is not expressible as a simple column constraint.

## No separate Session entity

A session is represented entirely by the JWT itself (identity, role, and
`exp` claims) — there is no `Sessions` table. See `research.md` §1–2 and §6
for the token lifecycle and the accepted trade-offs of a stateless bearer
token (no server-side revocation on logout).

## Configuration (not a database entity)

| Key | Source | Purpose |
|---|---|---|
| `Admin:Email` | user-secrets (dev) / environment variable (other envs) | Seed Admin email at first startup (FR-018). |
| `Admin:Password` | user-secrets (dev) / environment variable (other envs) | Seed Admin password at first startup; hashed with `PasswordHasher` before storage, never persisted or logged in plain text. Missing/empty ⇒ application fails to start (FR-018a). |
| `Jwt:SigningKey`, `Jwt:Issuer`, `Jwt:Audience` | user-secrets (dev) / environment variable (other envs) | JWT issuance/validation parameters. Never committed to git (constitution: Security & Data Protection Requirements). |
