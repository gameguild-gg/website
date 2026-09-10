using FluentAssertions;
using GameGuild.Commerce.Payments;
using GameGuild.Finance.Economy.Integrations;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Reserves;
using GameGuild.Finance.Economy.Risk;
using Xunit;

namespace GameGuild.Finance.Economy.UnitTests;

public sealed class StripeEconomyFundingIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void VerifiedStripeTopUp_FlowsThroughLedgerConfirmationAndHardToSoftConversion()
    {
        var adapter = new StripeEconomyFundingAdapter();
        var store = new InMemoryLedgerKernelStore();
        var ledger = new TransactionalPostingService(store);
        var wallet = WalletId.New();
        var payment = Payment.Create(Guid.NewGuid(), 2.50m, "USD", "stripe:phase-3-provider-ledger");
        payment.BindProviderMapping(
            "stripe", "test", "acct_platform", "pi_phase_3", "payment_intent", "capture");

        var observation = adapter.CreateObservation(payment, wallet, "stripe-observed", Now);
        var claim = ledger.ObserveTopUp(observation);
        var confirmationKey = adapter.ConfirmationIdempotencyKey(payment);
        var confirmation = adapter.CreateConfirmation(
            payment,
            claim,
            Authorize(
                PostingTemplateKind.ConfirmedTopUpMint,
                confirmationKey,
                wallet,
                claim.Amount,
                [claim.SourceId],
                Now.AddMinutes(1),
                claim.ProviderLeg.Key),
            new PolicyVersion(1),
            "stripe-settled",
            Now.AddMinutes(1));

        var confirmed = ledger.ConfirmObservedTopUp(confirmation);
        var confirmationReplay = ledger.ConfirmObservedTopUp(confirmation);

        confirmed.Status.Should().Be(PostingStatus.Accepted);
        confirmationReplay.Should().Be(confirmed);
        store.FundingClaims.Should().ContainSingle().Which.State.Should().Be(SourceConfirmationState.Confirmed);
        store.GetAvailableLots(wallet, CurrencyCode.HardCoin)
            .Should().ContainSingle().Which.Amount.Should().Be(new CoinAmount(CurrencyCode.HardCoin, 250));

        var conversionKey = new IdempotencyKey("phase-3-provider-ledger-conversion");
        var conversion = new ConvertHardToSoftCommand(
            PostingId.New(),
            PostingId.New(),
            conversionKey,
            wallet,
            CreditLotId.New(),
            100,
            10,
            new ReserveVersion(1),
            new PolicyVersion(1),
            Now.AddMinutes(2),
            Authorize(
                PostingTemplateKind.HardToSoftConversion,
                conversionKey,
                wallet,
                new CoinAmount(CurrencyCode.HardCoin, 110),
                [claim.SourceId],
                Now.AddMinutes(2),
                claim.ProviderLeg.Key,
                new CoinAmount(CurrencyCode.SoftCoin, 100_000)));

        var converted = ledger.ConvertHardToSoft(conversion);
        var conversionReplay = ledger.ConvertHardToSoft(conversion);

        converted.PrincipalPosting.Status.Should().Be(PostingStatus.Accepted);
        converted.FeePosting.Should().NotBeNull();
        converted.OutputLot.Amount.Should().Be(new CoinAmount(CurrencyCode.SoftCoin, 100_000));
        converted.OutputLot.Ranges.Should().OnlyContain(range => range.Root == claim.SourceId);
        conversionReplay.Should().BeEquivalentTo(converted);
        store.GetAvailableLots(wallet, CurrencyCode.HardCoin)
            .Should().ContainSingle().Which.Amount.Should().Be(new CoinAmount(CurrencyCode.HardCoin, 140));
        store.GetAvailableLots(wallet, CurrencyCode.SoftCoin)
            .Should().ContainSingle().Which.Should().BeEquivalentTo(converted.OutputLot);
        store.JournalEntries.Should().HaveCount(3);
        store.FragmentConsumptions.Should().HaveCount(2);
        store.Lineages.Should().ContainSingle().Which.Lot.Should().BeEquivalentTo(converted.OutputLot);
    }

    private static ProtectedIssuanceAuthorization Authorize(
        PostingTemplateKind operation,
        IdempotencyKey idempotencyKey,
        WalletId wallet,
        CoinAmount amount,
        IReadOnlyList<SourceStampId> roots,
        DateTimeOffset requestedAt,
        string providerReference,
        CoinAmount? reserveLiabilityIncrease = null)
    {
        var reserve = new CoreReserveAuthority();
        reserve.ValidateAndActivate(new ReserveProposal(
            new ReserveVersion(1),
            null,
            new PolicyVersion(1),
            1,
            requestedAt.AddMinutes(-1),
            requestedAt.AddMinutes(5),
            new ReserveLiabilityPosition(0, 0, 0, 0),
            new ReserveBufferPosition(0, 0, 0, 0, 0, 0, 0),
            [new ReserveServiceObservation(
                "stripe", 1, 1, 1, 1, 0, true,
                requestedAt.AddMinutes(-1), requestedAt.AddMinutes(5))],
            [
                new ExternalReserveAsset("hard", ReserveBackingPurpose.HardCoin, 2_000_000_000_000),
                new ExternalReserveAsset("soft", ReserveBackingPurpose.SoftCoin, 2_000_000_000_000)
            ],
            "phase-3-provider-ledger-reserve"), requestedAt);
        var context = new ProtectedOperationContext(
            idempotencyKey,
            Guid.NewGuid(),
            operation,
            wallet,
            wallet,
            amount,
            [new RiskCurrencyLeg(amount.Currency, amount.Units)],
            roots,
            providerReference,
            new PolicyVersion(1),
            new ReserveVersion(1),
            1,
            1,
            1,
            "phase-3-provider-ledger",
            1,
            1);
        var decision = RiskDecisionSnapshot.Create(
            Guid.NewGuid(),
            RiskOutcome.Allow,
            context,
            requestedAt.AddSeconds(-1),
            requestedAt.AddMinutes(1),
            [RiskReasonCode.WithinLimits]);
        var limits = new List<AggregateRiskLimit>
        {
            new(
                new RiskLimitKey(RiskLimitDimension.Wallet, wallet.Value.ToString("N")),
                1,
                long.MaxValue,
                TimeSpan.FromDays(1))
        };
        limits.AddRange(roots.Select(root => new AggregateRiskLimit(
            new RiskLimitKey(RiskLimitDimension.SourceRoot, root.Value.ToString("N")),
            1,
            long.MaxValue,
            TimeSpan.FromDays(1))));
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
                requestedAt,
                reserveLiabilityIncrease));
    }
}
