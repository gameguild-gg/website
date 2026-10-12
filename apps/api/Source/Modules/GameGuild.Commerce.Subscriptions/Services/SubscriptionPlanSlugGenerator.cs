using System.Globalization;
using System.Text;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Generates URL-safe, unique slugs for subscription plans (issue #416).
/// </summary>
/// <remarks>
///     Slugs are lowercase ASCII <c>[a-z0-9-]</c>, produced from the plan name by
///     lowercasing, stripping diacritics, replacing every non-alphanumeric character with a
///     hyphen, collapsing consecutive hyphens, trimming leading/trailing hyphens and
///     truncating to 50 characters (the column width). Collisions against the unique slug
///     index are resolved with a numeric suffix (<c>-2</c>, <c>-3</c>, ...).
/// </remarks>
public sealed class SubscriptionPlanSlugGenerator : ISubscriptionPlanSlugGenerator
{
    private const int MaxSlugLength = 50;
    private const int MaxUniquenessAttempts = 1000;

    /// <inheritdoc />
    public string? Slugify(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var normalized = name.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var lastWasHyphen = true; // trims leading hyphens

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
                continue; // strip diacritics

            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                lastWasHyphen = false;
            }
            else if (!lastWasHyphen)
            {
                builder.Append('-');
                lastWasHyphen = true;
            }
        }

        var length = builder.Length;
        while (length > 0 && builder[length - 1] == '-')
            length--; // trim trailing hyphens

        if (length > MaxSlugLength)
        {
            length = MaxSlugLength;
            while (length > 0 && builder[length - 1] == '-')
                length--; // keep the slug hyphen-free at the truncation boundary
        }

        return length == 0 ? null : builder.ToString(0, length);
    }

    /// <inheritdoc />
    public async Task<string?> GenerateUniqueSlugAsync(
        string? name,
        Func<string, CancellationToken, Task<bool>> isSlugTaken,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(isSlugTaken);

        var baseSlug = Slugify(name);
        if (baseSlug is null)
            return null;

        if (!await isSlugTaken(baseSlug, cancellationToken).ConfigureAwait(false))
            return baseSlug;

        for (var suffix = 2; suffix < MaxUniquenessAttempts; suffix++)
        {
            var suffixText = suffix.ToString(CultureInfo.InvariantCulture);
            var trimmedLength = Math.Min(baseSlug.Length, MaxSlugLength - suffixText.Length - 1);
            var candidate = $"{baseSlug[..trimmedLength]}-{suffixText}";

            if (!await isSlugTaken(candidate, cancellationToken).ConfigureAwait(false))
                return candidate;
        }

        return null;
    }
}

/// <summary>
///     Contract for subscription plan slug generation (issue #416).
/// </summary>
public interface ISubscriptionPlanSlugGenerator
{
    /// <summary>
    ///     Produces a URL-safe slug from a plan name, or <c>null</c> when the name contains
    ///     no usable characters.
    /// </summary>
    string? Slugify(string? name);

    /// <summary>
    ///     Produces a unique slug from a plan name, appending <c>-2</c>, <c>-3</c>, ...
    ///     suffixes while the candidate is already taken. Returns <c>null</c> when the name
    ///     has no usable characters or no unique candidate could be found.
    /// </summary>
    Task<string?> GenerateUniqueSlugAsync(
        string? name,
        Func<string, CancellationToken, Task<bool>> isSlugTaken,
        CancellationToken cancellationToken = default);
}
