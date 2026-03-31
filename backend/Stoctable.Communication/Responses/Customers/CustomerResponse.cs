namespace Stoctable.Communication.Responses.Customers;

public record CustomerResponse(
    Guid Id,
    string FullName,
    string DocumentType,
    string? DocumentNumber,
    string? Email,
    string? Phone,
    string? Mobile,
    string? Address,
    string? City,
    string? State,
    string? ZipCode,
    Guid? CustomerTypeId,
    string? CustomerTypeName,
    decimal CreditLimit,
    bool IsActive,
    string? Notes,
    DateTimeOffset CreatedAt);

public record CustomerCrmNoteResponse(
    Guid Id,
    Guid CustomerId,
    string Note,
    string NoteType,
    DateTimeOffset CreatedAt,
    string CreatedBy);
