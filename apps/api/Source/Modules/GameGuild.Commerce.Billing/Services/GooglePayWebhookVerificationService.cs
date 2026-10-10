using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Billing;

/// <summary>Result of verifying a Google Pay webhook callback.</summary>
public sealed record GooglePayWebhookVerificationResult
{
    public bool IsValid { get; init; }

    public string? ErrorMessage { get; init; }

    public string? EventId { get; init; }

    public string? EventType { get; init; }

    public DateTime? OccurredAtUtc { get; init; }

    public string? Audience { get; init; }

    public static GooglePayWebhookVerificationResult Failure(string errorMessage) => new()
    {
        IsValid = false,
        ErrorMessage = errorMessage
    };
}

/// <summary>
///     Verifies Google Pay webhook callbacks. Google Pay server notifications authenticate with a
///     bearer JWT whose audience is the receiving Google Cloud project. Verification is fail
///     closed: the JWT must carry a valid RS256 signature from a configured provider key, the
///     expected audience, and fresh timestamps (replay protection).
/// </summary>
public interface IGooglePayWebhookVerificationService
{
    GooglePayWebhookVerificationResult Verify(string payload, string authHeader, string projectId);
}

public sealed class GooglePayWebhookVerificationService(IOptions<BillingConfiguration> options) : IGooglePayWebhookVerificationService
{
    private const string BearerPrefix = "Bearer ";

    public GooglePayWebhookVerificationResult Verify(string payload, string authHeader, string projectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(authHeader);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);

        var settings = options.Value.GooglePay;

        if (string.IsNullOrWhiteSpace(settings.ProjectId))
        {
            return GooglePayWebhookVerificationResult.Failure(
                "Google Pay webhook verification is not configured for this endpoint.");
        }

        if (!string.Equals(settings.ProjectId, projectId, StringComparison.Ordinal))
        {
            return GooglePayWebhookVerificationResult.Failure(
                "Google Pay project identifier does not match this endpoint.");
        }

        var token = authHeader.Trim();
        if (!token.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return GooglePayWebhookVerificationResult.Failure("Authorization header must carry a bearer JWT.");
        }

        var jwt = token[BearerPrefix.Length..].Trim();
        var parts = jwt.Split('.');
        if (parts.Length != 3 || parts.Any(string.IsNullOrEmpty))
        {
            return GooglePayWebhookVerificationResult.Failure("Malformed bearer JWT.");
        }

        var headerJson = TryDecodeSegment(parts[0]);
        var payloadJson = TryDecodeSegment(parts[1]);
        if (headerJson is null || payloadJson is null)
        {
            return GooglePayWebhookVerificationResult.Failure("Bearer JWT segments are not valid base64url.");
        }

        if (!TryReadStringProperty(headerJson, "alg", out var algorithm) ||
            !string.Equals(algorithm, "RS256", StringComparison.Ordinal))
        {
            return GooglePayWebhookVerificationResult.Failure("Bearer JWT must use the RS256 algorithm.");
        }

        var signature = TryDecodeBytes(parts[2]);
        if (signature is null)
        {
            return GooglePayWebhookVerificationResult.Failure("Bearer JWT signature is not valid base64url.");
        }

        var signedContent = $"{parts[0]}.{parts[1]}";
        if (!VerifyRs256Signature(signedContent, signature, settings))
        {
            return GooglePayWebhookVerificationResult.Failure("Bearer JWT signature verification failed.");
        }

        // Audience binding: the JWT must be issued to this endpoint's project.
        if (!TryReadStringProperty(payloadJson, "aud", out var audience) ||
            !string.Equals(audience, settings.ProjectId, StringComparison.Ordinal))
        {
            return GooglePayWebhookVerificationResult.Failure("Bearer JWT audience does not match this endpoint.");
        }

        // Timestamp validation (replay protection).
        var now = SystemClock.UtcNow;
        var tolerance = TimeSpan.FromSeconds(Math.Max(1, settings.TimestampToleranceSeconds));
        if (TryReadEpochProperty(payloadJson, "exp", out var expiresAt) &&
            DateTime.UnixEpoch.AddSeconds(expiresAt) < now - tolerance)
        {
            return GooglePayWebhookVerificationResult.Failure("Bearer JWT has expired.");
        }

