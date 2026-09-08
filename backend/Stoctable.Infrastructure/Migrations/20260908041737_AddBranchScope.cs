using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stoctable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // As colunas entram como NULL, são preenchidas e só então viram NOT
            // NULL. Três razões para não usar o DEFAULT que o EF geraria:
            //
            //  1. Um DEFAULT permanente faria um INSERT que esqueceu a filial
            //     gravar silenciosamente numa filial arbitrária, em vez de
            //     falhar — que é exatamente o bug que o escopo por filial
            //     deveria impedir.
            //  2. ADD COLUMN NOT NULL DEFAULT reescreve a tabela inteira; em
            //     três passos o lock é curto.
            //  3. O valor de preenchimento não é Guid.Empty e sim a filial
            //     legada — as linhas existentes são todas da única loja que o
            //     banco tinha até agora, e o backfill do control plane depois
            //     as reaponta para o id verdadeiro da filial.
            const string legacyBranchId = "00000000-0000-0000-0000-000000000001";

            string[] tabelas =
            [
                "stock_reservations", "sales", "quotations",
                "payments", "inventory_movements", "audit_logs",
            ];

            foreach (var tabela in tabelas)
            {
                migrationBuilder.Sql($"ALTER TABLE {tabela} ADD COLUMN branch_id uuid;");
                migrationBuilder.Sql($"UPDATE {tabela} SET branch_id = '{legacyBranchId}'::uuid WHERE branch_id IS NULL;");
                migrationBuilder.Sql($"ALTER TABLE {tabela} ALTER COLUMN branch_id SET NOT NULL;");
            }

            // number_sequences troca de chave primária: (prefix) → (branch_id, prefix).
            // A tabela tem uma linha por prefixo de documento, então o lock é de
            // milissegundos. O prefixo também cresce, porque passa a embutir a
            // sigla da filial (ORC-MEGA-202609).
            migrationBuilder.DropPrimaryKey(
                name: "PK_number_sequences",
                table: "number_sequences");

            migrationBuilder.AlterColumn<string>(
                name: "prefix",
                table: "number_sequences",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.Sql("ALTER TABLE number_sequences ADD COLUMN branch_id uuid;");
            migrationBuilder.Sql($"UPDATE number_sequences SET branch_id = '{legacyBranchId}'::uuid WHERE branch_id IS NULL;");
            migrationBuilder.Sql("ALTER TABLE number_sequences ALTER COLUMN branch_id SET NOT NULL;");

            migrationBuilder.AddPrimaryKey(
                name: "PK_number_sequences",
                table: "number_sequences",
                columns: new[] { "branch_id", "prefix" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_reservations_branch_id_product_id",
                table: "stock_reservations",
                columns: new[] { "branch_id", "product_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_branch_id_created_at",
                table: "sales",
                columns: new[] { "branch_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_quotations_branch_id_created_at",
                table: "quotations",
                columns: new[] { "branch_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_payments_branch_id_paid_at",
                table: "payments",
                columns: new[] { "branch_id", "paid_at" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_branch_id_product_id",
                table: "inventory_movements",
                columns: new[] { "branch_id", "product_id" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_branch_id_occurred_at",
                table: "audit_logs",
                columns: new[] { "branch_id", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_stock_reservations_branch_id_product_id",
                table: "stock_reservations");

            migrationBuilder.DropIndex(
                name: "IX_sales_branch_id_created_at",
                table: "sales");

            migrationBuilder.DropIndex(
                name: "IX_quotations_branch_id_created_at",
                table: "quotations");

            migrationBuilder.DropIndex(
                name: "IX_payments_branch_id_paid_at",
                table: "payments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_number_sequences",
                table: "number_sequences");

            migrationBuilder.DropIndex(
                name: "IX_inventory_movements_branch_id_product_id",
                table: "inventory_movements");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_branch_id_occurred_at",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "branch_id",
                table: "stock_reservations");

            migrationBuilder.DropColumn(
                name: "branch_id",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "branch_id",
                table: "quotations");

            migrationBuilder.DropColumn(
                name: "branch_id",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "branch_id",
                table: "number_sequences");

            migrationBuilder.DropColumn(
                name: "branch_id",
                table: "inventory_movements");

            migrationBuilder.DropColumn(
                name: "branch_id",
                table: "audit_logs");

            migrationBuilder.AlterColumn<string>(
                name: "prefix",
                table: "number_sequences",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);

            migrationBuilder.AddPrimaryKey(
                name: "PK_number_sequences",
                table: "number_sequences",
                column: "prefix");
        }
    }
}
