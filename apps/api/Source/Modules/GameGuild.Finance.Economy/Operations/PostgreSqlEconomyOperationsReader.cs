using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Reserves;
using GameGuild.Finance.Economy.Risk;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Operations;

public sealed record EconomyJournalHeadStatus(long Sequence, string Hash, DateTimeOffset UpdatedAt);

public sealed record EconomyJournalVerificationStatus(
    long FromSequence,
    long ToSequence,
    string CurrentHash,
    bool IsValid,
    string? FailureCode,
    long FencingToken,
    DateTimeOffset CompletedAt);

public sealed record EconomyProjectionOperationalStatus(
    long Generation,
    long ToSequence,
    string JournalHash,
    string ProjectionHash,
    int MismatchCount,
    string State,
    Guid ProposedBy,
    Guid? ApprovedBy,
    Guid? SecondApprovedBy,
    DateTimeOffset? ActivatedAt);

public sealed record EconomyAnchorOperationalStatus(
    Guid Id,
    long JournalSequence,
    string JournalHash,
    string Provider,
    DateTimeOffset AnchoredAt,
    bool SignatureValid,
    bool ObjectMatches,
    DateTimeOffset? RetainUntil,
    DateTimeOffset? VerifiedAt);

public sealed record EconomyReserveOperationalStatus(
    long Version,
    long PolicyVersion,
    long AuthorizationEpoch,
    ReserveCoverageState Coverage,
    string EvidenceHash,
    DateTimeOffset ObservedAt,
    DateTimeOffset ExpiresAt,
    bool CustodyReconciled,
    long? CustodyVarianceUsdNanos,
    DateTimeOffset? ReconciledAt);

public sealed record EconomyLedgerHealthSnapshot(
    bool IsJournalHealthy,
    bool IsProjectionHealthy,
    bool IsAnchorHealthy,
    bool IsReserveHealthy,
    EconomyJournalHeadStatus? Head,
    EconomyJournalVerificationStatus? LatestVerification,
    EconomyProjectionOperationalStatus? ActiveProjection,
    EconomyAnchorOperationalStatus? LatestAnchor,
    EconomyReserveOperationalStatus? ActiveReserve,
    IReadOnlyList<string> Diagnostics);

public sealed record EconomyCapabilityPolicyOperationalStatus(
    Guid Id,
    Guid? TenantId,
    EconomyValueMovementCapability Capability,
    string JurisdictionCode,
    long Version,
    string PayloadHash,
    string KeyId,
    bool ProviderReady,
    EconomyCapabilityPolicyState State,
    DateTimeOffset EffectiveAt,
    DateTimeOffset ExpiresAt);

public sealed record EconomyKillSwitchOperationalStatus(
    Guid Id,
    EconomyKillSwitchScope Scope,
    long Epoch,
    bool IsActive,
    string Reason,
    Guid ActivatedBy,
    DateTimeOffset ActivatedAt,
    Guid? ReleaseProposedBy,
    IReadOnlyList<Guid> ReleaseApprovers,
    DateTimeOffset? ReleasedAt);

public sealed record EconomyCapabilityConfigurationSnapshot(
    IReadOnlyList<EconomyCapabilityPolicyOperationalStatus> Policies,
    IReadOnlyList<EconomyKillSwitchOperationalStatus> KillSwitches);

public interface IEconomyOperationsReader
{
    ValueTask<EconomyLedgerHealthSnapshot> ReadLedgerHealthAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken);

    ValueTask<EconomyCapabilityConfigurationSnapshot> ReadCapabilityConfigurationAsync(
        Guid tenantId,
        bool includeInactiveKillSwitches,
        int limit,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}

public sealed class PostgreSqlEconomyOperationsReader : IEconomyOperationsReader
{
    private readonly DbContext _db;

    public PostgreSqlEconomyOperationsReader(IApplicationDbContext context) =>
        _db = PostgreSqlEntityRiskGraphStore.RequireRelationalContext(context);

