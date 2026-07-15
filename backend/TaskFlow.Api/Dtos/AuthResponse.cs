namespace TaskFlow.Api.Dtos;

// DTOs exist separately from entities (see User.cs) so the API's public contract
// doesn't accidentally expose internal fields like PasswordHash — AuthResponse only
// ever carries what a client legitimately needs after registering or logging in.
public record AuthResponse(string Token, DateTime ExpiresAt, string FullName, string Role);
