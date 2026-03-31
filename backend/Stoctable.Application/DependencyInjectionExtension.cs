using Microsoft.Extensions.DependencyInjection;
using Stoctable.Application.Services.Auth;
using Stoctable.Application.Services.Customers;
using Stoctable.Application.Services.Inventory;
using Stoctable.Application.Services.Products;
using Stoctable.Application.Services.Quotations;
using Stoctable.Application.Services.Sales;
using Stoctable.Application.Services.Suppliers;
using Stoctable.Application.Services.Users;

namespace Stoctable.Application;

public static class DependencyInjectionExtension
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<ProductService>();
        services.AddScoped<CustomerService>();
        services.AddScoped<QuotationService>();
        services.AddScoped<SaleService>();
        services.AddScoped<InventoryService>();
        services.AddScoped<SupplierService>();
        services.AddScoped<UserService>();

        return services;
    }
}