    public async ValueTask<EconomyLedgerHealthSnapshot> ReadLedgerHealthAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var head = await _db.Set<EconomyChainHeadRow>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == 1, cancellationToken);
        var checkpoint = await _db.Set<EconomyJournalVerificationCheckpointRow>().AsNoTracking()
            .OrderByDescending(row => row.CompletedAt)
            .ThenByDescending(row => row.ToSequence)
            .FirstOrDefaultAsync(cancellationToken);
        var projections = await _db.Set<EconomyProjectionGenerationRow>().AsNoTracking()
            .Where(row => row.IsActive)
            .OrderByDescending(row => row.Generation)
            .ToArrayAsync(cancellationToken);
        var projection = projections.FirstOrDefault();
        var anchor = await _db.Set<EconomyExternalAnchorRow>().AsNoTracking()
            .OrderByDescending(row => row.JournalSequence)
            .ThenByDescending(row => row.AnchoredAt)
            .FirstOrDefaultAsync(cancellationToken);
        EconomyAnchorVerificationRow? anchorVerification = null;
        if (anchor is not null)
        {
            anchorVerification = await _db.Set<EconomyAnchorVerificationRow>().AsNoTracking()
                .Where(row => row.ExternalAnchorId == anchor.Id)
                .OrderByDescending(row => row.VerifiedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }
        var reserveHeads = await _db.Set<EconomyReserveHeadRow>().AsNoTracking()
            .Where(row => row.IsActive)
            .OrderByDescending(row => row.Version)
            .ToArrayAsync(cancellationToken);
        var reserve = reserveHeads.FirstOrDefault();
        EconomyCustodyReconciliationRow? reconciliation = null;
        if (reserve is not null)
        {
            reconciliation = await _db.Set<EconomyCustodyReconciliationRow>().AsNoTracking()
                .Where(row => row.ReserveVersion == reserve.Version)
                .OrderByDescending(row => row.ReconciledAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var journalHealthy = head is not null && checkpoint is
        {
            IsValid: true
        } && checkpoint.ToSequence == head.Sequence && checkpoint.CurrentHash == head.Hash;
        var projectionHealthy = journalHealthy && projections.Length == 1 && projection is
        {
            MismatchCount: 0,
            State: "Active",
            ApprovedBy: not null,
            SecondApprovedBy: not null,
            ActivatedAt: not null
        } && projection.ToSequence == head!.Sequence && projection.JournalHash == head.Hash &&
            projection.ProposedBy != projection.ApprovedBy &&
            projection.ProposedBy != projection.SecondApprovedBy &&
            projection.ApprovedBy != projection.SecondApprovedBy;
        var anchorHealthy = journalHealthy && anchor is not null && anchorVerification is
        {
            SignatureValid: true,
            ObjectMatches: true
        } && anchor.JournalSequence == head!.Sequence && anchor.JournalHash == head.Hash &&
            anchorVerification.RetainUntil > now;
        var reserveHealthy = reserveHeads.Length == 1 && reserve is
        {
            Coverage: ReserveCoverageState.Covered
        } && reserve.ExpiresAt > now && !string.IsNullOrWhiteSpace(reserve.EvidenceHash) && reconciliation is
        {
            IsReconciled: true,
            VarianceUsdNanos: 0
        };

        var diagnostics = new List<string>(4);
        if (!journalHealthy) diagnostics.Add("Journal head is missing or not covered by a matching valid checkpoint.");
        if (!projectionHealthy) diagnostics.Add("Exactly one dual-approved active projection matching the journal is required.");
        if (!anchorHealthy) diagnostics.Add("The journal head is not covered by a valid retained WORM anchor.");
        if (!reserveHealthy) diagnostics.Add("The active reserve or its custody reconciliation is missing, stale, or insufficient.");

        return new EconomyLedgerHealthSnapshot(
            journalHealthy,
            projectionHealthy,
            anchorHealthy,
            reserveHealthy,
            head is null ? null : new EconomyJournalHeadStatus(head.Sequence, head.Hash, head.UpdatedAt),
            checkpoint is null ? null : new EconomyJournalVerificationStatus(
                checkpoint.FromSequence, checkpoint.ToSequence, checkpoint.CurrentHash, checkpoint.IsValid,
                checkpoint.FailureCode, checkpoint.FencingToken, checkpoint.CompletedAt),
            projection is null ? null : new EconomyProjectionOperationalStatus(
                projection.Generation, projection.ToSequence, projection.JournalHash, projection.ProjectionHash,
                projection.MismatchCount, projection.State, projection.ProposedBy, projection.ApprovedBy,
                projection.SecondApprovedBy, projection.ActivatedAt),
            anchor is null ? null : new EconomyAnchorOperationalStatus(
                anchor.Id, anchor.JournalSequence, anchor.JournalHash, anchor.Provider, anchor.AnchoredAt,
                anchorVerification?.SignatureValid ?? false, anchorVerification?.ObjectMatches ?? false,
                anchorVerification?.RetainUntil, anchorVerification?.VerifiedAt),
            reserve is null ? null : new EconomyReserveOperationalStatus(
                reserve.Version, reserve.PolicyVersion, reserve.AuthorizationEpoch, reserve.Coverage,
                reserve.EvidenceHash, reserve.ObservedAt, reserve.ExpiresAt,
                reconciliation?.IsReconciled ?? false, reconciliation?.VarianceUsdNanos,
                reconciliation?.ReconciledAt),
            diagnostics.AsReadOnly());
    }

    public async ValueTask<EconomyCapabilityConfigurationSnapshot> ReadCapabilityConfigurationAsync(
        Guid tenantId,
        bool includeInactiveKillSwitches,
        int limit,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        if (limit is <= 0 or > 500) throw new ArgumentOutOfRangeException(nameof(limit));

        var policyRows = await _db.Set<EconomyCapabilityPolicyRow>().AsNoTracking()
            .Where(row => row.TenantId == null || row.TenantId == tenantId)
            .OrderBy(row => row.ScopeKey)
            .ThenByDescending(row => row.Version)
            .Take(limit)
            .ToArrayAsync(cancellationToken);
        var killSwitchQuery = _db.Set<EconomyKillSwitchRow>().AsNoTracking()
            .Where(row => row.TenantId == null || row.TenantId == tenantId);
        if (!includeInactiveKillSwitches)
            killSwitchQuery = killSwitchQuery.Where(row => row.IsActive);
        var killSwitchRows = await killSwitchQuery
            .OrderByDescending(row => row.ActivatedAt)
            .Take(limit)
            .ToArrayAsync(cancellationToken);
        var killSwitchIds = killSwitchRows.Select(row => row.Id).ToArray();
        var approvals = await _db.Set<EconomyKillSwitchReleaseApprovalRow>().AsNoTracking()
            .Where(row => killSwitchIds.Contains(row.KillSwitchId))
            .OrderBy(row => row.ApprovedAt)
            .ToArrayAsync(cancellationToken);
        var approvalLookup = approvals.GroupBy(row => row.KillSwitchId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<Guid>)Array.AsReadOnly(
                group.Select(row => row.ActorId).ToArray()));

        var policies = policyRows.Select(row => new EconomyCapabilityPolicyOperationalStatus(
            row.Id, row.TenantId, row.Capability, row.JurisdictionCode, row.Version,
            row.PayloadHash, row.KeyId, row.ProviderReady,
            row.IsActive
                ? row.ExpiresAt <= now ? EconomyCapabilityPolicyState.Expired : EconomyCapabilityPolicyState.Active
                : row.ApprovedBy is null ? EconomyCapabilityPolicyState.PendingApproval : EconomyCapabilityPolicyState.Approved,
            row.EffectiveAt, row.ExpiresAt)).ToArray();
        var killSwitches = killSwitchRows.Select(row => new EconomyKillSwitchOperationalStatus(
            row.Id,
            new EconomyKillSwitchScope(row.ScopeKey, row.TenantId, row.Capability),
            row.Epoch, row.IsActive, row.Reason, row.ActivatedBy, row.ActivatedAt,
            row.ReleaseProposedBy,
            approvalLookup.GetValueOrDefault(row.Id, Array.Empty<Guid>()),
            row.ReleasedAt)).ToArray();

        return new EconomyCapabilityConfigurationSnapshot(
            Array.AsReadOnly(policies),
            Array.AsReadOnly(killSwitches));
    }
}
