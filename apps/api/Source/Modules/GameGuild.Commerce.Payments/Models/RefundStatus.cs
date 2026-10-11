namespace GameGuild.Commerce.Payments;

/// <summary>Lifecycle status of a refund request (issue #419).</summary>
public enum RefundStatus
{
    /// <summary>Created and awaiting the approval decision (manual-approval path).</summary>
    Pending = 0,

    /// <summary>Approved by an administrator (or auto-approved under the configured threshold); ready to execute.</summary>
    Approved = 1,

    /// <summary>Rejected by an administrator; no money moves.</summary>
    Rejected = 2,

    /// <summary>Gateway refund call in flight.</summary>
    Processing = 3,

    /// <summary>Gateway confirmed the refund; accounting, ledger and payment aggregate updated.</summary>
    Completed = 4,

    /// <summary>Gateway rejected the refund attempt; the failure reason is recorded.</summary>
    Failed = 5
}
