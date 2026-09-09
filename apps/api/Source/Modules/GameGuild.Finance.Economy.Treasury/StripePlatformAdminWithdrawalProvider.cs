using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace GameGuild.Finance.Economy.Treasury;

public sealed class StripePlatformWithdrawalOptions
{
    public const string SectionName = "Modules:Economy:Treasury:StripePlatform";

    public bool Enabled { get; set; }
    public string ApiBaseUrl { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string EvidenceSigningSecret { get; set; } = string.Empty;
    public TimeSpan WebhookTolerance { get; set; }
}

public interface IStripeTreasuryWebhookNormalizer
{
    AdminWithdrawalProviderEvent Normalize(
        ReadOnlySpan<byte> rawPayload,
        string signatureHeader,
        DateTimeOffset receivedAt);
}

public sealed class StripePlatformAdminWithdrawalProvider :
    IAdminWithdrawalProvider,
    IStripeTreasuryWebhookNormalizer
{
    internal const long HardUnitsPerUsdMinor = 1_000;
    private readonly HttpClient _http;
    private readonly StripePlatformWithdrawalOptions _options;
    private readonly TimeProvider _time;

    public StripePlatformAdminWithdrawalProvider(
        HttpClient httpClient,
        IOptions<StripePlatformWithdrawalOptions> options,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _http = httpClient;
        _options = options.Value;
        _time = timeProvider;
    }

    public async ValueTask<AdminWithdrawalProviderReceipt> DispatchAsync(
        AdminWithdrawalDispatchCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureConfigured();
        if (command.TenantId == Guid.Empty || command.RunId == Guid.Empty || command.Amount.Currency != GameGuild.Finance.Economy.Contracts.CurrencyCode.HardCoin ||
            command.Amount.Units <= 0 || command.Amount.Units % HardUnitsPerUsdMinor != 0)
            throw new AdminWithdrawalEligibilityException(
                "Stripe platform payout requires a positive amount aligned to the fixed 1:1000 hard-unit scale.");
        try
        {
            using var request = CreateRequest(
                HttpMethod.Post,
                "/v1/payouts",
                [
                    new("amount", (command.Amount.Units / HardUnitsPerUsdMinor).ToString(CultureInfo.InvariantCulture)),
                    new("currency", "usd"),
                    new("metadata[tenant_id]", command.TenantId.ToString("N")),
                    new("metadata[run_id]", command.RunId.ToString("N")),
                    new("metadata[fencing_token]", command.FencingToken.ToString(CultureInfo.InvariantCulture)),
                    new("metadata[execution_epoch]", command.ExecutionEpoch.ToString(CultureInfo.InvariantCulture)),
                    new("metadata[source_asset_key]", command.SourceAssetKey),
                    new("metadata[destination_hash]", command.DestinationHash),
                    new("metadata[dispatch_snapshot_hash]", command.DispatchSnapshotHash)
                ],
                command.IdempotencyKey);
            using var response = await _http.SendAsync(request, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            if ((int)response.StatusCode >= 500)
                return SignReceipt(Ambiguous(command, Hash(raw), _time.GetUtcNow()));
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(raw);
            return SignReceipt(new AdminWithdrawalProviderReceipt(
                command.RunId,
                command.TenantId,
                MapOutcome(OptionalString(document.RootElement, "status")),
                RequireString(document.RootElement, "id"),
                command.FencingToken,
                command.ExecutionEpoch,
                command.Amount,
                command.SourceAssetKey,
                command.DestinationHash,
                Hash(raw),
                string.Empty,
                _time.GetUtcNow()));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return SignReceipt(Ambiguous(command, Hash(exception.GetType().Name), _time.GetUtcNow()));
        }
    }

    public async ValueTask<AdminWithdrawalProviderEvent> ReconcileAsync(
        Guid tenantId,
        Guid runId,
        string idempotencyKey,
        string? providerTransferId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        if (runId == Guid.Empty) throw new ArgumentException("Run ID is required.", nameof(runId));
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        EnsureConfigured();
        var payoutId = string.IsNullOrWhiteSpace(providerTransferId) ||
                       providerTransferId.StartsWith("unknown:", StringComparison.Ordinal)
            ? await FindPayoutByRunAsync(tenantId, runId, cancellationToken)
            : providerTransferId.Trim();
        using var request = CreateRequest(
            HttpMethod.Get, "/v1/payouts/" + Uri.EscapeDataString(payoutId), [], null);
        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(raw);
        return SignEvent(ToEvent(
            "reconcile:" + payoutId + ":" + Hash(raw), document.RootElement, raw, _time.GetUtcNow()));
    }

    public AdminWithdrawalProviderEvent Normalize(
        ReadOnlySpan<byte> rawPayload,
        string signatureHeader,
        DateTimeOffset receivedAt)
    {
        EnsureConfigured();
        if (!VerifyStripeSignature(rawPayload, signatureHeader, receivedAt))
            throw new AdminWithdrawalEvidenceException(
                "Stripe platform webhook signature or timestamp is invalid.");
        using var document = JsonDocument.Parse(rawPayload.ToArray());
        var root = document.RootElement;
        var type = RequireString(root, "type");
        var payout = root.GetProperty("data").GetProperty("object");
        var mapped = ToEvent(RequireString(root, "id"), payout, Hash(rawPayload), receivedAt);
        var outcome = type switch
        {
            "payout.paid" => AdminWithdrawalProviderOutcome.Succeeded,
            "payout.failed" or "payout.canceled" => AdminWithdrawalProviderOutcome.Failed,
            _ => mapped.Outcome
        };
        return SignEvent(mapped with { Outcome = outcome });
    }

    private AdminWithdrawalProviderEvent ToEvent(
        string eventId,
        JsonElement payout,
        string rawEvidence,
        DateTimeOffset observedAt)
    {
        var metadata = payout.GetProperty("metadata");
        if (!Guid.TryParseExact(RequireString(metadata, "tenant_id"), "N", out var tenantId) ||
            !Guid.TryParseExact(RequireString(metadata, "run_id"), "N", out var runId) ||
            !long.TryParse(RequireString(metadata, "fencing_token"), out var fencingToken) ||
            !long.TryParse(RequireString(metadata, "execution_epoch"), out var executionEpoch))
            throw new AdminWithdrawalEvidenceException(
                "Stripe platform payout evidence has invalid fencing metadata.");
        var amountMinor = payout.GetProperty("amount").GetInt64();
        return new AdminWithdrawalProviderEvent(
            eventId,
            runId,
            tenantId,
            MapOutcome(OptionalString(payout, "status")),
            RequireString(payout, "id"),
            fencingToken,
            executionEpoch,
            new GameGuild.Finance.Economy.Contracts.CoinAmount(
                GameGuild.Finance.Economy.Contracts.CurrencyCode.HardCoin,
                checked(amountMinor * HardUnitsPerUsdMinor)),
            RequireString(metadata, "source_asset_key"),
            RequireString(metadata, "destination_hash"),
            rawEvidence.Length == 64 ? rawEvidence : Hash(rawEvidence),
            string.Empty,
            observedAt);
    }

    private async ValueTask<string> FindPayoutByRunAsync(Guid tenantId, Guid runId, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, "/v1/payouts?limit=100", [], null);
        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        foreach (var item in document.RootElement.GetProperty("data").EnumerateArray())
            if (item.TryGetProperty("metadata", out var metadata) &&
                OptionalString(metadata, "tenant_id") == tenantId.ToString("N") &&
                OptionalString(metadata, "run_id") == runId.ToString("N"))
                return RequireString(item, "id");
        throw new AdminWithdrawalEvidenceException(
            "Stripe reconciliation found no platform payout for the withdrawal run.");
    }

    private HttpRequestMessage CreateRequest(
        HttpMethod method,
        string path,
        IReadOnlyList<KeyValuePair<string, string>> fields,
        string? idempotencyKey)
    {
        var request = new HttpRequestMessage(method, new Uri(new Uri(_options.ApiBaseUrl), path));
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.SecretKey);
        if (!string.IsNullOrWhiteSpace(idempotencyKey)) request.Headers.Add("Idempotency-Key", idempotencyKey.Trim());
        if (fields.Count > 0) request.Content = new FormUrlEncodedContent(fields);
        return request;
    }

