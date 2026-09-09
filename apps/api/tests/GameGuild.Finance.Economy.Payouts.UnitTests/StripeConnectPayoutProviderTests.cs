using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using GameGuild;
using GameGuild.API.Database;
using GameGuild.Finance.Economy.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace GameGuild.Finance.Economy.Payouts.UnitTests;

public sealed class StripeConnectPayoutProviderTests
{
    private const string EvidenceSecret = "payout-evidence-secret-that-is-at-least-32-bytes";
    private const string WebhookSecret = "whsec_payout_test";
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Accounts_CreateRefreshPersistAndReturnOnboardingOnlyWhenNotReady()
    {
        await using var context = Context();
        var payeeId = Guid.NewGuid();
        var handler = new RecordingHandler((request, call) => call switch
        {
            1 => Json(HttpStatusCode.OK, """{"id":"acct_new"}"""),
            2 => Json(HttpStatusCode.OK, AccountJson(false, false, null, "empty")),
            3 => Json(HttpStatusCode.OK, """{"url":"https://connect.example/onboard"}"""),
            _ => throw new InvalidOperationException()
        });
        var provider = Provider(context, handler);

        var created = await provider.CreateOrRefreshAccountAsync(payeeId);

        created.Account.State.Should().Be(ConnectAccountState.Pending);
        created.Account.Version.Should().Be(1);
        created.Account.DestinationHash.Should().HaveLength(64);
        created.OnboardingUri.Should().Be("https://connect.example/onboard");
        handler.Requests.Should().HaveCount(3);
        handler.Requests[0].Method.Should().Be(HttpMethod.Post);
        handler.Requests[0].Path.Should().Be("/v1/accounts");
        handler.Requests[0].IdempotencyKey.Should().Be($"connect-account:{payeeId:N}");
        handler.Requests[0].Body.Should().Contain("metadata%5Bpayee_id%5D=");
        handler.Requests[1].ConnectedAccount.Should().BeNull();
        handler.Requests[2].Body.Should().Contain("account=acct_new");
        var persisted = await context.Set<PayoutConnectAccountRow>().SingleAsync();
        persisted.Provider.Should().Be("stripe");
        persisted.Environment.Should().Be("sandbox");

        var readyHandler = new RecordingHandler((_, _) =>
            Json(HttpStatusCode.OK, AccountJson(true, true, null, "ready")));
        var ready = await Provider(context, readyHandler).CreateOrRefreshAccountAsync(payeeId);

        ready.Account.State.Should().Be(ConnectAccountState.Ready);
        ready.Account.Version.Should().Be(2);
        ready.Account.ChargesEnabled.Should().BeTrue();
        ready.Account.PayoutsEnabled.Should().BeTrue();
        ready.OnboardingUri.Should().BeNull();
        readyHandler.Requests.Should().ContainSingle();
        (await context.Set<PayoutConnectAccountRow>().SingleAsync()).Version.Should().Be(2);
    }

