# Feature Specification: User Registration, Login & Role-Based Access

**Feature Branch**: `001-user-auth`

**Created**: 2026-07-14

**Status**: Draft

**Input**: User description: "Build user registration, login, and role-based access for TaskFlow PMS. TaskFlow is a project management system for software teams; every other feature (projects, work items, sprints, boards) depends on knowing who the user is and what they are allowed to do, so this must be the first feature. Three roles with increasing privileges: Developer (default for new registrations), Manager (creates projects/sprints, assigns work), Admin (manages users and roles). Includes registration, login, session persistence across refresh with 8-hour expiry, logout, an Admin-only Users page for listing and changing roles with a last-admin self-demotion guard, a seeded initial Admin account, and login rate-limiting. Excludes password reset, email verification, OAuth/social login, profile editing, and refresh-token rotation."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Register for a TaskFlow Account (Priority: P1)

A visitor with no account creates one by providing their full name, email, and
password, and is immediately signed in as a Developer — the entry point that
every other TaskFlow capability depends on.

**Why this priority**: Nothing else in TaskFlow is reachable without an
account. This is the absolute foundation of the feature and the product.

**Independent Test**: Can be fully tested by submitting the registration form
with valid, unique details and confirming the visitor lands on the home page,
signed in with the Developer role — delivers a working account on its own.

**Acceptance Scenarios**:

1. **Given** a visitor on the registration page, **When** they submit a full
   name (2-100 characters), a valid unique email, and a password meeting the
   password rules, **Then** an account is created with the Developer role and
   the visitor is automatically signed in and taken to the home page.
2. **Given** a visitor submitting the registration form, **When** the email
   they enter already belongs to an existing account, **Then** registration
   is rejected with the message "An account with this email already exists."
   and no new account is created.
3. **Given** a visitor submitting the registration form, **When** the password
   does not meet the minimum rules (at least 8 characters, at least one
   letter and one number), **Then** registration is rejected and the password
   rules are shown to the visitor before they resubmit.

---

### User Story 2 - Log In and Stay Signed In (Priority: P1)

A person with an existing account signs in with their email and password,
remains signed in across page refreshes for the duration of their session,
and can sign out whenever they choose.

**Why this priority**: Returning users need a reliable way back into the
product, and session continuity is what makes the rest of the app usable
without repeated logins on every page load.

**Independent Test**: Can be fully tested by signing in with a known
account's credentials, refreshing the browser to confirm the session
persists, and signing out to confirm the session ends — delivers a complete,
working authentication cycle on its own.

**Acceptance Scenarios**:

1. **Given** a person with a valid account, **When** they submit the correct
   email and password, **Then** they are signed in and taken to the home
   page.
2. **Given** a person on the login page, **When** they submit an email that
   does not exist or a password that does not match, **Then** they see the
   same generic message ("Invalid email or password") in both cases, without
   any indication of which field was wrong.
3. **Given** a signed-in person, **When** they refresh the browser at any
   point within 8 hours of signing in, **Then** they remain signed in.
4. **Given** a person signed in for more than 8 hours, **When** they next
   interact with the app, **Then** they are redirected to the login page with
   the message "Your session has expired."
5. **Given** a signed-in person, **When** they choose to log out, **Then**
   their session ends immediately and they are returned to the login page.
6. **Given** an unauthenticated visitor, **When** they try to open a page
   that requires sign-in, **Then** they are redirected to the login page and,
   after signing in successfully, are taken to the page they originally
   requested.
7. **Given** a person who has failed to log in 5 times for the same email
   within 15 minutes, **When** they attempt to log in again, **Then** the
   attempt is blocked with the message "Too many attempts, try again later."
   until 15 minutes have elapsed.

---

### User Story 3 - See My Identity in the App (Priority: P2)

A signed-in person can see their own name and role at all times while using
TaskFlow, and can sign out from wherever they are in the app.

