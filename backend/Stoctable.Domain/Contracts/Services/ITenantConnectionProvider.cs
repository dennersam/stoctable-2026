namespace Stoctable.Domain.Contracts.Services;

public interface ITenantConnectionProvider
{
    string GetConnectionString();
}
