using FluentAssertions;
using GameGuild.Commerce.Payments;
using GameGuild.Finance.Economy.Integrations;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Reserves;
using GameGuild.Finance.Economy.Risk;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GameGuild.Finance.Economy.UnitTests;

public sealed class StripeEconomyFundingAdapterTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);
    private readonly StripeEconomyFundingAdapter _adapter = new();

    [Fact]
    public void Observation_Maps_Verified_Stripe_Usd_Fact_To_Exact_HardCoin_Units()
    {
        var payment = StripePayment(12.34m);
        var walletId = new WalletId(Guid.NewGuid());

        var first = _adapter.CreateObservation(payment, walletId, "stripe-event", Now);
        var replay = _adapter.CreateObservation(payment, walletId, "stripe-event", Now);

        first.Should().Be(replay);
        first.WalletId.Should().Be(walletId);
        first.AuthoritativeUsdMinorUnits.Should().Be(1_234);
        first.ProviderLeg.Provider.Should().Be("stripe");
        first.ProviderLeg.Environment.Should().Be("test");
        first.ProviderLeg.ConnectedAccount.Should().Be("acct_platform");
        first.ProviderLeg.ProviderObject.Should().Be("payment_intent:pi_123");
        first.ProviderLeg.MonetaryLeg.Should().Be("capture");
    }

    [Fact]
    public void Confirmation_Is_Deterministic_And_Bound_To_Claim_And_Authorization()
    {
        var payment = StripePayment(12.34m);
        var observation = _adapter.CreateObservation(payment, new WalletId(Guid.NewGuid()), "observed", Now);
        var claim = Claim(observation);
        var key = _adapter.ConfirmationIdempotencyKey(payment);
        var authorization = Authorize(claim, key);

        var first = _adapter.CreateConfirmation(
            payment, claim, authorization, new PolicyVersion(1), "settled", Now.AddMinutes(1));
        var replay = _adapter.CreateConfirmation(
            payment, claim, authorization, new PolicyVersion(1), "settled", Now.AddMinutes(1));

        first.Should().Be(replay);
        first.SourceId.Should().Be(claim.SourceId);
        first.IdempotencyKey.Should().Be(key);
        first.ReserveVersion.Should().Be(new ReserveVersion(1));
        first.PolicyVersion.Should().Be(new PolicyVersion(1));
        first.Authorization.Should().BeSameAs(authorization);
    }

    [Fact]
    public void DurableConfirmation_IsDeterministicAndBoundToTheCapabilityReceipt()
    {
        var payment = StripePayment(12.34m);
        var fact = PaymentFact(payment);
        var wallet = new WalletId(Guid.NewGuid());
        var observation = _adapter.CreateObservation(fact, wallet, "observed", Now);
        var claim = Claim(observation);
        var tenantId = fact.TenantId;
        var actorId = Guid.NewGuid();
        var riskDecisionId = Guid.NewGuid();
        const string fingerprint = "durable-stripe-confirmation";
        var receipt = new CapabilityAuthorizationReceipt(
            Guid.NewGuid(), tenantId, actorId, "top-up", "USA",
            EconomyValueMovementCapability.ConfirmHardCoinFunding, fingerprint, 4, 3,
            riskDecisionId, 0, "provider", "destination", [], ["evidence"],
            Now, Now.AddMinutes(5), "receipt", "key", "signature");
        var authorization = new EconomyProtectedOperationAuthorization(
            tenantId, actorId, "USA", riskDecisionId, fingerprint, receipt);
        var authority = new RegisteredPostingAuthority(
            Guid.NewGuid(), actorId, tenantId, riskDecisionId, fingerprint, 1);

        var first = _adapter.CreateDurableConfirmation(
            fact, claim, authorization, authority, "settled", Now.AddMinutes(1));
        var replay = _adapter.CreateDurableConfirmation(
            fact, claim, authorization, authority, "settled", Now.AddMinutes(1));

        first.Should().Be(replay);
        first.SourceId.Should().Be(claim.SourceId);
        first.Receipt.Should().BeSameAs(receipt);
        first.Authority.Should().BeSameAs(authority);

        var other = PaymentFact(StripePayment(12.34m, "pi_other"));
        var otherClaim = Claim(_adapter.CreateObservation(other, wallet, "other", Now));
        FluentActions.Invoking(() => _adapter.CreateDurableConfirmation(
                fact, otherClaim, authorization, authority, "settled", Now.AddMinutes(1)))
            .Should().Throw<InvalidOperationException>().WithMessage("*does not match*");
    }

    [Theory]
    [InlineData(SourceConfirmationState.Failed)]
    [InlineData(SourceConfirmationState.Expired)]
    public void Terminal_Failure_Only_Maps_Non_Minting_Provider_Outcomes(SourceConfirmationState state)
    {
        var command = _adapter.CreateTerminalFailure(StripePayment(1m), state, "provider-terminal", Now);

        command.State.Should().Be(state);
        command.Evidence.Should().Be("provider-terminal");
        command.OccurredAt.Should().Be(Now);
    }

    [Fact]
    public void Reversal_Uses_Cumulative_Provider_Amount_And_Stable_Identity()
    {
        var payment = StripePayment(12.34m);

        var first = _adapter.CreateReversal(
            payment, 2.34m, 1m, ProviderReversalDisposition.ResponsibleDebt,
            "refund-and-dispute", new ReserveVersion(3), new PolicyVersion(2), Now);
        var replay = _adapter.CreateReversal(
            payment, 2.34m, 1m, ProviderReversalDisposition.ResponsibleDebt,
            "refund-and-dispute", new ReserveVersion(3), new PolicyVersion(2), Now);

        first.Should().Be(replay);
        first.CumulativeProviderHardUnits.Should().Be(334);
        first.ReserveVersion.Should().Be(new ReserveVersion(3));
        first.PolicyVersion.Should().Be(new PolicyVersion(2));
    }

    [Fact]
    public void Module_Registers_A_Singleton_Stripe_Economy_Adapter()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEconomyCoreComposition(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IStripeEconomyFundingAdapter>()
            .Should().BeSameAs(provider.GetRequiredService<IStripeEconomyFundingAdapter>());
    }

    [Theory]
    [InlineData("paypal", "USD", true, 1)]
    [InlineData("stripe", "BRL", true, 1)]
    [InlineData("stripe", "USD", false, 1)]
    [InlineData("stripe", "USD", true, 1.001)]
    public void Observation_Rejects_Non_Authoritative_Provider_Facts(
        string provider,
        string currency,
        bool bindMapping,
        decimal amount)
    {
        var payment = Payment.Create(
            Guid.NewGuid(), amount, currency, "invalid-provider-fact", provider: provider);
        if (bindMapping)
            payment.BindProviderMapping(provider, "test", "acct_platform", "pi_invalid", "payment_intent", "capture");

        var act = () => _adapter.CreateObservation(payment, new WalletId(Guid.NewGuid()), "evidence", Now);

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Confirmation_Rejects_A_Different_Observed_Claim()
    {
        var payment = StripePayment(12.34m);
        var otherPayment = StripePayment(12.34m, "pi_other");
        var otherObservation = _adapter.CreateObservation(otherPayment, new WalletId(Guid.NewGuid()), "observed", Now);
        var otherClaim = Claim(otherObservation);
        var authorization = Authorize(otherClaim, _adapter.ConfirmationIdempotencyKey(payment));

        var act = () => _adapter.CreateConfirmation(
            payment, otherClaim, authorization, new PolicyVersion(1), "settled", Now.AddMinutes(1));

        act.Should().Throw<InvalidOperationException>().WithMessage("*does not match*");
    }

    [Theory]
    [InlineData(SourceConfirmationState.Observed)]
    [InlineData(SourceConfirmationState.Confirmed)]
    [InlineData(SourceConfirmationState.Disputed)]
    [InlineData(SourceConfirmationState.Reversed)]
    public void Terminal_Failure_Rejects_Minting_Or_Post_Confirmation_States(SourceConfirmationState state)
    {
        var act = () => _adapter.CreateTerminalFailure(StripePayment(1m), state, "invalid", Now);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(12.35, 0)]
    [InlineData(8, 5)]
    public void Reversal_Rejects_Invalid_Cumulative_Provider_Amounts(
        decimal cumulativeRefunded,
        decimal cumulativeDisputed)
    {
        var act = () => _adapter.CreateReversal(
            StripePayment(12.34m), cumulativeRefunded, cumulativeDisputed,
            ProviderReversalDisposition.PlatformLoss, "invalid", new ReserveVersion(1), new PolicyVersion(1), Now);

        act.Should().Throw<Exception>();
    }

    private static Payment StripePayment(decimal amount, string objectId = "pi_123")
    {
        var payment = Payment.Create(Guid.NewGuid(), amount, "USD", $"stripe:{objectId}");
        payment.BindProviderMapping(
            "stripe", "test", "acct_platform", objectId, "payment_intent", "capture");
        return payment;
    }

    private static EconomyTopUpPaymentFact PaymentFact(Payment payment) => new(
        payment.Id,
        payment.TenantId,
        payment.Amount,
        payment.Currency,
        payment.Provider,
        payment.ProviderEnvironment!,
        payment.ProviderAccountId!,
        payment.ProviderObjectId!,
        payment.ProviderObjectType!,
        payment.ProviderMonetaryLeg!);

    private static HardCoinFundingClaim Claim(ObserveHardCoinTopUpCommand observation) =>
        HardCoinFundingClaim.Observe(
            observation.SourceId,
            observation.WalletId,
            observation.ProviderLeg,
            observation.Evidence,
            observation.AuthoritativeUsdMinorUnits,
            observation.ObservedAt);

    private static ProtectedIssuanceAuthorization Authorize(
        HardCoinFundingClaim claim,
        IdempotencyKey idempotencyKey)
    {
        var reserve = new CoreReserveAuthority();
        reserve.ValidateAndActivate(new ReserveProposal(
            new ReserveVersion(1),
            null,
            new PolicyVersion(1),
            1,
            Now.AddMinutes(-1),
            Now.AddMinutes(5),
            new ReserveLiabilityPosition(0, 0, 0, 0),
            new ReserveBufferPosition(0, 0, 0, 0, 0, 0, 0),
            [new ReserveServiceObservation(
                "stripe", 1, 1, 1, 1, 0, true,
                Now.AddMinutes(-1), Now.AddMinutes(5))],
            [new ExternalReserveAsset("hard", ReserveBackingPurpose.HardCoin, 2_000_000_000_000)],
            "stripe-reserve"), Now);
        var context = new ProtectedOperationContext(
            idempotencyKey,
            Guid.NewGuid(),
            PostingTemplateKind.ConfirmedTopUpMint,
            claim.WalletId,
            claim.WalletId,
            claim.Amount,
            [new RiskCurrencyLeg(claim.Amount.Currency, claim.Amount.Units)],
            [claim.SourceId],
            claim.ProviderLeg.Key,
            new PolicyVersion(1),
            new ReserveVersion(1),
            1,
            1,
            1,
            "stripe-funding",
            1,
            1);
        var decision = RiskDecisionSnapshot.Create(
            Guid.NewGuid(),
            RiskOutcome.Allow,
            context,
            Now.AddSeconds(-1),
            Now.AddMinutes(1),
            [RiskReasonCode.WithinLimits]);
        var limits = new[]
        {
            new AggregateRiskLimit(
                new RiskLimitKey(RiskLimitDimension.Wallet, claim.WalletId.Value.ToString("N")),
                1,
                long.MaxValue,
                TimeSpan.FromDays(1)),
            new AggregateRiskLimit(
                new RiskLimitKey(RiskLimitDimension.SourceRoot, claim.SourceId.Value.ToString("N")),
                1,
                long.MaxValue,
                TimeSpan.FromDays(1))
        };
        return new ProtectedIssuanceAuthorizer(
                reserve,
                new CoreProtectedPostingGate(new RiskDecisionAuthorizer()),
                new AggregateRiskCounterStore(),
                new ProtectedChangeCooldownRegistry())
            .Authorize(new ProtectedIssuanceRequest(
                context,
                new RiskDecisionId(decision.Id),
                decision,
                new RiskPersistenceReadiness(true, true),
                Guid.NewGuid(),
                limits,
                context.ActorId,
                Now));
    }
}
