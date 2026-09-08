using Microsoft.EntityFrameworkCore;
using Npgsql;
using Stoctable.Domain.Entities.ControlPlane;
using Stoctable.Domain.Enums;
using Stoctable.Infrastructure.Context;
using Stoctable.Infrastructure.Tenancy;

namespace Stoctable.Migration;

/// <summary>
/// Transforma a instalação atual — um banco só, sem noção de empresa — na
/// primeira empresa do control plane.
///
/// Nenhum dado muda de banco: o <c>neondb</c> de hoje passa a ser o banco
/// tenant da empresa, e o control plane nasce apontando para ele. O que este
/// comando cria é a camada de identidade que faltava: a empresa, as filiais e
/// uma conta de login por usuário existente.
///
/// É idempotente — rodar duas vezes não duplica nada. A verificação é pelo CNPJ
/// da empresa, que é único.
/// </summary>
public class ControlPlaneBackfill(
    string controlPlaneConnStr,
    string tenantConnStr,
    IConnectionStringProtector protector)
{
    /// <summary>
    /// As três lojas são três pessoas jurídicas distintas — raízes de CNPJ
    /// 10800462, 30914869 e 53426966, todas com sufixo 0001 (matriz) próprio.
    /// Nenhuma é filial da outra no sentido da Receita.
    ///
    /// Elas se juntam sob uma Company só porque compartilham catálogo, clientes
    /// e fornecedores: Company aqui é a identidade do CONTRATO com o SaaS, e
    /// Branch carrega a identidade FISCAL de cada loja.
    ///
    /// Consequência a lembrar quando entrar emissão fiscal: a nota de cada loja
    /// sai sob o CNPJ da Branch, nunca sob o da Company.
    ///
    /// Razões sociais conferidas na base da Receita via BrasilAPI.
    /// </summary>
    private static readonly BranchSeed[] Branches =
    [
        new("MEGA",  "10800462000147", "ANDER MOTOS LTDA",                  "MEGAMOTOS",  IsHeadquarters: true),
        new("MOTO",  "30914869000102", "MOTO PENHA PECAS AUTOMOTIVAS LTDA", "MOTOPENHA",  IsHeadquarters: false),
        new("VILLA", "53426966000151", "VILLA MOTOS LTDA",                  "VILLAMOTOS", IsHeadquarters: false),
    ];

    /// <summary>
    /// O estoque migrado do SIC veio do banco da MEGAMOTOS, então é para essa
    /// filial que as linhas de <c>product_stocks</c> vão. As outras duas começam
    /// sem estoque, para inventário.
    /// </summary>
    private const string StockOwnerCode = "MEGA";

    private const string CompanyCnpj = "10800462000147";
    private const string CompanyRazaoSocial = "ANDER MOTOS LTDA";
    private const string CompanyNomeFantasia = "MEGAMOTOS";

    private record BranchSeed(string Code, string? Cnpj, string RazaoSocial, string NomeFantasia, bool IsHeadquarters);

    public async Task RunAsync()
    {
        await using var control = CreateControlContext();

        Log("→ Aplicando migrations do control plane...");
        await control.Database.MigrateAsync();

        var existing = await control.Companies
            .Include(c => c.Branches)
            .FirstOrDefaultAsync(c => c.Cnpj == CompanyCnpj);

        if (existing is not null)
        {
            Log($"✓ Empresa já existe ({existing.RazaoSocial}) — atualizando o que falta.");
            await StoreConnectionStringAsync(existing, control);
            await RemapBranchScopedRowsAsync(existing, control);
            return;
        }

        var company = new Company
        {
            Cnpj = CompanyCnpj,
            RazaoSocial = CompanyRazaoSocial,
            NomeFantasia = CompanyNomeFantasia,
            Status = CompanyStatus.Ready,
            DatabaseName = DatabaseNameOf(tenantConnStr),
            DatabaseProvider = "neon",
            ProvisionedAt = DateTimeOffset.UtcNow,
            CreatedBy = "backfill",
        };

        foreach (var seed in Branches)
        {
            company.Branches.Add(new Branch
            {
                Code = seed.Code,
                Cnpj = seed.Cnpj,
                RazaoSocial = seed.RazaoSocial,
                NomeFantasia = seed.NomeFantasia,
                IsHeadquarters = seed.IsHeadquarters,
                CreatedBy = "backfill",
            });
        }

        control.Companies.Add(company);
        await control.SaveChangesAsync();
        Log($"✓ Empresa criada: {company.RazaoSocial} ({company.Branches.Count} filiais)");

        await BackfillAccountsAsync(company, control);
        await StoreConnectionStringAsync(company, control);
        await RemapBranchScopedRowsAsync(company, control);

        Log("");
        Log("Concluído.");
    }

    /// <summary>
    /// Grava a connection string do tenant cifrada na empresa. Sem isto o
    /// CompanyConnectionResolver não tem como abrir o banco e toda requisição
    /// autenticada responde 503.
    /// </summary>
    private async Task StoreConnectionStringAsync(Company company, ControlPlaneDbContext control)
    {
        if (company.ConnectionStringEncrypted is { Length: > 0 })
        {
            Log("✓ Connection string cifrada já estava gravada.");
            return;
        }

        company.ConnectionStringEncrypted = protector.Protect(tenantConnStr);
        company.DatabaseName ??= DatabaseNameOf(tenantConnStr);
        await control.SaveChangesAsync();

        Log("✓ Connection string do tenant cifrada e gravada na empresa");
    }

    /// <summary>
    /// Cria uma conta de login por usuário do tenant, copiando o hash BCrypt
    /// verbatim — hash é portável, então ninguém precisa trocar de senha.
    ///
    /// O e-mail vira a identidade global de login. Usuário sem e-mail é pulado
    /// e reportado: não dá para inventar identidade.
    /// </summary>
    private async Task BackfillAccountsAsync(Company company, ControlPlaneDbContext control)
    {
        Log("→ Migrando usuários para contas de login...");

        await using var tenant = new NpgsqlConnection(tenantConnStr);
        await tenant.OpenAsync();

        const string sql = """
            SELECT id, username, email, password_hash, full_name, role, is_active, avatar_url, last_login_at
              FROM users
             ORDER BY created_at
            """;

        var branchIds = company.Branches.Select(b => b.Id).ToList();
        var skipped = new List<string>();
        var created = 0;

        await using (var cmd = new NpgsqlCommand(sql, tenant))
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var username = reader.GetString(reader.GetOrdinal("username"));
                var email = reader.IsDBNull(reader.GetOrdinal("email"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("email"));

                if (string.IsNullOrWhiteSpace(email))
                {
                    skipped.Add(username);
                    continue;
                }

                var account = new Account
                {
                    // Mesmo Id do usuário no tenant: a tabela `users` de lá vira
                    // projeção de exibição desta conta, e audit_logs/created_by
                    // continuam apontando para o mesmo identificador.
                    Id = reader.GetGuid(reader.GetOrdinal("id")),
                    CompanyId = company.Id,
                    Username = username,
                    Email = email.Trim().ToLowerInvariant(),
                    PasswordHash = reader.GetString(reader.GetOrdinal("password_hash")),
                    FullName = reader.GetString(reader.GetOrdinal("full_name")),
                    Role = Enum.Parse<UserRole>(reader.GetString(reader.GetOrdinal("role")), true),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                    AvatarUrl = reader.IsDBNull(reader.GetOrdinal("avatar_url"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("avatar_url")),
                    LastLoginAt = reader.IsDBNull(reader.GetOrdinal("last_login_at"))
                        ? null
                        : reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("last_login_at")),
                    CreatedBy = "backfill",
                };

                control.Accounts.Add(account);

                // Todo mundo começa com acesso às três lojas; restringir é
                // decisão de administração, feita depois pela interface.
                foreach (var branchId in branchIds)
                    control.AccountBranches.Add(new AccountBranch { AccountId = account.Id, BranchId = branchId });

                created++;
            }
        }

        await control.SaveChangesAsync();
        Log($"✓ {created} contas criadas, com acesso às {branchIds.Count} filiais");

        if (skipped.Count > 0)
        {
            Warn($"⚠ {skipped.Count} usuário(s) sem e-mail foram pulados e não conseguirão entrar:");
            foreach (var username in skipped)
                Warn($"    - {username}");
            Warn("  Cadastre um e-mail para cada um no sistema antigo e rode de novo.");
        }
    }

    /// <summary>
    /// As linhas de escopo de filial nasceram com o id provisório
    /// <see cref="BranchContext.LegacySingleBranchId"/>, gravado literalmente
    /// nas migrations (que não podem referenciar código da aplicação). Agora que
    /// a filial real existe, todas passam para ela.
    ///
    /// Todo o histórico é da MEGAMOTOS: era a única loja no sistema até aqui.
    /// </summary>
    private async Task RemapBranchScopedRowsAsync(Company company, ControlPlaneDbContext control)
    {
        var owner = company.Branches.FirstOrDefault(b => b.Code == StockOwnerCode)
            ?? await control.Branches.FirstAsync(b => b.CompanyId == company.Id && b.Code == StockOwnerCode);

        Log($"→ Reapontando dados de filial para {owner.NomeFantasia} ({owner.Code})...");

        string[] tabelas =
        [
            "product_stocks", "stock_reservations", "sales", "quotations",
            "payments", "inventory_movements", "audit_logs", "number_sequences",
        ];

        await using var tenant = new NpgsqlConnection(tenantConnStr);
        await tenant.OpenAsync();

        foreach (var tabela in tabelas)
        {
            await using var cmd = new NpgsqlCommand(
                $"UPDATE {tabela} SET branch_id = @branch_id WHERE branch_id = @legacy_id", tenant);
            cmd.Parameters.AddWithValue("branch_id", owner.Id);
            cmd.Parameters.AddWithValue("legacy_id", BranchContext.LegacySingleBranchId);

            var rows = await cmd.ExecuteNonQueryAsync();
            if (rows > 0) Log($"    {tabela}: {rows} linha(s)");
        }

        Log("✓ Reapontamento concluído");
    }

    private ControlPlaneDbContext CreateControlContext()
    {
        var opts = new DbContextOptionsBuilder<ControlPlaneDbContext>()
            .UseNpgsql(controlPlaneConnStr, npg =>
                npg.MigrationsHistoryTable("__EFMigrationsHistory_ControlPlane"))
            .Options;
        return new ControlPlaneDbContext(opts);
    }

    private static string DatabaseNameOf(string connStr)
        => new NpgsqlConnectionStringBuilder(connStr).Database ?? "(desconhecido)";

    private static void Log(string message) => Console.WriteLine(message);

    private static void Warn(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(message);
        Console.ResetColor();
    }
}
