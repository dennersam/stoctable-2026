using Stoctable.Domain.Entities.Base;

namespace Stoctable.Domain.Entities;

public class Product : BaseEntity
{
    /// <summary>Código do produto (TABEST1.codigo no SIC 6)</summary>
    public string Sku { get; set; } = string.Empty;

    /// <summary>Código de barras EAN (TABEST1.cean)</summary>
    public string? Barcode { get; set; }

    /// <summary>Descrição do produto (TABEST1.produto)</summary>
    public string Name { get; set; } = string.Empty;

    public Guid? ManufacturerId { get; set; }

    public Guid? CategoryId { get; set; }
    public Guid? SupplierId { get; set; }

    /// <summary>Preço de custo (TABEST1.precocusto)</summary>
    public decimal CostPrice { get; set; }

    /// <summary>Preço de venda (TABEST1.precovendas)</summary>
    public decimal SalePrice { get; set; }

    /// <summary>Quantidade em estoque (TABEST1.quantidade)</summary>
    public decimal StockQuantity { get; set; }

    /// <summary>Quantidade reservada por orçamentos finalizados</summary>
    public decimal StockReserved { get; set; }

    /// <summary>Estoque mínimo (TABEST1.estminimo)</summary>
    public decimal StockMinimum { get; set; }

    public string Unit { get; set; } = "UN";

    /// <summary>Alíquota ICMS (TABEST1.icms)</summary>
    public decimal? IcmsRate { get; set; }

    /// <summary>Alíquota IPI (TABEST1.ipi)</summary>
    public decimal? IpiRate { get; set; }

    /// <summary>Código de Situação Tributária (TABEST1.cst)</summary>
    public string? Cst { get; set; }

    /// <summary>Nomenclatura Comum do Mercosul (TABEST1.ncm)</summary>
    public string? Ncm { get; set; }

    /// <summary>URL da imagem no Azure Blob Storage</summary>
    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    public ProductCategory? Category { get; set; }
    public Manufacturer? Manufacturer { get; set; }
    public Supplier? Supplier { get; set; }
    public ICollection<QuotationItem> QuotationItems { get; set; } = [];
    public ICollection<SaleItem> SaleItems { get; set; } = [];
    public ICollection<InventoryMovement> InventoryMovements { get; set; } = [];
    public ICollection<StockReservation> StockReservations { get; set; } = [];
}
