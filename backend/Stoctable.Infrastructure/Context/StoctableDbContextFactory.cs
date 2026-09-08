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
        // Gerar migrations não abre conexão — o provider só precisa de uma string
        // sintaticamente válida. O placeholder abaixo existe para que
        // `dotnet ef migrations add` funcione sem configurar nada; APLICAR a
        // migration (`database update`) exige a variável de ambiente de verdade.
        //
        // Antes havia aqui uma connection string do Neon com senha real, em
        // arquivo versionado. Ela foi removida e precisa ser rotacionada.
        var connectionString = Environment.GetEnvironmentVariable("DEFAULT_CONN_STRING")
            ?? "Host=localhost;Database=stoctable_design_time;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<StoctableDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new StoctableDbContext(optionsBuilder.Options);
    }
}
