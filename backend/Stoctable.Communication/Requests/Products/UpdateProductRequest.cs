namespace Stoctable.Communication.Requests.Products;

public record UpdateProductRequest(
    string? Name = null,
    decimal? SalePrice = null,
    decimal? CostPrice = null,
    string? Unit = null,
    string? Barcode = null,
    string? Manufacturer = null,
    Guid? CategoryId = null,
    Guid? SupplierId = null,
    decimal? StockMinimum = null,
    decimal? IcmsRate = null,
    decimal? IpiRate = null,
    string? Cst = null,
    string? Ncm = null,
    string? Notes = null,
    bool? IsActive = null);
