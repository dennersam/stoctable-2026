using Stoctable.Application.Results;
using Stoctable.Application.Services.Auth;
using Stoctable.Communication.Requests.Users;
using Stoctable.Communication.Responses.Users;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Contracts.Services;
using Stoctable.Domain.Entities.ControlPlane;
using Stoctable.Domain.Enums;
using Stoctable.Exceptions;

namespace Stoctable.Application.Services.Users;

/// <summary>
/// Administração de usuários da empresa.
///
/// A fonte da verdade é a tabela <c>accounts</c>, no control plane — é dela que
/// o login lê. A tabela <c>users</c> dentro do banco da empresa é escrita em
/// seguida como projeção de exibição, porque auditoria e vendas apontam para
/// ela. Escrever só no tenant, como era antes, criaria usuários que aparecem na
/// tela e não conseguem entrar.
/// </summary>
public class UserService(
    IAccountRepository accountRepository,
    IUserProjectionWriter projectionWriter,
    ICurrentTenant currentTenant,
    PasswordResetService passwordResetService)
{
    public async Task<Result<IEnumerable<UserResponse>>> GetAllAsync(CancellationToken ct = default)
    {
        var accounts = await accountRepository.ListByCompanyAsync(currentTenant.CompanyId, ct);
        return Result<IEnumerable<UserResponse>>.Success(accounts.Select(MapToResponse));
    }

    public async Task<Result<UserResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var account = await FindInCompanyAsync(id, ct);
        return account is null
            ? Result<UserResponse>.NotFound(ErrorMessages.User.NotFound)
            : Result<UserResponse>.Success(MapToResponse(account));
    }

    public async Task<Result<UserResponse>> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var companyId = currentTenant.CompanyId;

        // Username é apelido dentro da empresa; e-mail é a identidade de login
        // do SaaS inteiro. Daí uma checagem ser por empresa e a outra global.
        if (await accountRepository.UsernameExistsAsync(companyId, request.Username, ct))
            return Result<UserResponse>.Conflict(ErrorMessages.User.UsernameAlreadyExists);

        if (await accountRepository.EmailExistsAsync(request.Email, ct))
            return Result<UserResponse>.Conflict(ErrorMessages.User.EmailAlreadyExists);

        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
            return Result<UserResponse>.Failure($"Role inválido: {request.Role}.");

        var branchIds = await ResolveBranchesAsync(companyId, request.BranchIds, ct);
        if (branchIds.Count == 0)
            return Result<UserResponse>.Failure(ErrorMessages.Auth.NoBranchAssigned);

        var account = new Account
        {
            CompanyId = companyId,
            Username = request.Username,
            Email = request.Email,
            FullName = request.FullName,
            // Sem senha definida pelo admin — o usuário define a própria via convite.
            PasswordHash = string.Empty,
            Role = role,
            IsActive = true,
        };

        // Gera o token de convite na entidade (em memória) e envia o email antes
        // de persistir, para não criar conta sem convite se o email falhar.
        await passwordResetService.SendInviteAsync(account, ct);

        await accountRepository.AddAsync(account, ct);
        await accountRepository.ReplaceBranchesAsync(account.Id, branchIds, ct);
        await projectionWriter.UpsertAsync(account, ct);

        return Result<UserResponse>.Success(MapToResponse(account), 201);
    }

    public async Task<Result<UserResponse>> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var account = await FindInCompanyAsync(id, ct);
        if (account is null)
            return Result<UserResponse>.NotFound(ErrorMessages.User.NotFound);

        if (request.Email is not null && !string.Equals(request.Email, account.Email, StringComparison.OrdinalIgnoreCase)
            && await accountRepository.EmailExistsAsync(request.Email, ct))
            return Result<UserResponse>.Conflict(ErrorMessages.User.EmailAlreadyExists);

        if (request.FullName is not null) account.FullName = request.FullName;
        if (request.Email is not null) account.Email = request.Email.Trim().ToLowerInvariant();
        if (request.IsActive is not null) account.IsActive = request.IsActive.Value;

        if (request.Role is not null)
        {
            if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
                return Result<UserResponse>.Failure($"Role inválido: {request.Role}.");
            account.Role = role;
        }

        await accountRepository.SaveAsync(ct);

        if (request.BranchIds is { Count: > 0 })
        {
            var branchIds = await ResolveBranchesAsync(account.CompanyId, request.BranchIds, ct);
            if (branchIds.Count > 0)
                await accountRepository.ReplaceBranchesAsync(account.Id, branchIds, ct);
        }

        await projectionWriter.UpsertAsync(account, ct);
        return Result<UserResponse>.Success(MapToResponse(account));
    }

    public async Task<Result<UserResponse>> GetMeAsync(Guid userId, CancellationToken ct = default)
    {
        var account = await FindInCompanyAsync(userId, ct);
        return account is null
            ? Result<UserResponse>.NotFound(ErrorMessages.User.NotFound)
            : Result<UserResponse>.Success(MapToResponse(account));
    }

    public async Task<Result<UserResponse>> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        var account = await FindInCompanyAsync(userId, ct);
        if (account is null)
            return Result<UserResponse>.NotFound(ErrorMessages.User.NotFound);

        if (request.NewPassword is not null)
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword))
                return Result<UserResponse>.Failure(ErrorMessages.User.IncorrectPassword);

            if (string.IsNullOrEmpty(account.PasswordHash)
                || !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, account.PasswordHash))
                return Result<UserResponse>.Failure(ErrorMessages.User.IncorrectPassword);

            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        }

        if (request.FullName is not null) account.FullName = request.FullName;
        if (request.AvatarUrl is not null) account.AvatarUrl = request.AvatarUrl;

        await accountRepository.SaveAsync(ct);
        await projectionWriter.UpsertAsync(account, ct);

        return Result<UserResponse>.Success(MapToResponse(account));
    }

    /// <summary>
    /// Reprojeta todas as contas da empresa na tabela do tenant. Existe porque a
    /// projeção atravessa dois bancos e vai divergir em algum momento — a
    /// correção é reprojetar, não uma transação distribuída.
    /// </summary>
    public async Task<Result<int>> ResyncProjectionAsync(CancellationToken ct = default)
    {
        var accounts = await accountRepository.ListByCompanyAsync(currentTenant.CompanyId, ct);
        var total = await projectionWriter.ResyncAsync(accounts, ct);
        return Result<int>.Success(total);
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Nunca busca conta só por id: sem o filtro por empresa, um administrador
    /// poderia ler ou alterar a conta de outra empresa chutando um GUID.
    /// </summary>
    private async Task<Account?> FindInCompanyAsync(Guid id, CancellationToken ct)
    {
        var account = await accountRepository.GetByIdAsync(id, ct);
        return account?.CompanyId == currentTenant.CompanyId ? account : null;
    }

    /// <summary>
    /// Converte os ids pedidos em filiais válidas DA EMPRESA. Sem nada pedido,
    /// dá acesso a todas — é o comportamento esperado na maioria dos cadastros e
    /// o que mantém o formulário atual funcionando.
    /// </summary>
    private async Task<List<Guid>> ResolveBranchesAsync(
        Guid companyId, List<string>? requested, CancellationToken ct)
    {
        var doCompany = await accountRepository.ListCompanyBranchesAsync(companyId, ct);

        if (requested is null || requested.Count == 0)
            return doCompany.Select(b => b.Id).ToList();

        var pedidos = requested
            .Select(id => Guid.TryParse(id, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .ToHashSet();

        return doCompany.Where(b => pedidos.Contains(b.Id)).Select(b => b.Id).ToList();
    }

    private static UserResponse MapToResponse(Account a) => new(
        Id: a.Id,
        Username: a.Username,
        Email: a.Email,
        FullName: a.FullName,
        Role: a.Role.ToString().ToLower(),
        IsActive: a.IsActive,
        AvatarUrl: a.AvatarUrl,
        LastLoginAt: a.LastLoginAt,
        CreatedAt: a.CreatedAt);
}
