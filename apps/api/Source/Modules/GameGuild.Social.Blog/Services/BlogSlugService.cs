using System.Globalization;
using System.Text;

namespace GameGuild.Social.Blog.Services;

/// <summary>
/// Generates and normalizes blog post slugs: lowercase, hyphenated, diacritics stripped,
/// charset [a-z0-9-], max 220 chars, with per-author uniqueness suffixing (-2, -3, ...).
/// </summary>
public interface IBlogSlugService
{
    /// <summary>Normalizes a candidate slug (no uniqueness check).</summary>
    string Normalize(string candidate);

    /// <summary>Generates a unique slug for the author, suffixing -2, -3… on collisions.</summary>
    Task<string> GenerateUniqueSlugAsync(Guid primaryAuthorId, string title, CancellationToken ct = default);

    /// <summary>Returns true when no live post of the author uses the slug (used by slug change).</summary>
    Task<bool> IsSlugAvailableAsync(Guid primaryAuthorId, string slug, CancellationToken ct = default);
}

/// <summary>Read-time estimation for blog bodies (200 words/minute, minimum 1).</summary>
public static class BlogReadTimeEstimator
{
    private const int WordsPerMinute = 200;

    /// <summary>Estimates read time for a markdown body.</summary>
    public static int EstimateFromMarkdown(string? markdown)
        => EstimateFromPlainText(markdown);

    /// <summary>
    /// Estimates read time for a serialized Lexical editor state by walking the JSON and
    /// concatenating every "text" property value, then word-splitting the result.
    /// </summary>
    public static int EstimateFromLexicalJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return 1;
        }

        var texts = new List<string>();
        using (var document = System.Text.Json.JsonDocument.Parse(json))
        {
            CollectTextValues(document.RootElement, texts);
        }

        return EstimateFromPlainText(string.Join(' ', texts));
    }

    /// <summary>Estimates read time from a format + bodies; picks the right extractor.</summary>
    public static int Estimate(BlogContentFormat format, string? content, string? jsonBody)
        => format == BlogContentFormat.Lexical
            ? EstimateFromLexicalJson(jsonBody ?? content)
            : EstimateFromMarkdown(content);

    private static void CollectTextValues(System.Text.Json.JsonElement element, List<string> texts)
    {
        switch (element.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Name == "text" && property.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        texts.Add(property.Value.GetString() ?? string.Empty);
                    }
                    else
                    {
                        CollectTextValues(property.Value, texts);
                    }
                }
                break;
            case System.Text.Json.JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectTextValues(item, texts);
                }
                break;
        }
    }

    private static int EstimateFromPlainText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 1;
        }

        var wordCount = 0;
        var inWord = false;
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                inWord = false;
            }
            else if (!inWord)
            {
                wordCount++;
                inWord = true;
            }
        }

        return Math.Max(1, (int)Math.Ceiling(wordCount / (double)WordsPerMinute));
    }
}

/// <summary>Default <see cref="IBlogSlugService"/> backed by the shared application DbContext.</summary>
public sealed class BlogSlugService(IApplicationDbContext context) : IBlogSlugService
{
    private const int MaxSlugLength = 220;

    public string Normalize(string candidate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidate);

        var normalized = candidate.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue; // diacritic stripper
            }

            var lower = char.ToLowerInvariant(ch);
            if (lower is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                builder.Append(lower);
            }
            else if (ch is ' ' or '_' or '/' or '.' or '-' or '+' or '&' || char.IsWhiteSpace(ch))
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString().Trim('-').Replace("--", "-", StringComparison.Ordinal);
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        if (slug.Length == 0)
        {
            slug = "post";
        }

        return slug.Length > MaxSlugLength ? slug[..MaxSlugLength].Trim('-') : slug;
    }

    public async Task<string> GenerateUniqueSlugAsync(Guid primaryAuthorId, string title, CancellationToken ct = default)
    {
        var baseSlug = Normalize(title);
        if (await IsSlugAvailableAsync(primaryAuthorId, baseSlug, ct).ConfigureAwait(false))
        {
            return baseSlug;
        }

        // Suffix budget: keep "base-suffix" within max length.
        var suffixRoom = Math.Max(1, MaxSlugLength - baseSlug.Length - 1);
        if (suffixRoom < 2)
        {
            baseSlug = baseSlug[..(MaxSlugLength - 2)].Trim('-');
        }

        var attempt = 2;
        while (true)
        {
            var candidate = $"{baseSlug}-{attempt}";
            if (candidate.Length > MaxSlugLength)
            {
                candidate = $"{baseSlug[..(MaxSlugLength - attempt.ToString().Length - 1)].Trim('-')}-{attempt}";
            }

            if (await IsSlugAvailableAsync(primaryAuthorId, candidate, ct).ConfigureAwait(false))
            {
                return candidate;
            }

            attempt++;
        }
    }

    public async Task<bool> IsSlugAvailableAsync(Guid primaryAuthorId, string slug, CancellationToken ct = default)
        => !await context.Set<BlogPost>()
            .AsNoTracking()
            .AnyAsync(post => post.PrimaryAuthorId == primaryAuthorId && post.Slug == slug, ct)
            .ConfigureAwait(false);
}
