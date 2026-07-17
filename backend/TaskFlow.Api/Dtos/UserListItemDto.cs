namespace TaskFlow.Api.Dtos;

public record UserListItemDto(int Id, string FullName, string Email, string Role, DateTime CreatedAt);
