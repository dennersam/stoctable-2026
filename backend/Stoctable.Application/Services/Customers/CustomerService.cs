using Stoctable.Application.Results;
using Stoctable.Communication.Requests.Customers;
using Stoctable.Communication.Responses;
using Stoctable.Communication.Responses.Customers;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Entities;
using Stoctable.Exceptions;

namespace Stoctable.Application.Services.Customers;

public class CustomerService(ICustomerRepository customerRepository)
{
    public async Task<Result<IEnumerable<CustomerResponse>>> GetAllAsync(CancellationToken ct = default)
    {
        var customers = await customerRepository.GetAllAsync(ct);
        return Result<IEnumerable<CustomerResponse>>.Success(customers.Select(MapToResponse));
    }

    public async Task<Result<PagedResult<CustomerResponse>>> GetPagedAsync(
        int page, int pageSize, string? search, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (items, totalCount) = await customerRepository.GetPagedAsync(page, pageSize, search, ct);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var result = new PagedResult<CustomerResponse>(
            Items: items.Select(MapToResponse),
            TotalCount: totalCount,
            Page: page,
            PageSize: pageSize,
            TotalPages: totalPages);

        return Result<PagedResult<CustomerResponse>>.Success(result);
    }

    public async Task<Result<CustomerResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var customer = await customerRepository.GetByIdAsync(id, ct);
        if (customer is null)
            return Result<CustomerResponse>.NotFound(ErrorMessages.Customer.NotFound);

