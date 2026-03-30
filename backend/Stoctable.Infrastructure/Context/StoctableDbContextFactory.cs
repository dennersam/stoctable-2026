using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Stoctable.Infrastructure.Context;

/// <summary>
/// Usado pelo CLI do EF Core para gerar e aplicar migrations.
/// Lê o connection string da variável de ambiente DEFAULT_CONN_STRING.
///
/// Uso:
///   DEFAULT_CONN_STRING="Host=...;Database=...;" dotnet ef migrations add InitialCreate \
///     --project Stoctable.Infrastructure \
///     --startup-project Stoctable.Api
/// </summary>
public class StoctableDbContextFactory : IDesignTimeDbContextFactory<StoctableDbContext>
{
    public StoctableDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DEFAULT_CONN_STRING")
            ?? "Host=localhost;Database=stoctable_branch_dev;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<StoctableDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new StoctableDbContext(optionsBuilder.Options);
    }
}
