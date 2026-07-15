# Quickstart: Validate User Registration, Login & Role-Based Access

## Prerequisites

- .NET 10 SDK, Node.js (for Angular 22 CLI), SQL Server 2022 (Developer
  Edition) running locally.
- Seed Admin credentials configured **before first run** (FR-018/FR-018a):
  ```
  dotnet user-secrets set "Admin:Email" "admin@taskflow.local" --project backend/TaskFlow.Api
  dotnet user-secrets set "Admin:Password" "ChangeMe123" --project backend/TaskFlow.Api
  ```
  (In non-development environments these are set as environment variables
  of the same names instead.)

## Setup

```
# Backend: apply EF Core migrations, then run the API
dotnet ef database update --project backend/TaskFlow.Api
dotnet run --project backend/TaskFlow.Api

# Frontend: run the Angular dev server
npm install --prefix frontend
npm run start --prefix frontend
```

## Validation scenarios

Each scenario below maps directly to an acceptance scenario in `spec.md`.
Endpoint shapes are documented in `contracts/`; entity shape in
`data-model.md`.

1. **Register** (User Story 1, spec scenarios 1–3): Open the registration
   page, submit a unique name/email/password. Expect: landed on the home
   page, signed in, header shows the new name and `Developer` role.
   Re-submit the same email → expect the duplicate-email error. Submit a
   7-character password → expect the password-rules error shown before
   submission is even accepted.

2. **Login** (User Story 2, spec scenarios 1–2, 5–6): Log out, then log back
   in with the same credentials → expect success and a redirect to home.
   Try a wrong password → expect the generic "Invalid email or password"
   message. Try an unregistered email → expect the *same* message. Visit a
   protected URL directly while signed out → expect a redirect to login,
   and after logging in, a redirect to that original URL.

3. **Session persistence & expiry** (User Story 2, spec scenarios 3–4):
   After logging in, refresh the browser → still signed in. (To validate
   expiry without waiting 8 hours, temporarily shorten `Jwt` expiry in local
   config, or manually verify the `exp` claim on the issued token equals
   issue time + 8 hours.)

4. **Identity display & logout** (User Story 3): Confirm name + role are
   visible in the header on multiple pages; click logout → returned to
   login page, subsequent requests to protected pages redirect to login.

5. **Admin role management** (User Story 4): Log in as the seeded Admin,
   open the Users page → see all users, paginated. Change a Developer to
   Manager → confirm the change is reflected on that user's next request.
   Attempt to demote the seeded Admin while it is the only Admin → expect
   the change to be refused. Log in as a Developer and attempt to open the
   Users page directly by URL, and call `GET /api/users` directly → expect
   both refused with `403`.

6. **Rate limiting** (spec scenario 7): Attempt to log in with a wrong
   password 5 times in a row for the same email → the 6th attempt (even with
   the correct password) is blocked with "Too many attempts, try again
   later." until 15 minutes pass.

7. **Startup fail-fast** (FR-018a): Remove/blank the `Admin:Password` user
   secret on a database with no existing Admin, then start the API → expect
   the process to fail to start with an error naming the missing
   configuration, not a silent start.
