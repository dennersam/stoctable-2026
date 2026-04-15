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
            ?? "Host=ep-divine-sky-ae4edken-pooler.c-2.us-east-2.aws.neon.tech; Database=neondb; Username=neondb_owner; Password=npg_f7edhy4IOJnK; SSL Mode=VerifyFull; Channel Binding=Require;";

        var optionsBuilder = new DbContextOptionsBuilder<StoctableDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new StoctableDbContext(optionsBuilder.Options);
    }
}
