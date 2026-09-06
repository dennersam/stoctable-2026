namespace Stoctable.Infrastructure.Search;

public static class DbSearchFunctions
{
    /// <summary>
    /// Mapeada para f_search_norm(text) via HasDbFunction — normaliza o texto
    /// no banco (minúsculas + sem acentos). Só existe para ser traduzida em
    /// consultas EF Core; nunca é executada em memória.
    /// </summary>
    public static string? Normalize(string? value)
        => throw new NotSupportedException("Disponível apenas em consultas EF Core.");
}
