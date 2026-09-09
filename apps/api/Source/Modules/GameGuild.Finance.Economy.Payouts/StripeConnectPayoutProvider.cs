using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GameGuild.Finance.Economy.Payouts;

public sealed class StripeConnectPayoutOptions
{
    public const string SectionName = "Modules:Economy:Payouts:StripeConnect";

    public bool Enabled { get; set; }
    public string ApiBaseUrl { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string EvidenceSigningSecret { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = string.Empty;
    public string RefreshUrl { get; set; } = string.Empty;
    public TimeSpan WebhookTolerance { get; set; }
    public TimeSpan AccountEvidenceLifetime { get; set; }
}

public interface IStripeConnectWebhookNormalizer
{
    ValueTask<PayoutProviderEvent> NormalizeAsync(
        ReadOnlyMemory<byte> rawPayload,
        string signatureHeader,
        DateTimeOffset receivedAt,
        CancellationToken cancellationToken = default);
}

public sealed class StripeConnectPayoutProvider : IConnectPayoutProvider, IStripeConnectWebhookNormalizer
{
    internal const long HardUnitsPerUsdMinor = 1_000;
    private readonly HttpClient _http;
    private readonly DbContext _db;
    private readonly StripeConnectPayoutOptions _options;
    private readonly TimeProvider _time;

    public StripeConnectPayoutProvider(
        HttpClient httpClient,
        IApplicationDbContext context,
        IOptions<StripeConnectPayoutOptions> options,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _http = httpClient;
        _db = context as DbContext
            ?? throw new InvalidOperationException("Stripe Connect requires the application's relational DbContext.");
        _options = options.Value;
        _time = timeProvider;
    }

    public async ValueTask<ConnectOnboardingResult> CreateOrRefreshAccountAsync(
        Guid payeeId,
        CancellationToken cancellationToken = default)
    {
        if (payeeId == Guid.Empty) throw new ArgumentException("Payee ID is required.", nameof(payeeId));
        EnsureConfigured();
        var binding = await _db.Set<PayoutConnectAccountRow>().SingleOrDefaultAsync(
            row => row.PayeeId == payeeId, cancellationToken);
        var accountId = binding?.ProviderAccountId;
        if (string.IsNullOrWhiteSpace(accountId))
        {
            using var created = await SendFormAsync(
                HttpMethod.Post,
                "/v1/accounts",
                [new("type", "express"), new("metadata[payee_id]", payeeId.ToString("N"))],
                $"connect-account:{payeeId:N}",
                null,
                cancellationToken);
            created.EnsureSuccessStatusCode();
            var raw = await created.Content.ReadAsStringAsync(cancellationToken);
            accountId = RequireString(JsonDocument.Parse(raw).RootElement, "id");
        }

        var snapshot = await RefreshAccountAsync(payeeId, accountId, cancellationToken);
        Uri? onboarding = null;
        if (snapshot.State != ConnectAccountState.Ready)
        {
            using var link = await SendFormAsync(
                HttpMethod.Post,
                "/v1/account_links",
                [
                    new("account", accountId),
                    new("type", "account_onboarding"),
                    new("return_url", _options.ReturnUrl),
                    new("refresh_url", _options.RefreshUrl)
                ],
                $"connect-link:{payeeId:N}:{snapshot.Version}",
                null,
                cancellationToken);
            link.EnsureSuccessStatusCode();
            var raw = await link.Content.ReadAsStringAsync(cancellationToken);
            onboarding = new Uri(RequireString(JsonDocument.Parse(raw).RootElement, "url"));
        }
        return new ConnectOnboardingResult(snapshot, onboarding);
    }

    public async ValueTask<ConnectAccountSnapshot> GetAccountAsync(
        Guid payeeId,
        CancellationToken cancellationToken = default)
    {
        if (payeeId == Guid.Empty) throw new ArgumentException("Payee ID is required.", nameof(payeeId));
        EnsureConfigured();
        var binding = await _db.Set<PayoutConnectAccountRow>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.PayeeId == payeeId, cancellationToken)
            ?? throw new PayoutEligibilityException("The payee has no Stripe Connect account binding.");
        return await RefreshAccountAsync(payeeId, binding.ProviderAccountId, cancellationToken);
    }

    public async ValueTask<PayoutDispatchReceipt> DispatchAsync(
        PayoutDispatchCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureConfigured();
        if (command.OperationId == Guid.Empty || command.Amount.Units <= 0 ||
            command.Amount.Units % HardUnitsPerUsdMinor != 0)
            throw new PayoutEligibilityException(
                "Stripe dispatch requires a positive amount aligned to the fixed 1:1000 hard-unit scale.");
        var amount = command.Amount.Units / HardUnitsPerUsdMinor;
        try
        {
            using var response = await SendFormAsync(
                HttpMethod.Post,
                "/v1/payouts",
                [
                    new("amount", amount.ToString(CultureInfo.InvariantCulture)),
                    new("currency", "usd"),
                    new("metadata[operation_id]", command.OperationId.ToString("N")),
                    new("metadata[destination_hash]", command.DestinationHash),
                    new("metadata[dispatch_snapshot_hash]", command.DispatchSnapshotHash)
                ],
                command.IdempotencyKey,
                command.ProviderAccountId,
                cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            if ((int)response.StatusCode >= 500)
                return CreateAmbiguousReceipt(command, Hash(raw), _time.GetUtcNow());
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(raw);
            var payoutId = RequireString(document.RootElement, "id");
            var outcome = MapPayoutOutcome(OptionalString(document.RootElement, "status"));
            return SignReceipt(new PayoutDispatchReceipt(
                command.OperationId,
                outcome,
                payoutId,
                command.ProviderAccountId,
                command.DestinationHash,
                Hash(raw),
                string.Empty,
                _time.GetUtcNow()));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return CreateAmbiguousReceipt(command, Hash(exception.GetType().Name), _time.GetUtcNow());
        }
    }

    public async ValueTask<PayoutProviderEvent> ReconcileAsync(
        Guid operationId,
        string providerPayoutId,
        CancellationToken cancellationToken = default)
    {
        if (operationId == Guid.Empty) throw new ArgumentException("Operation ID is required.", nameof(operationId));
        ArgumentException.ThrowIfNullOrWhiteSpace(providerPayoutId);
        EnsureConfigured();
        var operation = await _db.Set<PayoutOperationRow>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == operationId, cancellationToken)
            ?? throw new KeyNotFoundException("Payout operation was not found.");
        var payoutId = providerPayoutId.StartsWith("unknown:", StringComparison.Ordinal)
            ? await FindPayoutByOperationAsync(operation, cancellationToken)
            : providerPayoutId.Trim();
        using var response = await SendFormAsync(
            HttpMethod.Get,
            "/v1/payouts/" + Uri.EscapeDataString(payoutId),
            [],
            null,
            operation.ProviderAccountId,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(raw);
        return SignEvent(new PayoutProviderEvent(
            "reconcile:" + payoutId + ":" + Hash(raw),
            operationId,
            MapPayoutOutcome(OptionalString(document.RootElement, "status")),
            payoutId,
            operation.ProviderAccountId,
            operation.DestinationHash,
            Hash(raw),
            string.Empty,
            _time.GetUtcNow()));
    }

    public async ValueTask<PayoutProviderEvent> NormalizeAsync(
        ReadOnlyMemory<byte> rawPayload,
        string signatureHeader,
        DateTimeOffset receivedAt,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        if (!StripeSignature.Verify(rawPayload.Span, signatureHeader, _options.WebhookSecret,
                receivedAt, _options.WebhookTolerance))
            throw new PayoutEvidenceException("Stripe Connect webhook signature or timestamp is invalid.");
        using var document = JsonDocument.Parse(rawPayload);
        var root = document.RootElement;
        var eventId = RequireString(root, "id");
        var type = RequireString(root, "type");
        var accountId = RequireString(root, "account");
        var payout = root.GetProperty("data").GetProperty("object");
        var payoutId = RequireString(payout, "id");
        var metadata = payout.GetProperty("metadata");
        if (!Guid.TryParseExact(RequireString(metadata, "operation_id"), "N", out var operationId))
            throw new PayoutEvidenceException("Stripe payout webhook has no valid operation binding.");
        var destinationHash = RequireString(metadata, "destination_hash");
        var operation = await _db.Set<PayoutOperationRow>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == operationId, cancellationToken)
            ?? throw new PayoutEvidenceException("Stripe payout webhook references an unknown operation.");
        if (!string.Equals(operation.ProviderAccountId, accountId, StringComparison.Ordinal) ||
            !string.Equals(operation.DestinationHash, destinationHash, StringComparison.Ordinal))
            throw new PayoutProviderBindingException("Stripe payout webhook binding does not match the operation.");
        var outcome = type switch
        {
            "payout.paid" => PayoutProviderOutcome.Succeeded,
            "payout.failed" or "payout.canceled" => PayoutProviderOutcome.Failed,
            _ => MapPayoutOutcome(OptionalString(payout, "status"))
        };
        return SignEvent(new PayoutProviderEvent(
            eventId, operationId, outcome, payoutId, accountId, destinationHash,
            Hash(rawPayload.Span), string.Empty, receivedAt));
    }

    private async ValueTask<ConnectAccountSnapshot> RefreshAccountAsync(
        Guid payeeId,
        string accountId,
        CancellationToken cancellationToken)
    {
        using var response = await SendFormAsync(
            HttpMethod.Get,
            "/v1/accounts/" + Uri.EscapeDataString(accountId),
            [], null, null, cancellationToken);
        response.EnsureSuccessStatusCode();
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(raw);
        var root = document.RootElement;
        var charges = OptionalBoolean(root, "charges_enabled");
        var payouts = OptionalBoolean(root, "payouts_enabled");
        var disabledReason = root.TryGetProperty("requirements", out var requirements)
            ? OptionalString(requirements, "disabled_reason")
            : null;
        var destination = DestinationHash(root);
        var state = payouts && destination is not null
            ? ConnectAccountState.Ready
            : string.IsNullOrWhiteSpace(disabledReason)
                ? ConnectAccountState.Pending
                : ConnectAccountState.Restricted;
        var observedAt = _time.GetUtcNow();
        var current = await _db.Set<PayoutConnectAccountRow>()
            .SingleOrDefaultAsync(row => row.PayeeId == payeeId, cancellationToken);
        var version = checked((current?.Version ?? 0) + 1);
        var snapshot = new ConnectAccountSnapshot(
            payeeId, accountId, destination ?? Hash("unavailable:" + accountId), state,
            charges, payouts, version, observedAt, observedAt + _options.AccountEvidenceLifetime, Hash(raw));
        if (current is null)
        {
            _db.Add(new PayoutConnectAccountRow
            {
                PayeeId = payeeId,
                Provider = "stripe",
                Environment = _options.Environment.Trim(),
                ProviderAccountId = accountId,
                DestinationHash = snapshot.DestinationHash,
                State = snapshot.State,
                ChargesEnabled = snapshot.ChargesEnabled,
                PayoutsEnabled = snapshot.PayoutsEnabled,
                Version = snapshot.Version,
                ObservedAt = snapshot.ObservedAt,
                ExpiresAt = snapshot.ExpiresAt,
                EvidenceHash = snapshot.EvidenceHash
            });
        }
        else
        {
            current.ProviderAccountId = accountId;
            current.DestinationHash = snapshot.DestinationHash;
            current.State = snapshot.State;
            current.ChargesEnabled = snapshot.ChargesEnabled;
            current.PayoutsEnabled = snapshot.PayoutsEnabled;
            current.Version = snapshot.Version;
            current.ObservedAt = snapshot.ObservedAt;
            current.ExpiresAt = snapshot.ExpiresAt;
            current.EvidenceHash = snapshot.EvidenceHash;
        }
        await _db.SaveChangesAsync(cancellationToken);
        return snapshot;
    }

    private async ValueTask<string> FindPayoutByOperationAsync(
        PayoutOperationRow operation,
        CancellationToken cancellationToken)
    {
        using var response = await SendFormAsync(
            HttpMethod.Get, "/v1/payouts?limit=100", [], null,
            operation.ProviderAccountId, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        foreach (var item in document.RootElement.GetProperty("data").EnumerateArray())
            if (item.TryGetProperty("metadata", out var metadata) &&
                OptionalString(metadata, "operation_id") == operation.Id.ToString("N"))
                return RequireString(item, "id");
        throw new PayoutEvidenceException("Stripe reconciliation found no payout for the operation.");
    }

    private async Task<HttpResponseMessage> SendFormAsync(
        HttpMethod method,
        string path,
        IReadOnlyList<KeyValuePair<string, string>> fields,
        string? idempotencyKey,
        string? connectedAccount,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, new Uri(new Uri(_options.ApiBaseUrl), path));
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.SecretKey);
        if (!string.IsNullOrWhiteSpace(idempotencyKey)) request.Headers.Add("Idempotency-Key", idempotencyKey.Trim());
        if (!string.IsNullOrWhiteSpace(connectedAccount)) request.Headers.Add("Stripe-Account", connectedAccount.Trim());
        if (fields.Count > 0) request.Content = new FormUrlEncodedContent(fields);
        return await _http.SendAsync(request, cancellationToken);
    }

