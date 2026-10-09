using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace GameGuild;

/// <summary>
///     Utility for redacting sensitive identifiers in log output.
///     Produces a consistent, irreversible short hash so logs can still
///     correlate requests to the same tenant without leaking the raw GUID.
/// </summary>
public static partial class LogRedaction
{
    private static readonly byte[] IdentifierKey = RandomNumberGenerator.GetBytes(32);

    /// <summary>
    ///     Redacts a <see cref="Guid"/> to a short hash prefix (first 8 hex chars of SHA-256).
    ///     Returns <c>"none"</c> for <see cref="Guid.Empty"/> or null.
    ///     Deterministic — same input always produces the same output.
    /// </summary>
    /// <example>
    ///     <code>
    ///     var redacted = LogRedaction.RedactId(tenantId);
    ///     // e.g. "tid:a1b2c3d4"
    ///     </code>
    /// </example>
    public static string RedactId(Guid? id, string prefix = "tid")
    {
        if (!id.HasValue || id.Value == Guid.Empty)
        {
            return "none";
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(id.Value.ToString("N")));
        return $"{prefix}:{Convert.ToHexString(hash, 0, 4).ToLowerInvariant()}";
    }

    /// <summary>
    ///     Redacts a string identifier (e.g., user ID, subject ID) to a short hash.
    /// </summary>
    public static string RedactId(string? id, string prefix = "uid")
    {
        if (string.IsNullOrEmpty(id))
        {
            return "none";
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(id));
        return $"{prefix}:{Convert.ToHexString(hash, 0, 4).ToLowerInvariant()}";
    }

    /// <summary>
    ///     Produces an opaque keyed fingerprint for email log correlation.
    ///     No address characters, domain suffixes or lengths reach the output.
    ///     The private random key lasts for this process only; fingerprints
    ///     correlate events within a process, not across hosts or restarts.
    ///     Returns <c>"none"</c> for null/empty input and <c>"invalid"</c>
    ///     for values without a usable separator or leading domain character.
    /// </summary>
    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrEmpty(email))
        {
            return "none";
        }

        var atIndex = email.IndexOf('@');
        if (atIndex <= 0 || atIndex == email.Length - 1 || !char.IsLetterOrDigit(email[atIndex + 1]))
        {
            return "invalid";
        }

        return Fingerprint(email.ToLowerInvariant(), "email");
    }

    /// <summary>
    ///     Produces an opaque keyed fingerprint for username log correlation
    ///     within this process. No name characters or lengths are retained.
    ///     Returns <c>"none"</c> for null/empty input and <c>"invalid"</c>
    ///     when the value has no leading letter or digit.
    /// </summary>
    public static string MaskUsername(string? username)
    {
        if (string.IsNullOrEmpty(username))
        {
            return "none";
        }

        return char.IsLetterOrDigit(username[0])
            ? Fingerprint(username.ToLowerInvariant(), "username")
            : "invalid";
    }

    /// <summary>
    ///     Produces an opaque keyed fingerprint for address log correlation
    ///     within this process. Valid IPv4 values keep the existing grouping
    ///     by octets 1 and 3, without copying either octet into log output.
    ///     Other values are fingerprinted in full. Returns <c>"none"</c>
    ///     for null/empty input.
    /// </summary>
    public static string MaskIpAddress(string? ipAddress)
    {
        if (string.IsNullOrEmpty(ipAddress))
        {
            return "none";
        }

        if (TrySplitIpv4(ipAddress, out var octet1, out _, out var octet3))
        {
            return Fingerprint($"{octet1}.{octet3}", "ip-network");
        }

        return Fingerprint(ipAddress, "ip");
    }

    /// <summary>
    ///     Masks an identifier that may be an email address, an IPv4 address,
    ///     or a username, dispatching to the most specific fingerprint. Use for
    ///     security logs (sign-in identifiers, threat-detection alerts) where
    ///     the shape of the identifier is not known up front. Returns
    ///     <c>"none"</c> for null/empty input.
    /// </summary>
    public static string MaskIdentifier(string? identifier)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            return "none";
        }

        if (identifier.Contains('@'))
        {
            return MaskEmail(identifier);
        }

        if (TrySplitIpv4(identifier, out _, out _, out _))
        {
            return MaskIpAddress(identifier);
        }

        return MaskUsername(identifier);
    }

    private static bool TrySplitIpv4(string value, out string octet1, out string octet2, out string octet3)
    {
        octet1 = octet2 = octet3 = string.Empty;
        var parts = value.Split('.');
        if (parts.Length != 4)
        {
            return false;
        }

        foreach (var part in parts)
        {
            if (!byte.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            {
                return false;
            }
        }

        octet1 = parts[0];
        octet2 = parts[1];
        octet3 = parts[2];
        return true;
    }

    private static string Fingerprint(string value, string kind)
    {
        var digest = HMACSHA256.HashData(IdentifierKey, Encoding.UTF8.GetBytes($"{kind}\0{value}"));
        return $"{kind}:{Convert.ToHexString(digest).ToLowerInvariant()}";
    }

    /// <summary>
    ///     Redacts a secret (password, token, key) without retaining a fingerprint.
    ///     Hashing low-entropy secrets would let a log reader test password guesses.
    ///     Returns <c>"none"</c> for null/empty input.
    /// </summary>
    public static string RedactSecret(string? secret)
    {
        if (string.IsNullOrEmpty(secret))
        {
            return "none";
        }

        return "secret:redacted";
    }

    /// <summary>
    ///     Sanitizes an untrusted string for structured logging by replacing control
    ///     characters and Unicode line separators with visible escape markers.
    ///     Prevents log forging (CWE-117) without destroying diagnostic value.
    /// </summary>
    public static string Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return ControlCharactersRegex().Replace(value, "␀");
    }

    [GeneratedRegex(@"[\u0000-\u001F\u007F-\u009F\u2028\u2029]")]
    private static partial Regex ControlCharactersRegex();
}
