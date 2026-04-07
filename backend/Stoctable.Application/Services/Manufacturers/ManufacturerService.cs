using Stoctable.Application.Results;
using Stoctable.Communication.Responses;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Entities;
using Stoctable.Exceptions;

namespace Stoctable.Application.Services.Manufacturers;

public record ManufacturerRequest(string Name, string? Notes = null);

public record ManufacturerResponse(
    Guid Id,
    string Name,
    string? Notes,
    bool IsActive,
    DateTimeOffset CreatedAt);

public class ManufacturerService(IManufacturerRepository manufacturerRepository)
{
    public async Task<Result<IEnumerable<ManufacturerResponse>>> GetAllActiveAsync(CancellationToken ct = default)
    {
        var items = await manufacturerRepository.GetActiveAsync(ct);
        return Result<IEnumerable<ManufacturerResponse>>.Success(items.Select(MapToResponse));
    }

    public async Task<Result<IEnumerable<ManufacturerResponse>>> GetAllAsync(CancellationToken ct = default)
    {
        var items = await manufacturerRepository.GetAllAsync(ct);
        return Result<IEnumerable<ManufacturerResponse>>.Success(
            items.OrderBy(m => m.Name).Select(MapToResponse));
    }

    public async Task<Result<PagedResult<ManufacturerResponse>>> GetPagedAsync(
        int page, int pageSize, string? search, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (items, totalCount) = await manufacturerRepository.GetPagedAsync(page, pageSize, search, ct);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        return Result<PagedResult<ManufacturerResponse>>.Success(new PagedResult<ManufacturerResponse>(
            items.Select(MapToResponse), totalCount, page, pageSize, totalPages));
    }

    public async Task<Result<ManufacturerResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var manufacturer = await manufacturerRepository.GetByIdAsync(id, ct);
        if (manufacturer is null)
            return Result<ManufacturerResponse>.NotFound(ErrorMessages.Manufacturer.NotFound);

        return Result<ManufacturerResponse>.Success(MapToResponse(manufacturer));
    }

    public async Task<Result<ManufacturerResponse>> CreateAsync(ManufacturerRequest request, CancellationToken ct = default)
    {
        if (await manufacturerRepository.ExistsAsync(m => m.Name == request.Name, ct))
            return Result<ManufacturerResponse>.Conflict(ErrorMessages.Manufacturer.NameAlreadyExists);

        var manufacturer = new Manufacturer
        {
            Name = request.Name.Trim(),
            Notes = request.Notes
        };

        await manufacturerRepository.AddAsync(manufacturer, ct);
        return Result<ManufacturerResponse>.Success(MapToResponse(manufacturer), 201);
    }

    public async Task<Result<ManufacturerResponse>> UpdateAsync(Guid id, ManufacturerRequest request, CancellationToken ct = default)
    {
        var manufacturer = await manufacturerRepository.GetByIdAsync(id, ct);
        if (manufacturer is null)
            return Result<ManufacturerResponse>.NotFound(ErrorMessages.Manufacturer.NotFound);

        if (await manufacturerRepository.ExistsAsync(m => m.Name == request.Name && m.Id != id, ct))
            return Result<ManufacturerResponse>.Conflict(ErrorMessages.Manufacturer.NameAlreadyExists);

        manufacturer.Name = request.Name.Trim();
        manufacturer.Notes = request.Notes;

        await manufacturerRepository.UpdateAsync(manufacturer, ct);
        return Result<ManufacturerResponse>.Success(MapToResponse(manufacturer));
    }

    public async Task<Result<bool>> DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        var manufacturer = await manufacturerRepository.GetByIdAsync(id, ct);
        if (manufacturer is null)
            return Result<bool>.NotFound(ErrorMessages.Manufacturer.NotFound);

        manufacturer.IsActive = false;
        await manufacturerRepository.UpdateAsync(manufacturer, ct);
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> HardDeleteAsync(Guid id, CancellationToken ct = default)
    {
        var manufacturer = await manufacturerRepository.GetByIdAsync(id, ct);
        if (manufacturer is null)
            return Result<bool>.NotFound(ErrorMessages.Manufacturer.NotFound);

        try
        {
            await manufacturerRepository.DeleteAsync(manufacturer, ct);
            return Result<bool>.Success(true);
        }
        catch (Exception)
        {
            return Result<bool>.Conflict("Não é possível excluir este fabricante pois ele está vinculado a produtos.");
        }
    }

    private static ManufacturerResponse MapToResponse(Manufacturer m) => new(
        Id: m.Id,
        Name: m.Name,
        Notes: m.Notes,
        IsActive: m.IsActive,
        CreatedAt: m.CreatedAt);
}
