using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using Microsoft.Extensions.Options;

namespace GameGuild.Finance.Economy.Treasury.UnitTests;

public sealed class StripePlatformAdminWithdrawalProviderTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 18, 0, 0, TimeSpan.Zero);
    private const string WebhookSecret = "webhook-secret-with-at-least-32-bytes";
    private const string EvidenceSecret = "evidence-secret-with-at-least-32-bytes";

    [Theory]
    [InlineData("paid", AdminWithdrawalProviderOutcome.Succeeded)]
    [InlineData("failed", AdminWithdrawalProviderOutcome.Failed)]
    [InlineData("canceled", AdminWithdrawalProviderOutcome.Failed)]
    [InlineData("pending", AdminWithdrawalProviderOutcome.Submitted)]
    [InlineData("in_transit", AdminWithdrawalProviderOutcome.Submitted)]
    [InlineData("unknown", AdminWithdrawalProviderOutcome.Ambiguous)]
    [InlineData(null, AdminWithdrawalProviderOutcome.Ambiguous)]
    public async Task Dispatch_MapsStripeOutcomeSignsEvidenceAndBindsRequest(
        string? status,
        AdminWithdrawalProviderOutcome expected)
    {
        var handler = new RecordingHandler();
        handler.Respond(HttpStatusCode.OK, JsonSerializer.Serialize(new { id = "po_123", status }));
        var provider = CreateProvider(handler);
        var command = Command();

        var receipt = await provider.DispatchAsync(command);

        receipt.RunId.Should().Be(command.RunId);
        receipt.TenantId.Should().Be(command.TenantId);
        receipt.Outcome.Should().Be(expected);
        receipt.ProviderTransferId.Should().Be("po_123");
        receipt.FencingToken.Should().Be(command.FencingToken);
        receipt.ExecutionEpoch.Should().Be(command.ExecutionEpoch);
        receipt.Amount.Should().Be(command.Amount);
        receipt.SourceAssetKey.Should().Be(command.SourceAssetKey);
        receipt.DestinationHash.Should().Be(command.DestinationHash);
        receipt.EvidenceHash.Should().HaveLength(64);
        receipt.ObservedAt.Should().Be(Now);
        StripeTreasuryEvidenceSigning.VerifyReceipt(receipt, EvidenceSecret).Should().BeTrue();
        var request = handler.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Post);
        request.PathAndQuery.Should().Be("/v1/payouts");
        request.Authorization.Should().Be("Bearer sk_test");
        request.IdempotencyKey.Should().Be(command.IdempotencyKey);
        request.Body.Should().Contain("amount=2");
        request.Body.Should().Contain("currency=usd");
        request.Body.Should().Contain(command.TenantId.ToString("N"));
        request.Body.Should().Contain(command.RunId.ToString("N"));
    }

    [Fact]
    public async Task Dispatch_ReturnsSignedAmbiguousReceiptForServerAndTransportFailures()
    {
        var command = Command();
        var handler = new RecordingHandler();
        handler.Respond(HttpStatusCode.InternalServerError, "server-failure");
        handler.Throw(new HttpRequestException("network"));
        handler.Throw(new TaskCanceledException("timeout"));
        handler.Respond(HttpStatusCode.BadRequest, "client-failure");
        var provider = CreateProvider(handler);

        for (var index = 0; index < 4; index++)
        {
            var receipt = await provider.DispatchAsync(command);
            receipt.Outcome.Should().Be(AdminWithdrawalProviderOutcome.Ambiguous);
            receipt.ProviderTransferId.Should().Be("unknown:" + command.RunId.ToString("N"));
            receipt.EvidenceHash.Should().HaveLength(64);
            StripeTreasuryEvidenceSigning.VerifyReceipt(receipt, EvidenceSecret).Should().BeTrue();
        }
    }

    [Fact]
    public async Task Dispatch_RejectsResponseWithoutProviderIdentity()
    {
        var handler = new RecordingHandler();
        handler.Respond(HttpStatusCode.OK, "{\"status\":\"paid\"}");

        await FluentActions.Awaiting(() => CreateProvider(handler).DispatchAsync(Command()).AsTask())
            .Should().ThrowAsync<AdminWithdrawalEvidenceException>();
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("run")]
    [InlineData("currency")]
    [InlineData("zero")]
    [InlineData("scale")]
    public async Task Dispatch_RejectsIneligibleCommand(string invalid)
    {
        var command = Command() with
        {
            TenantId = invalid == "tenant" ? Guid.Empty : Command().TenantId,
            RunId = invalid == "run" ? Guid.Empty : Command().RunId,
            Amount = invalid switch
            {
                "currency" => new CoinAmount(CurrencyCode.SoftCoin, 2_000),
                "zero" => new CoinAmount(CurrencyCode.HardCoin, 0),
                "scale" => new CoinAmount(CurrencyCode.HardCoin, 1_001),
                _ => Command().Amount
            }
        };

        await FluentActions.Awaiting(() => CreateProvider(new RecordingHandler())
                .DispatchAsync(command).AsTask())
            .Should().ThrowAsync<AdminWithdrawalEligibilityException>();
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("url")]
    [InlineData("key")]
    [InlineData("webhook")]
    [InlineData("evidence")]
    [InlineData("tolerance")]
    public async Task Provider_RemainsDisabledForEveryIncompleteConfiguration(string invalid)
    {
        var options = ValidOptions();
        if (invalid == "disabled") options.Enabled = false;
        if (invalid == "url") options.ApiBaseUrl = "relative";
        if (invalid == "key") options.SecretKey = " ";
        if (invalid == "webhook") options.WebhookSecret = " ";
        if (invalid == "evidence") options.EvidenceSigningSecret = "short";
        if (invalid == "tolerance") options.WebhookTolerance = TimeSpan.Zero;

        var provider = new StripePlatformAdminWithdrawalProvider(
            new HttpClient(new RecordingHandler()), Options.Create(options), new FixedTimeProvider(Now));

        await FluentActions.Awaiting(() => provider.DispatchAsync(Command()).AsTask())
            .Should().ThrowAsync<AdminWithdrawalExecutionDisabledException>();
    }

    [Fact]
    public async Task Dispatch_RejectsNullCommandAndConstructorDependencies()
    {
        var handler = new RecordingHandler();
        var options = Options.Create(ValidOptions());
        var time = new FixedTimeProvider(Now);

        FluentActions.Invoking(() => new StripePlatformAdminWithdrawalProvider(null!, options, time))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new StripePlatformAdminWithdrawalProvider(
                new HttpClient(handler), null!, time))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new StripePlatformAdminWithdrawalProvider(
                new HttpClient(handler), options, null!))
            .Should().Throw<ArgumentNullException>();
        await FluentActions.Awaiting(() => CreateProvider(handler).DispatchAsync(null!).AsTask())
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData("direct")]
    [InlineData("missing")]
    [InlineData("unknown")]
    public async Task Reconcile_UsesDirectOrDiscoveredPayoutAndReturnsSignedEvent(string lookup)
    {
        var command = Command();
        var handler = new RecordingHandler();
        if (lookup != "direct")
        {
            handler.Respond(HttpStatusCode.OK, JsonSerializer.Serialize(new
            {
                data = new object[]
                {
                    new { id = "no-metadata" },
                    new { id = "wrong-tenant", metadata = Metadata(Guid.NewGuid(), command.RunId, command) },
                    new { id = "wrong-run", metadata = Metadata(command.TenantId, Guid.NewGuid(), command) },
                    new { id = "po_found", metadata = Metadata(command.TenantId, command.RunId, command) }
                }
            }));
        }
        handler.Respond(HttpStatusCode.OK, PayoutJson(command, "paid", "po_found"));
        var provider = CreateProvider(handler);

        var result = await provider.ReconcileAsync(
            command.TenantId, command.RunId, command.IdempotencyKey,
            lookup == "direct" ? " po_found " : lookup == "unknown" ? "unknown:run" : null);

        result.EventId.Should().StartWith("reconcile:po_found:");
        result.RunId.Should().Be(command.RunId);
        result.TenantId.Should().Be(command.TenantId);
        result.Outcome.Should().Be(AdminWithdrawalProviderOutcome.Succeeded);
        result.ProviderTransferId.Should().Be("po_found");
        result.FencingToken.Should().Be(command.FencingToken);
        result.ExecutionEpoch.Should().Be(command.ExecutionEpoch);
        result.Amount.Should().Be(command.Amount);
        result.SourceAssetKey.Should().Be(command.SourceAssetKey);
        result.DestinationHash.Should().Be(command.DestinationHash);
        result.EvidenceHash.Should().HaveLength(64);
        result.ObservedAt.Should().Be(Now);
        StripeTreasuryEvidenceSigning.VerifyEvent(result, EvidenceSecret).Should().BeTrue();
        handler.Requests.Last().PathAndQuery.Should().Be("/v1/payouts/po_found");
        handler.Requests.Last().IdempotencyKey.Should().BeNull();
    }

    [Fact]
    public async Task Reconcile_RejectsMissingDiscoveredPayout()
    {
        var handler = new RecordingHandler();
        handler.Respond(HttpStatusCode.OK, "{\"data\":[]}");

        await FluentActions.Awaiting(() => CreateProvider(handler).ReconcileAsync(
                Guid.NewGuid(), Guid.NewGuid(), "key", null).AsTask())
            .Should().ThrowAsync<AdminWithdrawalEvidenceException>();
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("run")]
    [InlineData("key")]
    public async Task Reconcile_RejectsInvalidIdentity(string invalid)
    {
        var command = Command();

        await FluentActions.Awaiting(() => CreateProvider(new RecordingHandler()).ReconcileAsync(
                invalid == "tenant" ? Guid.Empty : command.TenantId,
                invalid == "run" ? Guid.Empty : command.RunId,
                invalid == "key" ? " " : command.IdempotencyKey,
                "po").AsTask())
            .Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("run")]
    [InlineData("fence")]
    [InlineData("epoch")]
    public async Task Reconcile_RejectsInvalidFencingMetadata(string invalid)
    {
        var command = Command();
        var metadata = new Dictionary<string, string>
        {
            ["tenant_id"] = invalid == "tenant" ? "invalid" : command.TenantId.ToString("N"),
            ["run_id"] = invalid == "run" ? "invalid" : command.RunId.ToString("N"),
            ["fencing_token"] = invalid == "fence" ? "invalid" : command.FencingToken.ToString(),
            ["execution_epoch"] = invalid == "epoch" ? "invalid" : command.ExecutionEpoch.ToString(),
            ["source_asset_key"] = command.SourceAssetKey,
            ["destination_hash"] = command.DestinationHash
        };
        var handler = new RecordingHandler();
        handler.Respond(HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            id = "po", status = "paid", amount = 2, metadata
        }));

        await FluentActions.Awaiting(() => CreateProvider(handler).ReconcileAsync(
                command.TenantId, command.RunId, command.IdempotencyKey, "po").AsTask())
            .Should().ThrowAsync<AdminWithdrawalEvidenceException>();
    }

    [Fact]
    public async Task Reconcile_RejectsAmountOverflowAndHttpFailure()
    {
        var command = Command();
        var overflow = new RecordingHandler();
        overflow.Respond(HttpStatusCode.OK, PayoutJson(command, "paid", "po", long.MaxValue));
        await FluentActions.Awaiting(() => CreateProvider(overflow).ReconcileAsync(
                command.TenantId, command.RunId, command.IdempotencyKey, "po").AsTask())
            .Should().ThrowAsync<OverflowException>();

        var failed = new RecordingHandler();
        failed.Respond(HttpStatusCode.BadRequest, "failed");
        await FluentActions.Awaiting(() => CreateProvider(failed).ReconcileAsync(
                command.TenantId, command.RunId, command.IdempotencyKey, "po").AsTask())
            .Should().ThrowAsync<HttpRequestException>();
    }

    [Theory]
    [InlineData("payout.paid", "paid", AdminWithdrawalProviderOutcome.Succeeded)]
    [InlineData("payout.failed", "pending", AdminWithdrawalProviderOutcome.Failed)]
    [InlineData("payout.canceled", "pending", AdminWithdrawalProviderOutcome.Failed)]
    [InlineData("payout.updated", "in_transit", AdminWithdrawalProviderOutcome.Submitted)]
    public void Normalize_VerifiesWebhookAndMapsOutcome(
        string type,
        string status,
        AdminWithdrawalProviderOutcome expected)
    {
        var command = Command();
        var payload = Encoding.UTF8.GetBytes(WebhookJson(command, type, status));
        var provider = CreateProvider(new RecordingHandler());
        var lowerSignature = ComputeWebhookSignature(payload, Now);
        var header = SignatureHeader(payload, Now, includeWrongSignatureFirst: true)
            .Replace(lowerSignature, lowerSignature.ToUpperInvariant(), StringComparison.Ordinal);

        var result = provider.Normalize(payload, header, Now);

        result.Outcome.Should().Be(expected);
        result.EventId.Should().Be("evt_1");
        StripeTreasuryEvidenceSigning.VerifyEvent(result, EvidenceSecret).Should().BeTrue();
    }

    [Theory]
    [InlineData("blank")]
    [InlineData("malformed")]
    [InlineData("timestamp")]
    [InlineData("signature-missing")]
    [InlineData("future")]
    [InlineData("stale")]
    [InlineData("length")]
    [InlineData("value")]
    public void Normalize_RejectsInvalidSignatureOrTimestamp(string invalid)
    {
        var payload = Encoding.UTF8.GetBytes(WebhookJson(Command(), "payout.paid", "paid"));
        var at = invalid == "future" ? Now.AddMinutes(-1) : invalid == "stale" ? Now.AddHours(1) : Now;
        var timestamp = invalid == "timestamp" ? "invalid" : Now.ToUnixTimeSeconds().ToString();
        var signature = invalid switch
        {
            "blank" => " ",
            "malformed" => "ignored",
            "signature-missing" => $"t={timestamp}",
            "length" => $"t={timestamp},v1=short",
            "value" => $"t={timestamp},v1={new string('0', 64)}",
            _ => $"ignored,t={timestamp},v0=old,v1={ComputeWebhookSignature(payload, Now)}"
        };

        FluentActions.Invoking(() => CreateProvider(new RecordingHandler()).Normalize(payload, signature, at))
            .Should().Throw<AdminWithdrawalEvidenceException>();
    }

    [Fact]
    public void Normalize_RejectsMissingRequiredResponseProperties()
    {
        var provider = CreateProvider(new RecordingHandler());
        var payload = Encoding.UTF8.GetBytes("{\"id\":\"evt\",\"data\":{\"object\":{}}}");

        FluentActions.Invoking(() => provider.Normalize(payload, SignatureHeader(payload, Now), Now))
            .Should().Throw<AdminWithdrawalEvidenceException>();
    }

    [Fact]
    public void EvidenceSigningAndVerifier_FailClosedForTamperingAndWeakSecrets()
    {
        var command = Command();
        var receipt = new AdminWithdrawalProviderReceipt(
            command.RunId, command.TenantId, AdminWithdrawalProviderOutcome.Submitted, "po",
            command.FencingToken, command.ExecutionEpoch, command.Amount, command.SourceAssetKey,
            command.DestinationHash, "evidence", string.Empty, Now);
        receipt = receipt with { Signature = StripeTreasuryEvidenceSigning.SignReceipt(receipt, EvidenceSecret) };
        var providerEvent = new AdminWithdrawalProviderEvent(
            "evt", receipt.RunId, receipt.TenantId, receipt.Outcome, receipt.ProviderTransferId,
            receipt.FencingToken, receipt.ExecutionEpoch, receipt.Amount, receipt.SourceAssetKey,
            receipt.DestinationHash, receipt.EvidenceHash, string.Empty, receipt.ObservedAt);
        providerEvent = providerEvent with
        {
            Signature = StripeTreasuryEvidenceSigning.SignEvent(providerEvent, EvidenceSecret)
        };
        var verifier = new StripeAdminWithdrawalProviderEvidenceVerifier(Options.Create(ValidOptions()));

        verifier.Verify(receipt).Should().BeTrue();
        verifier.Verify(providerEvent).Should().BeTrue();
        verifier.Verify(receipt with { Amount = new CoinAmount(CurrencyCode.HardCoin, 3_000) }).Should().BeFalse();
        verifier.Verify(receipt with { Signature = "short" }).Should().BeFalse();
        verifier.Verify(receipt with { Signature = new string('0', receipt.Signature.Length) }).Should().BeFalse();
        verifier.Verify(providerEvent with { EventId = "changed" }).Should().BeFalse();
        StripeTreasuryEvidenceSigning.SignReceipt(receipt, "weak").Should().BeEmpty();
        StripeTreasuryEvidenceSigning.SignEvent(providerEvent, "weak").Should().BeEmpty();
        FluentActions.Invoking(() => new StripeAdminWithdrawalProviderEvidenceVerifier(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Options_ExposeFailClosedDefaultsAndConfiguredValues()
    {
        var defaults = new StripePlatformWithdrawalOptions();
        defaults.Enabled.Should().BeFalse();
        defaults.ApiBaseUrl.Should().BeEmpty();
        defaults.SecretKey.Should().BeEmpty();
        defaults.WebhookSecret.Should().BeEmpty();
        defaults.EvidenceSigningSecret.Should().BeEmpty();
        defaults.WebhookTolerance.Should().Be(TimeSpan.Zero);
        StripePlatformWithdrawalOptions.SectionName.Should().Be("Modules:Economy:Treasury:StripePlatform");
    }

    private static StripePlatformAdminWithdrawalProvider CreateProvider(RecordingHandler handler) => new(
        new HttpClient(handler), Options.Create(ValidOptions()), new FixedTimeProvider(Now));

    private static StripePlatformWithdrawalOptions ValidOptions() => new()
    {
        Enabled = true,
        ApiBaseUrl = "https://stripe.test",
        SecretKey = "sk_test",
        WebhookSecret = WebhookSecret,
        EvidenceSigningSecret = EvidenceSecret,
        WebhookTolerance = TimeSpan.FromMinutes(5)
    };

    private static AdminWithdrawalDispatchCommand Command()
    {
        var runId = Guid.Parse("a1000000-0000-0000-0000-000000000001");
        var tenantId = Guid.Parse("b1000000-0000-0000-0000-000000000001");
        return new AdminWithdrawalDispatchCommand(
            runId, tenantId, 3, 11, 11,
            new CoinAmount(CurrencyCode.HardCoin, 2_000), "stripe:platform:cash",
            "destination-hash", "snapshot-hash", "dispatch-key", Now.AddMinutes(-1));
    }

    private static Dictionary<string, string> Metadata(
        Guid tenantId,
        Guid runId,
        AdminWithdrawalDispatchCommand command) => new()
    {
        ["tenant_id"] = tenantId.ToString("N"),
        ["run_id"] = runId.ToString("N"),
        ["fencing_token"] = command.FencingToken.ToString(),
        ["execution_epoch"] = command.ExecutionEpoch.ToString(),
        ["source_asset_key"] = command.SourceAssetKey,
        ["destination_hash"] = command.DestinationHash
    };

    private static string PayoutJson(
        AdminWithdrawalDispatchCommand command,
        string status,
        string id,
        long amount = 2) => JsonSerializer.Serialize(new
    {
        id,
        status,
        amount,
        metadata = Metadata(command.TenantId, command.RunId, command)
    });

    private static string WebhookJson(
        AdminWithdrawalDispatchCommand command,
        string type,
        string status) => JsonSerializer.Serialize(new
    {
        id = "evt_1",
        type,
        data = new { @object = JsonSerializer.Deserialize<JsonElement>(PayoutJson(command, status, "po_1")) }
    });

    private static string SignatureHeader(
        byte[] payload,
        DateTimeOffset timestamp,
        bool includeWrongSignatureFirst = false)
    {
        var signature = ComputeWebhookSignature(payload, timestamp);
        return $"t={timestamp.ToUnixTimeSeconds()}," +
               (includeWrongSignatureFirst ? $"v1={new string('0', 64)}," : string.Empty) +
               $"v1={signature}";
    }

    private static string ComputeWebhookSignature(byte[] payload, DateTimeOffset timestamp)
    {
        var prefix = Encoding.UTF8.GetBytes(timestamp.ToUnixTimeSeconds() + ".");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(WebhookSecret));
        return Convert.ToHexStringLower(hmac.ComputeHash(prefix.Concat(payload).ToArray()));
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Queue<Func<Task<HttpResponseMessage>>> _responses = new();
        public List<CapturedRequest> Requests { get; } = [];

        public void Respond(HttpStatusCode status, string content) => _responses.Enqueue(() =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            }));

        public void Throw(Exception exception) => _responses.Enqueue(
            () => Task.FromException<HttpResponseMessage>(exception));

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(new CapturedRequest(
                request.Method,
                request.RequestUri!.PathAndQuery,
                request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("Idempotency-Key", out var values) ? values.Single() : null,
                request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));
            return await _responses.Dequeue()();
        }
    }

    private sealed record CapturedRequest(
        HttpMethod Method,
        string PathAndQuery,
        string? Authorization,
        string? IdempotencyKey,
        string Body);
}
