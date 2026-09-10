using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Ledger;

namespace GameGuild.Finance.Economy.UnitTests.Funding;

internal static class FundingTestDriver
{
    internal static HardCoinFundingClaim Observe(
        TransactionalPostingService service,
        DateTimeOffset observedAt,
        long units = 10,
        WalletId? walletId = null,
        SourceStampId? sourceId = null,
        string? providerObject = null)
    {
        var source = sourceId ?? SourceStampId.New();
        return service.ObserveTopUp(new ObserveHardCoinTopUpCommand(
            source,
            walletId ?? WalletId.New(),
            new ProviderMonetaryLeg(
                "stripe",
                "test",
                "platform",
                providerObject ?? $"pi_{source.Value:N}",
                "principal"),
            "provider-observed",
            units,
            observedAt));
    }

    internal static ConfirmObservedTopUpCommand Confirmation(
        HardCoinFundingClaim claim,
        DateTimeOffset confirmedAt,
        string idempotencyKey = "topup-1",
        PostingId? postingId = null,
        CreditLotId? creditLotId = null,
        string evidence = "provider-confirmed")
    {
        var key = new IdempotencyKey(idempotencyKey);
        return new ConfirmObservedTopUpCommand(
            postingId ?? PostingId.New(),
            key,
            claim.SourceId,
            creditLotId ?? CreditLotId.New(),
            new ReserveVersion(1),
            new PolicyVersion(1),
            evidence,
            confirmedAt,
            FundingAuthorizationFixture.Create(
                PostingTemplateKind.ConfirmedTopUpMint,
                key,
                claim.WalletId,
                claim.Amount,
                [claim.SourceId],
                confirmedAt,
                claim.Amount));
    }

    internal static PostingResult Confirm(
        TransactionalPostingService service,
        HardCoinFundingClaim claim,
        DateTimeOffset confirmedAt,
        string idempotencyKey = "topup-1") =>
        service.ConfirmObservedTopUp(Confirmation(claim, confirmedAt, idempotencyKey));
}
