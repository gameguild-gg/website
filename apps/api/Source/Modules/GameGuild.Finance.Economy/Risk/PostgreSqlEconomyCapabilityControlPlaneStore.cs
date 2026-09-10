using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Reserves;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Risk;

public interface ICapabilityPolicySignatureVerifier
{
    ValueTask<bool> VerifyAsync(
        string canonicalPayload,
        string keyId,
        string signature,
        CancellationToken cancellationToken);
}

public sealed class PostgreSqlEconomyCapabilityControlPlaneStore : IEconomyCapabilityControlPlaneStore
{
    private static readonly TimeSpan MaximumAnchorAge = TimeSpan.FromMinutes(5);
    private readonly DbContext _db;
    private readonly ICapabilityPolicySignatureVerifier _policySignatureVerifier;

    public PostgreSqlEconomyCapabilityControlPlaneStore(
        IApplicationDbContext context,
        ICapabilityPolicySignatureVerifier policySignatureVerifier)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(policySignatureVerifier);
        _db = context as DbContext
            ?? throw new InvalidOperationException(
                "Persistent Economy capability evaluation requires the application's relational DbContext.");
        _policySignatureVerifier = policySignatureVerifier;
    }

    public async ValueTask<EconomyCapabilityControlPlaneSnapshot> ReadSnapshotAsync(
        EconomyCapabilityEvaluationContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var now = context.EvaluatedAt;
        var policy = await _db.Set<EconomyCapabilityPolicyRow>()
            .AsNoTracking()
            .Where(row => row.IsActive &&
                          row.Capability == context.Capability &&
                          row.JurisdictionCode == context.JurisdictionCode &&
                          (row.TenantId == null || row.TenantId == context.TenantId))
            .OrderByDescending(row => row.TenantId == context.TenantId)
            .ThenByDescending(row => row.Version)
            .FirstOrDefaultAsync(cancellationToken);
        var policySignatureValid = policy is not null &&
                                   await _policySignatureVerifier.VerifyAsync(
                                       policy.CanonicalPayload,
                                       policy.KeyId,
                                       policy.Signature,
                                       cancellationToken);

        var complianceRows = await _db.Set<EconomyComplianceEvidenceRow>()
            .AsNoTracking()
            .Where(row => row.TenantId == context.TenantId && row.SubjectHash == context.SubjectReference)
            .ToArrayAsync(cancellationToken);
        string[] requiredComplianceKinds =
        [
            ComplianceEvidenceKinds.KycAml,
            ComplianceEvidenceKinds.FinancialCrime,
            ComplianceEvidenceKinds.TrustSafety
        ];
        var compliance = requiredComplianceKinds
            .Select(kind => complianceRows.Where(row => row.EvidenceKind == kind)
                .OrderByDescending(row => row.Version).FirstOrDefault())
            .ToArray();
        var complianceAvailable = compliance.All(row => row is not null && row.SignatureVerified);
        var complianceExpiresAt = complianceAvailable
            ? compliance.Select(row => row!.ExpiresAt).Min()
            : now;
        var riskDecision = await _db.Set<EconomyRiskDecisionRow>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                row => row.Id == context.RiskDecisionId &&
                       row.Outcome == RiskOutcome.Allow &&
                       row.OperationFingerprint == context.OperationFingerprint,
                cancellationToken);

        var chainHead = await _db.Set<EconomyChainHeadRow>()
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == 1, cancellationToken);
        var checkpoint = await _db.Set<EconomyJournalVerificationCheckpointRow>()
            .AsNoTracking()
            .OrderByDescending(row => row.ToSequence)
            .FirstOrDefaultAsync(cancellationToken);
        var ledgerHealthy = chainHead is not null && checkpoint is not null && checkpoint.IsValid &&
                            checkpoint.ToSequence == chainHead.Sequence &&
                            string.Equals(checkpoint.CurrentHash, chainHead.Hash, StringComparison.Ordinal);

        var projection = await _db.Set<EconomyProjectionGenerationRow>()
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.IsActive, cancellationToken);
        var projectionMatches = chainHead is not null && projection is not null &&
                                projection.ToSequence == chainHead.Sequence &&
                                projection.JournalHash == chainHead.Hash;

        var reserve = await _db.Set<EconomyReserveHeadRow>()
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.IsActive, cancellationToken);
        var reserveSufficient = reserve is not null && reserve.Coverage == ReserveCoverageState.Covered;
        var custody = reserve is null
            ? null
            : await _db.Set<EconomyCustodyReconciliationRow>()
                .AsNoTracking()
                .SingleOrDefaultAsync(row => row.ReserveVersion == reserve.Version, cancellationToken);
        var custodyObservationIds = custody is null
            ? []
            : JsonSerializer.Deserialize<Guid[]>(custody.ObservationIds) ?? [];
        var custodyObservations = custodyObservationIds.Length == 0
            ? []
            : await _db.Set<EconomyCustodyObservationRow>().AsNoTracking()
                .Where(row => custodyObservationIds.Contains(row.Id)).ToArrayAsync(cancellationToken);
        var custodyReconciled = custody?.IsReconciled == true &&
                                custodyObservations.Length == custodyObservationIds.Length &&
                                custodyObservations.All(row => row.ObservedAt <= now && row.ExpiresAt > now);

        var anchor = chainHead is null
            ? null
            : await _db.Set<EconomyExternalAnchorRow>()
                .AsNoTracking()
                .Where(row => row.JournalSequence == chainHead.Sequence && row.JournalHash == chainHead.Hash)
                .OrderByDescending(row => row.AnchoredAt)
                .FirstOrDefaultAsync(cancellationToken);
        var anchorVerification = anchor is null
            ? null
            : await _db.Set<EconomyAnchorVerificationRow>()
                .AsNoTracking()
                .Where(row => row.ExternalAnchorId == anchor.Id)
                .OrderByDescending(row => row.VerifiedAt)
                .FirstOrDefaultAsync(cancellationToken);
        var anchorValid = anchorVerification is not null &&
                          anchorVerification.SignatureValid &&
                          anchorVerification.ObjectMatches &&
                          anchorVerification.RetainUntil > now;

        var relevantKillSwitches = await _db.Set<EconomyKillSwitchRow>()
            .AsNoTracking()
            .Where(row => (row.TenantId == null || row.TenantId == context.TenantId) &&
                          (row.Capability == null || row.Capability == context.Capability))
            .ToListAsync(cancellationToken);
        var killSwitchEpoch = relevantKillSwitches.Count == 0
            ? 0
            : relevantKillSwitches.Max(row => row.Epoch);
        var complianceHold = await _db.Set<EconomyComplianceHoldRow>()
            .AsNoTracking()
            .Where(row => row.TenantId == context.TenantId &&
                          row.SubjectHash == context.SubjectReference &&
                          (row.Capability == null || row.Capability == context.Capability) &&
                          row.ReleasedAt == null &&
                          row.ActivatedAt <= now &&
                          row.ExpiresAt > now)
            .OrderByDescending(row => row.ActivatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var evidenceHashes = new[] { policy?.PayloadHash }
            .Concat(compliance.Where(value => value is not null).Select(value => value!.EvidenceHash))
            .Concat([reserve?.EvidenceHash, custody?.EvidenceHash, complianceHold?.EvidenceHash])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();

        return new EconomyCapabilityControlPlaneSnapshot(
            HasActivePolicy: policy is not null,
            PolicySignatureValid: policySignatureValid,
            PolicyVersion: policy?.Version ?? 0,
            PolicyExpiresAt: policy?.ExpiresAt ?? now,
            JurisdictionAllowed: policy is not null,
            ComplianceAvailable: complianceAvailable,
            ComplianceExpiresAt: complianceExpiresAt,
            ManualReviewRequired: !complianceAvailable ||
                                  compliance.Any(row => !string.Equals(row!.Result, "Approved", StringComparison.Ordinal)) ||
                                  riskDecision is null || complianceHold is not null,
            RiskDecisionId: riskDecision?.Id ?? Guid.Empty,
            LedgerHealthy: ledgerHealthy,
            ProjectionMatches: projectionMatches,
            ReserveSufficient: reserveSufficient,
            ReserveVersion: reserve?.Version ?? 0,
            ReserveExpiresAt: reserve?.ExpiresAt ?? now,
            CustodyReconciled: custodyReconciled,
            AnchorValid: anchorValid,
            AnchorExpiresAt: anchorVerification is null
                ? now
                : new[] { anchorVerification.VerifiedAt.Add(MaximumAnchorAge), anchorVerification.RetainUntil }.Min(),
            ProviderReady: policy?.ProviderReady == true,
            KillSwitchActive: relevantKillSwitches.Any(row => row.IsActive),
            KillSwitchEpoch: killSwitchEpoch)
        {
            EvidenceHashes = Array.AsReadOnly(evidenceHashes)
        };
    }

    public async ValueTask PersistReceiptAsync(
        CapabilityAuthorizationReceipt receipt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        _db.Set<EconomyCapabilityReceiptRow>().Add(new EconomyCapabilityReceiptRow
        {
            Id = receipt.Id,
            TenantId = receipt.TenantId,
            ActorId = receipt.ActorId,
            SubjectReference = receipt.SubjectReference,
            JurisdictionCode = receipt.JurisdictionCode,
            Capability = receipt.Capability,
            OperationFingerprint = receipt.OperationFingerprint,
            PolicyVersion = receipt.PolicyVersion,
            ReserveVersion = receipt.ReserveVersion,
            RiskDecisionId = receipt.RiskDecisionId,
            KillSwitchEpoch = receipt.KillSwitchEpoch,
            ProviderHash = receipt.ProviderHash,
            DestinationHash = receipt.DestinationHash,
            SourceRootHashes = JsonSerializer.Serialize(receipt.SourceRootHashes),
            EvidenceHashes = JsonSerializer.Serialize(receipt.EvidenceHashes),
            IssuedAt = receipt.IssuedAt,
            ExpiresAt = receipt.ExpiresAt,
            ReceiptHash = receipt.ReceiptHash,
            KeyId = receipt.KeyId,
            Signature = receipt.Signature
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask ConsumeAsync(
        Guid receiptId,
        string operationFingerprint,
        Guid tenantId,
        Guid actorId,
        long currentKillSwitchEpoch,
        DateTimeOffset consumedAt,
        CancellationToken cancellationToken)
    {
        var receiptRow = await _db.Set<EconomyCapabilityReceiptRow>()
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == receiptId, cancellationToken);
        if (receiptRow is null)
            throw new CapabilityReceiptConsumptionException("The capability receipt is not consumable.");

        var relevantKillSwitches = await _db.Set<EconomyKillSwitchRow>()
            .AsNoTracking()
            .Where(row => (row.TenantId == null || row.TenantId == tenantId) &&
                          (row.Capability == null || row.Capability == receiptRow.Capability))
            .ToArrayAsync(cancellationToken);
        var databaseKillSwitchEpoch = relevantKillSwitches.Length == 0
            ? 0
            : relevantKillSwitches.Max(row => row.Epoch);
        var activePolicy = await _db.Set<EconomyCapabilityPolicyRow>()
            .AsNoTracking()
            .AnyAsync(row => row.IsActive &&
                             row.Version == receiptRow.PolicyVersion &&
                             row.Capability == receiptRow.Capability &&
                             row.JurisdictionCode == receiptRow.JurisdictionCode &&
                             row.EffectiveAt <= consumedAt &&
                             row.ExpiresAt > consumedAt &&
                             (row.TenantId == null || row.TenantId == tenantId), cancellationToken);
        var activeReserve = await _db.Set<EconomyReserveHeadRow>()
            .AsNoTracking()
            .AnyAsync(row => row.IsActive &&
                             row.Version == receiptRow.ReserveVersion &&
                             row.Coverage == ReserveCoverageState.Covered &&
                             row.ObservedAt <= consumedAt &&
                             row.ExpiresAt > consumedAt, cancellationToken);
        var alreadyConsumed = await _db.Set<EconomyCapabilityReceiptConsumptionRow>()
            .AsNoTracking()
            .AnyAsync(row => row.ReceiptId == receiptId, cancellationToken);
        var activeComplianceHold = await _db.Set<EconomyComplianceHoldRow>()
            .AsNoTracking()
            .AnyAsync(row => row.TenantId == tenantId &&
                             row.SubjectHash == receiptRow.SubjectReference &&
                             (row.Capability == null || row.Capability == receiptRow.Capability) &&
                             row.ReleasedAt == null &&
                             row.ActivatedAt <= consumedAt &&
                             row.ExpiresAt > consumedAt,
                cancellationToken);
        var receipt = MapReceipt(receiptRow);
        var canonicalPayload = EconomyCapabilityEvaluator.Canonicalize(receipt);
        var canonicalHash = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPayload)));
        var signatureValid = canonicalHash == receipt.ReceiptHash &&
                             await _policySignatureVerifier.VerifyAsync(
                                 canonicalPayload, receipt.KeyId, receipt.Signature, cancellationToken);
        if (receipt.OperationFingerprint != operationFingerprint ||
            receipt.TenantId != tenantId ||
            receipt.ActorId != actorId ||
            receipt.KillSwitchEpoch != currentKillSwitchEpoch ||
            currentKillSwitchEpoch != databaseKillSwitchEpoch ||
            relevantKillSwitches.Any(row => row.IsActive) ||
            !activePolicy ||
            !activeReserve ||
            activeComplianceHold ||
            !signatureValid ||
            consumedAt < receipt.IssuedAt ||
            consumedAt >= receipt.ExpiresAt ||
            alreadyConsumed)
            throw new CapabilityReceiptConsumptionException("The capability receipt is not consumable.");

        _db.Set<EconomyCapabilityReceiptConsumptionRow>().Add(new EconomyCapabilityReceiptConsumptionRow
        {
            Id = Guid.NewGuid(),
            ReceiptId = receiptId,
            TenantId = tenantId,
            ActorId = actorId,
            OperationFingerprint = operationFingerprint,
            KillSwitchEpoch = currentKillSwitchEpoch,
            ConsumedAt = consumedAt
        });
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            throw new CapabilityReceiptConsumptionException(
                "The capability receipt was consumed concurrently.",
                exception);
        }
    }

    private static CapabilityAuthorizationReceipt MapReceipt(EconomyCapabilityReceiptRow row) => new(
        row.Id,
        row.TenantId,
        row.ActorId,
        row.SubjectReference,
        row.JurisdictionCode,
        row.Capability,
        row.OperationFingerprint,
        row.PolicyVersion,
        row.ReserveVersion,
        row.RiskDecisionId,
        row.KillSwitchEpoch,
        row.ProviderHash,
        row.DestinationHash,
        JsonSerializer.Deserialize<string[]>(row.SourceRootHashes) ?? [],
        JsonSerializer.Deserialize<string[]>(row.EvidenceHashes) ?? [],
        row.IssuedAt,
        row.ExpiresAt,
        row.ReceiptHash,
        row.KeyId,
        row.Signature);
}

public sealed class CapabilityReceiptConsumptionException : InvalidOperationException
{
    public CapabilityReceiptConsumptionException(string message) : base(message) { }

    public CapabilityReceiptConsumptionException(string message, Exception innerException)
        : base(message, innerException) { }
}
