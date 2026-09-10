using FluentAssertions;
using GameGuild.Commerce.Billing;
using GameGuild.Commerce.Payments;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Integrations;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Risk;
using Moq;

namespace GameGuild.Finance.Economy.UnitTests.Integrations.Payments;

public sealed class StripeEconomyTopUpEventConsumerTests
{
    private static readonly Guid TenantId = Guid.Parse("99100000-0000-0000-0000-000000000001");
    private static readonly Guid ActorId = Guid.Parse("99100000-0000-0000-0000-000000000002");
    private static readonly Guid WalletValue = Guid.Parse("99100000-0000-0000-0000-000000000003");
    private static readonly Guid SourceValue = Guid.Parse("99100000-0000-0000-0000-000000000004");
    private static readonly Guid PostingValue = Guid.Parse("99100000-0000-0000-0000-000000000005");
    private static readonly DateTimeOffset OccurredAt = new(2026, 8, 27, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TryConsumeAsync_IgnoresEventsOutsideTheSupportedPaymentIntentLifecycle()
    {
        var fixture = Fixture();

        var handled = await fixture.Consumer.TryConsumeAsync(Event("charge.succeeded"), default);

        handled.Should().BeFalse();
        fixture.TopUps.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TryConsumeAsync_IgnoresAValidPaymentIntentThatDoesNotBelongToEconomy()
    {
        var fixture = Fixture();
        fixture.TopUps.Setup(candidate => candidate.FindAsync(
                It.IsAny<EconomyTopUpProviderIdentity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EconomyTopUpSettlementContext?)null);

        var handled = await fixture.Consumer.TryConsumeAsync(Event("payment_intent.processing"), default);

        handled.Should().BeFalse();
    }

    [Theory]
    [InlineData("payment_intent.processing", EconomyTopUpProviderStatus.Processing)]
    [InlineData("payment_intent.requires_action", EconomyTopUpProviderStatus.RequiresAction)]
    [InlineData("payment_intent.payment_failed", EconomyTopUpProviderStatus.Failed)]
    [InlineData("payment_intent.canceled", EconomyTopUpProviderStatus.Cancelled)]
    public async Task TryConsumeAsync_PersistsEveryNonMintingProviderState(
        string eventType,
        EconomyTopUpProviderStatus expected)
    {
        var fixture = Fixture();
        fixture.TopUps.Setup(candidate => candidate.FindAsync(
                It.IsAny<EconomyTopUpProviderIdentity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Context());
        EconomyTopUpProviderEvent? applied = null;
        fixture.TopUps.Setup(candidate => candidate.ApplyAsync(
                It.IsAny<EconomyTopUpProviderEvent>(), It.IsAny<CancellationToken>()))
            .Callback((EconomyTopUpProviderEvent providerEvent, CancellationToken _) => applied = providerEvent)
            .ReturnsAsync(new EconomyTopUpProviderEventResult(true, false, expected));

        var handled = await fixture.Consumer.TryConsumeAsync(Event(eventType), default);

        handled.Should().BeTrue();
        applied!.Status.Should().Be(expected);
        applied.ProviderUsdMinorUnits.Should().Be(250);
        applied.Currency.Should().Be("USD");
        applied.FailureCode.Should().Be(expected switch
        {
            EconomyTopUpProviderStatus.Failed => "payment_failed",
            EconomyTopUpProviderStatus.Cancelled => "cancelled",
            _ => null
        });
        fixture.Protected.Calls.Should().Be(0);
    }

    [Fact]
    public async Task TryConsumeAsync_ReauthorizesAndPostsAProviderSucceededTopUp()
    {
        var fixture = SuccessfulFixture();
        EconomyTopUpProviderEvent? applied = null;
        fixture.TopUps.Setup(candidate => candidate.ApplyAsync(
                It.IsAny<EconomyTopUpProviderEvent>(), It.IsAny<CancellationToken>()))
            .Callback((EconomyTopUpProviderEvent providerEvent, CancellationToken _) => applied = providerEvent)
            .ReturnsAsync(new EconomyTopUpProviderEventResult(true, false, EconomyTopUpProviderStatus.Posted));

        var handled = await fixture.Consumer.TryConsumeAsync(Event("payment_intent.succeeded"), default);

        handled.Should().BeTrue();
        fixture.Protected.Calls.Should().Be(1);
        fixture.Protected.Intent!.Capability.Should().Be(EconomyValueMovementCapability.ConfirmHardCoinFunding);
        fixture.Protected.Intent.TemplateKind.Should().Be(PostingTemplateKind.ConfirmedTopUpMint);
        fixture.Protected.Intent.ProtectedSubjectId.Should().Be(ActorId);
        applied!.Status.Should().Be(EconomyTopUpProviderStatus.Posted);
        applied.PostingGroupId.Should().Be(PostingValue);
        fixture.Funding.Verify(candidate => candidate.ConfirmDurable(
            It.IsAny<PersistedDurableHardCoinFundingConfirmation>()), Times.Once);
    }

    [Fact]
    public async Task TryConsumeAsync_HoldsProviderFundsAndRethrowsWhenAuthorizationFails()
    {
        var fixture = SuccessfulFixture();
        fixture.Protected.Exception = new EconomyCapabilityAuthorizationException(
            EconomyCapabilityReadinessStatus.ComplianceStale,
            ["Compliance evidence is stale."]);
        EconomyTopUpProviderEvent? applied = null;
        fixture.TopUps.Setup(candidate => candidate.ApplyAsync(
                It.IsAny<EconomyTopUpProviderEvent>(), It.IsAny<CancellationToken>()))
            .Callback((EconomyTopUpProviderEvent providerEvent, CancellationToken _) => applied = providerEvent)
            .ReturnsAsync(new EconomyTopUpProviderEventResult(true, false, EconomyTopUpProviderStatus.Held));

        var action = () => fixture.Consumer.TryConsumeAsync(
            Event("payment_intent.succeeded"), default).AsTask();

        await action.Should().ThrowAsync<EconomyCapabilityAuthorizationException>();
        applied!.Status.Should().Be(EconomyTopUpProviderStatus.Held);
        applied.PostingGroupId.Should().BeNull();
    }

    [Theory]
    [InlineData(EconomyTopUpProviderStatus.Posted)]
    [InlineData(EconomyTopUpProviderStatus.Cancelled)]
    public async Task TryConsumeAsync_DoesNotReopenTerminalTopUps(EconomyTopUpProviderStatus status)
    {
        var fixture = Fixture();
        fixture.TopUps.Setup(candidate => candidate.FindAsync(
                It.IsAny<EconomyTopUpProviderIdentity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Context(status));

        var handled = await fixture.Consumer.TryConsumeAsync(Event("payment_intent.succeeded"), default);

        handled.Should().BeTrue();
        fixture.Protected.Calls.Should().Be(0);
    }

    [Fact]
    public async Task TryConsumeAsync_RejectsIncompleteEvidenceAndMismatchedAuthority()
    {
        var fixture = Fixture();
        var invalid = Event("payment_intent.processing") with { OccurredAt = default };
        await FluentActions.Awaiting(() => fixture.Consumer.TryConsumeAsync(invalid, default).AsTask())
            .Should().ThrowAsync<InvalidWebhookPayloadException>();

        fixture.TopUps.Setup(candidate => candidate.FindAsync(
                It.IsAny<EconomyTopUpProviderIdentity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Context());
        var mismatched = Event("payment_intent.processing") with { Amount = 3m };
        await FluentActions.Awaiting(() => fixture.Consumer.TryConsumeAsync(mismatched, default).AsTask())
            .Should().ThrowAsync<InvalidWebhookPayloadException>();
    }

    [Fact]
    public void AuthoritativePaymentValidationCoversOptionalAndMismatchedProviderFacts()
    {
        var context = Context();
        var valid = Event("payment_intent.processing");

        StripeEconomyTopUpEventConsumer.EnsureAuthoritativePayment(valid, context);
        StripeEconomyTopUpEventConsumer.EnsureAuthoritativePayment(valid with { TenantId = null }, context);
        VerifiedStripeWebhookEvent[] invalid =
        [
            valid with { Amount = null },
            valid with { Amount = 3m },
            valid with { Currency = "EUR" },
            valid with { TenantId = Guid.NewGuid() }
        ];
        foreach (var providerEvent in invalid)
            FluentActions.Invoking(() =>
                    StripeEconomyTopUpEventConsumer.EnsureAuthoritativePayment(providerEvent, context))
                .Should().Throw<InvalidWebhookPayloadException>();
    }

    private static ConsumerFixture SuccessfulFixture()
    {
        var fixture = Fixture();
        var context = Context();
        var observation = new ObserveHardCoinTopUpCommand(
            new SourceStampId(SourceValue),
            new WalletId(WalletValue),
            new ProviderMonetaryLeg(
                "stripe", "test", "acct_platform", "payment_intent:pi_topup", "capture"),
            "evidence",
            250,
            OccurredAt);
        var claim = HardCoinFundingClaim.Observe(
            observation.SourceId,
            observation.WalletId,
            observation.ProviderLeg,
            observation.Evidence,
            observation.AuthoritativeUsdMinorUnits,
            observation.ObservedAt);
        var authority = new RegisteredPostingAuthority(
            Guid.NewGuid(), ActorId, TenantId, fixture.Protected.Authorization.RiskDecisionId,
            fixture.Protected.Authorization.OperationFingerprint, 1);
        var confirmation = new PersistedDurableHardCoinFundingConfirmation(
            new PostingId(PostingValue),
            new IdempotencyKey("confirm-key"),
            observation.SourceId,
            new CreditLotId(Guid.NewGuid()),
            "evidence",
            OccurredAt.AddMinutes(1),
            fixture.Protected.Authorization.Receipt,
            authority);
        fixture.TopUps.Setup(candidate => candidate.FindAsync(
                It.IsAny<EconomyTopUpProviderIdentity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(context);
        fixture.Adapter.Setup(candidate => candidate.CreateObservation(
                context.Payment, context.TopUp.WalletId, It.IsAny<string>(), OccurredAt))
            .Returns(observation);
        fixture.Adapter.Setup(candidate => candidate.ConfirmationIdempotencyKey(context.Payment))
            .Returns(new IdempotencyKey("confirm-key"));
        fixture.Funding.Setup(candidate => candidate.Observe(
                It.IsAny<PersistedHardCoinFundingObservation>()))
            .Returns(claim);
        fixture.Capabilities.Setup(candidate => candidate.ResolveAuthorityAsync(
                StripeEconomyTopUpEventConsumer.RegisteredCapabilityName,
                PostingTemplateKind.ConfirmedTopUpMint,
                fixture.Protected.Authorization.Receipt,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(authority);
        fixture.Adapter.Setup(candidate => candidate.CreateDurableConfirmation(
                context.Payment,
                claim,
                fixture.Protected.Authorization,
                authority,
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>()))
            .Returns(confirmation);
        fixture.Funding.Setup(candidate => candidate.ConfirmDurable(confirmation))
            .Returns(new RegisteredPostingReceipt(new PostingId(PostingValue), 1, new string('a', 64), false));
        return fixture;
    }

    private static ConsumerFixture Fixture()
    {
        var topUps = new Mock<IEconomyTopUpSettlementStore>(MockBehavior.Strict);
        var adapter = new Mock<IStripeEconomyFundingAdapter>(MockBehavior.Strict);
        var funding = new Mock<IHardCoinFundingGateway>(MockBehavior.Strict);
        var capabilities = new Mock<IRegisteredPostingCapabilityResolver>(MockBehavior.Strict);
        var protectedOperations = new StubTrustedAuthorizer();
        var time = new FixedTimeProvider(OccurredAt.AddMinutes(1));
        return new ConsumerFixture(
            new StripeEconomyTopUpEventConsumer(
                topUps.Object,
                adapter.Object,
                funding.Object,
                protectedOperations,
                capabilities.Object,
                time),
            topUps,
            adapter,
            funding,
            capabilities,
            protectedOperations);
    }

    private static EconomyTopUpSettlementContext Context(
        EconomyTopUpProviderStatus status = EconomyTopUpProviderStatus.Processing)
    {
        var paymentEntity = Payment.Create(TenantId, 2.50m, "USD", "top-up", provider: "stripe");
        paymentEntity.BindProviderMapping(
            "stripe", "test", "acct_platform", "pi_topup", "payment_intent", "capture");
        paymentEntity.MarkAsProcessing("pi_topup");
        var payment = new EconomyTopUpPaymentFact(
            paymentEntity.Id,
            TenantId,
            2.50m,
            "USD",
            "stripe",
            "test",
            "acct_platform",
            "pi_topup",
            "payment_intent",
            "capture");
        var topUp = new PreparedEconomyTopUpIntent(
            Guid.NewGuid(), payment.Id, TenantId, ActorId, new WalletId(WalletValue),
            250, 250, "BRA", 1, new string('b', 64), "stripe",
            new IdempotencyKey("top-up-key"), new string('c', 64),
            "test", "acct_platform", "pi_topup", status, OccurredAt.AddMinutes(-1), false);
        return new EconomyTopUpSettlementContext(topUp, payment);
    }

    private static VerifiedStripeWebhookEvent Event(string eventType) => new()
    {
        EventId = "evt_topup",
        EventType = eventType,
        ProviderEnvironment = "test",
        ProviderAccountId = "acct_platform",
        WebhookEndpointId = "we_topup",
        EventSchemaVersion = "2026-08-27",
        ProviderObjectId = "pi_topup",
        ProviderObjectType = "payment_intent",
        ProviderMonetaryLeg = eventType.EndsWith("succeeded", StringComparison.Ordinal) ? "capture" : "nonmonetary",
        VerifiedPayload = "{}",
        RetainedPayload = "{}",
        PayloadSha256 = new string('a', 64),
        OccurredAt = OccurredAt,
        TenantId = TenantId,
        Amount = 2.50m,
        Currency = "USD"
    };

    private sealed record ConsumerFixture(
        StripeEconomyTopUpEventConsumer Consumer,
        Mock<IEconomyTopUpSettlementStore> TopUps,
        Mock<IStripeEconomyFundingAdapter> Adapter,
        Mock<IHardCoinFundingGateway> Funding,
        Mock<IRegisteredPostingCapabilityResolver> Capabilities,
        StubTrustedAuthorizer Protected);

    private sealed class StubTrustedAuthorizer : IEconomyTrustedProtectedOperationAuthorizer
    {
        public int Calls { get; private set; }
        public Exception? Exception { get; set; }
        public EconomyProtectedOperationIntent? Intent { get; private set; }
        public EconomyProtectedOperationAuthorization Authorization { get; } = CreateAuthorization();

        public async Task<TResult> ExecuteAsync<TResult>(
            Guid tenantId,
            Guid actorId,
            EconomyProtectedOperationIntent intent,
            Func<EconomyProtectedOperationAuthorization, CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken)
        {
            Calls++;
            Intent = intent;
            if (Exception is not null)
                throw Exception;
            tenantId.Should().Be(TenantId);
            actorId.Should().Be(ActorId);
            return await operation(Authorization, cancellationToken);
        }

        private static EconomyProtectedOperationAuthorization CreateAuthorization()
        {
            var riskDecisionId = Guid.NewGuid();
            var fingerprint = new string('d', 64);
            var receipt = new CapabilityAuthorizationReceipt(
                Guid.NewGuid(), TenantId, ActorId, EconomySubjectReference.ForUser(TenantId, ActorId),
                "BRA", EconomyValueMovementCapability.ConfirmHardCoinFunding, fingerprint,
                1, 1, riskDecisionId, 1, new string('e', 64), new string('f', 64),
                [new string('1', 64)], [new string('2', 64)],
                OccurredAt, OccurredAt.AddMinutes(5), new string('3', 64), "test-key", "signature");
            return new EconomyProtectedOperationAuthorization(
                TenantId, ActorId, "BRA", riskDecisionId, fingerprint, receipt);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