    private PayoutDispatchReceipt CreateAmbiguousReceipt(
        PayoutDispatchCommand command,
        string evidenceHash,
        DateTimeOffset observedAt) => SignReceipt(new PayoutDispatchReceipt(
            command.OperationId,
            PayoutProviderOutcome.Ambiguous,
            "unknown:" + command.OperationId.ToString("N"),
            command.ProviderAccountId,
            command.DestinationHash,
            evidenceHash,
            string.Empty,
            observedAt));

    private PayoutDispatchReceipt SignReceipt(PayoutDispatchReceipt receipt) => receipt with
    {
        Signature = StripePayoutEvidenceSigning.SignReceipt(receipt, _options.EvidenceSigningSecret)
    };

    private PayoutProviderEvent SignEvent(PayoutProviderEvent providerEvent) => providerEvent with
    {
        Signature = StripePayoutEvidenceSigning.SignEvent(providerEvent, _options.EvidenceSigningSecret)
    };

    private void EnsureConfigured()
    {
        if (!_options.Enabled || !Uri.TryCreate(_options.ApiBaseUrl, UriKind.Absolute, out _) ||
            string.IsNullOrWhiteSpace(_options.SecretKey) || string.IsNullOrWhiteSpace(_options.WebhookSecret) ||
            Encoding.UTF8.GetByteCount(_options.EvidenceSigningSecret) < 32 ||
            string.IsNullOrWhiteSpace(_options.Environment) ||
            !Uri.TryCreate(_options.ReturnUrl, UriKind.Absolute, out _) ||
            !Uri.TryCreate(_options.RefreshUrl, UriKind.Absolute, out _) ||
            _options.WebhookTolerance <= TimeSpan.Zero || _options.AccountEvidenceLifetime <= TimeSpan.Zero)
            throw new PayoutExecutionDisabledException(
                "Stripe Connect remains disabled until endpoint, credentials, URLs and evidence policies are configured.");
    }

