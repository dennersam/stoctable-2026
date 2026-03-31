namespace Stoctable.Communication.Requests.Customers;

public record CreateCustomerRequest(
    string FullName,
    string DocumentType = "CPF",
    string? DocumentNumber = null,
    string? Email = null,
    string? Phone = null,
    string? Mobile = null,
    string? Address = null,
    string? City = null,
    string? State = null,
    string? ZipCode = null,
    Guid? CustomerTypeId = null,
    decimal CreditLimit = 0,
    string? Notes = null);
