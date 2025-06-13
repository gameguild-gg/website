using System.ComponentModel;

namespace GameGuild.Common.Enums;

public enum SubscriptionType
{
    [Description("Subscription billed on a monthly basis")]
    Monthly,

    [Description("Subscription billed every three months")]
    Quarterly,

    [Description("Subscription billed once per year")]
    Annual,

    [Description("One-time payment for permanent access")]
    Lifetime
}

public enum SubscriptionBillingInterval
{
    [Description("Billing occurs daily")]
    Day,

    [Description("Billing occurs weekly")]
    Week,

    [Description("Billing occurs monthly")]
    Month,

    [Description("Billing occurs yearly")]
    Year
}

public enum SubscriptionStatus
{
    [Description("Subscription is currently valid and paid up")]
    Active,

    [Description("In free trial period before regular billing begins")]
    Trialing,

    [Description("Payment failed but subscription still active, pending retry")]
    PastDue,

    [Description("User has canceled the subscription")]
    Canceled,

    [Description("Initial payment failed, subscription not fully activated")]
    Incomplete,

    [Description("Initial payment failed and the trial period expired")]
    IncompleteExpired,

    [Description("Payment failed after retries, subscription suspended")]
    Unpaid
}