    private static PayoutProviderOutcome MapPayoutOutcome(string? status) => status switch
    {
        "paid" => PayoutProviderOutcome.Succeeded,
        "failed" or "canceled" => PayoutProviderOutcome.Failed,
        "pending" or "in_transit" => PayoutProviderOutcome.Submitted,
        _ => PayoutProviderOutcome.Ambiguous
    };

    private static string? DestinationHash(JsonElement account)
    {
        if (!account.TryGetProperty("external_accounts", out var external) ||
            !external.TryGetProperty("data", out var data) || data.GetArrayLength() == 0)
            return null;
        var destination = data[0];
        var fingerprint = OptionalString(destination, "fingerprint");
        if (string.IsNullOrWhiteSpace(fingerprint)) return null;
        return Hash(string.Join('|',
            fingerprint,
            OptionalString(destination, "currency") ?? string.Empty,
            OptionalString(destination, "country") ?? string.Empty,
            OptionalString(destination, "last4") ?? string.Empty));
    }

    private static string RequireString(JsonElement element, string name) =>
        OptionalString(element, name) is { Length: > 0 } value
            ? value
            : throw new PayoutEvidenceException($"Stripe response requires '{name}'.");

    private static string? OptionalString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool OptionalBoolean(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static string Hash(string value) => Hash(Encoding.UTF8.GetBytes(value));
    private static string Hash(ReadOnlySpan<byte> value) =>
        Convert.ToHexStringLower(SHA256.HashData(value));
}

public sealed class StripePayoutProviderEvidenceVerifier(
    IOptions<StripeConnectPayoutOptions> options) : IPayoutProviderEvidenceVerifier
{
    private readonly StripeConnectPayoutOptions _options = options?.Value
        ?? throw new ArgumentNullException(nameof(options));

    public bool Verify(PayoutDispatchReceipt receipt) =>
        StripePayoutEvidenceSigning.VerifyReceipt(receipt, _options.EvidenceSigningSecret);

    public bool Verify(PayoutProviderEvent providerEvent) =>
        StripePayoutEvidenceSigning.VerifyEvent(providerEvent, _options.EvidenceSigningSecret);
}

public static class StripePayoutEvidenceSigning
{
    public static string SignReceipt(PayoutDispatchReceipt receipt, string secret) =>
        Sign(CanonicalReceipt(receipt), secret);