    [Theory]
    [InlineData("restricted", ConnectAccountState.Restricted)]
    [InlineData("missing-external", ConnectAccountState.Pending)]
    [InlineData("missing-data", ConnectAccountState.Pending)]
    [InlineData("empty", ConnectAccountState.Pending)]
    [InlineData("missing-fingerprint", ConnectAccountState.Pending)]
    [InlineData("fingerprint-only", ConnectAccountState.Ready)]
    public async Task GetAccount_MapsRestrictedAndEveryUnavailableDestinationShape(
        string accountShape,
        ConnectAccountState expected)
    {
        await using var context = Context();
        var payeeId = Guid.NewGuid();
        await SeedAccountAsync(context, payeeId, "acct_existing");
        var response = AccountJson(
            payouts: accountShape == "restricted" ? false : true,
            charges: false,
            disabledReason: accountShape == "restricted" ? "requirements.past_due" : null,
            externalShape: accountShape == "restricted" ? "empty" : accountShape);
        var provider = Provider(context, new RecordingHandler((_, _) => Json(HttpStatusCode.OK, response)));

        var snapshot = await provider.GetAccountAsync(payeeId);

        snapshot.State.Should().Be(expected);
        snapshot.Version.Should().Be(2);
        snapshot.ExpiresAt.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public async Task GetAccount_TreatsMissingBooleanAndRequirementsPropertiesAsPending()
    {
        await using var context = Context();
        var payeeId = Guid.NewGuid();
        await SeedAccountAsync(context, payeeId, "acct_missing_optional_properties");
        var response = JsonSerializer.Serialize(new
        {
            external_accounts = new { data = Array.Empty<object>() }
        });
        var provider = Provider(context, new RecordingHandler((_, _) => Json(HttpStatusCode.OK, response)));

        var snapshot = await provider.GetAccountAsync(payeeId);

        snapshot.State.Should().Be(ConnectAccountState.Pending);
        snapshot.ChargesEnabled.Should().BeFalse();
        snapshot.PayoutsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task AccountOperations_RejectMissingBindingsInvalidIdsProviderPayloadsAndOverflow()
    {
        await using var context = Context();
        var provider = Provider(context, new RecordingHandler((_, _) =>
            Json(HttpStatusCode.OK, AccountJson(false, false, null, "empty"))));
        await FluentActions.Awaiting(() => provider.CreateOrRefreshAccountAsync(Guid.Empty).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => provider.GetAccountAsync(Guid.Empty).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => provider.GetAccountAsync(Guid.NewGuid()).AsTask())
            .Should().ThrowAsync<PayoutEligibilityException>();

        var missingId = Provider(context, new RecordingHandler((_, _) =>
            Json(HttpStatusCode.OK, "{}")));
        await FluentActions.Awaiting(() => missingId.CreateOrRefreshAccountAsync(Guid.NewGuid()).AsTask())
            .Should().ThrowAsync<PayoutEvidenceException>();

        var payee = Guid.NewGuid();
        await SeedAccountAsync(context, payee, "acct_overflow", long.MaxValue);
        await FluentActions.Awaiting(() => provider.GetAccountAsync(payee).AsTask())
            .Should().ThrowAsync<OverflowException>();
    }

    [Theory]
    [InlineData("paid", PayoutProviderOutcome.Succeeded)]
    [InlineData("failed", PayoutProviderOutcome.Failed)]
    [InlineData("canceled", PayoutProviderOutcome.Failed)]
    [InlineData("pending", PayoutProviderOutcome.Submitted)]
    [InlineData("in_transit", PayoutProviderOutcome.Submitted)]
    [InlineData("unknown", PayoutProviderOutcome.Ambiguous)]
    [InlineData(null, PayoutProviderOutcome.Ambiguous)]
    public async Task Dispatch_MapsEveryProviderStatusAndBindsRequest(
        string? status,
        PayoutProviderOutcome expected)
    {
        await using var context = Context();
        var handler = new RecordingHandler((_, _) => Json(HttpStatusCode.OK,
            JsonSerializer.Serialize(new { id = "po_1", status })));
        var provider = Provider(context, handler);
        var command = Command();

        var receipt = await provider.DispatchAsync(command);

        receipt.Outcome.Should().Be(expected);
        receipt.ProviderPayoutId.Should().Be("po_1");
        StripePayoutEvidenceSigning.VerifyReceipt(receipt, EvidenceSecret).Should().BeTrue();
        var request = handler.Requests.Should().ContainSingle().Subject;
        request.Path.Should().Be("/v1/payouts");
        request.Authorization.Should().Be("Bearer sk_test");
        request.IdempotencyKey.Should().Be(command.IdempotencyKey);
        request.ConnectedAccount.Should().Be(command.ProviderAccountId);
        request.Body.Should().Contain("amount=2").And.Contain("currency=usd");
    }

    [Theory]
    [InlineData("server")]
    [InlineData("http")]
    [InlineData("timeout")]
    [InlineData("client")]
    public async Task Dispatch_TreatsUncertainTransportAndResponsesAsAmbiguous(string failure)
    {
        await using var context = Context();
        var handler = new RecordingHandler((_, _) => failure switch
        {
            "server" => Json(HttpStatusCode.InternalServerError, "server-error"),
            "client" => Json(HttpStatusCode.BadRequest, "client-error"),
            "http" => throw new HttpRequestException("network"),
            "timeout" => throw new TaskCanceledException("timeout"),
            _ => throw new InvalidOperationException()
        });
        var provider = Provider(context, handler);

        var receipt = await provider.DispatchAsync(Command());
        receipt.Outcome.Should().Be(PayoutProviderOutcome.Ambiguous);
        receipt.ProviderPayoutId.Should().StartWith("unknown:");
        StripePayoutEvidenceSigning.VerifyReceipt(receipt, EvidenceSecret).Should().BeTrue();
    }

    [Fact]
    public async Task Dispatch_RejectsNullMissingOperationNonPositiveAndUnalignedAmounts()
    {
        await using var context = Context();
        var provider = Provider(context, new RecordingHandler((_, _) =>
            Json(HttpStatusCode.OK, """{"id":"po"}""")));

        await FluentActions.Awaiting(() => provider.DispatchAsync(null!).AsTask())
            .Should().ThrowAsync<ArgumentNullException>();
        await FluentActions.Awaiting(() => provider.DispatchAsync(Command() with
                { OperationId = Guid.Empty }).AsTask())
            .Should().ThrowAsync<PayoutEligibilityException>();
        await FluentActions.Awaiting(() => provider.DispatchAsync(Command() with
                { Amount = new CoinAmount(CurrencyCode.HardCoin, 0) }).AsTask())
            .Should().ThrowAsync<PayoutEligibilityException>();
        await FluentActions.Awaiting(() => provider.DispatchAsync(Command() with
                { Amount = new CoinAmount(CurrencyCode.HardCoin, 1) }).AsTask())
            .Should().ThrowAsync<PayoutEligibilityException>();

        var missingId = Provider(context, new RecordingHandler((_, _) => Json(HttpStatusCode.OK, "{}")));
        await FluentActions.Awaiting(() => missingId.DispatchAsync(Command()).AsTask())
            .Should().ThrowAsync<PayoutEvidenceException>();
    }

    [Fact]
    public async Task Reconcile_UsesDirectIdOrDiscoversUnknownDispatchAndRejectsMissingEvidence()
    {
        await using var context = Context();
        var operation = await SeedOperationAsync(context);
        var direct = Provider(context, new RecordingHandler((_, _) =>
            Json(HttpStatusCode.OK, """{"id":"po_direct","status":"paid"}""")));

        var directEvent = await direct.ReconcileAsync(operation.Id, " po_direct ");
        directEvent.Outcome.Should().Be(PayoutProviderOutcome.Succeeded);
        directEvent.ProviderPayoutId.Should().Be("po_direct");
        StripePayoutEvidenceSigning.VerifyEvent(directEvent, EvidenceSecret).Should().BeTrue();

        var discover = Provider(context, new RecordingHandler((_, call) => call == 1
            ? Json(HttpStatusCode.OK, JsonSerializer.Serialize(new
            {
                data = new object[]
                {
                    new { id = "ignored" },
                    new { id = "po_found", metadata = new { operation_id = operation.Id.ToString("N") } }
                }
            }))
            : Json(HttpStatusCode.OK, """{"id":"po_found","status":"failed"}""")));
        (await discover.ReconcileAsync(operation.Id, "unknown:dispatch")).Outcome
            .Should().Be(PayoutProviderOutcome.Failed);

        var absent = Provider(context, new RecordingHandler((_, _) =>
            Json(HttpStatusCode.OK, """{"data":[{"id":"other","metadata":{"operation_id":"other"}}]}""")));
        await FluentActions.Awaiting(() => absent.ReconcileAsync(operation.Id, "unknown:dispatch").AsTask())
            .Should().ThrowAsync<PayoutEvidenceException>();
        await FluentActions.Awaiting(() => direct.ReconcileAsync(Guid.Empty, "po").AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => direct.ReconcileAsync(operation.Id, " ").AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => direct.ReconcileAsync(Guid.NewGuid(), "po").AsTask())
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Theory]
    [InlineData("payout.paid", "pending", PayoutProviderOutcome.Succeeded)]
    [InlineData("payout.failed", "pending", PayoutProviderOutcome.Failed)]
    [InlineData("payout.canceled", "pending", PayoutProviderOutcome.Failed)]
    [InlineData("payout.updated", "in_transit", PayoutProviderOutcome.Submitted)]
    public async Task Normalize_VerifiesWebhookAndMapsEveryEventType(
        string type,
        string status,
        PayoutProviderOutcome expected)
    {
        await using var context = Context();
        var operation = await SeedOperationAsync(context);
        var payload = Encoding.UTF8.GetBytes(WebhookJson(operation, type, status));
        var provider = Provider(context, new RecordingHandler((_, _) => throw new InvalidOperationException()));

        var result = await provider.NormalizeAsync(payload, SignatureHeader(payload, Now), Now);

        result.Outcome.Should().Be(expected);
        result.EventId.Should().Be("evt_1");
        StripePayoutEvidenceSigning.VerifyEvent(result, EvidenceSecret).Should().BeTrue();
    }

    [Theory]
    [InlineData("signature")]
    [InlineData("operation-format")]
    [InlineData("operation-missing")]
    [InlineData("account")]
    [InlineData("destination")]
    [InlineData("event-id")]
    [InlineData("type")]
    [InlineData("payout-id")]
    public async Task Normalize_FailsClosedForInvalidSignatureIdentityAndBindings(string invalid)
    {
        await using var context = Context();
        var operation = await SeedOperationAsync(context);
        var json = WebhookJson(operation, "payout.paid", "paid");
        json = invalid switch
        {
            "operation-format" => json.Replace(operation.Id.ToString("N"), "not-a-guid"),
            "account" => json.Replace(operation.ProviderAccountId, "acct_other"),
            "destination" => json.Replace(operation.DestinationHash, "other-destination"),
            "event-id" => json.Replace("\"id\":\"evt_1\"", "\"id\":\"\""),
            "type" => json.Replace("\"type\":\"payout.paid\"", "\"type\":\"\""),
            "payout-id" => json.Replace("\"id\":\"po_1\"", "\"id\":\"\""),
            _ => json
        };
        if (invalid == "operation-missing")
        {
            json = json.Replace(operation.Id.ToString("N"), Guid.NewGuid().ToString("N"));
        }
        var payload = Encoding.UTF8.GetBytes(json);
        var header = invalid == "signature" ? "t=1,v1=invalid" : SignatureHeader(payload, Now);
        var provider = Provider(context, new RecordingHandler((_, _) => throw new InvalidOperationException()));

        await FluentActions.Awaiting(() => provider.NormalizeAsync(payload, header, Now).AsTask())
            .Should().ThrowAsync<Exception>();
    }

    [Theory]
    [InlineData("enabled")]
    [InlineData("base")]
    [InlineData("secret")]
    [InlineData("webhook")]
    [InlineData("evidence")]
    [InlineData("environment")]
    [InlineData("return")]
    [InlineData("refresh")]
    [InlineData("tolerance")]
    [InlineData("lifetime")]
    public async Task Configuration_IsFailClosedForEveryRequiredSetting(string invalid)
    {
        await using var context = Context();
        var options = ValidOptions();
        switch (invalid)
        {
            case "enabled": options.Enabled = false; break;
            case "base": options.ApiBaseUrl = "relative"; break;
            case "secret": options.SecretKey = " "; break;
            case "webhook": options.WebhookSecret = " "; break;
            case "evidence": options.EvidenceSigningSecret = "short"; break;
            case "environment": options.Environment = " "; break;
            case "return": options.ReturnUrl = "relative"; break;
            case "refresh": options.RefreshUrl = "relative"; break;
            case "tolerance": options.WebhookTolerance = TimeSpan.Zero; break;
            case "lifetime": options.AccountEvidenceLifetime = TimeSpan.Zero; break;
        }
        var provider = Provider(context, new RecordingHandler((_, _) => throw new InvalidOperationException()), options);

        await FluentActions.Awaiting(() => provider.GetAccountAsync(Guid.NewGuid()).AsTask())
            .Should().ThrowAsync<PayoutExecutionDisabledException>();
    }

    [Fact]
    public void ConstructorOptionsEvidenceAndStripeSignature_CoverAllSecurityBoundaries()
    {
        using var context = Context();
        var options = Options.Create(ValidOptions());
        var http = new HttpClient(new RecordingHandler((_, _) => Json(HttpStatusCode.OK, "{}")));
        FluentActions.Invoking(() => new StripeConnectPayoutProvider(null!, context, options, TimeProvider.System))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new StripeConnectPayoutProvider(http, null!, options, TimeProvider.System))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new StripeConnectPayoutProvider(http, context, null!, TimeProvider.System))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new StripeConnectPayoutProvider(http, context, options, null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new StripeConnectPayoutProvider(
                http, new NonRelationalContext(), options, TimeProvider.System))
            .Should().Throw<InvalidOperationException>();

        var command = Command();
        var receipt = new PayoutDispatchReceipt(
            command.OperationId, PayoutProviderOutcome.Submitted, "po", command.ProviderAccountId,
            command.DestinationHash, "evidence", string.Empty, Now);
        var signedReceipt = receipt with
        {
            Signature = StripePayoutEvidenceSigning.SignReceipt(receipt, EvidenceSecret)
        };
        var providerEvent = new PayoutProviderEvent(
            "event", command.OperationId, PayoutProviderOutcome.Succeeded, "po",
            command.ProviderAccountId, command.DestinationHash, "evidence", string.Empty, Now);
        var signedEvent = providerEvent with
        {
            Signature = StripePayoutEvidenceSigning.SignEvent(providerEvent, EvidenceSecret)
        };
        StripePayoutEvidenceSigning.SignReceipt(receipt, "short").Should().BeEmpty();
        StripePayoutEvidenceSigning.VerifyReceipt(signedReceipt, EvidenceSecret).Should().BeTrue();
        StripePayoutEvidenceSigning.VerifyReceipt(signedReceipt with { EvidenceHash = "changed" }, EvidenceSecret)
            .Should().BeFalse();
        StripePayoutEvidenceSigning.VerifyReceipt(signedReceipt with { Signature = "x" }, EvidenceSecret)
            .Should().BeFalse();
        StripePayoutEvidenceSigning.VerifyEvent(signedEvent, EvidenceSecret).Should().BeTrue();
        StripePayoutEvidenceSigning.VerifyEvent(signedEvent with { DestinationHash = "changed" }, EvidenceSecret)
            .Should().BeFalse();

        FluentActions.Invoking(() => new StripePayoutProviderEvidenceVerifier(null!))
            .Should().Throw<ArgumentNullException>();
        var verifier = new StripePayoutProviderEvidenceVerifier(options);
        verifier.Verify(signedReceipt).Should().BeTrue();
        verifier.Verify(signedEvent).Should().BeTrue();

        var payload = Encoding.UTF8.GetBytes("payload");
        var header = SignatureHeader(payload, Now, wrongFirst: true);
        StripeSignature.Verify(payload, header, WebhookSecret, Now, TimeSpan.FromMinutes(5)).Should().BeTrue();
        StripeSignature.Verify(payload, header.ToUpperInvariant(), WebhookSecret, Now, TimeSpan.FromMinutes(5))
            .Should().BeFalse("Stripe header keys are case-sensitive");
        StripeSignature.Verify(payload, " ", WebhookSecret, Now, TimeSpan.FromMinutes(5)).Should().BeFalse();
        StripeSignature.Verify(payload, header, " ", Now, TimeSpan.FromMinutes(5)).Should().BeFalse();
        StripeSignature.Verify(payload, header, WebhookSecret, Now, TimeSpan.Zero).Should().BeFalse();
        StripeSignature.Verify(payload, "junk,t=nope,v0=x", WebhookSecret, Now, TimeSpan.FromMinutes(5))
            .Should().BeFalse();
        StripeSignature.Verify(payload, $"t={Now.ToUnixTimeSeconds()},v1=x", WebhookSecret, Now,
            TimeSpan.FromMinutes(5)).Should().BeFalse();
        StripeSignature.Verify(payload, SignatureHeader(payload, Now.AddSeconds(1)), WebhookSecret, Now,
            TimeSpan.FromMinutes(5)).Should().BeFalse();
        StripeSignature.Verify(payload, SignatureHeader(payload, Now.AddMinutes(-6)), WebhookSecret, Now,
            TimeSpan.FromMinutes(5)).Should().BeFalse();

        var defaults = new StripeConnectPayoutOptions();
        defaults.Enabled.Should().BeFalse();
        defaults.ApiBaseUrl.Should().BeEmpty();
        defaults.SecretKey.Should().BeEmpty();
        defaults.WebhookSecret.Should().BeEmpty();
        defaults.EvidenceSigningSecret.Should().BeEmpty();
        defaults.Environment.Should().BeEmpty();
        defaults.ReturnUrl.Should().BeEmpty();
        defaults.RefreshUrl.Should().BeEmpty();
        defaults.WebhookTolerance.Should().Be(TimeSpan.Zero);
        defaults.AccountEvidenceLifetime.Should().Be(TimeSpan.Zero);
        StripeConnectPayoutOptions.SectionName.Should().NotBeEmpty();
    }

