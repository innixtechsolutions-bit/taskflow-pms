# Contract: Users API (Admin only)

All endpoints below require a valid bearer token **and** the `Admin` role
(FR-017). Any non-Admin caller — regardless of how the request is made —
receives `403 Forbidden` (`ProblemDetails`).

## GET /api/users?page={int}&pageSize={int}

**Purpose**: List every registered person for the Users page (FR-014).

**Responses**:
- `200 OK` →
  ```json
  {
    "items": [
      { "id": 1, "fullName": "string", "email": "string", "role": "Developer", "createdAt": "ISO-8601 datetime" }
    ],
    "page": 1,
    "pageSize": 20,
    "totalCount": 42
  }
  ```
- `403 Forbidden` — caller is not an Admin.

## PUT /api/users/{id}/role

**Purpose**: Change a person's role (FR-015).

**Request body**:
```json
{ "role": "Developer|Manager|Admin" }
```

**Responses**:
- `200 OK` → updated user record (same shape as a list item above). Takes
  effect on the target user's next request (FR-015) — no push/real-time
  update to an already-open session, per spec Assumptions.
- `400 Bad Request` (`ProblemDetails`, `detail` explaining the guard) — the
  caller is the last remaining Admin and the request would remove their
  Admin role (FR-016).
- `403 Forbidden` — caller is not an Admin.
- `404 Not Found` — `{id}` does not match an existing user.