        if (TryReadEpochProperty(payloadJson, "nbf", out var notBefore) &&
            DateTime.UnixEpoch.AddSeconds(notBefore) > now + tolerance)
        {
            return GooglePayWebhookVerificationResult.Failure("Bearer JWT is not yet valid.");
        }

        // Payload integrity: the notification body must be well-formed JSON and carry an
        // identifier usable for idempotency.
        if (!TryParseJson(payload, out var body))
        {
            return GooglePayWebhookVerificationResult.Failure("Webhook payload is not valid JSON.");
        }

        var eventId = ReadString(body, "event_id") ?? ReadString(body, "messageId") ?? ReadString(body, "eventId");
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return GooglePayWebhookVerificationResult.Failure("Webhook payload is missing its event identifier.");
        }

        return new GooglePayWebhookVerificationResult
        {
            IsValid = true,
            EventId = eventId,
            EventType = ReadString(body, "event_type") ?? ReadString(body, "eventType") ?? "unknown",
            OccurredAtUtc = TryReadEpochProperty(payloadJson, "iat", out var issuedAt)
                ? DateTime.UnixEpoch.AddSeconds(issuedAt)
                : now,
            Audience = audience
        };
    }

    /// <summary>
    ///     Whether a configured verification key can be imported as an RSA public key.
    ///     Accepts RFC 7468 PEM ("-----BEGIN PUBLIC KEY-----") or base64-encoded SPKI DER.
    ///     Public so configuration validation tests can exercise the key checks directly.
    /// </summary>
    public static bool IsValidVerificationKey(string candidate)
    {
        return TryImportKey(candidate, out _);
    }

    private static bool VerifyRs256Signature(string signedContent, byte[] signature, GooglePaySettings settings)
    {
        if (settings.VerificationKeys.Count == 0)
        {
            // Fail closed: without provider keys, nothing can be authenticated.
            return false;
        }

        var data = System.Text.Encoding.ASCII.GetBytes(signedContent);
        foreach (var candidate in settings.VerificationKeys)
        {
            if (!TryImportKey(candidate, out var rsa))
            {
                continue;
            }

            using (rsa)
            {
                if (rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryImportKey(string candidate, out RSA rsa)
    {
        rsa = RSA.Create();
        try
        {
            var trimmed = candidate.Trim();
            if (trimmed.Contains("BEGIN", StringComparison.Ordinal))
            {
                rsa.ImportFromPem(trimmed);
            }
            else
            {
                rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(trimmed), out _);
            }

            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or CryptographicException)
        {
            rsa.Dispose();
            rsa = null!;
            return false;
        }
    }

    private static string? TryDecodeSegment(string segment)
    {
        try
        {
            var bytes = Base64UrlDecode(segment);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static byte[]? TryDecodeBytes(string segment)
    {
        try
        {
            return Base64UrlDecode(segment);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static byte[] Base64UrlDecode(string segment)
    {
        var padded = segment.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '='));
    }

    private static bool TryParseJson(string json, out System.Text.Json.JsonElement element)
    {
        try
        {
            var document = System.Text.Json.JsonDocument.Parse(json);
            element = document.RootElement.Clone();
            return element.ValueKind == System.Text.Json.JsonValueKind.Object;
        }
        catch (System.Text.Json.JsonException)
        {
            element = default;
            return false;
        }
    }

    private static bool TryReadStringProperty(string json, string propertyName, out string? value)
    {
        value = null;
        if (!TryParseJson(json, out var element))
        {
            return false;
        }

        value = ReadString(element, propertyName);
        return value is not null;
    }

    private static bool TryReadEpochProperty(string json, string propertyName, out long epochSeconds)
    {
        epochSeconds = 0;
        if (!TryParseJson(json, out var element) ||
            !element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        return property.ValueKind switch
        {
            System.Text.Json.JsonValueKind.Number => property.TryGetInt64(out epochSeconds),
            System.Text.Json.JsonValueKind.String => long.TryParse(property.GetString(), out epochSeconds),
            _ => false
        };
    }

    private static string? ReadString(System.Text.Json.JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == System.Text.Json.JsonValueKind.String
            ? property.GetString()
            : null;
}
