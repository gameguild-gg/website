using FluentAssertions;
using GameGuild.Commerce.Payments;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Integrations;
using Microsoft.Extensions.Options;

namespace GameGuild.Finance.Economy.UnitTests.Integrations.Payments;

public sealed class StripeEconomyTopUpProviderTests
{
    private static readonly Guid TenantId = Guid.Parse("99000000-0000-0000-0000-000000000001");
    private static readonly Guid TopUpId = Guid.Parse("99000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task CreateAsync_CreatesAnUnconfirmedServerPricedPaymentIntent()
    {
        var stripe = new StubStripePaymentService();
        var provider = Provider(stripe);

        var result = await provider.CreateAsync(
            new EconomyTopUpProviderCreateRequest(TopUpId, TenantId, 250, "USD", "top-up-key"),
            default);

        stripe.Created.Should().BeEquivalentTo(new GatewayPaymentIntentSetupRequest(
            "economy-top-up:top-up-key",
            2.50m,
            "USD",
            "Economy HardCoin top-up",
            new Dictionary<string, string>
            {
                ["economy_top_up_id"] = TopUpId.ToString("N"),
                ["tenant_id"] = TenantId.ToString("N"),
                ["purpose"] = "economy_hard_coin_top_up"
            }));
        result.Should().Be(new EconomyTopUpProviderResult(
            "stripe", "test", "acct_platform", "pi_topup", "payment_intent", "capture",
            EconomyTopUpProviderStatus.RequiresAction, "pi_topup_secret", "pk_test"));
    }

    [Fact]
    public async Task RetrieveAsync_UsesTheExistingProviderObject()
    {
        var stripe = new StubStripePaymentService();

        var result = await Provider(stripe).RetrieveAsync("pi_existing", default);

        stripe.Retrieved.Should().Be("pi_existing");
        result.ProviderObjectId.Should().Be("pi_existing");
        result.Status.Should().Be(EconomyTopUpProviderStatus.RequiresAction);
    }

    [Theory]
    [InlineData(false, false, "sk_test", "pk_test", "acct_platform")]
    [InlineData(true, true, "sk_test", "pk_test", "acct_platform")]
    [InlineData(true, false, "", "pk_test", "acct_platform")]
    [InlineData(true, false, "sk_test", "", "acct_platform")]
    [InlineData(true, false, "sk_test", "pk_test", "")]
    public async Task CreateAsync_FailsClosedWithoutARealCompleteProviderConfiguration(
        bool enabled,
        bool simulation,
        string apiKey,
        string publishableKey,
        string accountId)
    {
        var options = Options.Create(new StripeGatewayOptions
        {
            IsEnabled = enabled,
            UseSimulation = simulation,
            ApiKey = apiKey,
            PublishableKey = publishableKey,
            AccountId = accountId
        });
        var provider = new StripeEconomyTopUpProvider(options, new StubStripePaymentService());

        await FluentActions.Awaiting(() => provider.CreateAsync(
                new EconomyTopUpProviderCreateRequest(TopUpId, TenantId, 250, "USD", "key"), default).AsTask())
            .Should().ThrowAsync<EconomyTopUpProviderUnavailableException>();
    }

    [Fact]
    public void ConstructorAndCreateRequestValidationRejectEveryInvalidInput()
    {
        FluentActions.Invoking(() => new StripeEconomyTopUpProvider(
                null!, new StubStripePaymentService())).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new StripeEconomyTopUpProvider(
                Options.Create(new StripeGatewayOptions()), null!)).Should().Throw<ArgumentNullException>();
        var valid = new EconomyTopUpProviderCreateRequest(TopUpId, TenantId, 250, "USD", "key");
        StripeEconomyTopUpProvider.Validate(valid);
        FluentActions.Invoking(() => StripeEconomyTopUpProvider.Validate(null!))
            .Should().Throw<ArgumentNullException>();
        EconomyTopUpProviderCreateRequest[] invalid =
        [
            valid with { TopUpId = Guid.Empty },
            valid with { TenantId = Guid.Empty },
            valid with { UsdMinorUnits = 0 },
            valid with { Currency = "EUR" },
            valid with { IdempotencyKey = "" }
        ];
        foreach (var request in invalid)
            FluentActions.Invoking(() => StripeEconomyTopUpProvider.Validate(request))
                .Should().Throw<ArgumentException>();
        _ = new EconomyTopUpProviderUnavailableException("unavailable", new Exception());
        _ = new EconomyTopUpProviderAmbiguousException("ambiguous", new Exception());
    }

