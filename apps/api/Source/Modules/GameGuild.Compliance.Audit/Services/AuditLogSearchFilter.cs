using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Compliance.Audit;

/// <summary>
/// Builds the full-text search predicate over audit log descriptions and serialized
/// metadata/context data (issue #164). The predicate is translated to SQL
/// (PostgreSQL ILIKE) so the search runs in the database, never in memory.
/// </summary>
public static class AuditLogSearchFilter
{
    /// <summary>Maximum length accepted for user-supplied search text.</summary>
    public const int MaxSearchTextLength = 200;

    /// <summary>
    /// Normalizes user-supplied search text: trims surrounding whitespace, treats an
    /// empty result as "no filter" (null) and caps the length at
    /// <see cref="MaxSearchTextLength"/> characters.
    /// </summary>
    public static string? Normalize(string? searchText)
    {
        var trimmed = searchText?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length <= MaxSearchTextLength ? trimmed : trimmed[..MaxSearchTextLength];
    }

    /// <summary>
    /// Builds a case-insensitive match predicate covering both the audit log
    /// description and the serialized metadata/context JSON. Returns <c>null</c> when
    /// the normalized search text is empty, meaning "no filter".
    /// </summary>
    public static Expression<Func<AuditLog, bool>>? BuildPredicate(string? searchText)
    {
        var normalized = Normalize(searchText);
        if (normalized is null)
        {
            return null;
        }

        var pattern = $"%{EscapeLikeWildcards(normalized)}%";
        return log =>
            (log.Description != null && EF.Functions.ILike(log.Description, pattern)) ||
            (log.Metadata != null && EF.Functions.ILike(log.Metadata, pattern));
    }

    /// <summary>
    /// Escapes the SQL LIKE wildcards (<c>%</c>, <c>_</c>) and the escape character
    /// (<c>\</c>) so user input is matched literally inside the generated pattern.
    /// </summary>
    public static string EscapeLikeWildcards(string value) =>
        value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
