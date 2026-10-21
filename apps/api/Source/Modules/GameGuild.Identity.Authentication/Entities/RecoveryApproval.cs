namespace GameGuild.Identity.Authentication;

/// <summary>
///     One approval factor issued for a recovery request (issue #209): either a
///     single-use recovery code mailed to a confirmed trusted contact, or a passkey
///     assertion slot for an enrolled WebAuthn credential. Codes are stored as
///     SHA-256 hex digests only and are consumed on first redemption.
/// </summary>
public sealed class RecoveryApproval
{
    public Guid Id { get; set; }

    public Guid RecoveryRequestId { get; set; }

    /// <summary>Denormalized owner for tenant isolation and audit queries.</summary>
    public Guid OwnerUserId { get; set; }

    /// <summary>Denormalized tenant of the recovered account.</summary>
    public Guid TenantId { get; set; }

    public RecoveryApprovalFactor Factor { get; set; } = RecoveryApprovalFactor.TrustedContact;

    /// <summary>The trusted contact this code was issued to; null for passkey approvals.</summary>
    public Guid? TrustedContactId { get; set; }

    /// <summary>Hex digest of the contact's recovery code; null for passkey approvals.</summary>
    public string? ApprovalCodeHash { get; set; }

    /// <summary>The WebAuthn credential that produced (or may produce) this approval; null for contacts.</summary>
    public Guid? WebAuthnCredentialId { get; set; }

    /// <summary>When the code (or passkey slot) was issued for the request.</summary>
    public DateTimeOffset IssuedAt { get; set; }

    /// <summary>When the proof was redeemed; null until then (single use).</summary>
    public DateTimeOffset? ApprovedAt { get; set; }

    /// <summary>Observability-only redemption context; never used for authorization.</summary>
    public string? ApproverIpAddress { get; set; }

    /// <summary>Observability-only redemption context; never used for authorization.</summary>
    public string? ApproverUserAgent { get; set; }
}
