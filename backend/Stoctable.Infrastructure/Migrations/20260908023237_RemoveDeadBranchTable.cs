using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stoctable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDeadBranchTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A tabela deveria estar vazia: nenhum serviço, endpoint ou seed
            // jamais escreveu nela — a resolução de filial sempre usou o nome do
            // segredo no Key Vault. Mas "deveria" não basta para um DROP, então a
            // migration se recusa a rodar se encontrar qualquer linha, em vez de
            // destruir dado que alguém tenha inserido à mão.
            migrationBuilder.Sql("""
                DO $$
                DECLARE linhas bigint;
                BEGIN
                    IF to_regclass('public.branches') IS NULL THEN
                        RETURN;
                    END IF;

                    SELECT count(*) INTO linhas FROM branches;

                    IF linhas > 0 THEN
                        RAISE EXCEPTION
                            'A tabela branches tem % linha(s) e nao pode ser removida automaticamente. '
                            'Migre esses dados para o control plane (companies/branches) antes de aplicar.',
                            linhas;
                    END IF;
                END $$;
                """);

            migrationBuilder.DropTable(
                name: "branches");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "branches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    address = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    cnpj = table.Column<string>(type: "character varying(18)", maxLength: 18, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branches", x => x.id);
                });
        }
    }
}
