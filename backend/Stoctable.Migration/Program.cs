using Microsoft.Extensions.Configuration;
using Stoctable.Infrastructure.Tenancy;
using Stoctable.Migration;

Console.OutputEncoding = System.Text.Encoding.UTF8;

// Variáveis de ambiente por último, para sobrescrever o arquivo: é assim que
// se aponta a ferramenta para produção sem gravar credencial em disco.
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

// A variável de ambiente vem primeiro: assim dá para apontar para produção sem
// gravar credencial em arquivo versionado.
//
// ⚠️ appsettings.json É VERSIONADO (o .gitignore só cobre appsettings.*.json,
// com sufixo). Não escreva senha nele: use DEFAULT_CONN_STRING. Chave vazia é
// tratada como ausente justamente para que o arquivo possa ficar sem segredo.
var pgConnStr = FirstNonEmpty(
        Environment.GetEnvironmentVariable("DEFAULT_CONN_STRING"),
        config["PostgresConnectionString"])
    ?? throw new InvalidOperationException(
        "Connection string do Postgres não configurada. Defina DEFAULT_CONN_STRING.");

// Duas operações distintas convivem nesta ferramenta:
//   sic       → importa o sistema legado (SQL Server) para o Postgres
//   backfill  → cria a empresa e as filiais no control plane
var command = args.FirstOrDefault()?.ToLowerInvariant() ?? "sic";

try
{
    switch (command)
    {
        case "sic":
            await RunSicMigrationAsync();
            break;

        case "backfill":
            await RunControlPlaneBackfillAsync();
            break;

        case "verify":
            await RunVerificationAsync();
            break;

        case "stock-cutover":
            await RunStockCutoverAsync();
            break;

        default:
            Console.WriteLine($"Comando desconhecido: '{command}'");
            Console.WriteLine("Uso: dotnet run -- [sic|backfill|verify|stock-cutover [apply]]");
            Environment.Exit(1);
            break;
    }
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\n✗ Erro: {ex.Message}");
    Console.ResetColor();
    Environment.Exit(1);
}

async Task RunSicMigrationAsync()
{
    var sicConnStr = config["SicConnectionString"]
        ?? throw new InvalidOperationException("SicConnectionString não configurada em appsettings.json");

    Console.WriteLine("╔══════════════════════════════════════════╗");
    Console.WriteLine("║   Stoctable Migration Tool — SIC 6 → PG  ║");
    Console.WriteLine("╚══════════════════════════════════════════╝");
    Console.WriteLine($"  Origem : SQL Server  ({GetDatabaseName(sicConnStr)})");
    Console.WriteLine($"  Destino: PostgreSQL  ({GetDatabaseName(pgConnStr)})");
    Console.WriteLine();
    Console.Write("Pressione ENTER para iniciar ou Ctrl+C para cancelar... ");
    Console.ReadLine();

    var runner = new MigrationRunner(sicConnStr, pgConnStr);
    await runner.RunAsync();
}

async Task RunVerificationAsync()
{
    var verification = new ControlPlaneVerification(
        ControlPlaneConnStr(), pgConnStr, new ConnectionStringProtector(config));
    var ok = await verification.RunAsync();

    // Código de saída != 0 para que dê para agendar isto e ser avisado.
    if (!ok) Environment.Exit(2);
}

async Task RunStockCutoverAsync()
{
    // Sem "apply" é só relatório — o padrão seguro, porque este comando decide
    // qual saldo cada loja passa a ter.
    var apply = args.Skip(1).Any(a => a.Equals("apply", StringComparison.OrdinalIgnoreCase));

    Console.WriteLine("╔══════════════════════════════════════════╗");
    Console.WriteLine("║  Corte do estoque → product_stocks       ║");
    Console.WriteLine("╚══════════════════════════════════════════╝");
    Console.WriteLine($"  Control plane: {GetDatabaseName(ControlPlaneConnStr())}");
    Console.WriteLine($"  Tenant       : {GetDatabaseName(pgConnStr)}");
    Console.WriteLine($"  Modo         : {(apply ? "APLICAR (escreve)" : "verificação (só leitura)")}");
    Console.WriteLine();

    if (apply)
    {
        Console.WriteLine("  O saldo de products.stock_quantity vira o estoque da filial MEGA.");
        Console.WriteLine("  As outras lojas não são tocadas.");
        Console.WriteLine();
        Console.Write("Pressione ENTER para aplicar ou Ctrl+C para cancelar... ");
        Console.ReadLine();
    }

    var cutover = new StockCutover(ControlPlaneConnStr(), pgConnStr);
    var ok = await cutover.RunAsync(apply);

    // Código de saída != 0 para que um pipeline consiga barrar o deploy que
    // derruba as colunas enquanto ainda houver divergência.
    if (!ok) Environment.Exit(2);
}

async Task RunControlPlaneBackfillAsync()
{
    var controlConnStr = ControlPlaneConnStr();

    Console.WriteLine("╔══════════════════════════════════════════╗");
    Console.WriteLine("║  Backfill do control plane — Megamotos   ║");
    Console.WriteLine("╚══════════════════════════════════════════╝");
    Console.WriteLine($"  Control plane: {GetDatabaseName(controlConnStr)}");
    Console.WriteLine($"  Tenant       : {GetDatabaseName(pgConnStr)}");
    Console.WriteLine();
    Console.WriteLine("  Cria a empresa, as três filiais e uma conta de login por");
    Console.WriteLine("  usuário existente. Nenhum dado sai do banco atual.");
    Console.WriteLine();
    Console.Write("Pressione ENTER para iniciar ou Ctrl+C para cancelar... ");
    Console.ReadLine();

    var backfill = new ControlPlaneBackfill(
        controlConnStr, pgConnStr, new ConnectionStringProtector(config));
    await backfill.RunAsync();
}

string ControlPlaneConnStr()
    => Environment.GetEnvironmentVariable("CONTROL_PLANE_CONN_STRING")
       ?? NullIfEmpty(config["ControlPlaneConnectionString"])
       ?? throw new InvalidOperationException(
           "Connection string do control plane não configurada "
           + "(CONTROL_PLANE_CONN_STRING ou appsettings.json).");

// Chave presente porém vazia conta como ausente: é o que permite manter o
// appsettings.json versionado sem segredo dentro.
static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

static string? FirstNonEmpty(params string?[] values) => values.Select(NullIfEmpty).FirstOrDefault(v => v is not null);

static string GetDatabaseName(string connStr)
{
    foreach (var part in connStr.Split(';'))
    {
        var kv = part.Split('=');
        if (kv.Length == 2 &&
            (kv[0].Trim().Equals("Database", StringComparison.OrdinalIgnoreCase) ||
             kv[0].Trim().Equals("Initial Catalog", StringComparison.OrdinalIgnoreCase)))
            return kv[1].Trim();
    }
    return "(desconhecido)";
}
