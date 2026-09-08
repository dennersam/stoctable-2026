using Stoctable.Domain.Entities.Base;
using Stoctable.Domain.Enums;

namespace Stoctable.Domain.Entities.ControlPlane;

/// <summary>
/// Uma empresa cliente do SaaS. É o tenant: cada empresa tem um banco Postgres
/// próprio, cuja connection string mora cifrada aqui.
///
/// A connection string fica nesta tabela e não no Key Vault de propósito — o
/// Key Vault tem soft delete de 90 dias (um cadastro que falha e é retentado
/// colidiria no mesmo nome de segredo), tem limite de escrita, e os segredos
/// <c>STOCTABLE-CONN-*</c> são declarados pelo Terraform, que proporia destruí-los
/// se a aplicação passasse a escrever lá.
/// </summary>
public class Company : BaseEntity
{
    /// <summary>Somente dígitos, 14 posições. Único no SaaS inteiro.</summary>
    public string Cnpj { get; set; } = string.Empty;

    public string RazaoSocial { get; set; } = string.Empty;
    public string? NomeFantasia { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }

    public CompanyStatus Status { get; set; } = CompanyStatus.Provisioning;

    /// <summary>
    /// Derivado de forma determinística do id da empresa. Precisa ser gravado
    /// ANTES da chamada ao provedor: se o processo morrer entre criar o banco e
    /// registrar o nome, a retentativa reusa o mesmo nome e o provedor devolve
    /// "já existe", que o provisionamento trata como sucesso.
    /// </summary>
    public string? DatabaseName { get; set; }

    /// <summary>
    /// Qual implementação de <c>ITenantDatabaseProvisioner</c> criou este banco
    /// ("neon", "azure"). Guardado por empresa, e não em configuração global,
    /// porque a migração para a Azure será gradual — empresas antigas continuam
    /// no Neon enquanto as novas já nascem na Azure.
    /// </summary>
    public string DatabaseProvider { get; set; } = "neon";

    /// <summary>Connection string cifrada em AES-GCM. Nunca sai daqui em texto puro.</summary>
    public byte[]? ConnectionStringEncrypted { get; set; }

    public DateTimeOffset? ProvisionedAt { get; set; }

    public ICollection<Branch> Branches { get; set; } = [];
    public ICollection<Account> Accounts { get; set; } = [];
}