    private AdminWithdrawalProviderReceipt SignReceipt(AdminWithdrawalProviderReceipt receipt) => receipt with
    {
        Signature = StripeTreasuryEvidenceSigning.SignReceipt(receipt, _options.EvidenceSigningSecret)
    };

    private AdminWithdrawalProviderEvent SignEvent(AdminWithdrawalProviderEvent providerEvent) => providerEvent with
    {
        Signature = StripeTreasuryEvidenceSigning.SignEvent(providerEvent, _options.EvidenceSigningSecret)
    };

    private static AdminWithdrawalProviderReceipt Ambiguous(
        AdminWithdrawalDispatchCommand command,
        string evidenceHash,
        DateTimeOffset observedAt) => new(
        command.RunId,
        command.TenantId,
        AdminWithdrawalProviderOutcome.Ambiguous,
        "unknown:" + command.RunId.ToString("N"),
        command.FencingToken,
        command.ExecutionEpoch,
        command.Amount,
        command.SourceAssetKey,
        command.DestinationHash,
        evidenceHash,
        string.Empty,
        observedAt);

    private bool VerifyStripeSignature(
        ReadOnlySpan<byte> payload,
        string header,
        DateTimeOffset receivedAt)
    {
        if (string.IsNullOrWhiteSpace(header)) return false;
        long timestamp = 0;
        var signatures = new List<string>();
        foreach (var part in header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0) continue;
            if (part[..separator] == "t") long.TryParse(part[(separator + 1)..], out timestamp);
            if (part[..separator] == "v1") signatures.Add(part[(separator + 1)..]);
        }
        if (timestamp <= 0 || signatures.Count == 0) return false;
        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(timestamp);
        if (issuedAt > receivedAt || receivedAt - issuedAt > _options.WebhookTolerance) return false;
        var signed = Encoding.UTF8.GetBytes(timestamp.ToString(CultureInfo.InvariantCulture) + ".")
            .Concat(payload.ToArray()).ToArray();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.WebhookSecret));
        var expected = Convert.ToHexStringLower(hmac.ComputeHash(signed));
        return signatures.Any(signature => signature.Length == expected.Length &&
            CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(signature.ToLowerInvariant()), Encoding.ASCII.GetBytes(expected)));
    }

    private void EnsureConfigured()
    {
        if (!_options.Enabled || !Uri.TryCreate(_options.ApiBaseUrl, UriKind.Absolute, out _) ||
            string.IsNullOrWhiteSpace(_options.SecretKey) || string.IsNullOrWhiteSpace(_options.WebhookSecret) ||
            Encoding.UTF8.GetByteCount(_options.EvidenceSigningSecret) < 32 ||
            _options.WebhookTolerance <= TimeSpan.Zero)
            throw new AdminWithdrawalExecutionDisabledException(
                "Stripe platform payouts remain disabled until endpoint, credentials and evidence policies are configured.");
    }

    private static AdminWithdrawalProviderOutcome MapOutcome(string? status) => status switch
    {
        "paid" => AdminWithdrawalProviderOutcome.Succeeded,
        "failed" or "canceled" => AdminWithdrawalProviderOutcome.Failed,
        "pending" or "in_transit" => AdminWithdrawalProviderOutcome.Submitted,
        _ => AdminWithdrawalProviderOutcome.Ambiguous
    };

    private static string RequireString(JsonElement element, string name) =>
        OptionalString(element, name) is { Length: > 0 } value
            ? value
            : throw new AdminWithdrawalEvidenceException($"Stripe response requires '{name}'.");

    private static string? OptionalString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string Hash(string value) => Hash(Encoding.UTF8.GetBytes(value));
    private static string Hash(ReadOnlySpan<byte> value) =>
        Convert.ToHexStringLower(SHA256.HashData(value));
}