**Why this priority**: Builds directly on sign-in (User Story 2) and is
necessary for a person to trust and understand which account and permission
level they are acting under, but the product is minimally usable without it
if sign-in already works.

**Independent Test**: Can be fully tested by signing in and confirming the
signed-in person's name and role are visible in the app header, with a working
sign-out control — delivers a complete, self-contained identity display.

**Acceptance Scenarios**:

1. **Given** a signed-in person, **When** they view any page in the app,
   **Then** their name and current role are visible in the app header.
2. **Given** a signed-in person, **When** they select log out from the
   header, **Then** their session ends and they return to the login page.

---

### User Story 4 - Admin Manages User Roles (Priority: P3)

An Admin views a list of every registered person and changes any person's
role between Developer, Manager, and Admin, while being prevented from
locking the system out of Admin access entirely.

**Why this priority**: Role management is essential for the product long
term, but the system is already functional for individual users once
Stories 1-3 work; this story extends control to administrators and can be
delivered after the core sign-up/sign-in loop is in place.

**Independent Test**: Can be fully tested by signing in as an Admin, opening
the Users page, changing another person's role, and confirming the change
takes effect — delivers complete role-management capability on its own.

**Acceptance Scenarios**:

1. **Given** a signed-in Admin, **When** they open the Users page, **Then**
   they see every registered person's name, email, role, and registration
   date, presented with pagination.
2. **Given** a signed-in Admin viewing the Users page, **When** they change
   another person's role, **Then** the change takes effect on that person's
   next request.
3. **Given** a signed-in Admin who is the only remaining Admin, **When** they
   attempt to change their own role away from Admin, **Then** the change is
   refused and they remain an Admin.
4. **Given** a signed-in person who is not an Admin, **When** they try to
   reach the Users page or the underlying role-management capability by any
   means (navigation, direct link, or direct request), **Then** access is
   refused.

---

### Edge Cases

- What happens when a visitor submits the registration form with an email
  that differs from an existing account only by letter case (e.g.,
  `User@Example.com` vs `user@example.com`)? Treated as the same account for
  uniqueness purposes.
- What happens when a person's session expires while they are in the middle
  of filling out a form? Their next action is redirected to login with the
  expiry message; unsaved input is lost.
- What happens when the last remaining Admin is deleted or role-managed by
  another path outside this feature? Out of scope here; this feature only
  guards the self-demotion path described in User Story 4.
- How does the system handle a login attempt for an email that has exceeded
  the rate limit but with the correct password? Still blocked until the
  15-minute window elapses — the limit is on attempts, not correctness.
- What happens when someone who is signed in visits the registration or
  login page directly? They are redirected to the home page instead of
  seeing the form again.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow a visitor to register for a new account by
  providing full name (2-100 characters), email, and password.
- **FR-002**: System MUST require email addresses to be unique (case-
  insensitive) and validly formatted, and MUST reject duplicate registration
  attempts with the message "An account with this email already exists."
- **FR-003**: System MUST require passwords to be at least 8 characters long
  and contain at least one letter and one number, and MUST display these
  rules to the visitor before they submit the registration form.
- **FR-004**: System MUST create every newly registered account with the
  Developer role by default.
- **FR-005**: System MUST automatically sign a person in and take them to the
  home page immediately after successful registration.
- **FR-006**: System MUST never store a password in plain text and MUST never
  return a password in any response.
- **FR-007**: System MUST allow a person with an existing account to sign in
  using their email and password.
- **FR-008**: System MUST reject an incorrect email or incorrect password
  with the same generic message ("Invalid email or password") in both cases,
  without revealing which one was wrong.
- **FR-009**: System MUST keep a signed-in person's session active for 8
  hours from sign-in, including across browser refreshes.
- **FR-010**: System MUST redirect a person whose session has expired to the
  login page with the message "Your session has expired."
- **FR-011**: System MUST end a person's session immediately when they log
  out and return them to the login page.
