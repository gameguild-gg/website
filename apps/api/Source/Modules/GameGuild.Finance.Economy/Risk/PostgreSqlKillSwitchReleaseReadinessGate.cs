using System.Text.Json;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Reserves;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Risk;

/// <summary>
/// Proves that the shared Economy control plane is healthy before an operator can release containment.
/// Transaction-specific compliance and risk are intentionally re-evaluated by the capability evaluator;
/// this gate never authorizes value movement on its own.
/// </summary>
public sealed class PostgreSqlKillSwitchReleaseReadinessGate : IKillSwitchReleaseReadinessGate
{
    private static readonly TimeSpan MaximumAnchorAge = TimeSpan.FromMinutes(5);
    private readonly DbContext _db;
    private readonly ICapabilityPolicySignatureVerifier _policySignatureVerifier;
    private readonly TimeProvider _timeProvider;

    public PostgreSqlKillSwitchReleaseReadinessGate(
        IApplicationDbContext context,
        ICapabilityPolicySignatureVerifier policySignatureVerifier,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(policySignatureVerifier);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _db = context as DbContext
            ?? throw new InvalidOperationException(
                "Persistent kill-switch readiness requires the application's relational DbContext.");
        _policySignatureVerifier = policySignatureVerifier;
        _timeProvider = timeProvider;
    }