    [Fact]
    public async Task CreateAndRetrieveNormalizeAmbiguousProviderOutcomes()
    {
        var createStripe = new StubStripePaymentService
        {
            CreateResult = ValidCreateResult() with { OutcomeUnknown = true }
        };
        await FluentActions.Awaiting(() => Provider(createStripe).CreateAsync(
                new EconomyTopUpProviderCreateRequest(TopUpId, TenantId, 250, "USD", "key"), default).AsTask())
            .Should().ThrowAsync<EconomyTopUpProviderAmbiguousException>();

        var retrieveStripe = new StubStripePaymentService
        {
            RetrieveResult = ValidRetrieveResult("pi_ambiguous") with { ErrorCode = "stripe_outcome_unknown" }
        };
        await FluentActions.Awaiting(() => Provider(retrieveStripe)
                .RetrieveAsync("pi_ambiguous", default).AsTask())
            .Should().ThrowAsync<EconomyTopUpProviderAmbiguousException>();
        await FluentActions.Awaiting(() => Provider(new StubStripePaymentService())
                .RetrieveAsync("", default).AsTask())
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ProviderRejectsEveryIncompleteMappingAndUnsupportedInitialState()
    {
        var valid = ValidCreateResult();
        GatewayPaymentIntentSetupResult[] invalid =
        [
            valid with { TransactionId = null },
            valid with { ClientSecret = null },
            valid with { ProviderMapping = null },
            valid with { ProviderMapping = valid.ProviderMapping! with { ProviderObjectId = "pi_other" } },
            valid with { ProviderMapping = valid.ProviderMapping! with { ProviderObjectType = "charge" } },
            valid with { ProviderMapping = valid.ProviderMapping! with { ProviderMonetaryLeg = "refund" } },
            valid with { ProviderMapping = valid.ProviderMapping! with { ProviderEnvironment = "" } },
            valid with { ProviderMapping = valid.ProviderMapping! with { ProviderAccountId = "" } },
            valid with { Status = PaymentStatus.Succeeded }
        ];
        foreach (var result in invalid)
        {
            var stripe = new StubStripePaymentService { CreateResult = result };
            await FluentActions.Awaiting(() => Provider(stripe).CreateAsync(
                    new EconomyTopUpProviderCreateRequest(TopUpId, TenantId, 250, "USD", "key"), default).AsTask())
                .Should().ThrowAsync<EconomyTopUpProviderUnavailableException>();
        }

        foreach (var status in new[] { PaymentStatus.Pending, PaymentStatus.Processing })
        {
            var stripe = new StubStripePaymentService { CreateResult = valid with { Status = status } };
            var mapped = await Provider(stripe).CreateAsync(
                new EconomyTopUpProviderCreateRequest(TopUpId, TenantId, 250, "USD", "key"), default);
            mapped.Status.Should().Be(status == PaymentStatus.Processing
                ? EconomyTopUpProviderStatus.Processing
                : EconomyTopUpProviderStatus.RequiresAction);
        }
    }

    private static StripeEconomyTopUpProvider Provider(IStripePaymentService stripe) => new(
        Options.Create(new StripeGatewayOptions
        {
            IsEnabled = true,
            UseSimulation = false,
            ApiKey = "sk_test",
            PublishableKey = "pk_test",
            AccountId = "acct_platform"
        }),
        stripe);

    private static GatewayPaymentIntentSetupResult ValidCreateResult() => new(
        "pi_topup",
        PaymentStatus.RequiresAction,
        "pi_topup_secret",
        new GatewayProviderMapping("test", "acct_platform", "pi_topup", "payment_intent", "capture"));

    private static GatewayPaymentResult ValidRetrieveResult(string id) => new(
        false,
        id,
        null,
        null,
        null,
        PaymentStatus.RequiresAction,
        DateTime.UtcNow,
        $"{id}_secret",
        new GatewayProviderMapping("test", "acct_platform", id, "payment_intent", "capture"));

    private sealed class StubStripePaymentService : IStripePaymentService
    {
        public GatewayPaymentIntentSetupRequest? Created { get; private set; }
        public string? Retrieved { get; private set; }
        public GatewayPaymentIntentSetupResult CreateResult { get; init; } = ValidCreateResult();
        public GatewayPaymentResult? RetrieveResult { get; init; }

        public Task<GatewayPaymentIntentSetupResult> CreatePaymentIntentAsync(
            GatewayPaymentIntentSetupRequest request,
            CancellationToken cancellationToken = default)
        {
            Created = request;
            return Task.FromResult(CreateResult);
        }

        public Task<GatewayPaymentResult> GetPaymentAsync(
            string externalTransactionId,
            CancellationToken cancellationToken = default)
        {
            Retrieved = externalTransactionId;
            return Task.FromResult(RetrieveResult ?? ValidRetrieveResult(externalTransactionId));
        }

        public Task<GatewayPaymentResult> ProcessPaymentAsync(
            GatewayPaymentRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<GatewayPaymentCancellationResult> CancelPaymentAsync(
            string externalTransactionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<GatewayRefundResult> ProcessRefundAsync(
            GatewayRefundRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ValidateWebhookSignatureAsync(string payload, string signature, string secret) =>
            throw new NotSupportedException();
    }
}
