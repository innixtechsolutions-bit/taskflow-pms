namespace TaskFlow.Api.Dtos;

// Separate from AuthResponse: /me re-confirms identity/role for an already-signed-in
// caller (FR-013) and has no reason to hand back a token the caller already has.
public record MeResponse(int Id, string FullName, string Role);
