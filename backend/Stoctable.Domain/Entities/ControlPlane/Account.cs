using Stoctable.Domain.Entities.Base;
using Stoctable.Domain.Enums;

namespace Stoctable.Domain.Entities.ControlPlane;

/// <summary>
/// A identidade de login, única no SaaS inteiro pelo e-mail.
///
/// Chama-se Account e não User de propósito: <c>Stoctable.Domain.Entities.User</c>
/// continua existindo no banco de cada tenant como projeção de exibição (o
/// <c>audit_logs</c>, o <c>created_by</c> e o vendedor da venda apontam para ela).
/// Duas classes com o mesmo nome em namespaces diferentes seria fonte garantida
/// de erro de <c>using</c>.
///
/// A senha e os tokens vivem aqui e SAEM da tabela do tenant — autenticação
/// acontece antes de saber qual banco abrir.
/// </summary>
public class Account : BaseEntity
{
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }

    /// <summary>Identidade de login. Único globalmente, guardado em minúsculas.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Apelido de exibição, herdado do sistema antigo. Único dentro da empresa,
    /// não globalmente — duas empresas podem ter um "admin" cada.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Atendente;
    public bool IsActive { get; set; } = true;
    public string? AvatarUrl { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>Hash SHA-256 do refresh token; o token cru só existe na resposta HTTP.</summary>
    public string? RefreshTokenHash { get; set; }
    public DateTimeOffset? RefreshTokenExpiresAt { get; set; }

    public string? PasswordResetTokenHash { get; set; }
    public DateTimeOffset? PasswordResetTokenExpiresAt { get; set; }

    public ICollection<AccountBranch> Branches { get; set; } = [];
}
