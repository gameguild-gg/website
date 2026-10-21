namespace GameGuild.Commerce.Payments;

/// <summary>Read model for a <see cref="Refund"/> row (issue #419).</summary>
public sealed record RefundRecord
{
    /// <summary>Refund identifier.</summary>
    public required Guid RefundId { get; init; }

    /// <summary>Tenant that owns the refunded payment.</summary>
    public required Guid TenantId { get; init; }

    /// <summary>Payment the refund was issued against.</summary>
    public required Guid PaymentId { get; init; }

    /// <summary>Refund amount in major currency units.</summary>
    public required decimal Amount { get; init; }

    /// <summary>How the amount was determined: full remaining, explicit partial, or computed proration.</summary>
    public required RefundKind Kind { get; init; }

    /// <summary>ISO 4217 currency code.</summary>
    public required string Currency { get; init; }

    /// <summary>Short machine-readable reason.</summary>
    public required string Reason { get; init; }

    /// <summary>Optional human-readable reason details.</summary>
    public string? ReasonDetails { get; init; }

    /// <summary>Current lifecycle status.</summary>
    public required RefundStatus Status { get; init; }

    /// <summary>Whether manual approval is (or was) required.</summary>
    public required bool RequiresApproval { get; init; }

    /// <summary>Identity of the requester.</summary>
    public required Guid RequestedByUserId { get; init; }

    /// <summary>When the refund was requested.</summary>
    public required DateTime RequestedAt { get; init; }

    /// <summary>Administrator who approved or rejected the refund, when manual.</summary>
    public Guid? DecidedByUserId { get; init; }

    /// <summary>When the approval decision was recorded.</summary>
    public DateTime? DecidedAt { get; init; }

    /// <summary>Notes recorded with the approval or rejection decision.</summary>
    public string? DecisionNotes { get; init; }

    /// <summary>Gateway refund identifier once the gateway accepted the refund.</summary>
    public string? GatewayRefundId { get; init; }

    /// <summary>Gateway that executed the refund.</summary>
    public string? GatewayName { get; init; }

    /// <summary>When the gateway confirmed the refund.</summary>
    public DateTime? ProcessedAt { get; init; }

    /// <summary>When the gateway rejected the attempt.</summary>
    public DateTime? FailedAt { get; init; }

    /// <summary>Bounded failure reason, when failed.</summary>
    public string? FailureReason { get; init; }

    /// <summary>Number of gateway execution attempts.</summary>
    public int AttemptCount { get; init; }

    /// <summary>Dispute that triggered this refund, when applicable.</summary>
    public Guid? DisputeId { get; init; }

    /// <summary>Maps an entity to the read model.</summary>
    public static RefundRecord From(Refund refund) => new()
    {
        RefundId = refund.Id,
        TenantId = refund.TenantId,
        PaymentId = refund.PaymentId,
        Amount = refund.Amount,
        Kind = refund.Kind,
        Currency = refund.Currency,
        Reason = refund.Reason,
        ReasonDetails = refund.ReasonDetails,
        Status = refund.Status,
        RequiresApproval = refund.RequiresApproval,
        RequestedByUserId = refund.RequestedByUserId,
        RequestedAt = refund.RequestedAt,
        DecidedByUserId = refund.DecidedByUserId,
        DecidedAt = refund.DecidedAt,
        DecisionNotes = refund.DecisionNotes,
        GatewayRefundId = refund.GatewayRefundId,
        GatewayName = refund.GatewayName,
        ProcessedAt = refund.ProcessedAt,
        FailedAt = refund.FailedAt,
        FailureReason = refund.FailureReason,
        AttemptCount = refund.AttemptCount,
        DisputeId = refund.DisputeId
    };
}
