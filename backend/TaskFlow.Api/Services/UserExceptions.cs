namespace TaskFlow.Api.Services;

public class UserNotFoundException() : Exception("User not found.");

public class InvalidRoleException() : Exception("Role must be one of Developer, Manager, or Admin.");

// Thrown only for the self-demotion path (spec.md Edge Cases explicitly scopes FR-016's
// guard to that path) — an Admin changing a *different* Admin's role away from Admin is
// always allowed, since the caller remains an Admin afterward either way.
public class LastAdminException() : Exception(
    "You are the last remaining Admin. Promote someone else to Admin before changing your own role.");