- **FR-012**: System MUST redirect an unauthenticated person who requests a
  protected page to the login page, and MUST send them to the originally
  requested page after they successfully sign in.
- **FR-013**: System MUST display the signed-in person's name and current
  role in the app header on every page, with a log-out control available from
  the header.
- **FR-014**: System MUST provide an Admin-only Users page listing every
  registered person's name, email, role, and registration date, with
  pagination.
- **FR-015**: System MUST allow an Admin to change any person's role among
  Developer, Manager, and Admin, with the change taking effect on that
  person's next request.
- **FR-016**: System MUST prevent an Admin from changing their own role away
  from Admin when they are the last remaining Admin.
- **FR-017**: System MUST refuse access to the Users page and to role-change
  capability for any person who is not an Admin, regardless of how access is
  attempted (navigation, direct link, or direct request).
- **FR-018**: System MUST ensure at least one Admin account exists from first
  startup, without requiring manual, direct creation in the underlying data
  store.
- **FR-019**: System MUST block further sign-in attempts for a given email
  for 15 minutes after 5 failed attempts within a 15-minute window, showing
  the message "Too many attempts, try again later."
- **FR-020**: System MUST present all API-facing error responses in a
  consistent shape.
- **FR-021**: System MUST treat any client-side (frontend) route protection
  as a usability aid only; every protected server-side capability MUST
  independently verify the requester's identity and role.

*Deferred to a future feature (explicitly out of scope for this feature)*:
password reset / forgot password, email verification, OAuth or social login,
profile editing (including avatars), and refresh-token rotation.

### Key Entities

- **User Account**: Represents a registered person. Key attributes: full
  name, unique email, password (never exposed), role (Developer, Manager, or
  Admin), registration date.
- **Role**: One of three ordered permission levels — Developer (default),
  Manager, Admin — that determines what a signed-in person is allowed to see
  and do, both in this feature and in every future TaskFlow feature.
- **Session**: Represents a person's signed-in state, with a defined 8-hour
  lifetime from sign-in, ended either by expiry or explicit log-out.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A new visitor can go from an empty registration form to being
  signed in on the home page in under 1 minute.
- **SC-002**: 100% of attempts to open a protected page while signed out
  result in a redirect to login, and 100% of those, once signed in
  successfully, land on the originally requested page.
- **SC-003**: A signed-in person remains signed in across browser refreshes
  for the entirety of their 8-hour session with zero unexpected sign-outs.
- **SC-004**: An Admin can locate any registered person and change their role
  in under 30 seconds from opening the Users page.
- **SC-005**: Zero passwords are ever observable in an API response or in
  application logs across a full audit of the feature.
- **SC-006**: 100% of accounts that accumulate 5 failed sign-in attempts
  within 15 minutes are blocked from further attempts until the 15-minute
  window elapses.
- **SC-007**: 100% of non-Admin attempts to reach the Users page or change a
  role, by any route, are refused.

## Assumptions

- Session/authentication mechanism details (e.g., token format, storage) are
  left to the planning phase; this spec fixes only the observable behavior —
  an 8-hour absolute session lifetime with no rolling renewal or refresh-token
  rotation, per the explicit out-of-scope list.
- Email uniqueness is enforced case-insensitively, matching common industry
  practice, since the source description did not specify case sensitivity.
- The initial seeded Admin account's email and password are supplied by the
  system owner via configuration (user-secrets in development, environment
  variables in other environments) rather than a fixed, publicly-known
  default. If no Admin account exists yet and these configured credentials
  are missing or empty at first startup, the system MUST fail to start with
  a clear error explaining what must be configured, rather than starting
  without an Admin or silently falling back to a default.
- "Registration date" shown on the Users page refers to the date the account
  was created, in the system's standard date format.
- Role changes are visible to the affected person starting with their next
  request; there is no requirement to push an immediate, real-time update to
  an already-open session.
