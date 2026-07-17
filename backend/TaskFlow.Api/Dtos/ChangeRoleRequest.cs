using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Api.Dtos;

// Role travels as a string (Developer|Manager|Admin), same convention as every other
// role-carrying DTO in this API (see AuthResponse) — validated against the actual
// Role enum in UserService, not here, since "not a real role" and "last-Admin guard"
// are both business rules that belong together.
public class ChangeRoleRequest
{
    [Required]
    public required string Role { get; set; }
}