    public static string SignEvent(PayoutProviderEvent providerEvent, string secret) =>
        Sign(CanonicalEvent(providerEvent), secret);

    public static bool VerifyReceipt(PayoutDispatchReceipt receipt, string secret) =>
        Verify(CanonicalReceipt(receipt), receipt.Signature, secret);

    public static bool VerifyEvent(PayoutProviderEvent providerEvent, string secret) =>
        Verify(CanonicalEvent(providerEvent), providerEvent.Signature, secret);

    private static string CanonicalReceipt(PayoutDispatchReceipt value) => string.Join('|',
        value.OperationId.ToString("N"), (int)value.Outcome, value.ProviderPayoutId,
        value.ProviderAccountId, value.DestinationHash, value.EvidenceHash,
        value.ObservedAt.UtcTicks);

    private static string CanonicalEvent(PayoutProviderEvent value) => string.Join('|',
        value.EventId, value.OperationId.ToString("N"), (int)value.Outcome,
        value.ProviderPayoutId, value.ProviderAccountId, value.DestinationHash,
        value.EvidenceHash, value.ObservedAt.UtcTicks);

    private static string Sign(string payload, string secret)
    {
        if (Encoding.UTF8.GetByteCount(secret) < 32) return string.Empty;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    private static bool Verify(string payload, string signature, string secret)
    {
        var expected = Sign(payload, secret);
        return expected.Length > 0 && expected.Length == signature.Length &&
               CryptographicOperations.FixedTimeEquals(
                   Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(signature.ToLowerInvariant()));
    }
}

public static class StripeSignature
{
    public static bool Verify(
        ReadOnlySpan<byte> payload,
        string header,
        string secret,
        DateTimeOffset receivedAt,
        TimeSpan tolerance)
    {
        if (string.IsNullOrWhiteSpace(header) || string.IsNullOrWhiteSpace(secret) || tolerance <= TimeSpan.Zero)
            return false;
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
        if (issuedAt > receivedAt || receivedAt - issuedAt > tolerance) return false;
        var signed = Encoding.UTF8.GetBytes(timestamp.ToString(CultureInfo.InvariantCulture) + ".")
            .Concat(payload.ToArray()).ToArray();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = Convert.ToHexStringLower(hmac.ComputeHash(signed));
        return signatures.Any(signature => signature.Length == expected.Length &&
            CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(signature.ToLowerInvariant()), Encoding.ASCII.GetBytes(expected)));
    }
}
