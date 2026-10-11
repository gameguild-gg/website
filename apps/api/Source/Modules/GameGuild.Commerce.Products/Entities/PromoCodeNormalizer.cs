using System.Text.RegularExpressions;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Canonical normalization for promo code strings. Codes are stored uppercase and
/// trimmed; every create, lookup, and apply path must normalize input through this
/// helper so lookups behave case-insensitively end to end.
/// </summary>
public static partial class PromoCodeNormalizer
{
    /// <summary>Allowed promo code format after normalization (uppercase letters, digits, hyphens; 3–50 chars).</summary>
    public const string Pattern = "^[A-Z0-9-]{3,50}$";

    private static readonly Regex PatternRegex = new(Pattern, RegexOptions.Compiled);

    /// <summary>Trim and uppercase a promo code so it matches the stored form. Null/whitespace stays null.</summary>
    public static string? Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return code.Trim().ToUpperInvariant();
    }

    /// <summary>Check whether a (already normalized) promo code matches the required format.</summary>
    public static bool IsValidFormat(string? normalizedCode)
    {
        return normalizedCode is not null && PatternRegex.IsMatch(normalizedCode);
    }
}
