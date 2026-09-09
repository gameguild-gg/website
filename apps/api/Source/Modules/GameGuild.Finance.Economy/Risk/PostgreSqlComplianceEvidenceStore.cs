using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Finance.Economy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Risk;

public enum ComplianceEvidenceResult
{
    Approved = 1,
    Rejected = 2,
    NeedsReview = 3,
    Unavailable = 4
}

public enum ComplianceEvidenceIngestionStatus
{
    Published = 1,
    Duplicate = 2,
    Deferred = 3,
    Rejected = 4
}

public sealed record ComplianceEvidenceEnvelope(
    string Provider,
    string Environment,
    string ProviderEventId,
    Guid TenantId,
    string SubjectHash,
    long Version,
    ComplianceEvidenceResult Result,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    long PolicyVersion,
    string PayloadHash,
    bool SignatureVerified,
    string RawObjectReference,
    string EvidenceHash,
    DateTimeOffset ReceivedAt)
{
    public string EvidenceKind { get; init; } = ComplianceEvidenceKinds.KycAml;
    public string? JurisdictionCode { get; init; }

    public static ComplianceEvidenceEnvelope Create(
        string provider,
        string environment,
        string providerEventId,
        Guid tenantId,
        string subjectHash,
        long version,
        ComplianceEvidenceResult result,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        long policyVersion,
        string payloadHash,
        bool signatureVerified,
        string rawObjectReference,
        DateTimeOffset receivedAt,
        string? jurisdictionCode = null)
    {
        var normalizedJurisdiction = string.IsNullOrWhiteSpace(jurisdictionCode)
            ? null
            : EconomyJurisdictionCode.Require(jurisdictionCode, nameof(jurisdictionCode));
        var canonical = string.Join(
            '\n',
            provider,
            environment,
            providerEventId,
            tenantId.ToString("N"),
            subjectHash,
            version.ToString(CultureInfo.InvariantCulture),
            ((int)result).ToString(CultureInfo.InvariantCulture),
            issuedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            expiresAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            policyVersion.ToString(CultureInfo.InvariantCulture),
            payloadHash,
            signatureVerified.ToString(CultureInfo.InvariantCulture),
            rawObjectReference,
            receivedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            normalizedJurisdiction ?? string.Empty);
        var evidenceHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        return new ComplianceEvidenceEnvelope(
            provider,
            environment,
            providerEventId,
            tenantId,
            subjectHash,
            version,
            result,
            issuedAt,
            expiresAt,
            policyVersion,
            payloadHash,
            signatureVerified,
            rawObjectReference,
            evidenceHash,
            receivedAt)
        {
            JurisdictionCode = normalizedJurisdiction
        };
    }
}

public sealed record ComplianceEvidenceIngestionResult(
    ComplianceEvidenceIngestionStatus Status,
    Guid? EvidenceId);

public interface IComplianceEvidenceStore
{
    ValueTask<ComplianceEvidenceIngestionResult> IngestAsync(
        ComplianceEvidenceEnvelope envelope,
        CancellationToken cancellationToken);
}

public static class ComplianceEvidenceKinds
{
    public const string KycAml = "kyc-aml";
    public const string FinancialCrime = "financial-crime";
    public const string TrustSafety = "trust-safety";
}

public sealed record DurableComplianceEvidence(
    string Provider,
    string Environment,
    string ProviderEventId,
    Guid TenantId,
    string SubjectHash,
    string EvidenceKind,
    long Version,
    ComplianceEvidenceResult Result,
    long PolicyVersion,
    string EvidenceHash,
    bool SignatureVerified,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt)
{
    public string? JurisdictionCode { get; init; }
}

public interface IComplianceEvidenceReader
{
    ValueTask<DurableComplianceEvidence?> ReadLatestAsync(
        Guid tenantId,
        string subjectHash,
        string evidenceKind,
        CancellationToken cancellationToken);
}

public sealed class UnavailableComplianceEvidenceReader : IComplianceEvidenceReader
{
    public ValueTask<DurableComplianceEvidence?> ReadLatestAsync(
        Guid tenantId,
        string subjectHash,
        string evidenceKind,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceKind);
        return ValueTask.FromResult<DurableComplianceEvidence?>(null);
    }
}

