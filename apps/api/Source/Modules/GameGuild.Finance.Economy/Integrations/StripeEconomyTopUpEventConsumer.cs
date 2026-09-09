using System.Security.Cryptography;
using System.Text;
using GameGuild.Commerce.Billing;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.Integrations;

public sealed class StripeEconomyTopUpEventConsumer(
    IEconomyTopUpSettlementStore topUps,
    IStripeEconomyFundingAdapter fundingAdapter,
    IHardCoinFundingGateway fundingGateway,
    IEconomyTrustedProtectedOperationAuthorizer protectedOperations,
    IRegisteredPostingCapabilityResolver capabilities,
    TimeProvider timeProvider) : IStripeVerifiedEventConsumer
{
    internal const string RegisteredCapabilityName = "economy.confirm-hard-coin-funding.v1";

    public async ValueTask<bool> TryConsumeAsync(
        VerifiedStripeWebhookEvent verifiedEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verifiedEvent);
        var status = MapStatus(verifiedEvent.EventType);
        if (!status.HasValue)
            return false;
        Validate(verifiedEvent);
        var identity = new EconomyTopUpProviderIdentity(
            "stripe",
            verifiedEvent.ProviderEnvironment,
            verifiedEvent.ProviderAccountId,
            verifiedEvent.ProviderObjectId,
            "payment_intent",
            "capture");
        var context = await topUps.FindAsync(identity, cancellationToken).ConfigureAwait(false);
        if (context is null)
            return false;
        EnsureAuthoritativePayment(verifiedEvent, context);

        if (status != EconomyTopUpProviderStatus.Posted)
        {
            await topUps.ApplyAsync(
                ProviderEvent(verifiedEvent, identity, status.Value),
                cancellationToken).ConfigureAwait(false);
            return true;
        }
        if (context.TopUp.Status is EconomyTopUpProviderStatus.Posted or EconomyTopUpProviderStatus.Cancelled)
            return true;

        var evidence = $"stripe-event:{verifiedEvent.EventId}:sha256:{verifiedEvent.PayloadSha256}";
        var observation = fundingAdapter.CreateObservation(
            context.Payment,
            context.TopUp.WalletId,
            evidence,
            verifiedEvent.OccurredAt);
        var confirmedAt = timeProvider.GetUtcNow();
        var intent = new EconomyProtectedOperationIntent(
            EconomyValueMovementCapability.ConfirmHardCoinFunding,
            PostingTemplateKind.ConfirmedTopUpMint,
            context.TopUp.WalletId,
            context.TopUp.WalletId,
            new CoinAmount(CurrencyCode.HardCoin, context.TopUp.HardCoinUnits),
            [new RiskCurrencyLeg(CurrencyCode.HardCoin, context.TopUp.HardCoinUnits)],
            [observation.SourceId],
            Hash(identity),
            Hash(context.TopUp.WalletId.Value.ToString("N")),
            fundingAdapter.ConfirmationIdempotencyKey(context.Payment),
            confirmedAt,
            ProtectedSubjectId: context.TopUp.ActorId);

        try
        {
            _ = await protectedOperations.ExecuteAsync(
                context.TopUp.TenantId,
                context.TopUp.ActorId,
                intent,
                async (authorization, token) =>
                {
                    var claim = fundingGateway.Observe(new PersistedHardCoinFundingObservation(
                        observation,
                        authorization.ActorId,
                        authorization.TenantId,
                        new PolicyVersion(authorization.Receipt.PolicyVersion)));
                    var authority = await capabilities.ResolveAuthorityAsync(
                        RegisteredCapabilityName,
                        PostingTemplateKind.ConfirmedTopUpMint,
                        authorization.Receipt,
                        token).ConfigureAwait(false);
                    var confirmation = fundingAdapter.CreateDurableConfirmation(
                        context.Payment,
                        claim,
                        authorization,
                        authority,
                        evidence,
                        confirmedAt);
                    var receipt = fundingGateway.ConfirmDurable(confirmation);
                    await topUps.ApplyAsync(
                        ProviderEvent(
                            verifiedEvent,
                            identity,
                            EconomyTopUpProviderStatus.Posted,
                            receipt.PostingId.Value),
                        token).ConfigureAwait(false);
                    return receipt;
                },
                cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await topUps.ApplyAsync(
                ProviderEvent(verifiedEvent, identity, EconomyTopUpProviderStatus.Held),
                cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    internal static EconomyTopUpProviderStatus? MapStatus(string eventType) => eventType switch
    {
        "payment_intent.processing" => EconomyTopUpProviderStatus.Processing,
        "payment_intent.requires_action" => EconomyTopUpProviderStatus.RequiresAction,
        "payment_intent.succeeded" => EconomyTopUpProviderStatus.Posted,
        "payment_intent.payment_failed" => EconomyTopUpProviderStatus.Failed,
        "payment_intent.canceled" => EconomyTopUpProviderStatus.Cancelled,
        _ => null
    };

    internal static void Validate(VerifiedStripeWebhookEvent verifiedEvent)
    {
        if (verifiedEvent.OccurredAt == default ||
            string.IsNullOrWhiteSpace(verifiedEvent.EventId) ||
            string.IsNullOrWhiteSpace(verifiedEvent.ProviderEnvironment) ||
            string.IsNullOrWhiteSpace(verifiedEvent.ProviderAccountId) ||
            string.IsNullOrWhiteSpace(verifiedEvent.ProviderObjectId) ||
            !string.Equals(verifiedEvent.ProviderObjectType, "payment_intent", StringComparison.Ordinal) ||
            verifiedEvent.PayloadSha256.Length != 64 ||
            !verifiedEvent.PayloadSha256.All(Uri.IsHexDigit))
            throw new InvalidWebhookPayloadException(
                "Stripe top-up event identity or evidence is incomplete.");
    }

    internal static void EnsureAuthoritativePayment(
        VerifiedStripeWebhookEvent verifiedEvent,
        EconomyTopUpSettlementContext context)
    {
        if (!verifiedEvent.Amount.HasValue || verifiedEvent.Amount.Value != context.Payment.Amount ||
            !string.Equals(verifiedEvent.Currency, context.Payment.Currency, StringComparison.OrdinalIgnoreCase) ||
            verifiedEvent.TenantId.HasValue && verifiedEvent.TenantId.Value != context.TopUp.TenantId)
            throw new InvalidWebhookPayloadException(
                "Stripe top-up event does not match the authoritative payment.");
    }

    private static EconomyTopUpProviderEvent ProviderEvent(
        VerifiedStripeWebhookEvent verifiedEvent,
        EconomyTopUpProviderIdentity identity,
        EconomyTopUpProviderStatus status,
        Guid? postingGroupId = null) => new(
        identity,
        verifiedEvent.EventId,
        verifiedEvent.OccurredAt,
        status,
        verifiedEvent.PayloadSha256,
        decimal.ToInt64(verifiedEvent.Amount!.Value * 100m),
        verifiedEvent.Currency!.ToUpperInvariant(),
        postingGroupId,
        status switch
        {
            EconomyTopUpProviderStatus.Failed => "payment_failed",
            EconomyTopUpProviderStatus.Cancelled => "cancelled",
            _ => null
        });

    private static string Hash(EconomyTopUpProviderIdentity identity) => Hash(string.Join(
        '\u001f',
        identity.Provider,
        identity.ProviderEnvironment,
        identity.ProviderAccountId,
        identity.ProviderObjectId,
        identity.ProviderObjectType,
        identity.ProviderMonetaryLeg));

    private static string Hash(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
