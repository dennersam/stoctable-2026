namespace Stoctable.Communication.Responses.Auth;

public record AuthTokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    UserResponse User);

public record UserResponse(
    Guid Id,
    string Username,
    string FullName,
    string Email,
    string Role,
    List<string> BranchIds,
    string? AvatarUrl = null);
