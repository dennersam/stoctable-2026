using Microsoft.EntityFrameworkCore;
using Stoctable.Infrastructure.Context;
using Stoctable.Infrastructure.Search;
using Testcontainers.PostgreSql;

namespace Stoctable.Tests.Integration;

/// <summary>
/// Sobe um Postgres efêmero via Testcontainers para os testes de integração
/// — cada execução começa do zero. Usa EnsureCreated em vez de Migrate
/// porque o snapshot do projeto tem divergências pré-existentes que não
/// fazem parte deste escopo. Compartilhado por classe via IClassFixture.
/// </summary>
public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("stoctable_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public virtual async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var ctx = CreateContext();
        await ctx.Database.EnsureCreatedAsync();

        // EnsureCreated não roda migrations, então o DDL de busca (extensões,
        // f_search_norm e índices GIN) precisa ser aplicado explicitamente —
        // sem ele qualquer consulta de busca falha com "function does not exist".
        await ctx.Database.ExecuteSqlRawAsync(SearchSchema.Up);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public StoctableDbContext CreateContext()
    {
        var opts = new DbContextOptionsBuilder<StoctableDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new StoctableDbContext(opts);
    }
}
