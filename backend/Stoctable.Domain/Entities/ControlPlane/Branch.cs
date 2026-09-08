using Stoctable.Domain.Entities.Base;

namespace Stoctable.Domain.Entities.ControlPlane;

/// <summary>
/// Um estabelecimento da empresa. No CNPJ isso é o sufixo de ordem:
/// <c>0001</c> é a matriz e <c>0002</c> em diante são as filiais, todas
/// compartilhando a mesma raiz de 8 dígitos.
///
/// Mora no control plane, e não no banco do tenant, porque a lista de filiais
/// precisa ser conhecida ANTES de escolher a qual conectar — o contrário seria
/// circular. Substitui a entidade <c>Branch</c> que existia no tenant e nunca
/// chegou a ser usada por nada.
/// </summary>
public class Branch : BaseEntity
{
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }

    /// <summary>
    /// Sigla curta usada na interface e no prefixo dos documentos
    /// (<c>ORC-PENHA-2026090001</c>). Única dentro da empresa.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Somente dígitos, 14 posições. Cada estabelecimento tem o seu.</summary>
    public string? Cnpj { get; set; }

    public string RazaoSocial { get; set; } = string.Empty;
    public string? NomeFantasia { get; set; }

    /// <summary>Verdadeiro para o estabelecimento de sufixo 0001.</summary>
    public bool IsHeadquarters { get; set; }

    public string? Address { get; set; }
    public string? Neighborhood { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Phone { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Nome de exibição: fantasia quando houver, senão a razão social.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(NomeFantasia) ? RazaoSocial : NomeFantasia;
}
