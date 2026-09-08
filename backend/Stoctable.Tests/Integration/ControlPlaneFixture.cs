using Microsoft.EntityFrameworkCore;
using Stoctable.Infrastructure.Context;
using Testcontainers.PostgreSql;

namespace Stoctable.Tests.Integration;

/// <summary>
/// Postgres efêmero para o control plane. Diferente do <see cref="PostgresFixture"/>,
/// este aplica as migrations DE VERDADE (<c>MigrateAsync</c>) em vez de
/// EnsureCreated: as migrations do control plane são novas, não têm o histórico
/// de divergência do snapshot do tenant, e é justamente o artefato mais delicado
/// da fase — vale exercitá-lo a cada execução da suíte.
/// </summary>
public class ControlPlaneFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("stoctable_control_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public ControlPlaneDbContext CreateContext()
    {
        var opts = new DbContextOptionsBuilder<ControlPlaneDbContext>()
            .UseNpgsql(ConnectionString, npg =>
                npg.MigrationsHistoryTable("__EFMigrationsHistory_ControlPlane"))
            .Options;
        return new ControlPlaneDbContext(opts);
    }
}
