#nullable disable

namespace Stoctable.Infrastructure.Migrations
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Migrations;
    using Stoctable.Infrastructure.Context;
    using Stoctable.Infrastructure.Search;

    /// <summary>
    /// Cria as extensões unaccent/pg_trgm, a função IMMUTABLE f_search_norm e os
    /// índices GIN trigrama que sustentam a busca textual por tokens.
    /// Não altera o modelo EF, portanto o ModelSnapshot permanece inalterado.
    /// </summary>
    [DbContext(typeof(StoctableDbContext))]
    [Migration("20260906120000_AddSearchNormalizationAndTrigramIndexes")]
    public partial class AddSearchNormalizationAndTrigramIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
            => migrationBuilder.Sql(SearchSchema.Up);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
            => migrationBuilder.Sql(SearchSchema.Down);
    }
}