    private static ApplicationDbContext Context() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("payout-stripe-" + Guid.NewGuid().ToString("N")).Options);

    private static StripeConnectPayoutProvider Provider(
        ApplicationDbContext context,
        HttpMessageHandler handler,
        StripeConnectPayoutOptions? options = null) => new(
        new HttpClient(handler), context, Options.Create(options ?? ValidOptions()), new FixedTimeProvider(Now));

    private static StripeConnectPayoutOptions ValidOptions() => new()
    {
        Enabled = true,
        ApiBaseUrl = "https://api.stripe.test",
        SecretKey = "sk_test",
        WebhookSecret = WebhookSecret,
        EvidenceSigningSecret = EvidenceSecret,
        Environment = "sandbox",
        ReturnUrl = "https://gameguild.test/payout/return",
        RefreshUrl = "https://gameguild.test/payout/refresh",
        WebhookTolerance = TimeSpan.FromMinutes(5),
        AccountEvidenceLifetime = TimeSpan.FromHours(1)
    };

    private static PayoutDispatchCommand Command() => new(
        Guid.NewGuid(), 3, 7, 9, "acct_1", "destination-hash",
        new CoinAmount(CurrencyCode.HardCoin, 2_000), "snapshot-hash",
        "payout-dispatch-key", Now.AddMinutes(-1));

    private static async Task SeedAccountAsync(
        ApplicationDbContext context,
        Guid payeeId,
        string accountId,
        long version = 1)
    {
        context.Set<PayoutConnectAccountRow>().Add(new PayoutConnectAccountRow
        {
            PayeeId = payeeId,
            Provider = "stripe",
            Environment = "sandbox",
            ProviderAccountId = accountId,
            DestinationHash = "old-destination",
            State = ConnectAccountState.Pending,
            Version = version,
            ObservedAt = Now.AddHours(-1),
            ExpiresAt = Now,
            EvidenceHash = "old-evidence"
        });
        await context.SaveChangesAsync();
    }

    private static async Task<PayoutOperationRow> SeedOperationAsync(ApplicationDbContext context)
    {
        var row = new PayoutOperationRow
        {
            Id = Guid.NewGuid(),
            IdempotencyKey = "operation",
            RequestHash = "request",
            ActorId = Guid.NewGuid(),
            PayeeId = Guid.NewGuid(),
            WalletId = Guid.NewGuid(),
            AmountUnits = 2_000,
            ProviderAccountId = "acct_1",
            DestinationHash = "destination-hash",
            ProviderBindingHash = "provider-binding",
            EligibilityHash = "eligibility",
            State = PayoutOperationState.Dispatching,
            Version = 3,
            FencingToken = 7,
            KillSwitchEpoch = 9,
            ReserveVersion = 1,
            ReserveAuthorizationEpoch = 1,
            PolicyVersion = 1,
            RiskDecisionId = Guid.NewGuid(),
            CreatedAt = Now.AddMinutes(-2),
            UpdatedAt = Now.AddMinutes(-1)
        };
        context.Set<PayoutOperationRow>().Add(row);
        await context.SaveChangesAsync();
        return row;
    }

    private static string AccountJson(
        bool payouts,
        bool charges,
        string? disabledReason,
        string externalShape)
    {
        object? external = externalShape switch
        {
            "missing-external" => null,
            "missing-data" => new { other = Array.Empty<object>() },
            "empty" => new { data = Array.Empty<object>() },
            "missing-fingerprint" => new { data = new[] { new { currency = "usd", country = "US", last4 = "4242" } } },
            "fingerprint-only" => new { data = new[] { new { fingerprint = "fingerprint" } } },
            _ => new { data = new[] { new { fingerprint = "fingerprint", currency = "usd", country = "US", last4 = "4242" } } }
        };
        var values = new Dictionary<string, object?>
        {
            ["charges_enabled"] = charges,
            ["payouts_enabled"] = payouts,
            ["requirements"] = new { disabled_reason = disabledReason }
        };
        if (external is not null) values["external_accounts"] = external;
        return JsonSerializer.Serialize(values);
    }

    private static string WebhookJson(PayoutOperationRow operation, string type, string status) =>
        JsonSerializer.Serialize(new
        {
            id = "evt_1",
            type,
            account = operation.ProviderAccountId,
            data = new
            {
                @object = new
                {
                    id = "po_1",
                    status,
                    metadata = new
                    {
                        operation_id = operation.Id.ToString("N"),
                        destination_hash = operation.DestinationHash
                    }
                }
            }
        });

    private static string SignatureHeader(byte[] payload, DateTimeOffset timestamp, bool wrongFirst = false)
    {
        var value = ComputeSignature(payload, timestamp);
        return wrongFirst
            ? $"junk,t={timestamp.ToUnixTimeSeconds()},v0=ignored,v1={new string('0', 64)},v1={value.ToUpperInvariant()}"
            : $"t={timestamp.ToUnixTimeSeconds()},v1={value}";
    }

    private static string ComputeSignature(byte[] payload, DateTimeOffset timestamp)
    {
        var signed = Encoding.UTF8.GetBytes(timestamp.ToUnixTimeSeconds() + ".")
            .Concat(payload).ToArray();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(WebhookSecret));
        return Convert.ToHexStringLower(hmac.ComputeHash(signed));
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string content) => new(status)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json")
    };

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, int, HttpResponseMessage> response) : HttpMessageHandler
    {
        private int _calls;
        public List<RequestSnapshot> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _calls++;
            Requests.Add(new RequestSnapshot(
                request.Method,
                request.RequestUri!.PathAndQuery,
                request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("Idempotency-Key", out var keys) ? keys.Single() : null,
                request.Headers.TryGetValues("Stripe-Account", out var accounts) ? accounts.Single() : null,
                request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult()));
            return Task.FromResult(response(request, _calls));
        }
    }

    private sealed record RequestSnapshot(
        HttpMethod Method,
        string Path,
        string? Authorization,
        string? IdempotencyKey,
        string? ConnectedAccount,
        string? Body);

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class NonRelationalContext : IApplicationDbContext
    {
        public DbSet<T> Set<T>() where T : class => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
