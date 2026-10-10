
namespace GameGuild.Compliance.Audit;

/// <summary>
/// Service for managing tamper-evident audit logs with cryptographic hash chains (WORM storage).
/// </summary>
public interface ITamperEvidentAuditService {
    Task<Result<TamperEvidentAuditLog>> CreateAuditLogAsync(
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
        string userAgent);
    Task<Result<TamperEvidentAuditLog>> CreateAuditLogAsync(
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
        CancellationToken cancellationToken);
    Task<Result<TamperEvidentAuditLog>> CreateAuditLogAsync(
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
        AuditEventMetadata metadata,
        CancellationToken cancellationToken);

    Task<Result<bool>> VerifyChainIntegrityAsync(Guid tenantId);
    Task<Result<bool>> VerifyChainIntegrityAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<Result<TamperEvidentAuditLog>> GetByIdAsync(Guid id);
    Task<Result<TamperEvidentAuditLog>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<IEnumerable<TamperEvidentAuditLog>>> GetByTenantAsync(Guid tenantId);
    Task<Result<IEnumerable<TamperEvidentAuditLog>>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<Result<IEnumerable<TamperEvidentAuditLog>>> GetByTenantAsync(Guid tenantId, int skip, int take);
    Task<Result<IEnumerable<TamperEvidentAuditLog>>> GetByTenantAsync(Guid tenantId, int skip, int take, CancellationToken cancellationToken);
    Task<Result<IEnumerable<TamperEvidentAuditLog>>> GetUnverifiedAsync(Guid tenantId);
    Task<Result<IEnumerable<TamperEvidentAuditLog>>> GetUnverifiedAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<Result> MarkAsVerifiedAsync(Guid id);
    Task<Result> MarkAsVerifiedAsync(Guid id, CancellationToken cancellationToken);
    Task<Result> MarkAsVerifiedAsync(Guid id, string? notes);
    Task<Result> MarkAsVerifiedAsync(Guid id, string? notes, CancellationToken cancellationToken);
}

/// <summary>
/// Optional location and correlation fields associated with an audit event.
/// </summary>
public sealed record AuditEventMetadata
{
    public string? Country { get; init; }
    public string? Region { get; init; }
    public string? City { get; init; }
    public Guid? SessionId { get; init; }
    public string? CorrelationId { get; init; }
}

/// <summary>
/// Service for cryptographic signing and integrity verification of audit records.
/// Uses RSA/ECDSA digital signatures with SHA-256 hashing.
/// </summary>
public interface ICryptographicSigningService {
    string GetActiveKeyId();
    string ComputeContentHash(string content);
    string ComputeChainHash(string contentHash, string previousHash, long sequenceNumber);
    string SignData(string data, string keyId);
    bool VerifySignature(string data, string signature, string keyId);
    Task<Result<string>> GetPublicKeyAsync(string keyId);
    Task<Result<string>> GetPublicKeyAsync(string keyId, CancellationToken cancellationToken);
    Task<Result> RotateSigningKeyAsync(string newKeyId);
    Task<Result> RotateSigningKeyAsync(string newKeyId, CancellationToken cancellationToken);
}

/// <summary>
/// Service for field-level data access auditing with PII masking and redaction.
/// </summary>
public interface IFieldAccessAuditService {
    Task<Result<FieldAccessAudit>> RecordFieldAccessAsync(
        Guid tenantId,
        Guid userId,
        string entityType,
        Guid entityId,
        string fieldName,
        FieldAccessType accessType,
        string? oldValue,
        string? newValue,
        bool isSensitive,
        SensitivityLevel sensitivityLevel,
        string ipAddress,
        string userAgent,
        CancellationToken cancellationToken = default);

    string MaskSensitiveData(string? value, SensitivityLevel level);
    string RedactPii(string content);
    Task<Result<IEnumerable<FieldAccessAudit>>> GetFieldAccessHistoryAsync(Guid entityId, string? fieldName = null, CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<FieldAccessAudit>>> GetSensitiveFieldAccessesAsync(Guid tenantId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for compliance evidence packaging (SOC2, ISO 27001, GDPR, HIPAA).
/// Creates tamper-evident packages with digital signatures for regulatory submissions.
/// </summary>
public interface ICompliancePackagingService {
    Task<Result<ComplianceEvidencePackage>> CreatePackageAsync(
        Guid tenantId,
        string packageName,
        ComplianceFramework framework,
        DateTime periodStart,
        DateTime periodEnd,
        string preparedBy,
        CancellationToken cancellationToken = default);

    Task<Result> AddAuditLogsToPackageAsync(Guid packageId, IEnumerable<Guid> auditLogIds, CancellationToken cancellationToken = default);
    Task<Result> AddAnomaliesAsync(Guid packageId, IEnumerable<Guid> anomalyIds, CancellationToken cancellationToken = default);
    Task<Result> SignPackageAsync(Guid packageId, CancellationToken cancellationToken = default);
    Task<Result> ReviewPackageAsync(Guid packageId, string reviewedBy, string? notes = null, CancellationToken cancellationToken = default);
    Task<Result> ApprovePackageAsync(Guid packageId, string approvedBy, CancellationToken cancellationToken = default);
    Task<Result<Stream>> ExportPackageAsync(Guid packageId, CancellationToken cancellationToken = default);
    Task<Result> DeliverPackageAsync(Guid packageId, string deliveryMethod, string deliveredTo, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for forwarding audit events to SIEM systems (Splunk, ELK, Azure Sentinel).
/// Supports multiple SIEM integrations with batching and retry logic.
/// </summary>
public interface ISiemIntegrationService {
    Task<Result> ForwardToSiemAsync(TamperEvidentAuditLog auditLog, string siemType, CancellationToken cancellationToken = default);
    Task<Result> ForwardBatchAsync(IEnumerable<TamperEvidentAuditLog> auditLogs, string siemType, CancellationToken cancellationToken = default);
    Task<Result<bool>> TestConnectionAsync(string siemType, CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<TamperEvidentAuditLog>>> GetPendingForwardsAsync(string? siemType = null, CancellationToken cancellationToken = default);
    Task<Result> RetryFailedForwardsAsync(CancellationToken cancellationToken = default);
}

public sealed record ChainVerificationResult(
    bool IsValid,
    int TotalLogs,
    int VerifiedLogs,
    int FailedLogs,
    List<string> Errors);
