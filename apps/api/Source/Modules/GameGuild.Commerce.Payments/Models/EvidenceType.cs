namespace GameGuild.Commerce.Payments;

/// <summary>Evidence types</summary>
public enum EvidenceType
{
    /// <summary>Receipt</summary>
    Receipt = 0,

    /// <summary>Communication</summary>
    Communication = 1,

    /// <summary>Photo</summary>
    Photo = 2,

    /// <summary>Video</summary>
    Video = 3,

    /// <summary>Shipping information</summary>
    ShippingInfo = 4,

    /// <summary>Contract</summary>
    Contract = 5,

    /// <summary>Bank statement</summary>
    BankStatement = 6,

    /// <summary>Documentation</summary>
    Documentation = 7,

    /// <summary>Other</summary>
    Other = 8,

    /// <summary>Invoice issued for the disputed charge (issue #419 evidence collection)</summary>
    Invoice = 9,

    /// <summary>Subscription/billing record for the disputed charge (issue #419 evidence collection)</summary>
    SubscriptionRecord = 10
}
