using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Deterministic canonicalization and hashing for certificate anchoring payloads.
/// </summary>
/// <remarks>
///     <para>
///         CANONICALIZATION CONTRACT (must remain stable so existing anchors stay verifiable):
///         the anchored data is a JSON object whose keys are exactly, in lexicographic (sorted) order:
///         <c>"id"</c>, <c>"issued-at"</c>, <c>"issuer"</c>, <c>"recipient-hash"</c>, <c>"version"</c>.
///     </para>
///     <para>
///         PII EXCLUSION: this whitelist is the enforcement point. Any other property present in the
///         input payload (names, e-mails, free text, ...) is silently dropped before anything is
///         hashed, persisted, or published. The recipient is represented only by
///         <c>"recipient-hash"</c> — a SHA-256 hex digest — never by identifying data. The revocation
///         payload similarly whitelists <c>"original-anchor-id"</c>, <c>"original-certificate-hash"</c>
///         and <c>"revoked-at"</c> only; revocation reasons are stored locally but never anchored.
///     </para>
///     <para>
///         DETERMINISM: values are emitted with fixed formats (GUID "D" lowercase, ISO-8601
///         round-trip timestamps, lowercase hex digests, invariant culture) so the same logical
///         payload always produces the same bytes and therefore the same SHA-256 hash, on any
///         machine and at any time.
///     </para>
/// </remarks>
public static class BlockchainCertificateCanonicalizer
{
    /// <summary>Canonical key: unique certificate identifier (GUID, "D" format, lowercase).</summary>
    public const string IdKey = "id";

    /// <summary>Canonical key: issuance timestamp (ISO-8601 round-trip, UTC).</summary>
    public const string IssuedAtKey = "issued-at";

    /// <summary>Canonical key: issuer identity (organization, not a person).</summary>
    public const string IssuerKey = "issuer";

    /// <summary>Canonical key: SHA-256 hex digest of the recipient identity.</summary>
    public const string RecipientHashKey = "recipient-hash";

    /// <summary>Canonical key: payload schema version (integer).</summary>
    public const string VersionKey = "version";

    /// <summary>Canonical key: revocation payload — identifier of the anchor being revoked.</summary>
    public const string OriginalAnchorIdKey = "original-anchor-id";

    /// <summary>Canonical key: revocation payload — certificate hash being revoked.</summary>
    public const string OriginalCertificateHashKey = "original-certificate-hash";

    /// <summary>Canonical key: revocation payload — revocation timestamp.</summary>
    public const string RevokedAtKey = "revoked-at";

    private static readonly string[] IssuanceRequiredKeys =
    [
        IdKey, IssuedAtKey, IssuerKey, RecipientHashKey, VersionKey
    ];

    /// <summary>
    ///     Builds the canonical issuance payload from typed inputs. Deterministic for equal inputs.
    /// </summary>
    public static string BuildIssuancePayload(
        Guid id,
        string recipientHash,
        DateTime issuedAt,
        string issuer,
        int version)
    {
        if (string.IsNullOrWhiteSpace(recipientHash))
        {
            throw new ArgumentException("Recipient hash is required.", nameof(recipientHash));
        }

        if (string.IsNullOrWhiteSpace(issuer))
        {
            throw new ArgumentException("Issuer is required.", nameof(issuer));
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString(IdKey, id.ToString("D", CultureInfo.InvariantCulture));
            writer.WriteString(IssuedAtKey, ToCanonicalTimestamp(issuedAt));
            writer.WriteString(IssuerKey, issuer);
            writer.WriteString(RecipientHashKey, recipientHash.ToLowerInvariant());
            writer.WriteNumber(VersionKey, version);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    ///     Builds the canonical revocation payload linking a revocation to its original anchor.
    ///     Deterministic for equal inputs. Never includes the revocation reason.
    /// </summary>
    public static string BuildRevocationPayload(Guid originalAnchorId, string originalCertificateHash, DateTime revokedAt)
    {
        if (string.IsNullOrWhiteSpace(originalCertificateHash))
        {
            throw new ArgumentException("Original certificate hash is required.", nameof(originalCertificateHash));
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString(OriginalAnchorIdKey, originalAnchorId.ToString("D", CultureInfo.InvariantCulture));
            writer.WriteString(OriginalCertificateHashKey, originalCertificateHash.ToLowerInvariant());
            writer.WriteString(RevokedAtKey, ToCanonicalTimestamp(revokedAt));
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    ///     Canonicalizes an arbitrary JSON issuance payload: keeps only the whitelisted keys
    ///     (dropping everything else, including any personal data), requires all five keys with
    ///     the expected value kinds, and re-emits them in sorted order. Returns <c>null</c> when
    ///     the input is not a JSON object that satisfies the whitelist contract.
    /// </summary>
    public static string? CanonicalizeIssuancePayload(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(payload);
        }
        catch (JsonException)
        {
            return null;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                values[property.Name] = property.Value.Clone();
            }

            foreach (var key in IssuanceRequiredKeys)
            {
                if (!values.TryGetValue(key, out var value))
                {
                    return null;
                }

                var expectedKind = key == VersionKey ? JsonValueKind.Number : JsonValueKind.String;
                if (value.ValueKind != expectedKind)
                {
                    return null;
                }
            }

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString(IdKey, values[IdKey].GetString()!);
                writer.WriteString(IssuedAtKey, values[IssuedAtKey].GetString()!);
                writer.WriteString(IssuerKey, values[IssuerKey].GetString()!);
                writer.WriteString(RecipientHashKey, values[RecipientHashKey].GetString()!);
                writer.WriteNumber(VersionKey, values[VersionKey].GetInt32());
                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }

    /// <summary>
    ///     Computes the lowercase hex SHA-256 digest of a UTF-8 string. Deterministic.
    ///     Used both for the anchored certificate hash and for PII-free recipient digests.
    /// </summary>
    public static string ComputeSha256Hex(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(value));

        return Convert.ToHexString(digest).ToLowerInvariant();
    }

    /// <summary>
    ///     Computes a deterministic, provider-scoped pseudo transaction hash for the local
    ///     provider (e.g. <c>0x&lt;sha-256 hex&gt;</c>). The local provider submits no transaction to
    ///     any network; the value is derived solely from the canonical payload so results are
    ///     reproducible in development and tests.
    /// </summary>
    public static string ComputeDeterministicTransactionHash(string scope, string canonicalPayload)
    {
        ArgumentException.ThrowIfNullOrEmpty(scope);
        ArgumentException.ThrowIfNullOrEmpty(canonicalPayload);

        return $"0x{ComputeSha256Hex($"{scope}|{canonicalPayload}")}";
    }

    private static string ToCanonicalTimestamp(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        return utc.ToString("O", CultureInfo.InvariantCulture);
    }
}
