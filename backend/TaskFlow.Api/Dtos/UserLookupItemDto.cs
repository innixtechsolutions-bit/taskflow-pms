namespace TaskFlow.Api.Dtos;

// Deliberately minimal — id and full name only, not email/role/registration date like
// UserListItemDto — because this is exposed to any authenticated caller (research.md
// §9, Feature 002), not just Admins.
public record UserLookupItemDto(int Id, string FullName);