    public async ValueTask<bool> IsReadyAsync(
        EconomyKillSwitchScope scope,
        CancellationToken cancellationToken)
    {
        ValidateScope(scope);
        var now = _timeProvider.GetUtcNow();

        var chainHeads = await _db.Set<EconomyChainHeadRow>().AsNoTracking().ToArrayAsync(cancellationToken);
        if (chainHeads.Length != 1) return false;
        var chainHead = chainHeads[0];

        var checkpoint = await _db.Set<EconomyJournalVerificationCheckpointRow>().AsNoTracking()
            .OrderByDescending(row => row.ToSequence)
            .ThenByDescending(row => row.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (checkpoint is null || !checkpoint.IsValid || checkpoint.FailureCode is not null ||
            checkpoint.ToSequence != chainHead.Sequence ||
            !string.Equals(checkpoint.CurrentHash, chainHead.Hash, StringComparison.Ordinal))
            return false;

        var projections = await _db.Set<EconomyProjectionGenerationRow>().AsNoTracking()
            .Where(row => row.IsActive)
            .ToArrayAsync(cancellationToken);
        if (projections.Length != 1) return false;
        var projection = projections[0];
        if (projection.ToSequence != chainHead.Sequence ||
            !string.Equals(projection.JournalHash, chainHead.Hash, StringComparison.Ordinal) ||
            projection.MismatchCount != 0 ||
            !string.Equals(projection.State, "Active", StringComparison.Ordinal) ||
            projection.CompletedAt is null || projection.ActivatedAt is null ||
            projection.ApprovedBy is null || projection.SecondApprovedBy is null ||
            projection.ProposedBy == projection.ApprovedBy ||
            projection.ProposedBy == projection.SecondApprovedBy ||
            projection.ApprovedBy == projection.SecondApprovedBy)
            return false;

        var reserves = await _db.Set<EconomyReserveHeadRow>().AsNoTracking()
            .Where(row => row.IsActive)
            .ToArrayAsync(cancellationToken);
        if (reserves.Length != 1) return false;
        var reserve = reserves[0];
        if (reserve.Coverage != ReserveCoverageState.Covered ||
            reserve.ObservedAt > now || reserve.ExpiresAt <= now ||
            string.IsNullOrWhiteSpace(reserve.EvidenceHash))
            return false;

        var reconciliations = await _db.Set<EconomyCustodyReconciliationRow>().AsNoTracking()
            .Where(row => row.ReserveVersion == reserve.Version)
            .ToArrayAsync(cancellationToken);
        if (reconciliations.Length != 1) return false;
        var reconciliation = reconciliations[0];
        if (!reconciliation.IsReconciled || reconciliation.VarianceUsdNanos != 0 ||
            string.IsNullOrWhiteSpace(reconciliation.EvidenceHash))
            return false;

        Guid[] observationIds;
        try
        {
            observationIds = JsonSerializer.Deserialize<Guid[]>(reconciliation.ObservationIds) ?? [];
        }
        catch (JsonException)
        {
            return false;
        }
        if (observationIds.Length == 0 || observationIds.Distinct().Count() != observationIds.Length)
            return false;
        var observations = await _db.Set<EconomyCustodyObservationRow>().AsNoTracking()
            .Where(row => observationIds.Contains(row.Id))
            .ToArrayAsync(cancellationToken);
        if (observations.Length != observationIds.Length || observations.Any(row =>
                row.ObservedAt > now || row.ExpiresAt <= now ||
                string.IsNullOrWhiteSpace(row.PayloadHash) ||
                string.IsNullOrWhiteSpace(row.KeyId) ||
                string.IsNullOrWhiteSpace(row.Signature)))
            return false;

        var anchor = await _db.Set<EconomyExternalAnchorRow>().AsNoTracking()
            .Where(row => row.JournalSequence == chainHead.Sequence && row.JournalHash == chainHead.Hash)
            .OrderByDescending(row => row.AnchoredAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (anchor is null || string.IsNullOrWhiteSpace(anchor.Signature) ||
            string.IsNullOrWhiteSpace(anchor.WormReference) ||
            string.IsNullOrWhiteSpace(anchor.ProviderReference))
            return false;
        var anchorVerification = await _db.Set<EconomyAnchorVerificationRow>().AsNoTracking()
            .Where(row => row.ExternalAnchorId == anchor.Id)
            .OrderByDescending(row => row.VerifiedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (anchorVerification is null || !anchorVerification.SignatureValid ||
            !anchorVerification.ObjectMatches || anchorVerification.RetainUntil <= now ||
            anchorVerification.VerifiedAt > now ||
            anchorVerification.VerifiedAt.Add(MaximumAnchorAge) <= now ||
            string.IsNullOrWhiteSpace(anchorVerification.KeyId) ||
            string.IsNullOrWhiteSpace(anchorVerification.ObjectVersion) ||
            string.IsNullOrWhiteSpace(anchorVerification.ETag) ||
            string.IsNullOrWhiteSpace(anchorVerification.ObjectHash))
            return false;

        var policiesQuery = _db.Set<EconomyCapabilityPolicyRow>().AsNoTracking()
            .Where(row => row.IsActive);
        if (scope.TenantId is Guid tenantId)
            policiesQuery = policiesQuery.Where(row => row.TenantId == null || row.TenantId == tenantId);
        if (scope.Capability is EconomyValueMovementCapability capability)
            policiesQuery = policiesQuery.Where(row => row.Capability == capability);
        var policies = await policiesQuery.ToArrayAsync(cancellationToken);
        if (scope.Capability is not null && policies.Length == 0) return false;
        foreach (var policy in policies)
        {
            if (!policy.ProviderReady || policy.EffectiveAt > now || policy.ExpiresAt <= now ||
                string.IsNullOrWhiteSpace(policy.KeyId) || string.IsNullOrWhiteSpace(policy.Signature) ||
                !await _policySignatureVerifier.VerifyAsync(
                    policy.CanonicalPayload, policy.KeyId, policy.Signature, cancellationToken))
                return false;
        }

        return true;
    }

    private static void ValidateScope(EconomyKillSwitchScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope.ScopeKey);
        if (scope.TenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID cannot be empty.", nameof(scope));
        if (scope.Capability is not null && scope.TenantId is null)
            throw new ArgumentException("Capability readiness must be tenant-scoped.", nameof(scope));
        if (scope.Capability is not null && !Enum.IsDefined(scope.Capability.Value))
            throw new ArgumentOutOfRangeException(nameof(scope));
    }
}
