namespace GameGuild.Compliance.Audit;

/// <summary>
///     A preserved evidence item under formal chain of custody (issue #177). Preservation copies
///     the source content (audit log, security event, tamper-evident chain entry, value snapshot,
///     or raw bytes) with a SHA-256 integrity hash so later verification recomputes and compares.
///     Items are append-only: custody advances through <see cref="CustodyEvent"/> rows and the
///     custody state fields, never by rewriting history.
/// </summary>
public sealed class CustodyEvidenceItem : EntityBase
{
    /// <summary>Source system kind: AuditLog, SecurityEvent, TamperEvidentAuditLog, MutationValueSnapshot, ComplianceSealedPackage, or RawContent.</summary>
    public required string SourceKind { get; set; }

    /// <summary>Identifier of the source record inside its system ("inline" for RawContent).</summary>
    public required string SourceId { get; set; }

    /// <summary>MIME/media type of the preserved content.</summary>
    public string ContentType { get; set; } = "application/json";

    /// <summary>The preserved forensic copy of the evidence content.</summary>
    public byte[] Content { get; set; } = [];

    /// <summary>SHA-256 over <see cref="Content"/> at preservation time — the integrity anchor.</summary>
    public required string ContentSha256 { get; set; }

    public Guid CollectedByUserId { get; set; }

    public DateTime CollectedAtUtc { get; set; }

    /// <summary>Custody state: InCustody, TransferPending, or Released.</summary>
    public string CustodyState { get; set; } = "InCustody";

    /// <summary>The custodian currently holding the evidence.</summary>
    public Guid CurrentCustodianId { get; set; }

    /// <summary>Set when the tamper-evident seal was deliberately broken for inspection; broken-seal evidence requires forensic-scope access.</summary>
    public DateTime? SealBrokenAtUtc { get; set; }

    /// <summary>Legal hold currently preserving this item, when any.</summary>
    public Guid? LegalHoldId { get; set; }

    /// <summary>Isolation marker: a sealed item may only be accessed through the custody-gated path.</summary>
    public bool Isolated { get; set; } = true;
}

/// <summary>
///     Append-only custody chain entry (issue #177): collection, seal breaks, accesses, transfer
///     initiations, acknowledgments, and releases are recorded as sequenced events per evidence
///     item. Transfer events carry the cryptographic proof of handoff.
/// </summary>
public sealed class CustodyEvent : EntityBase
{
    public Guid EvidenceId { get; set; }

    /// <summary>Per-evidence sequence number; the chain must be gapless from 1 upward.</summary>
    public int SequenceNumber { get; set; }

    /// <summary>Collected, Accessed, SealBroken, TransferInitiated, TransferAcknowledged, or Released.</summary>
    public required string EventType { get; set; }

    public Guid? FromCustodianId { get; set; }

    public Guid? ToCustodianId { get; set; }

    public Guid ActorUserId { get; set; }

    public string? Purpose { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    /// <summary>Id of the transfer this event belongs to, when applicable.</summary>
    public Guid? TransferId { get; set; }

    /// <summary>Cryptographic proof JSON for transfer events (challenge, hashes, signature, key id).</summary>
    public string? ProofJson { get; set; }
}

/// <summary>
///     Custodian authorization registry (issue #177): only registered, active custodians may
///     access, handle, transfer, acknowledge, release, or certify evidence. Authorization fails
///     closed — an unknown or revoked custodian is denied, and every decision is logged.
/// </summary>
public sealed class EvidenceCustodian : EntityBase
{
    /// <summary>The user authorized to act as a custodian.</summary>
    public Guid UserId { get; set; }

    public required string DisplayName { get; set; }

    /// <summary>JSON array of granted scopes: access, handle, transfer, acknowledge, release, certify, forensic.</summary>
    public string ScopesJson { get; set; } = "[]";

    public Guid AuthorizedByUserId { get; set; }

    public DateTime AuthorizedAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public Guid? RevokedByUserId { get; set; }

    public string? RevocationReason { get; set; }

    public bool IsActive => RevokedAtUtc is null;
}

/// <summary>
///     A digital evidence transfer with cryptographic proof of proper handoff (issue #177):
///     initiated by the current custodian, verified against a challenge nonce, and completed only
///     when the receiving custodian acknowledges with their own signature.
/// </summary>
public sealed class EvidenceTransfer : EntityBase
{
    public Guid EvidenceId { get; set; }

    public Guid FromCustodianId { get; set; }

    public Guid ToCustodianId { get; set; }

    public Guid InitiatedByUserId { get; set; }

    public DateTime InitiatedAtUtc { get; set; }

    /// <summary>Random challenge nonce binding the proof to this transfer.</summary>
    public required string ChallengeNonce { get; set; }

    /// <summary>SHA-256 of the evidence content, embedded in the proof.</summary>
    public required string EvidenceHash { get; set; }

    /// <summary>Canonical proof document JSON (hashes + custodians + nonce + timestamp + signature).</summary>
    public required string ProofJson { get; set; }

    /// <summary>ECDSA signature over the canonical proof payload.</summary>
    public required string Signature { get; set; }

    public string SigningKeyId { get; set; } = string.Empty;

    /// <summary>Transfer state: PendingAcknowledgment, Acknowledged, or Cancelled.</summary>
    public string State { get; set; } = "PendingAcknowledgment";

    public DateTime? AcknowledgedAtUtc { get; set; }

    public Guid? AcknowledgedByUserId { get; set; }

    /// <summary>Signature by the receiving custodian over the acknowledged proof.</summary>
    public string? AcknowledgmentSignature { get; set; }

    public string? Purpose { get; set; }
}

/// <summary>
///     A legal hold (issue #177): while active it preserves the in-scope evidence under custody,
///     blocks release, and carries the automated stakeholder notification workflow.
/// </summary>
public sealed class LegalHold : EntityBase
{
    public required string CaseNumber { get; set; }

    public string? Description { get; set; }

    /// <summary>JSON array of preservation targets ({sourceKind, sourceId}) captured when the hold was created.</summary>
    public string PreservationTargetsJson { get; set; } = "[]";

    /// <summary>JSON array of notification targets ({kind: custodian|stakeholder, target}).</summary>
    public string NotificationTargetsJson { get; set; } = "[]";

    public Guid CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Active or Released.</summary>
    public string Status { get; set; } = "Active";

    public DateTime? ReleasedAtUtc { get; set; }

    public Guid? ReleasedByUserId { get; set; }

    public string? ReleaseReason { get; set; }
}

/// <summary>One stakeholder/custodian notification produced by the legal-hold workflow (issue #177).</summary>
public sealed class LegalHoldNotification : EntityBase
{
    public Guid LegalHoldId { get; set; }

    /// <summary>Custodian or Stakeholder.</summary>
    public required string TargetKind { get; set; }

    /// <summary>User id (custodian) or address/identifier (stakeholder).</summary>
    public required string Target { get; set; }

    /// <summary>Pending, Sent, or Failed.</summary>
    public string Status { get; set; } = "Pending";

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? DispatchedAtUtc { get; set; }

    public string? FailureReason { get; set; }
}

/// <summary>
///     Isolation-enforced access log (issue #177): every attempt to access evidence — allowed or
///     denied — is recorded with the decision reason, providing the handling log required for
///     admissible chain-of-custody documentation.
/// </summary>
public sealed class CustodyAccessLog : EntityBase
{
    public Guid EvidenceId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Allowed or Denied.</summary>
    public required string Decision { get; set; }

    public required string Reason { get; set; }

    public string? Purpose { get; set; }

    public DateTime OccurredAtUtc { get; set; }
}
