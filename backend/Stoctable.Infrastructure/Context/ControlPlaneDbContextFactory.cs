using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Stoctable.Infrastructure.Context;

/// <summary>
/// Factory de design-time do control plane. Como o projeto tem dois DbContext,
/// todo comando do EF precisa dizer qual:
///
///   CONTROL_PLANE_CONN_STRING="Host=...;Database=stoctable_control;..." \
///   dotnet ef migrations add NomeDaMigration \
///     --context ControlPlaneDbContext \
///     --output-dir Migrations/ControlPlane \
///     --project Stoctable.Infrastructure \
///     --startup-project Stoctable.Api
///
/// Sem <c>--context</c> o CLI aborta com "More than one DbContext was found".
/// </summary>
public class ControlPlaneDbContextFactory : IDesignTimeDbContextFactory<ControlPlaneDbContext>
{
    public ControlPlaneDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("CONTROL_PLANE_CONN_STRING")
            ?? "Host=localhost;Database=stoctable_control_design_time;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<ControlPlaneDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npg =>
            npg.MigrationsHistoryTable("__EFMigrationsHistory_ControlPlane"));

        return new ControlPlaneDbContext(optionsBuilder.Options);
    }
}
