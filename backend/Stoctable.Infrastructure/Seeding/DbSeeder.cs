using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Stoctable.Domain.Entities;
using Stoctable.Domain.Enums;
using Stoctable.Infrastructure.Context;

namespace Stoctable.Infrastructure.Seeding;

public static class DbSeeder
{
    public static async Task SeedAsync(StoctableDbContext context, ILogger logger)
    {
        // Aplica migrations pendentes automaticamente
        await context.Database.MigrateAsync();

        await SeedPaymentMethodsAsync(context, logger);
        await SeedCustomerTypesAsync(context, logger);
        await SeedAdminUserAsync(context, logger);
    }

    private static async Task SeedAdminUserAsync(StoctableDbContext context, ILogger logger)
    {
        if (await context.Users.AnyAsync(u => u.Username == "admin"))
            return;

        var admin = new User
        {
            Username = "admin",
            Email = "admin@stoctable.local",
            FullName = "Administrador",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
            Role = UserRole.Admin,
            IsActive = true,
            CreatedBy = "seeder"
        };

        context.Users.Add(admin);
        await context.SaveChangesAsync();

        logger.LogInformation("Usuário admin criado. Login: admin / Admin@123");
    }

    private static async Task SeedPaymentMethodsAsync(StoctableDbContext context, ILogger logger)
    {
        if (await context.PaymentMethods.AnyAsync())
            return;

        var methods = new[]
        {
            new PaymentMethod { Name = "Dinheiro",        IsActive = true, RequiresInstallments = false, MaxInstallments = 1 },
            new PaymentMethod { Name = "PIX",             IsActive = true, RequiresInstallments = false, MaxInstallments = 1 },
            new PaymentMethod { Name = "Débito",          IsActive = true, RequiresInstallments = false, MaxInstallments = 1 },
            new PaymentMethod { Name = "Crédito",         IsActive = true, RequiresInstallments = true,  MaxInstallments = 12 },
            new PaymentMethod { Name = "Boleto",          IsActive = true, RequiresInstallments = false, MaxInstallments = 1 },
            new PaymentMethod { Name = "Cheque",          IsActive = true, RequiresInstallments = false, MaxInstallments = 1 },
        };

        context.PaymentMethods.AddRange(methods);
        await context.SaveChangesAsync();

        logger.LogInformation("Formas de pagamento inseridas ({Count}).", methods.Length);
    }

    private static async Task SeedCustomerTypesAsync(StoctableDbContext context, ILogger logger)
    {
        if (await context.CustomerTypes.AnyAsync())
            return;

        var types = new[]
        {
            new CustomerType { Name = "Varejo",     DiscountPct = 0,  CreditLimit = 0 },
            new CustomerType { Name = "Atacado",    DiscountPct = 10, CreditLimit = 5000 },
            new CustomerType { Name = "Oficina",    DiscountPct = 15, CreditLimit = 10000 },
            new CustomerType { Name = "Revendedor", DiscountPct = 20, CreditLimit = 20000 },
        };

        context.CustomerTypes.AddRange(types);
        await context.SaveChangesAsync();

        logger.LogInformation("Tipos de cliente inseridos ({Count}).", types.Length);
    }
}