public sealed class StripeAdminWithdrawalProviderEvidenceVerifier(
    IOptions<StripePlatformWithdrawalOptions> options) : IAdminWithdrawalProviderEvidenceVerifier
{
    private readonly StripePlatformWithdrawalOptions _options = options?.Value
        ?? throw new ArgumentNullException(nameof(options));

    public bool Verify(AdminWithdrawalProviderReceipt receipt) =>
        StripeTreasuryEvidenceSigning.VerifyReceipt(receipt, _options.EvidenceSigningSecret);

    public bool Verify(AdminWithdrawalProviderEvent providerEvent) =>
        StripeTreasuryEvidenceSigning.VerifyEvent(providerEvent, _options.EvidenceSigningSecret);
}

public static class StripeTreasuryEvidenceSigning
{
    public static string SignReceipt(AdminWithdrawalProviderReceipt value, string secret) =>
        Sign(Canonical(value.TenantId, value.RunId, value.Outcome, value.ProviderTransferId, value.FencingToken,
            value.ExecutionEpoch, value.Amount.Units, value.SourceAssetKey, value.DestinationHash,
            value.EvidenceHash, value.ObservedAt), secret);

    public static string SignEvent(AdminWithdrawalProviderEvent value, string secret) =>
        Sign(value.EventId + "|" + Canonical(value.TenantId, value.RunId, value.Outcome, value.ProviderTransferId,
            value.FencingToken, value.ExecutionEpoch, value.Amount.Units, value.SourceAssetKey,
            value.DestinationHash, value.EvidenceHash, value.ObservedAt), secret);

    public static bool VerifyReceipt(AdminWithdrawalProviderReceipt value, string secret) =>
        Verify(SignReceipt(value with { Signature = string.Empty }, secret), value.Signature);

    public static bool VerifyEvent(AdminWithdrawalProviderEvent value, string secret) =>
        Verify(SignEvent(value with { Signature = string.Empty }, secret), value.Signature);

    private static string Canonical(
        Guid tenantId, Guid id, AdminWithdrawalProviderOutcome outcome, string providerId, long fence, long epoch,
        long units, string source, string destination, string evidence, DateTimeOffset observedAt) =>
        string.Join('|', tenantId.ToString("N"), id.ToString("N"), (int)outcome, providerId, fence, epoch, units,
            source, destination, evidence, observedAt.UtcTicks);

    private static string Sign(string payload, string secret)
    {
        if (Encoding.UTF8.GetByteCount(secret) < 32) return string.Empty;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    private static bool Verify(string expected, string supplied) =>
        expected.Length > 0 && expected.Length == supplied.Length &&
        CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(supplied.ToLowerInvariant()));
}
