
namespace GameGuild.Compliance.Audit;

/// <summary>
/// Tamper-evident audit log entry with cryptographic hash chain for immutability (WORM - Write Once Read Many).
/// Each entry includes a hash of its content plus the previous entry's hash, forming an immutable chain.
/// </summary>
public sealed class TamperEvidentAuditLog : EntityBase {
    public Guid? UserId { get; private set; }
    public Guid? SessionId { get; private set; }
    public string? CorrelationId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public Guid? EntityId { get; private set; }
    public string? BeforeSnapshot { get; private set; }
    public string? AfterSnapshot { get; private set; }
    public string Changes { get; private set; } = string.Empty;
    public string RiskLevel { get; private set; } = "Low";
    public string IpAddress { get; private set; } = string.Empty;
    public string UserAgent { get; private set; } = string.Empty;
    public string? Country { get; private set; }
    public string? Region { get; private set; }
    public string? City { get; private set; }
    public DateTime Timestamp { get; private set; }

    // Cryptographic integrity fields
    public string ContentHash { get; private set; } = string.Empty;
    public string PreviousHash { get; private set; } = string.Empty;
    public string ChainHash { get; private set; } = string.Empty;
    public long SequenceNumber { get; private set; }
    public string DigitalSignature { get; private set; } = string.Empty;
    public string SigningKeyId { get; private set; } = string.Empty;
    public DateTime SignedAt { get; private set; }

    // Verification status
    public bool IsVerified { get; private set; }
    public DateTime? LastVerifiedAt { get; private set; }
    public string? VerificationNotes { get; private set; }

    // Chain-of-custody
    public string? CustodyChain { get; private set; }
    public string? EvidencePackageId { get; private set; }
    public bool IsPartOfEvidence { get; private set; }

    // SIEM integration
    public bool ForwardedToSiem { get; private set; }
    public DateTime? ForwardedAt { get; private set; }
    public string? SiemCorrelationId { get; private set; }

    private TamperEvidentAuditLog() { }

    public static TamperEvidentAuditLog Create(
        Guid tenantId,
        Guid? userId,
        string action,
        string entityType,
        Guid? entityId,
        string? beforeSnapshot,
        string? afterSnapshot,
        string changes,
        string riskLevel,
        string ipAddress,
        string userAgent,
        string? country,
        string? region,
        string? city,
        string previousHash,
        long sequenceNumber)
        => Create(
            tenantId, userId, action, entityType, entityId, beforeSnapshot, afterSnapshot,
            changes, riskLevel, ipAddress, userAgent, country, region, city,
            previousHash, sequenceNumber, null, null);

    public static TamperEvidentAuditLog Create(
        Guid tenantId,
        Guid? userId,
        string action,
        string entityType,
        Guid? entityId,
        string? beforeSnapshot,
        string? afterSnapshot,
        string changes,
        string riskLevel,
        string ipAddress,
        string userAgent,
        string? country,
        string? region,
        string? city,
        string previousHash,
        long sequenceNumber,
        Guid? sessionId,
        string? correlationId) {
        return new TamperEvidentAuditLog {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            SessionId = sessionId,
            CorrelationId = correlationId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            BeforeSnapshot = beforeSnapshot,
            AfterSnapshot = afterSnapshot,
            Changes = changes,
            RiskLevel = riskLevel,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Country = country,
            Region = region,
            City = city,
            Timestamp = NormalizeStorageTimestamp(SystemClock.UtcNow),
            PreviousHash = previousHash,
            SequenceNumber = sequenceNumber,
            IsVerified = false,
            ForwardedToSiem = false,
            IsPartOfEvidence = false
        };
    }

    private static DateTime NormalizeStorageTimestamp(DateTime value) => new(value.Ticks - value.Ticks % 10, DateTimeKind.Utc);

    public void SetCryptographicHashes(string contentHash, string chainHash) {
        ContentHash = contentHash;
        ChainHash = chainHash;
        UpdatedAt = SystemClock.UtcNow;
    }

    public void Sign(string digitalSignature, string signingKeyId) {
        DigitalSignature = digitalSignature;
        SigningKeyId = signingKeyId;
        SignedAt = SystemClock.UtcNow;
    }

    public void MarkAsVerified(string? notes = null) {
        IsVerified = true;
        LastVerifiedAt = SystemClock.UtcNow;
        VerificationNotes = notes;
    }

    public void RecordCustody(string custodyEntry) {
        var currentChain = string.IsNullOrEmpty(CustodyChain) ? "" : CustodyChain + " → ";
        CustodyChain = currentChain + custodyEntry;
    }

    public void MarkAsEvidence(string packageId) {
        EvidencePackageId = packageId;
        IsPartOfEvidence = true;
    }

    public void MarkAsForwardedToSiem(string correlationId) {
        ForwardedToSiem = true;
        ForwardedAt = SystemClock.UtcNow;
        SiemCorrelationId = correlationId;
    }

    public bool VerifyChain(string expectedPreviousHash) {
        return PreviousHash == expectedPreviousHash;
    }

    public bool VerifyContentHash(string computedHash) {
        return ContentHash == computedHash;
    }
}
