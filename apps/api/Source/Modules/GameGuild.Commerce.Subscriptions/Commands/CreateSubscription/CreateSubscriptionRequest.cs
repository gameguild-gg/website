
namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Request model for creating a subscription
/// </summary>
/// <param name="TenantId">The tenant ID</param>
/// <param name="PlanId">The subscription plan ID</param>
/// <param name="CreatedByUserId">The user who created the subscription</param>
/// <param name="BillingCycle">The billing cycle (Monthly, Yearly, etc.)</param>
/// <param name="Amount">The subscription amount</param>
/// <param name="Currency">The currency code</param>
/// <param name="FulfilledOrderId">Optional Order ID that triggered this subscription (Economic Model: Order→Subscription causality)</param>
/// <param name="StartDate">Optional start date</param>
/// <param name="TrialDays">Optional trial period in days</param>
/// <param name="PromoCodes">Optional promo codes applied at creation (issue #418)</param>
/// <param name="PromoCodeIdempotencyKey">Optional replay-protection key for the promo application</param>
/// <param name="ProductId">Optional product context for product-scoped promo codes</param>
public sealed record CreateSubscriptionRequest(
    Guid TenantId,
    Guid PlanId,
    Guid CreatedByUserId,
    BillingCycle BillingCycle,
    decimal Amount,
    string Currency,
    Guid? FulfilledOrderId = null,
    DateTime? StartDate = null,
    int? TrialDays = null,
    IReadOnlyList<string>? PromoCodes = null,
    string? PromoCodeIdempotencyKey = null,
    Guid? ProductId = null);
