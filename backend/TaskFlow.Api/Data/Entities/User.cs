namespace TaskFlow.Api.Data.Entities;

// Only three roles exist and nothing in this feature calls for admin-configurable
// roles, so Role is a fixed enum rather than a separate table (constitution
// Principle III — Clarity Over Cleverness: no speculative generality).
public enum Role
{
    Developer,
    Manager,
    Admin
}

public class User
{
    public int Id { get; set; }

    public required string FullName { get; set; }

    public required string Email { get; set; }

    // Produced by PasswordHasher<User>; never the raw password, never serialized in a DTO.
    public required string PasswordHash { get; set; }

    public Role Role { get; set; } = Role.Developer;

    public DateTime CreatedAt { get; set; }
}
