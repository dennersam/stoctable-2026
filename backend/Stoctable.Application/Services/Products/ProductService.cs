using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Stoctable.Application.Results;
using Stoctable.Communication.Requests.Products;
using Stoctable.Communication.Responses;
using Stoctable.Communication.Responses.Products;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Entities;
using Stoctable.Exceptions;

namespace Stoctable.Application.Services.Products;

public class ProductService(IProductRepository productRepository, IConfiguration configuration)
{
    public async Task<Result<IEnumerable<ProductResponse>>> GetAllAsync(CancellationToken ct = default)
    {
        var products = await productRepository.GetAllAsync(ct);
        return Result<IEnumerable<ProductResponse>>.Success(products.Select(MapToResponse));
    }

    public async Task<Result<PagedResult<ProductResponse>>> GetPagedAsync(
        int page, int pageSize, string? search, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (items, totalCount) = await productRepository.GetPagedAsync(page, pageSize, search, ct);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var result = new PagedResult<ProductResponse>(
            Items: items.Select(MapToResponse),
            TotalCount: totalCount,
            Page: page,
            PageSize: pageSize,
            TotalPages: totalPages);

        return Result<PagedResult<ProductResponse>>.Success(result);
    }

    public async Task<Result<ProductResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var product = await productRepository.GetByIdAsync(id, ct);
        if (product is null)
            return Result<ProductResponse>.NotFound(ErrorMessages.Product.NotFound);

        return Result<ProductResponse>.Success(MapToResponse(product));
    }

    public async Task<Result<IEnumerable<ProductResponse>>> SearchAsync(string query, CancellationToken ct = default)
    {
        var products = await productRepository.SearchAsync(query, ct);
        return Result<IEnumerable<ProductResponse>>.Success(products.Select(MapToResponse));
    }

    public async Task<Result<IEnumerable<ProductResponse>>> GetLowStockAsync(CancellationToken ct = default)
    {
        var products = await productRepository.GetLowStockAsync(ct);
        return Result<IEnumerable<ProductResponse>>.Success(products.Select(MapToResponse));
    }

    public async Task<Result<ProductResponse>> CreateAsync(CreateProductRequest request, CancellationToken ct = default)
    {
        if (request.Barcode is not null &&
            await productRepository.ExistsAsync(p => p.Barcode == request.Barcode, ct))
            return Result<ProductResponse>.Conflict(ErrorMessages.Product.BarcodeAlreadyExists);

        // SKU gerado automaticamente como número sequencial
        var nextSku = await productRepository.GetNextSkuAsync(ct);

        var product = new Product
        {
            Sku = nextSku.ToString(),
            Name = request.Name,
            SalePrice = request.SalePrice,
            CostPrice = request.CostPrice,
            Unit = request.Unit,
            Barcode = request.Barcode,
            ManufacturerId = request.ManufacturerId,
            CategoryId = request.CategoryId,
            SupplierId = request.SupplierId,
            StockMinimum = request.StockMinimum,
            IcmsRate = request.IcmsRate,
            IpiRate = request.IpiRate,
            Cst = request.Cst,
            Ncm = request.Ncm,
            Notes = request.Notes
        };

        await productRepository.AddAsync(product, ct);
        return Result<ProductResponse>.Success(MapToResponse(product), 201);
    }

    public async Task<Result<ProductResponse>> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct = default)
    {
        var product = await productRepository.GetByIdAsync(id, ct);
        if (product is null)
            return Result<ProductResponse>.NotFound(ErrorMessages.Product.NotFound);

        if (request.Barcode is not null && request.Barcode != product.Barcode &&
            await productRepository.ExistsAsync(p => p.Barcode == request.Barcode && p.Id != id, ct))
            return Result<ProductResponse>.Conflict(ErrorMessages.Product.BarcodeAlreadyExists);

        if (request.Name is not null) product.Name = request.Name;
        if (request.SalePrice is not null) product.SalePrice = request.SalePrice.Value;
        if (request.CostPrice is not null) product.CostPrice = request.CostPrice.Value;
        if (request.Unit is not null) product.Unit = request.Unit;
        if (request.Barcode is not null) product.Barcode = request.Barcode;
        if (request.ManufacturerId is not null) product.ManufacturerId = request.ManufacturerId;
        if (request.CategoryId is not null) product.CategoryId = request.CategoryId;
        if (request.SupplierId is not null) product.SupplierId = request.SupplierId;
        if (request.StockMinimum is not null) product.StockMinimum = request.StockMinimum.Value;
        if (request.IcmsRate is not null) product.IcmsRate = request.IcmsRate;
        if (request.IpiRate is not null) product.IpiRate = request.IpiRate;
        if (request.Cst is not null) product.Cst = request.Cst;
        if (request.Ncm is not null) product.Ncm = request.Ncm;
        if (request.Notes is not null) product.Notes = request.Notes;
        if (request.IsActive is not null) product.IsActive = request.IsActive.Value;

        await productRepository.UpdateAsync(product, ct);
        return Result<ProductResponse>.Success(MapToResponse(product));
    }

    public async Task<Result<ProductResponse>> UploadImageAsync(Guid id, Stream imageStream, string contentType, string fileName, CancellationToken ct = default)
    {
        var product = await productRepository.GetByIdAsync(id, ct);
        if (product is null)
            return Result<ProductResponse>.NotFound(ErrorMessages.Product.NotFound);

        var connectionString = configuration["Storage:ConnectionString"];
        if (string.IsNullOrEmpty(connectionString))
            return Result<ProductResponse>.Failure("Armazenamento de imagens não configurado.");

        var containerClient = new BlobContainerClient(connectionString, "product-images");
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);

        var blobName = $"{id}/{fileName}";
        var blobClient = containerClient.GetBlobClient(blobName);

        await blobClient.UploadAsync(imageStream, new BlobHttpHeaders { ContentType = contentType }, cancellationToken: ct);

        product.ImageUrl = blobClient.Uri.ToString();
        await productRepository.UpdateAsync(product, ct);

        return Result<ProductResponse>.Success(MapToResponse(product));
    }

    public async Task<Result<bool>> DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        var product = await productRepository.GetByIdAsync(id, ct);
        if (product is null)
            return Result<bool>.NotFound(ErrorMessages.Product.NotFound);

        product.IsActive = false;
        await productRepository.UpdateAsync(product, ct);
        return Result<bool>.Success(true);
    }

    private static ProductResponse MapToResponse(Product p) => new(
        Id: p.Id,
        Sku: p.Sku,
        Name: p.Name,
        Barcode: p.Barcode,
        ManufacturerId: p.ManufacturerId,
        ManufacturerName: p.Manufacturer?.Name,
        CategoryId: p.CategoryId,
        CategoryName: p.Category?.Name,
        SupplierId: p.SupplierId,
        SupplierName: p.Supplier?.CompanyName,
        CostPrice: p.CostPrice,
        SalePrice: p.SalePrice,
        StockQuantity: p.StockQuantity,
        StockReserved: p.StockReserved,
        StockAvailable: p.StockQuantity - p.StockReserved,
        StockMinimum: p.StockMinimum,
        Unit: p.Unit,
        IcmsRate: p.IcmsRate,
        IpiRate: p.IpiRate,
        Cst: p.Cst,
        Ncm: p.Ncm,
        ImageUrl: p.ImageUrl,
        IsActive: p.IsActive,
        Notes: p.Notes,
        CreatedAt: p.CreatedAt);
}
