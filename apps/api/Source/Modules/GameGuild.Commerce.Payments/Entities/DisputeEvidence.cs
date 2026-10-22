using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Payments;

/// <summary>Entity representing dispute evidence</summary>
[Table("dispute_evidence")]
[Index(nameof(DisputeId))]
[Index(nameof(EvidenceType))]
[Index(nameof(SubmittedAt))]
[Index(nameof(IsFromMerchant))]
public class DisputeEvidence : EntityBase
{
    /// <summary>Default constructor</summary>
    public DisputeEvidence() { }

    /// <summary>Constructor for partial initialization</summary>
    /// <param name="partial">Partial evidence data</param>
    public DisputeEvidence(object partial) : base(partial) { }

    /// <summary>Foreign key to the PaymentDispute entity</summary>
    [Required]
    public Guid DisputeId { get; set; }

    /// <summary>Navigation property to the PaymentDispute entity</summary>
    [ForeignKey(nameof(DisputeId))]
    public virtual PaymentDispute Dispute { get; set; } = null!;

    /// <summary>Evidence type</summary>
    public EvidenceType EvidenceType { get; set; }

    /// <summary>Evidence title</summary>
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Evidence description</summary>
    [MaxLength(2000)]
    public string? Description { get; set; }

    /// <summary>File URL</summary>
    [MaxLength(1000)]
    public string? FileUrl { get; set; }

    /// <summary>File name</summary>
    [MaxLength(255)]
    public string? FileName { get; set; }

    /// <summary>File size in bytes</summary>
    public long? FileSize { get; set; }

    /// <summary>MIME type</summary>
    [MaxLength(100)]
    public string? MimeType { get; set; }

    /// <summary>Submitted timestamp</summary>
    public DateTime SubmittedAt { get; set; } = SystemClock.UtcNow;

    /// <summary>Submitted by user ID</summary>
    [Required]
    public Guid SubmittedBy { get; set; }

    /// <summary>Whether this evidence is from the merchant</summary>
    public bool IsFromMerchant { get; set; }

    /// <summary>Metadata (JSON)</summary>
    [MaxLength(2000)]
    public string? Metadata { get; set; }

    /// <summary>
    ///     When this evidence row was included in a bundle successfully submitted to the gateway
    ///     (issue #419 evidence submission tracking); null while it has not been submitted. The
    ///     first submission wins so each row keeps the timestamp of when it reached the gateway.
    /// </summary>
    public DateTime? GatewaySubmittedAt { get; set; }
}