        return Result<CustomerResponse>.Success(MapToResponse(customer));
    }

    public async Task<Result<IEnumerable<CustomerResponse>>> SearchAsync(string query, CancellationToken ct = default)
    {
        var customers = await customerRepository.SearchAsync(query, ct);
        return Result<IEnumerable<CustomerResponse>>.Success(customers.Select(MapToResponse));
    }

    public async Task<Result<CustomerResponse>> GetWithCrmHistoryAsync(Guid id, CancellationToken ct = default)
    {
        var customer = await customerRepository.GetWithCrmNotesAsync(id, ct);
        if (customer is null)
            return Result<CustomerResponse>.NotFound(ErrorMessages.Customer.NotFound);

        return Result<CustomerResponse>.Success(MapToResponse(customer));
    }

    public async Task<Result<IEnumerable<CustomerCrmNoteResponse>>> GetCrmNotesAsync(Guid id, CancellationToken ct = default)
    {
        var customer = await customerRepository.GetWithCrmNotesAsync(id, ct);
        if (customer is null)
            return Result<IEnumerable<CustomerCrmNoteResponse>>.NotFound(ErrorMessages.Customer.NotFound);

        var notes = customer.CrmNotes.Select(n => new CustomerCrmNoteResponse(
            Id: n.Id,
            CustomerId: n.CustomerId,
            Note: n.Note,
            NoteType: n.NoteType,
            CreatedAt: n.CreatedAt,
            CreatedBy: n.CreatedBy));

        return Result<IEnumerable<CustomerCrmNoteResponse>>.Success(notes);
    }

    public async Task<Result<CustomerResponse>> CreateAsync(CreateCustomerRequest request, CancellationToken ct = default)
    {
        if (request.DocumentNumber is not null &&
            await customerRepository.ExistsAsync(c => c.DocumentNumber == request.DocumentNumber, ct))
            return Result<CustomerResponse>.Conflict(ErrorMessages.Customer.DocumentAlreadyExists);

        var customer = new Customer
        {
            FullName = request.FullName,
            DocumentType = request.DocumentType,
            DocumentNumber = request.DocumentNumber,
            Email = request.Email,
            Phone = request.Phone,
            Mobile = request.Mobile,
            Address = request.Address,
            City = request.City,
            State = request.State,
            ZipCode = request.ZipCode,
            CustomerTypeId = request.CustomerTypeId,
            CreditLimit = request.CreditLimit,
            Notes = request.Notes
        };

        await customerRepository.AddAsync(customer, ct);
        return Result<CustomerResponse>.Success(MapToResponse(customer), 201);
    }

    public async Task<Result<CustomerResponse>> UpdateAsync(Guid id, CreateCustomerRequest request, CancellationToken ct = default)
    {
        var customer = await customerRepository.GetByIdAsync(id, ct);
        if (customer is null)
            return Result<CustomerResponse>.NotFound(ErrorMessages.Customer.NotFound);

        if (request.DocumentNumber is not null &&
            request.DocumentNumber != customer.DocumentNumber &&
            await customerRepository.ExistsAsync(c => c.DocumentNumber == request.DocumentNumber && c.Id != id, ct))
            return Result<CustomerResponse>.Conflict(ErrorMessages.Customer.DocumentAlreadyExists);

        customer.FullName = request.FullName;
        customer.DocumentType = request.DocumentType;
        customer.DocumentNumber = request.DocumentNumber;
        customer.Email = request.Email;
        customer.Phone = request.Phone;
        customer.Mobile = request.Mobile;
        customer.Address = request.Address;
        customer.City = request.City;
        customer.State = request.State;
        customer.ZipCode = request.ZipCode;
        customer.CustomerTypeId = request.CustomerTypeId;
        customer.CreditLimit = request.CreditLimit;
        customer.Notes = request.Notes;

        await customerRepository.UpdateAsync(customer, ct);
        return Result<CustomerResponse>.Success(MapToResponse(customer));
    }

    public async Task<Result<CustomerCrmNoteResponse>> AddCrmNoteAsync(Guid customerId, string note, string noteType, string createdBy, CancellationToken ct = default)
    {
        var customer = await customerRepository.GetByIdAsync(customerId, ct);
        if (customer is null)
            return Result<CustomerCrmNoteResponse>.NotFound(ErrorMessages.Customer.NotFound);

        var crmNote = new CustomerCrmNote
        {
            CustomerId = customerId,
            Note = note,
            NoteType = noteType,
            CreatedBy = createdBy
        };

        customer.CrmNotes.Add(crmNote);
        await customerRepository.UpdateAsync(customer, ct);

        return Result<CustomerCrmNoteResponse>.Success(new CustomerCrmNoteResponse(
            Id: crmNote.Id,
            CustomerId: crmNote.CustomerId,
            Note: crmNote.Note,
            NoteType: crmNote.NoteType,
            CreatedAt: crmNote.CreatedAt,
            CreatedBy: crmNote.CreatedBy), 201);
    }

    public async Task<Result<bool>> DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        var customer = await customerRepository.GetByIdAsync(id, ct);
        if (customer is null)
            return Result<bool>.NotFound(ErrorMessages.Customer.NotFound);

        customer.IsActive = false;
        await customerRepository.UpdateAsync(customer, ct);
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> HardDeleteAsync(Guid id, CancellationToken ct = default)
    {
        var customer = await customerRepository.GetByIdAsync(id, ct);
        if (customer is null)
            return Result<bool>.NotFound(ErrorMessages.Customer.NotFound);

        await customerRepository.DeleteAsync(customer, ct);
        return Result<bool>.Success(true);
    }

    private static CustomerResponse MapToResponse(Customer c) => new(
        Id: c.Id,
        FullName: c.FullName,
        DocumentType: c.DocumentType,
        DocumentNumber: c.DocumentNumber,
        Email: c.Email,
        Phone: c.Phone,
        Mobile: c.Mobile,
        Address: c.Address,
        City: c.City,
        State: c.State,
        ZipCode: c.ZipCode,
        CustomerTypeId: c.CustomerTypeId,
        CustomerTypeName: c.CustomerType?.Name,
        CreditLimit: c.CreditLimit,
        IsActive: c.IsActive,
        Notes: c.Notes,
        CreatedAt: c.CreatedAt);
}
