# Contract: Auth API

All responses use `application/json`. All error responses use
`ProblemDetails` / `ValidationProblemDetails` (see `research.md` §8).

## POST /api/auth/register

**Auth required**: No.

**Request body**:
```json
{ "fullName": "string (2-100 chars)", "email": "string", "password": "string (>=8 chars, >=1 letter, >=1 digit)" }
```

**Responses**:
- `201 Created` →
  ```json
  { "token": "string", "expiresAt": "ISO-8601 datetime", "fullName": "string", "role": "Developer" }
  ```
  (FR-004, FR-005 — new accounts are always created as `Developer`.)
- `400 Bad Request` (`ValidationProblemDetails`) — name/email/password fail
  validation (FR-001, FR-003).
- `409 Conflict` (`ProblemDetails`, `detail`: "An account with this email
  already exists.") — duplicate email (FR-002).

## POST /api/auth/login

**Auth required**: No.

**Request body**:
```json
{ "email": "string", "password": "string" }
```

**Responses**:
- `200 OK` → same shape as register's `201` body.
- `401 Unauthorized` (`ProblemDetails`, `detail`: "Invalid email or
  password.") — wrong email OR wrong password; identical response for both
  cases (FR-008).
- `429 Too Many Requests` (`ProblemDetails`, `detail`: "Too many attempts,
  try again later.") — 5th+ failed attempt for this email within 15 minutes
  (FR-019).

## POST /api/auth/logout

**Auth required**: Yes (valid bearer token).

**Request body**: none.

**Responses**:
- `204 No Content` — client is responsible for discarding the token
  (see `research.md` §6 for the stateless-token trade-off).

## GET /api/auth/me

**Auth required**: Yes (valid bearer token).

**Purpose**: Lets the frontend re-establish trusted identity/role after a
refresh instead of trusting only the client-decoded JWT (supports FR-013).

**Responses**:
- `200 OK` → `{ "fullName": "string", "role": "Developer|Manager|Admin" }`
- `401 Unauthorized` — missing, invalid, or expired token (FR-010).
