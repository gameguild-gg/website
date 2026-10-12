using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Links a GameGuild user to an external identity provider (e.g. Google, GitHub).
///     One row per (Provider, ProviderKey) pair — the unique index enforces that a single
///     external identity can never map to more than one GameGuild user.
/// </summary>
public class ExternalLogin
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    [Required]
    [MaxLength(64)]
    public string Provider { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string ProviderKey { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>
    ///     JSON array of the OAuth scope tokens the user granted at the most recent
    ///     authorization (see <see cref="ExternalLoginGrants" />). Null on rows linked
    ///     before per-scope consent tracking existed (issue #250).
    /// </summary>
    [MaxLength(1024)]
    public string? GrantedScopes { get; set; }

    /// <summary>
    ///     UTC moment the recorded scope consent was given. Null while no consent is
    ///     recorded for the link.
    /// </summary>
    public DateTime? ConsentedAt { get; set; }

    /// <summary>
    ///     Version of the consent terms the user agreed to. 0 = legacy row predating
    ///     consent tracking; otherwise a value from <see cref="OAuthConsentVersions" />.
    /// </summary>
    public int ConsentVersion { get; set; }
}

/// <summary>
///     Version constants for OAuth scope-consent records. Bump <see cref="Current" />
///     whenever the consent wording or the default scope set changes materially, so
///     re-authorization can be required from users who consented under older terms.
/// </summary>
public static class OAuthConsentVersions
{
    /// <summary>Consent terms in force since per-scope grant tracking was introduced (issue #250).</summary>
    public const int Current = 1;
}

/// <summary>
///     (De)serialization of <see cref="ExternalLogin.GrantedScopes" />. Single-sourced so
///     every writer (link, sign-in auto-link, revocation) and reader (list, revoke) agrees
///     on the JSON shape: a flat array of scope tokens, order preserved.
/// </summary>
public static class ExternalLoginGrants
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>Serializes scope tokens; empty list serializes as an empty JSON array.</summary>
    public static string Serialize(IEnumerable<string> scopes) => JsonSerializer.Serialize(scopes.ToList(), Options);

    /// <summary> Parses the stored JSON array; null, empty, or malformed values read as no granted scopes.</summary>
    public static IReadOnlyList<string> Deserialize(string? grantedScopes)
    {
        if (string.IsNullOrWhiteSpace(grantedScopes))
        {
            return [];
        }

        try
        {
            var scopes = JsonSerializer.Deserialize<List<string>>(grantedScopes, Options);
            return scopes ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    ///     True when both lists contain the same scope tokens (order-insensitive,
    ///     case-insensitive — provider scope tokens are conventionally lowercase but
    ///     Microsoft uses PascalCase).
    /// </summary>
    public static bool SameScopeSet(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        if (left.Count == 0 && right.Count == 0)
        {
            return true;
        }

        if (left.Count != right.Count)
        {
            return false;
        }

        var remaining = right.Select(scope => scope.ToLowerInvariant()).ToList();
        foreach (var scope in left)
        {
            if (!remaining.Remove(scope.ToLowerInvariant()))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Validates a caller-supplied scope token the way provider options are validated:
    ///     non-empty, no whitespace, no control characters.
    /// </summary>
    public static bool IsValidScopeToken(string? scope) =>
        !string.IsNullOrWhiteSpace(scope) && !scope.Any(static c => char.IsControl(c) || char.IsWhiteSpace(c));
}
