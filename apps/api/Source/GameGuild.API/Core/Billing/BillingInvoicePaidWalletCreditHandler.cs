using GameGuild.Commerce.Billing;
using GameGuild.Commerce.Payments;
using GameGuild.Commerce.Subscriptions;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Options;

namespace GameGuild.API.Core.Billing;

/// <summary>
///     Wallet &amp; balance tracking integration with the billing stack (issue #406): when a
///     provider webhook records a successful invoice payment (durable
///     <see cref="BillingInvoicePaidV1" /> from Commerce.Billing), the paying user's billing
///     wallet is credited with the paid amount through the audited
///     <see cref="AddFundsCommand" />, using the provider event identifier as the idempotency
///     reference so webhook retries never double-credit. Follows the host-side
///     SuspiciousLoginAlertHandler durable-consumer precedent.
/// </summary>
/// <remarks>
///     <para>
///         The credit is dispatched as the well-known system actor (SystemAdmin role) because
///         <see cref="AddFundsCommand" /> is administrator-gated; the command's audit trail is
///         attributed through the resulting WalletTransaction's reference identifier, since the
///         system actor carries no user GUID.
///     </para>
///     <para>
///         Opt-out via <c>Wallets:WebhookCreditEnabled=false</c>.
///     </para>
/// </remarks>
internal sealed class BillingInvoicePaidWalletCreditHandler(
    ISubscriptionQueryService subscriptions,
    ISender sender,
    IActorContextAccessor actorContextAccessor,
    IOptions<WalletsOptions> options) : IIntegrationEventHandler<BillingInvoicePaidV1>
{
    private const int MaxReferenceLength = 100;

    public async Task HandleAsync(BillingInvoicePaidV1 @event, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        if (!options.Value.WebhookCreditEnabled)
        {
            return;
        }

        if (@event.Amount <= 0)
        {
            return;
        }

        var subscription = await subscriptions.GetByIdAsync(@event.SubscriptionId, cancellationToken).ConfigureAwait(false);
        if (subscription is null)
        {
            // Unknown subscription — nothing to credit; the invoice remains recorded in Billing.
            return;
        }

        var referenceId = BuildReferenceId(@event.Provider, @event.ExternalEventId);
        // The system-actor swap must be visible only inside this dispatch: the ambient
        // context of the caller (the webhook pipeline) has to survive it intact, so the
        // swap runs through a scope that repairs the original holder on dispose.
        using var _ = actorContextAccessor.BeginActorContextScope(
            ActorContextBuilder.ForSystem("BillingWalletCredit").Build());
        await sender.Send(
            new AddFundsCommand(
                subscription.CreatedByUserId,
                @event.Amount,
                $"Billing credit: {(@event.Provider)} invoice payment",
                referenceId,
                @event.Currency),
            cancellationToken).ConfigureAwait(false);
    }

    private static string BuildReferenceId(string provider, string externalEventId)
    {
        var reference = $"billing-invoice-paid:{provider}:{externalEventId}";
        return reference.Length <= MaxReferenceLength ? reference : reference[..MaxReferenceLength];
    }
}