public sealed class PostgreSqlComplianceEvidenceStore : IComplianceEvidenceStore, IComplianceEvidenceReader
{
    private readonly DbContext _db;

    public PostgreSqlComplianceEvidenceStore(IApplicationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _db = context as DbContext
            ?? throw new InvalidOperationException(
                "Persistent compliance evidence requires the application's relational DbContext.");
    }

    public async ValueTask<ComplianceEvidenceIngestionResult> IngestAsync(
        ComplianceEvidenceEnvelope envelope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ValidateEnvelope(envelope);
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async _ =>
        {
        var existingInbox = await _db.Set<EconomyComplianceInboxRow>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                row => row.Provider == envelope.Provider &&
                       row.Environment == envelope.Environment &&
                       row.ProviderEventId == envelope.ProviderEventId,
                cancellationToken);
        if (existingInbox is not null)
        {
            if (!string.Equals(existingInbox.PayloadHash, envelope.PayloadHash, StringComparison.Ordinal))
                throw new ComplianceEvidenceConflictException(
                    "The provider event was replayed with a different payload hash.");

            var existingEvidence = await _db.Set<EconomyComplianceEvidenceRow>()
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    row => row.Provider == envelope.Provider &&
                           row.Environment == envelope.Environment &&
                           row.ProviderEventId == envelope.ProviderEventId,
                    cancellationToken);
            var duplicate = new ComplianceEvidenceIngestionResult(
                ComplianceEvidenceIngestionStatus.Duplicate,
                existingEvidence?.Id);
            return duplicate;
        }
        var inbox = new EconomyComplianceInboxRow
        {
            Id = Guid.NewGuid(),
            Provider = envelope.Provider,
            Environment = envelope.Environment,
            ProviderEventId = envelope.ProviderEventId,
            PayloadHash = envelope.PayloadHash,
            RawObjectReference = envelope.RawObjectReference,
            ReceivedAt = envelope.ReceivedAt
        };
        _db.Set<EconomyComplianceInboxRow>().Add(inbox);

        if (!envelope.SignatureVerified || envelope.ExpiresAt <= envelope.IssuedAt)
        {
            inbox.ProcessingError = "invalid-signature-or-lifetime";
            await _db.SaveChangesAsync(cancellationToken);
            return new ComplianceEvidenceIngestionResult(ComplianceEvidenceIngestionStatus.Rejected, null);
        }

        var latest = await _db.Set<EconomyComplianceEvidenceRow>()
            .AsNoTracking()
            .Where(row => row.TenantId == envelope.TenantId && row.SubjectHash == envelope.SubjectHash &&
                          row.EvidenceKind == envelope.EvidenceKind)
            .OrderByDescending(row => row.Version)
            .FirstOrDefaultAsync(cancellationToken);
        var latestVersion = latest?.Version ?? 0;
        if (envelope.Version != latestVersion + 1 ||
            latest is not null && envelope.IssuedAt <= latest.IssuedAt)
        {
            inbox.ProcessingError = envelope.Version != latestVersion + 1
                ? "out-of-order-version"
                : "out-of-order-issued-at";
            await _db.SaveChangesAsync(cancellationToken);
            return new ComplianceEvidenceIngestionResult(ComplianceEvidenceIngestionStatus.Deferred, null);
        }

