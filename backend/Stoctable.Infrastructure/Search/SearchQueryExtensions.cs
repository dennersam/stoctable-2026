using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Stoctable.Infrastructure.Search;

/// <summary>
/// Busca textual por tokens: cada palavra do termo precisa casar em ao menos
/// um dos campos informados, em qualquer ordem, ignorando acentos e caixa.
/// Substitui o padrão antigo (LOWER(col) LIKE '%termo inteiro%'), que exigia
/// a frase inteira como substring contígua de uma única coluna — por isso
/// "capacete rosa" não encontrava "Capacete Moto Rosa".
/// </summary>
public static class SearchQueryExtensions
{
    // Cada token vira um .Where adicional; sem teto, um texto longo colado no
    // campo de busca geraria uma query desproporcional.
    private const int MaxTokens = 6;

    private static readonly MethodInfo NormalizeMethod =
        typeof(DbSearchFunctions).GetMethod(nameof(DbSearchFunctions.Normalize))!;

    // Usamos ILike do Npgsql, e não EF.Functions.Like, porque a tradução de Like
    // emite "ESCAPE ''" — que desativa o escape e faria '\%' virar barra literal,
    // deixando o '%' digitado pelo usuário agir como curinga. A sobrecarga de 4
    // argumentos permite declarar a barra como escape. ILIKE também é suportado
    // pelo índice gin_trgm_ops, e a coluna já vem em minúsculas de f_search_norm.
    private static readonly MethodInfo ILikeMethod =
        typeof(NpgsqlDbFunctionsExtensions).GetMethod(
            nameof(NpgsqlDbFunctionsExtensions.ILike),
            [typeof(DbFunctions), typeof(string), typeof(string), typeof(string)])!;

    private const string EscapeChar = "\\";

    public static IQueryable<T> WhereMatchesAllTokens<T>(
        this IQueryable<T> source,
        string? search,
        params Expression<Func<T, string?>>[] fields)
    {
        if (fields.Length == 0) return source;

        foreach (var token in Tokenize(search))
        {
            var pattern = $"%{token}%";
            var parameter = Expression.Parameter(typeof(T), "e");
            Expression? predicate = null;

            foreach (var field in fields)
            {
                var member = new ParameterRebinder(field.Parameters[0], parameter).Visit(field.Body);

                // EF.Functions.ILike(DbSearchFunctions.Normalize(campo), pattern, "\")
                var normalized = Expression.Call(NormalizeMethod, member);
                var like = Expression.Call(
                    ILikeMethod,
                    Expression.Constant(EF.Functions),
                    normalized,
                    Expression.Constant(pattern),
                    Expression.Constant(EscapeChar));

                predicate = predicate is null ? like : Expression.OrElse(predicate, like);
            }

            source = source.Where(Expression.Lambda<Func<T, bool>>(predicate!, parameter));
        }

        return source;
    }

    /// <summary>
    /// A coluna é normalizada no banco por f_search_norm; aqui normalizamos o
    /// lado do parâmetro em C# para que os dois lados se encontrem.
    /// </summary>
    private static string[] Tokenize(string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return [];

        return search
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(Normalize)
            .Select(EscapeLike)
            .Where(t => t.Length > 0)
            .Take(MaxTokens)
            .ToArray();
    }

    private static string Normalize(string value)
    {
        var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                builder.Append(ch);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    // Sem escape, um '%' digitado vira curinga e um '_' casa qualquer
    // caractere — além de distorcer o resultado, inutiliza o índice.
    private static string EscapeLike(string token)
        => token.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private sealed class ParameterRebinder(ParameterExpression from, ParameterExpression to)
        : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
            => node == from ? to : base.VisitParameter(node);
    }
}
