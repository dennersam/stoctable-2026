namespace Stoctable.Communication.Requests.Products;

public record CreateProductRequest(
    string Name,
    decimal SalePrice,
    decimal CostPrice,
    string Unit = "UN",
    string? Barcode = null,
    Guid? ManufacturerId = null,
    Guid? CategoryId = null,
    Guid? SupplierId = null,
    decimal StockMinimum = 0,
    decimal? IcmsRate = null,
    decimal? IpiRate = null,
    string? Cst = null,
    string? Ncm = null,
    string? Notes = null);
