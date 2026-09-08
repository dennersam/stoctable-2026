using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stoctable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMinimumToProductStocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "minimum",
                table: "product_stocks",
                type: "numeric(10,3)",
                precision: 10,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            // O mínimo era do catálogo e passa a ser de cada loja. As linhas que
            // já existem herdam o valor que valia para todo mundo; daí em diante
            // cada filial ajusta o seu. Produto sem linha nesta filial não ganha
            // uma aqui de propósito — ela nasce na primeira movimentação.
            migrationBuilder.Sql("""
                UPDATE product_stocks ps
                   SET minimum = p.stock_minimum
                  FROM products p
                 WHERE p.id = ps.product_id
                   AND p.stock_minimum > 0
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "minimum",
                table: "product_stocks");
        }
    }
}
