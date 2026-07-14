# Feature 01 — User Registration & Login (paste this into /speckit.specify)

Build user registration, login, and role-based access for TaskFlow PMS.

## Why (context)

TaskFlow is a project management system for software teams. Every other
feature (projects, work items, sprints, boards) depends on knowing WHO
the user is and WHAT they are allowed to do. This must be the first
feature. Nothing in the app is accessible without logging in, except
the registration and login screens themselves.

## Users and roles

There are three roles with strictly increasing privileges:

- **Developer** — the default role for every newly registered user.
  Can view and work on items assigned to them or their projects.
- **Manager** — can create projects, sprints, and assign work.
- **Admin** — can manage users, including changing any user's role.

Exact per-feature permissions will be defined in later feature specs;
this feature only needs to establish the roles and make the current
user's role available to both backend and frontend.

## User stories

1. As a visitor, I can register with my full name, email, and password
   so that I can access TaskFlow. My account is created with the
   Developer role.
2. As a registered user, I can log in with email and password and stay
   logged in across page refreshes until my session expires or I log
   out.
3. As a logged-in user, I can see my name and role in the app header
   and can log out from there.
4. As an Admin, I can view a list of all users and change any user's
   role (Developer ↔ Manager ↔ Admin).
5. As the system owner, I need one initial Admin account to exist so
   that the first Admin does not have to be created by hand in the
   database (seed it at first startup).

## Acceptance criteria

### Registration
- Full name (2–100 chars), email (valid format, unique), password
  required.
- Password rules: minimum 8 characters, at least one letter and one
  number. Show the rules on the form before the user submits.
- Duplicate email returns a clear error: "An account with this email
  already exists."
- Passwords are never stored in plain text and never returned by any
  API response.
- On success, the user is automatically logged in and taken to the
  app's home page.

### Login
- Email + password form with inline validation messages.
- Wrong email or wrong password both return the SAME generic error
  ("Invalid email or password") — do not reveal which one was wrong.
- On success the user lands on the app's home page.

### Session behavior
- The session lasts 8 hours; after expiry the user is redirected to
  the login page with the message "Your session has expired."
- Refreshing the browser keeps the user logged in (within the 8 hours).
- Logout ends the session immediately and returns to the login page.
- An unauthenticated user who tries to open any protected page is
  redirected to login, and after logging in is taken to the page they
  originally requested.

### Role management (Admin only)
- A "Users" page, visible ONLY to Admins, lists all users with name,
  email, role, and registration date, with pagination.
- Admin can change a user's role from this list; the change takes
  effect on that user's next request.
- An Admin cannot demote themselves if they are the last remaining
  Admin (guard against locking everyone out).
- Non-admins attempting to access user management (by URL or API) are
  refused.

### Non-functional
- All API error responses use a consistent error shape.
- Rate-limit failed login attempts: after 5 failed attempts for the
  same email within 15 minutes, block further attempts for 15 minutes
  with the message "Too many attempts, try again later."
- Frontend route protection is user experience only; every protected
  API endpoint must independently verify identity and role.

## Out of scope (do NOT include in this feature)

- Password reset / forgot password (future feature)
- Email verification (future feature)
- OAuth / social login (not planned)
- Profile editing, avatars (future feature)
- Refresh-token rotation (keep session handling simple for now)

## Success check

Feature is complete when: a new visitor can register, log in, see
their name and role in the header, stay logged in across refreshes,
log out; the seeded Admin can log in and change that user's role to
Manager; and a Developer cannot open the Users page by any means.
