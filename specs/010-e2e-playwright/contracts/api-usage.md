# Phase 1 Contracts: Existing API Endpoints Consumed Directly

This feature adds no API endpoints and changes no request/response
contracts (Constitution Principle V — no changes). This document instead
records which **existing** backend endpoints the E2E suite calls directly
(bypassing the UI, via Playwright's `request` fixture), since that is the
one place this suite has an "external interface" relationship: it is a new
consumer of contracts that already exist and must not drift from them.

All endpoints below are read from the current controller source under
`backend/TaskFlow.Api/Controllers/` and are unmodified by this feature.

## Used by `frontend/e2e/global-setup.ts` (account provisioning)

| Endpoint | Auth | Purpose in this suite |
|---|---|---|
| `POST /api/auth/login` | none | Log in as the config-seeded Admin to obtain a bearer token for the role-promotion call below. Also used to log in as the seeded Developer for the permission-boundary journey's direct call. |
| `POST /api/auth/register` | none | Create the Manager-candidate and Developer test accounts. Returns `AuthResponse { Id, Token, ExpiresAt, FullName, Role }` — the `Id` is captured for the role-promotion call. A `409 Conflict` (`EmailAlreadyExistsException`) is treated as "already provisioned" for idempotent reruns (see research.md Decision 3). |
| `PUT /api/users/{id}/role` | Bearer token, `Admin` role required | Promote the Manager-candidate from the default `Developer` role to `Manager`. Body: `ChangeRoleRequest { Role: "Manager" }`. This is the exact endpoint Story 7 (Role-change journey) also exercises through the UI — no separate mechanism is introduced. |

## Used by `frontend/e2e/tests/permissions.spec.ts` (server-side enforcement proof, FR-012)

| Endpoint | Auth | Expected result |
|---|---|---|
| `DELETE /api/projects/{id}` | Bearer token for the seeded **Developer** | `403 Forbidden` — the endpoint is decorated `[Authorize(Roles = "Manager,Admin")]` on `ProjectsController.Delete`; a Developer's token must be rejected server-side regardless of what the UI does or doesn't render. |

This single direct call satisfies FR-012's requirement for "at least one
direct API call made from within a test" proving server-side enforcement.
Additional direct calls MAY be added during implementation (e.g. against
`ProjectStatusesController` or `UsersController`) if `/speckit-tasks`
determines more than one strengthens the journey, but one is the documented
minimum this plan commits to.

## Contract stability note

If any of the endpoints above change shape during a future feature, this
suite's `global-setup.ts` or `permissions.spec.ts` will fail fast and
loudly (not silently) — which is itself a useful regression signal for
Principle V (API Contract Stability), even though this feature does not
modify any contract itself.
