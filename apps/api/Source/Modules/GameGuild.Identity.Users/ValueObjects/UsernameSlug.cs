using System.Globalization;
using System.Text;

namespace GameGuild.Identity.Users;

/// <summary>Canonical handles for newly created users. Existing stored handles are not rewritten.</summary>
public static class UsernameSlug
{
    public static int MaximumLength => 256;

    /// <summary>Removes accents, lowercases ASCII, and replaces separators with a single hyphen.</summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl))
        {
            return null;
        }

        string decomposed;
        try
        {
            decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        }
        catch (ArgumentException)
        {
            return null;
        }

        var result = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) is UnicodeCategory.NonSpacingMark
                or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark)
            {
                continue;
            }

            var lower = char.ToLowerInvariant(character);
            if (lower is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_')
            {
                result.Append(lower);
            }
            else if (result.Length > 0 && result[^1] != '-')
            {
                result.Append('-');
            }
        }

        var slug = result.ToString().Trim('.', '_', '-');
        return slug.Length == 0 ? null : slug;
    }

    public static string Generate(string name)
    {
        var slug = Normalize(name) ?? "user";
        return slug[..Math.Min(slug.Length, MaximumLength)].TrimEnd('.', '_', '-');
    }

    public static string FromExplicit(string username)
    {
        var slug = Normalize(username);
        if (slug is null || username.Length > MaximumLength || slug.Length > MaximumLength)
        {
            throw new ArgumentException("Username must produce a nonempty handle of at most 256 characters.", nameof(username));
        }

        return slug;
    }

    internal static string WithDisambiguator(string slug, Guid userId, int attempt = 1)
    {
        var suffix = $"-{userId:N}" + (attempt == 1 ? string.Empty : $"-{attempt}");
        return slug[..Math.Min(slug.Length, MaximumLength - suffix.Length)].TrimEnd('.', '_', '-') + suffix;
    }
}
