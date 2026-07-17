namespace TaskFlow.Api.Services;

// Thrown by AuthService and translated to specific HTTP status codes by AuthController —
// keeps the service focused on business rules and the controller focused on HTTP concerns.
public class EmailAlreadyExistsException() : Exception("An account with this email already exists.");

public class InvalidPasswordException() : Exception(
    "Password must be at least 8 characters and include at least one letter and one number.");

// Deliberately the same message/exception for both a wrong email and a wrong password
// (FR-008) — the caller must not be able to distinguish which one was wrong.
public class InvalidCredentialsException() : Exception("Invalid email or password.");

public class TooManyAttemptsException() : Exception("Too many attempts, try again later.");
