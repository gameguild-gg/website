namespace GameGuild.Commerce.Billing;

/// <summary>
///     A provider webhook transitioned to processed in the durable billing inbox.
///     Payload identifiers are provider-scoped and non-personal.
/// </summary>
public sealed record BillingWebhookProcessedV1(
    [property: NonPersonalEventData] Guid WebhookEventId,
    [property: NonPersonalEventData] string Provider,
    [property: NonPersonalEventData] string ExternalEventId,
    [property: NonPersonalEventData] string EventType,
    [property: NonPersonalEventData] int AttemptNumber) : DurableIntegrationEventBase
{
    public override string EventName => "commerce.billing.webhook-processed.v1";
    public override string SourceModule => "Commerce.Billing";
}

/// <summary>
///     A provider webhook processing attempt failed and the inbox row is awaiting retry.
///     The error message is technical processing output and never contains credentials.
/// </summary>
public sealed record BillingWebhookFailedV1(
    [property: NonPersonalEventData] Guid WebhookEventId,
    [property: NonPersonalEventData] string Provider,
    [property: NonPersonalEventData] string ExternalEventId,
    [property: NonPersonalEventData] string EventType,
    [property: NonPersonalEventData] int AttemptNumber,
    [property: NonPersonalEventData] string ErrorMessage) : DurableIntegrationEventBase
{
    public override string EventName => "commerce.billing.webhook-failed.v1";
    public override string SourceModule => "Commerce.Billing";
}

/// <summary>
///     A provider invoice payment succeeded and was recorded against a subscription.
/// </summary>
public sealed record BillingInvoicePaidV1(
    [property: NonPersonalEventData] Guid SubscriptionId,
    [property: NonPersonalEventData] string Provider,
    [property: NonPersonalEventData] string ExternalEventId,
    [property: NonPersonalEventData] decimal Amount,
    [property: NonPersonalEventData] string Currency) : DurableIntegrationEventBase
{
    public override string EventName => "commerce.billing.invoice-paid.v1";
    public override string SourceModule => "Commerce.Billing";
}

/// <summary>
///     A recurring payment succeeded against an already-active subscription (billing renewal).
/// </summary>
public sealed record BillingSubscriptionRenewedV1(
    [property: NonPersonalEventData] Guid SubscriptionId,
    [property: NonPersonalEventData] string Provider,
    [property: NonPersonalEventData] string ExternalEventId) : DurableIntegrationEventBase
{
    public override string EventName => "commerce.billing.subscription-renewed.v1";
    public override string SourceModule => "Commerce.Billing";
}

/// <summary>
///     A subscription was cancelled following a provider webhook lifecycle event.
/// </summary>
public sealed record BillingSubscriptionCancelledV1(
    [property: NonPersonalEventData] Guid SubscriptionId,
    [property: NonPersonalEventData] string Provider,
    [property: NonPersonalEventData] string ExternalEventId) : DurableIntegrationEventBase
{
    public override string EventName => "commerce.billing.subscription-cancelled.v1";
    public override string SourceModule => "Commerce.Billing";
}
