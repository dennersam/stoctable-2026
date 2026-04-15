namespace Stoctable.Communication.Responses.Users;

public record UserResponse(
    Guid Id,
    string Username,
    string Email,
    string FullName,
    string Role,
    bool IsActive,
    string? AvatarUrl,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt);
