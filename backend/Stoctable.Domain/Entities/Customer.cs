using Stoctable.Domain.Entities.Base;

namespace Stoctable.Domain.Entities;

public class Customer : BaseEntity
{
    /// <summary>Nome completo (TABCLI.nome)</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>CPF ou CNPJ</summary>
    public string DocumentType { get; set; } = "CPF";

    /// <summary>Número do CPF ou CNPJ (TABCLI.cgc)</summary>
    public string? DocumentNumber { get; set; }

    public string? Email { get; set; }
    public string? Phone { get; set; }

    /// <summary>Celular (TABCLI.celular)</summary>
    public string? Mobile { get; set; }

    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }

    public Guid? CustomerTypeId { get; set; }

    /// <summary>Limite de crédito (TABCLI.limitecred)</summary>
    public decimal CreditLimit { get; set; }

    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    public CustomerType? CustomerType { get; set; }
    public ICollection<CustomerCrmNote> CrmNotes { get; set; } = [];
    public ICollection<Quotation> Quotations { get; set; } = [];
    public ICollection<Sale> Sales { get; set; } = [];
}
