namespace Stoctable.Communication.Responses.Auth;

public record AuthTokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    UserResponse User,
    CompanyResponse Company,
    List<BranchResponse> Branches,
    /// <summary>
    /// Verdadeiro quando o token entregue é o de PRÉ-FILIAL: a conta tem acesso
    /// a mais de uma loja e ainda não escolheu. Esse token só serve para chamar
    /// /api/auth/select-branch — nenhum endpoint de negócio o aceita.
    /// </summary>
    bool RequiresBranchSelection,
    /// <summary>Filial ativa. Nulo enquanto a escolha não foi feita.</summary>
    Guid? ActiveBranchId);

public record UserResponse(
    Guid Id,
    string Username,
    string FullName,
    string Email,
    string Role,
    List<string> BranchIds,
    string? AvatarUrl = null);

public record CompanyResponse(
    Guid Id,
    string RazaoSocial,
    string? NomeFantasia,
    string Cnpj);

public record BranchResponse(
    Guid Id,
    string Code,
    string Name,
    string? Cnpj,
    bool IsHeadquarters);