        var evidenceId = Guid.NewGuid();
        _db.Set<EconomyComplianceEvidenceRow>().Add(new EconomyComplianceEvidenceRow
        {
            Id = evidenceId,
            Provider = envelope.Provider,
            Environment = envelope.Environment,
            ProviderEventId = envelope.ProviderEventId,
            TenantId = envelope.TenantId,
            SubjectHash = envelope.SubjectHash,
            EvidenceKind = envelope.EvidenceKind,
            JurisdictionCode = envelope.JurisdictionCode,
            Version = envelope.Version,
            Result = envelope.Result.ToString(),
            PolicyVersion = envelope.PolicyVersion,
            PayloadHash = envelope.PayloadHash,
            SignatureVerified = envelope.SignatureVerified,
            RawObjectReference = envelope.RawObjectReference,
            EvidenceHash = envelope.EvidenceHash,
            IssuedAt = envelope.IssuedAt,
            ExpiresAt = envelope.ExpiresAt,
            ReceivedAt = envelope.ReceivedAt
        });
        var outboxPayload = JsonSerializer.Serialize(new
        {
            EvidenceId = evidenceId,
            envelope.TenantId,
            envelope.SubjectHash,
            envelope.EvidenceKind,
            envelope.Version,
            Result = envelope.Result.ToString(),
            envelope.EvidenceHash
        });
        _db.Set<EconomyComplianceOutboxRow>().Add(new EconomyComplianceOutboxRow
        {
            Id = Guid.NewGuid(),
            EvidenceId = evidenceId,
            Type = "economy.compliance-evidence.published.v1",
            Payload = outboxPayload,
            PayloadHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(outboxPayload))).ToLowerInvariant(),
            OccurredAt = envelope.ReceivedAt
        });
        inbox.ProcessedAt = envelope.ReceivedAt;
        await _db.SaveChangesAsync(cancellationToken);
        return new ComplianceEvidenceIngestionResult(ComplianceEvidenceIngestionStatus.Published, evidenceId);
        }, cancellationToken);
    }

    internal static void ValidateEnvelope(ComplianceEvidenceEnvelope envelope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(envelope.Provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(envelope.Environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(envelope.ProviderEventId);
        if (envelope.TenantId == Guid.Empty)
            throw new ArgumentException("Compliance evidence tenant cannot be empty.", nameof(envelope));
        ArgumentException.ThrowIfNullOrWhiteSpace(envelope.SubjectHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(envelope.EvidenceKind);
        if (envelope.Version <= 0)
            throw new ArgumentOutOfRangeException(nameof(envelope), "Compliance evidence version must be positive.");
        if (envelope.PolicyVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(envelope), "Compliance policy version must be positive.");
        if (envelope.JurisdictionCode is not null &&
            EconomyJurisdictionCode.NormalizeOptional(envelope.JurisdictionCode) != envelope.JurisdictionCode)
            throw new ArgumentException("Compliance evidence jurisdiction is invalid.", nameof(envelope));
        if (envelope.EvidenceKind == ComplianceEvidenceKinds.KycAml &&
            envelope.Result == ComplianceEvidenceResult.Approved &&
            envelope.JurisdictionCode is null)
            throw new ArgumentException(
                "Approved KYC/AML evidence requires a verified jurisdiction.", nameof(envelope));
        ArgumentException.ThrowIfNullOrWhiteSpace(envelope.PayloadHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(envelope.RawObjectReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(envelope.EvidenceHash);
        if (envelope.ReceivedAt < envelope.IssuedAt)
            throw new ArgumentException("Compliance evidence cannot be received before it was issued.", nameof(envelope));
    }

    public async ValueTask<DurableComplianceEvidence?> ReadLatestAsync(
        Guid tenantId,
        string subjectHash,
        string evidenceKind,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceKind);
        var row = await _db.Set<EconomyComplianceEvidenceRow>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.SubjectHash == subjectHash &&
                           item.EvidenceKind == evidenceKind)
            .OrderByDescending(item => item.Version)
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null) return null;
        if (!Enum.TryParse<ComplianceEvidenceResult>(row.Result, out var result))
            result = ComplianceEvidenceResult.Unavailable;
        return new DurableComplianceEvidence(
            row.Provider, row.Environment, row.ProviderEventId, row.TenantId, row.SubjectHash,
            row.EvidenceKind, row.Version, result, row.PolicyVersion, row.EvidenceHash,
            row.SignatureVerified, row.IssuedAt, row.ExpiresAt)
        {
            JurisdictionCode = row.JurisdictionCode
        };
    }
}

public sealed class ComplianceEvidenceConflictException(string message) : InvalidOperationException(message);
