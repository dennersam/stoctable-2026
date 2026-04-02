using Stoctable.Application.Results;
using Stoctable.Communication.Responses;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Entities;
using Stoctable.Exceptions;

namespace Stoctable.Application.Services.Suppliers;

public record SupplierRequest(
    string CompanyName,
    string? TradeName = null,
    string? Cnpj = null,
    string? Address = null,
    string? Phone = null,
    string? Email = null,
    string? ContactPerson = null,
    string? Notes = null);

public record SupplierResponse(
    Guid Id,
    string CompanyName,
    string? TradeName,
    string? Cnpj,
    string? Address,
    string? Phone,
    string? Email,
    string? ContactPerson,
    bool IsActive,
    string? Notes,
    DateTimeOffset CreatedAt);

public class SupplierService(ISupplierRepository supplierRepository)
{
    public async Task<Result<IEnumerable<SupplierResponse>>> GetAllAsync(CancellationToken ct = default)
    {
        var suppliers = await supplierRepository.GetAllAsync(ct);
        return Result<IEnumerable<SupplierResponse>>.Success(suppliers.Select(MapToResponse));
    }

    public async Task<Result<PagedResult<SupplierResponse>>> GetPagedAsync(
        int page, int pageSize, string? search, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (items, totalCount) = await supplierRepository.GetPagedAsync(page, pageSize, search, ct);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        return Result<PagedResult<SupplierResponse>>.Success(new PagedResult<SupplierResponse>(
            items.Select(MapToResponse), totalCount, page, pageSize, totalPages));
    }

    public async Task<Result<SupplierResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var supplier = await supplierRepository.GetByIdAsync(id, ct);
        if (supplier is null)
            return Result<SupplierResponse>.NotFound(ErrorMessages.Supplier.NotFound);

        return Result<SupplierResponse>.Success(MapToResponse(supplier));
    }

    public async Task<Result<IEnumerable<SupplierResponse>>> SearchAsync(string query, CancellationToken ct = default)
    {
        var suppliers = await supplierRepository.SearchAsync(query, ct);
        return Result<IEnumerable<SupplierResponse>>.Success(suppliers.Select(MapToResponse));
    }

    public async Task<Result<SupplierResponse>> CreateAsync(SupplierRequest request, CancellationToken ct = default)
    {
        if (request.Cnpj is not null &&
            await supplierRepository.ExistsAsync(s => s.Cnpj == request.Cnpj, ct))
            return Result<SupplierResponse>.Conflict(ErrorMessages.Supplier.CnpjAlreadyExists);

        var supplier = new Supplier
        {
            CompanyName = request.CompanyName,
            TradeName = request.TradeName,
            Cnpj = request.Cnpj,
            Address = request.Address,
            Phone = request.Phone,
            Email = request.Email,
            ContactPerson = request.ContactPerson,
            Notes = request.Notes
        };

        await supplierRepository.AddAsync(supplier, ct);
        return Result<SupplierResponse>.Success(MapToResponse(supplier), 201);
    }

    public async Task<Result<SupplierResponse>> UpdateAsync(Guid id, SupplierRequest request, CancellationToken ct = default)
    {
        var supplier = await supplierRepository.GetByIdAsync(id, ct);
        if (supplier is null)
            return Result<SupplierResponse>.NotFound(ErrorMessages.Supplier.NotFound);

        if (request.Cnpj is not null && request.Cnpj != supplier.Cnpj &&
            await supplierRepository.ExistsAsync(s => s.Cnpj == request.Cnpj && s.Id != id, ct))
            return Result<SupplierResponse>.Conflict(ErrorMessages.Supplier.CnpjAlreadyExists);

        supplier.CompanyName = request.CompanyName;
        supplier.TradeName = request.TradeName;
        supplier.Cnpj = request.Cnpj;
        supplier.Address = request.Address;
        supplier.Phone = request.Phone;
        supplier.Email = request.Email;
        supplier.ContactPerson = request.ContactPerson;
        supplier.Notes = request.Notes;

        await supplierRepository.UpdateAsync(supplier, ct);
        return Result<SupplierResponse>.Success(MapToResponse(supplier));
    }

    public async Task<Result<bool>> DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        var supplier = await supplierRepository.GetByIdAsync(id, ct);
        if (supplier is null)
            return Result<bool>.NotFound(ErrorMessages.Supplier.NotFound);

        supplier.IsActive = false;
        await supplierRepository.UpdateAsync(supplier, ct);
        return Result<bool>.Success(true);
    }

    private static SupplierResponse MapToResponse(Supplier s) => new(
        Id: s.Id,
        CompanyName: s.CompanyName,
        TradeName: s.TradeName,
        Cnpj: s.Cnpj,
        Address: s.Address,
        Phone: s.Phone,
        Email: s.Email,
        ContactPerson: s.ContactPerson,
        IsActive: s.IsActive,
        Notes: s.Notes,
        CreatedAt: s.CreatedAt);
}
