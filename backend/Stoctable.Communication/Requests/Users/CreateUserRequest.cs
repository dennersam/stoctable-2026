namespace Stoctable.Communication.Requests.Users;

public record CreateUserRequest(
    string Username,
    string Email,
    string FullName,
    string Password,
    string Role,
    List<string>? BranchIds = null);

public record UpdateUserRequest(
    string? FullName = null,
    string? Email = null,
    string? Password = null,
    string? Role = null,
    bool? IsActive = null);
