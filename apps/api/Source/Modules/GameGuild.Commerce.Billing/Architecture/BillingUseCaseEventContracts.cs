using GameGuild;
using GameGuild.Commerce.Billing;

// Named billing integration events (issue #396): the provider webhook commands declare the
// named inbox-transition events that occur conditionally on the processing outcome. A
// webhook either completes processing (BillingWebhookProcessedV1) or records a failed
// attempt awaiting retry (BillingWebhookFailedV1), so both events are conditional rather
// than expected on every mutating execution. Invoice-paid, subscription-renewed and
// subscription-cancelled events are published from the shared webhook routing handlers
// and ride the same conditional semantics.
[assembly: UseCaseEventContract(
    typeof(ProcessStripeWebhookCommand),
    "commerce.billing.process-stripe-webhook",
    ConditionalEventTypes = [typeof(BillingWebhookProcessedV1), typeof(BillingWebhookFailedV1)])]
[assembly: UseCaseEventContract(
    typeof(ProcessPayPalWebhookCommand),
    "commerce.billing.process-pay-pal-webhook",
    ConditionalEventTypes = [typeof(BillingWebhookProcessedV1), typeof(BillingWebhookFailedV1)])]
[assembly: UseCaseEventContract(
    typeof(ProcessApplePayWebhookCommand),
    "commerce.billing.process-apple-pay-webhook",
    ConditionalEventTypes = [typeof(BillingWebhookProcessedV1), typeof(BillingWebhookFailedV1)])]
[assembly: UseCaseEventContract(
    typeof(ProcessGooglePayWebhookCommand),
    "commerce.billing.process-google-pay-webhook",
    ConditionalEventTypes = [typeof(BillingWebhookProcessedV1), typeof(BillingWebhookFailedV1)])]

// Pricing-decision ledger drift validation (issue #400): stamps Verified/Drifted
// outcomes on sampled ledger entries. The ledger rows themselves are the durable
// record; no named cross-module event is declared and the durable generic operation
// event remains the observable contract.
[assembly: UseCaseEventContract(
    typeof(ValidatePricingLedgerDriftCommand),
    "commerce.billing.validate-pricing-ledger-drift",
    NoDomainEventReason = "Drift validation stamps ledger entries in place; the ledger rows and audit trail are the durable record, and the durable generic operation event remains the observable contract.")]
