namespace GameGuild.Commerce.Payments;

/// <summary>Where an account credit grant came from.</summary>
public enum AccountCreditSource
{
    /// <summary>Manually issued by an operator.</summary>
    Manual = 0,

    /// <summary>Gift subscription redemption.</summary>
    Gift = 1,

    /// <summary>Referral program reward.</summary>
    Referral = 2,

    /// <summary>Goodwill / refund credit.</summary>
    Refund = 3
}

/// <summary>What an account credit grant may be applied to.</summary>
public enum AccountCreditType
{
    /// <summary>Applies to any charge.</summary>
    General = 0,

    /// <summary>Applies only to subscription invoices.</summary>
    Subscription = 1,

    /// <summary>Applies only to addon charges (reserved: no addon invoices exist in v1, so addon credits never match).</summary>
    Addon = 2
}

/// <summary>Lifecycle status of an account credit grant.</summary>
public enum AccountCreditGrantStatus
{
    /// <summary>Spendable balance remains.</summary>
    Active = 0,

    /// <summary>Fully consumed.</summary>
    FullyUsed = 1,

    /// <summary>Expired with or without remaining balance.</summary>
    Expired = 2,

    /// <summary>Revoked by an operator with a reason.</summary>
    Revoked = 3
}

/// <summary>Lifecycle event kinds recorded in the account credit ledger.</summary>
public enum AccountCreditTransactionType
{
    /// <summary>Credit granted.</summary>
    Grant = 0,

    /// <summary>Credit consumed (applied to an invoice).</summary>
    Consume = 1,

    /// <summary>Credit revoked.</summary>
    Revoke = 2,

    /// <summary>Credit expired unused.</summary>
    Expire = 3
}

/// <summary>Gift subscription status machine (Purchased → Delivered → Redeemed / Expired, plus Refunded).</summary>
public enum GiftStatus
{
    /// <summary>Paid, awaiting delivery (immediately deliverable when no delivery date is set).</summary>
    Purchased = 0,

    /// <summary>Delivery date reached; redeemable.</summary>
    Delivered = 1,

    /// <summary>Recipient redeemed the gift (credit granted).</summary>
    Redeemed = 2,

    /// <summary>Not redeemed before the one-year deadline.</summary>
    Expired = 3,

    /// <summary>Unredeemed gift refunded to the giver.</summary>
    Refunded = 4
}

/// <summary>Referral funnel status.</summary>
public enum ReferralStatus
{
    /// <summary>Code shared, no click yet.</summary>
    Pending = 0,

    /// <summary>Link clicked, no signup.</summary>
    Clicked = 1,

    /// <summary>Referee account created, no payment yet.</summary>
    SignedUp = 2,

    /// <summary>Referee's first invoice paid.</summary>
    Converted = 3,

    /// <summary>Both parties rewarded.</summary>
    RewardIssued = 4,

    /// <summary>Expired before conversion.</summary>
    Expired = 5,

    /// <summary>Flagged by the fraud blocks.</summary>
    Fraudulent = 6
}
