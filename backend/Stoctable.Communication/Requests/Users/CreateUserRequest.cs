namespace Stoctable.Communication.Requests.Users;

/// <summary>
/// <paramref name="BranchIds"/> lista as filiais que o usuário poderá acessar.
/// Vazio ou nulo dá acesso a todas as filiais da empresa — que é o esperado na
/// maioria dos cadastros. Até a fase 3 este campo era aceito e silenciosamente
/// ignorado.
/// </summary>
public record CreateUserRequest(
    string Username,
    string Email,
    string FullName,
    string Role,
    List<string>? BranchIds = null);

public record UpdateUserRequest(
    string? FullName = null,
    string? Email = null,
    string? Role = null,
    bool? IsActive = null,
    /// <summary>Quando informado, SUBSTITUI a lista de filiais do usuário.</summary>
    List<string>? BranchIds = null);

public record UpdateProfileRequest(
    string? FullName = null,
    string? AvatarUrl = null,
    string? CurrentPassword = null,
    string? NewPassword = null);
