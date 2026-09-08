using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stoctable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductStocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "product_stocks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    reserved = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_stocks", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_stocks_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_product_stocks_branch_id_product_id",
                table: "product_stocks",
                columns: new[] { "branch_id", "product_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_stocks_product_id",
                table: "product_stocks",
                column: "product_id");

            // Backfill: todo o estoque existente pertence à única filial que o
            // banco tem hoje. O id abaixo é o BranchContext.LegacySingleBranchId
            // — literal aqui porque migration não referencia código da aplicação.
            // Mudar um exige mudar o outro.
            //
            // Só produtos com saldo ou reserva ganham linha: criar registro
            // zerado para o catálogo inteiro seria ruído, e o upsert do
            // ProductRepository cria a linha sob demanda quando precisar.
            migrationBuilder.Sql("""
                INSERT INTO product_stocks
                    (id, branch_id, product_id, quantity, reserved, created_at, created_by)
                SELECT gen_random_uuid(),
                       '00000000-0000-0000-0000-000000000001'::uuid,
                       p.id,
                       p.stock_quantity,
                       p.stock_reserved,
                       NOW(),
                       'migration'
                  FROM products p
                 WHERE p.stock_quantity <> 0 OR p.stock_reserved <> 0
                ON CONFLICT (branch_id, product_id) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_stocks");
        }
    }
}
