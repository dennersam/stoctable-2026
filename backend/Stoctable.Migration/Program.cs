using Microsoft.Extensions.Configuration;
using Stoctable.Migration;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var sicConnStr = config["SicConnectionString"]
    ?? throw new InvalidOperationException("SicConnectionString não configurada em appsettings.json");

var pgConnStr = config["PostgresConnectionString"]
    ?? throw new InvalidOperationException("PostgresConnectionString não configurada em appsettings.json");

Console.WriteLine("╔══════════════════════════════════════════╗");
Console.WriteLine("║   Stoctable Migration Tool — SIC 6 → PG  ║");
Console.WriteLine("╚══════════════════════════════════════════╝");
Console.WriteLine($"  Origem : SQL Server  ({GetDatabaseName(sicConnStr)})");
Console.WriteLine($"  Destino: PostgreSQL  ({GetDatabaseName(pgConnStr)})");
Console.WriteLine();
Console.Write("Pressione ENTER para iniciar ou Ctrl+C para cancelar... ");
Console.ReadLine();

try
{
    var runner = new MigrationRunner(sicConnStr, pgConnStr);
    await runner.RunAsync();
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\n✗ Erro: {ex.Message}");
    Console.ResetColor();
    Environment.Exit(1);
}

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
