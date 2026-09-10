using System.Data;
using System.Text.Json;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Reserves;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Risk;

public sealed class PostgreSqlEconomyProtectedOperationRiskDecisionIssuer :
    IEconomyProtectedOperationRiskDecisionIssuer
{
    private readonly DbContext _db;
    private readonly IEconomyCapabilityPolicyStore _policies;
    private readonly ICapabilityPolicySignatureVerifier _signatures;
    private readonly IFinancialCrimeRiskInputSource _financialCrime;
    private readonly ITrustSafetyRiskInputSource _trustSafety;
    private readonly IEntityRiskGraphStore _entityGraph;
    private readonly IAggregateRiskCounterStore _counters;
    private readonly IRiskReviewStore _reviews;
    private readonly IComplianceHoldStore _holds;

    public PostgreSqlEconomyProtectedOperationRiskDecisionIssuer(
        IApplicationDbContext context,
        IEconomyCapabilityPolicyStore policies,
        ICapabilityPolicySignatureVerifier signatures,
        IFinancialCrimeRiskInputSource financialCrime,
        ITrustSafetyRiskInputSource trustSafety,
        IEntityRiskGraphStore entityGraph,
        IAggregateRiskCounterStore counters,
        IRiskReviewStore reviews,
        IComplianceHoldStore holds)
    {
        _db = PostgreSqlEntityRiskGraphStore.RequireRelationalContext(context);
        _policies = policies ?? throw new ArgumentNullException(nameof(policies));
        _signatures = signatures ?? throw new ArgumentNullException(nameof(signatures));
        _financialCrime = financialCrime ?? throw new ArgumentNullException(nameof(financialCrime));
        _trustSafety = trustSafety ?? throw new ArgumentNullException(nameof(trustSafety));
        _entityGraph = entityGraph ?? throw new ArgumentNullException(nameof(entityGraph));
        _counters = counters ?? throw new ArgumentNullException(nameof(counters));
        _reviews = reviews ?? throw new ArgumentNullException(nameof(reviews));
        _holds = holds ?? throw new ArgumentNullException(nameof(holds));
    }

    public ValueTask<EconomyProtectedRiskDecision> IssueAsync(
        EconomyProtectedRiskDecisionRequest request,
        CancellationToken cancellationToken) => new(PostgreSqlTransactionExecutor.ExecuteAsync(
        _db,
        IsolationLevel.Serializable,
        token => IssueCoreAsync(request, token),
        cancellationToken));

    private async Task<EconomyProtectedRiskDecision> IssueCoreAsync(
        EconomyProtectedRiskDecisionRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);
        var now = request.Intent.RequestedAt;
        var policy = await CurrentPolicyAsync(request, cancellationToken).ConfigureAwait(false);
        if (policy is null || policy.State != EconomyCapabilityPolicyState.Active ||
            policy.EffectiveAt > now || policy.ExpiresAt <= now ||
            policy.PayloadHash != EconomyProtectedRiskDecisionIssuerSupport.Hash(policy.CanonicalPayload) ||
            !await _signatures.VerifyAsync(
                policy.CanonicalPayload, policy.KeyId, policy.Signature, cancellationToken).ConfigureAwait(false))
            return Rejected(EconomyProtectedOperationState.InvalidPolicy,
                "A current signed risk policy is required.");

        EconomyProtectedRiskPolicy riskPolicy;
        try
        {
            riskPolicy = EconomyProtectedRiskPolicy.Parse(policy.CanonicalPayload);
        }
        catch (EconomyProtectedRiskPolicyException exception)
        {
            return Rejected(EconomyProtectedOperationState.InvalidPolicy, exception.Message);
        }

        var evidence = await ReadEvidenceAsync(request, cancellationToken).ConfigureAwait(false);
        var assessment = EconomyProtectedRiskDecisionIssuerSupport.Assess(evidence, now);
        if (assessment.State is EconomyProtectedOperationState.ComplianceUnavailable or
            EconomyProtectedOperationState.ComplianceStale or EconomyProtectedOperationState.Denied)
            return new EconomyProtectedRiskDecision(
                Guid.Empty, assessment.Outcome, assessment.State, null, assessment.Diagnostics);

        var reserve = await _db.Set<EconomyReserveHeadRow>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.IsActive, cancellationToken).ConfigureAwait(false);
        if (reserve is null || reserve.Coverage != ReserveCoverageState.Covered ||
            reserve.ObservedAt > now || reserve.ExpiresAt <= now)
            return Rejected(EconomyProtectedOperationState.ReserveInsufficient,
                "A current covered reserve head is required.");
        if (!await WalletsBelongToTenantAsync(request, cancellationToken).ConfigureAwait(false))
            throw new RiskDecisionBindingException(
                "Protected operation wallets are not active in the actor tenant.");

        var cluster = await _entityGraph.ClusterForAsync(
            request.TenantId,
            new RiskEntityNode(
                RiskEntityType.Account,
                EconomyProtectedRiskDecisionIssuerSupport.Hash(request.SubjectReference)),
            cancellationToken).ConfigureAwait(false);
        var killSwitches = await _db.Set<EconomyKillSwitchRow>().AsNoTracking()
            .Where(row => (row.TenantId == null || row.TenantId == request.TenantId) &&
                          (row.Capability == null || row.Capability == request.Intent.Capability))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var killSwitchEpoch = killSwitches.Length == 0 ? 0 : killSwitches.Max(row => row.Epoch);
        var decisionId = EconomyProtectedRiskDecisionIssuerSupport.DeterministicGuid(request, "risk-decision");
        var reviewId = assessment.Outcome == RiskOutcome.Review
            ? EconomyProtectedRiskDecisionIssuerSupport.DeterministicGuid(request, "risk-review")
            : (Guid?)null;
        var existing = await _db.Set<EconomyRiskDecisionRow>()
            .SingleOrDefaultAsync(row => row.Id == decisionId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            EnsureReplay(existing, request, policy, reserve, assessment);
            return new EconomyProtectedRiskDecision(
                existing.Id, existing.Outcome, assessment.State, reviewId, assessment.Diagnostics);
        }

        var expiresAt = new[]
        {
            now.Add(riskPolicy.DecisionLifetime), policy.ExpiresAt, reserve.ExpiresAt, assessment.ExpiresAt
        }.Min();
        var row = CreateDecisionRow(
            decisionId, request, policy, reserve, riskPolicy, cluster,
            killSwitchEpoch, assessment, expiresAt);
        _db.Set<EconomyRiskDecisionRow>().Add(row);
        AddAuditEvidence(row, request, policy, reserve, cluster, evidence);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var evidenceHashes = evidence.Select(item => item.EvidenceHash)
            .Append(request.JurisdictionEvidenceHash)
            .Append(policy.PayloadHash)
            .Append(reserve.EvidenceHash)
            .Append(cluster.EvidenceHash)
            .ToArray();
        if (assessment.Outcome == RiskOutcome.Review)
            await _reviews.SubmitAsync(
                request.TenantId, reviewId!.Value, decisionId, request.ActorId,
                evidenceHashes, now, riskPolicy.RequiredReviewApprovals, cancellationToken).ConfigureAwait(false);
        else if (assessment.Outcome == RiskOutcome.Hold)
            await _holds.ActivateAsync(new ComplianceHoldActivation(
                EconomyProtectedRiskDecisionIssuerSupport.DeterministicGuid(request, "compliance-hold"),
                new ComplianceHoldScope(request.TenantId, request.SubjectReference, request.Intent.Capability),
                EconomyProtectedRiskDecisionIssuerSupport.Hash(decisionId.ToString("N")),
                "protected-operation-compliance-hold",
                EconomyProtectedRiskDecisionIssuerSupport.Hash(string.Join('|', evidenceHashes)),
                request.Intent.IdempotencyKey.Value,
                request.ActorId,
                now,
                now.Add(riskPolicy.ComplianceHoldDuration)), cancellationToken).ConfigureAwait(false);
        else
            await ReserveCountersAsync(
                request, decisionId, riskPolicy, cluster, expiresAt, cancellationToken).ConfigureAwait(false);

        return new EconomyProtectedRiskDecision(
            decisionId, assessment.Outcome, assessment.State, reviewId, assessment.Diagnostics);
    }

    private async ValueTask<EconomyCapabilityPolicy?> CurrentPolicyAsync(
        EconomyProtectedRiskDecisionRequest request,
        CancellationToken cancellationToken) =>
        await _policies.CurrentAsync(
            request.TenantId, request.Intent.Capability, request.JurisdictionCode, cancellationToken)
        ?? await _policies.CurrentAsync(
            null, request.Intent.Capability, request.JurisdictionCode, cancellationToken);

    private async Task<IReadOnlyList<ExternalRiskEvidence>> ReadEvidenceAsync(
        EconomyProtectedRiskDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var financialCrime = await _financialCrime.ReadAsync(
            request.TenantId, request.SubjectReference, request.Intent.RequestedAt, cancellationToken);
        var trustSafety = await _trustSafety.ReadAsync(
            request.TenantId, request.SubjectReference, request.Intent.RequestedAt, cancellationToken);
        return [financialCrime.ToEvidence(), trustSafety.ToEvidence()];
    }

    private async Task<bool> WalletsBelongToTenantAsync(
        EconomyProtectedRiskDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var ids = new[] { request.Intent.SourceWalletId.Value, request.Intent.DestinationWalletId.Value };
        return await _db.Set<EconomyWalletRow>().AsNoTracking().CountAsync(row =>
            ids.Contains(row.Id) && row.TenantId == request.TenantId &&
            row.State == WalletLifecycleState.Active, cancellationToken) == ids.Distinct().Count();
    }

    private async Task ReserveCountersAsync(
        EconomyProtectedRiskDecisionRequest request,
        Guid decisionId,
        EconomyProtectedRiskPolicy policy,
        EntityRiskCluster cluster,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        var limits = EconomyProtectedRiskDecisionIssuerSupport.MaterializeLimits(policy, request, cluster);
        await _counters.ReserveAsync(
            EconomyProtectedRiskDecisionIssuerSupport.DeterministicGuid(request, "risk-counters"),
            request.TenantId,
            decisionId,
            request.Intent.TemplateKind,
            request.Intent.Amount,
            limits,
            request.Intent.RequestedAt,
            expiresAt,
            cancellationToken).ConfigureAwait(false);
    }

    private static EconomyRiskDecisionRow CreateDecisionRow(
        Guid id,
        EconomyProtectedRiskDecisionRequest request,
        EconomyCapabilityPolicy policy,
        EconomyReserveHeadRow reserve,
        EconomyProtectedRiskPolicy riskPolicy,
        EntityRiskCluster cluster,
        long killSwitchEpoch,
        EconomyExternalRiskAssessment assessment,
        DateTimeOffset expiresAt) => new()
    {
        Id = id,
        Outcome = assessment.Outcome,
        OperationFingerprint = request.OperationFingerprint,
        IdempotencyKey = request.Intent.IdempotencyKey.Value,
        ActorHash = EconomyProtectedRiskDecisionIssuerSupport.Hash(
            $"{request.TenantId:N}:{request.ActorId:N}"),
        TemplateKind = request.Intent.TemplateKind,
        SourceWalletId = request.Intent.SourceWalletId.Value,
        DestinationWalletId = request.Intent.DestinationWalletId.Value,
        Currency = request.Intent.Amount.Currency,
        AmountUnits = request.Intent.Amount.Units,
        CurrencyLegs = JsonSerializer.Serialize(request.Intent.CurrencyLegs.Select(leg => new
        {
            currency = (int)leg.Currency, units = leg.Units
        })),
        SourceRoots = JsonSerializer.Serialize(request.Intent.SourceRoots.Select(root => root.Value)),
        ProviderReferenceHash = request.Intent.ProviderReferenceHash.Trim(),
        PolicyVersion = policy.Version,
        ReserveVersion = reserve.Version,
        ReserveAuthorizationEpoch = reserve.AuthorizationEpoch,
        FeatureVersion = 1,
        KillSwitchEpoch = killSwitchEpoch,
        CounterVersion = riskPolicy.CounterVersion,
        EntityGraphVersion = cluster.Version,
        EntityGraphEvidenceHash = cluster.EvidenceHash,
        ReasonCodes = JsonSerializer.Serialize(new[]
        {
            (int)(assessment.Outcome == RiskOutcome.Allow
                ? RiskReasonCode.WithinLimits
                : assessment.Outcome == RiskOutcome.Review
                    ? RiskReasonCode.ManualReviewRequired
                    : RiskReasonCode.ExternalEvidenceDenied)
        }),
        IssuedAt = request.Intent.RequestedAt,
        ExpiresAt = expiresAt
    };

    private void AddAuditEvidence(
        EconomyRiskDecisionRow decision,
        EconomyProtectedRiskDecisionRequest request,
        EconomyCapabilityPolicy policy,
        EconomyReserveHeadRow reserve,
        EntityRiskCluster cluster,
        IReadOnlyList<ExternalRiskEvidence> evidence)
    {
        var rows = evidence.Select(item => new EconomyRiskAuditEvidenceRow
        {
            Id = Guid.NewGuid(), RiskDecisionId = decision.Id, EventKind = "external-risk-evidence",
            OperationFingerprint = request.OperationFingerprint, EvidenceHash = item.EvidenceHash,
            Payload = JsonSerializer.Serialize(item), RecordedAt = request.Intent.RequestedAt
        }).Append(new EconomyRiskAuditEvidenceRow
        {
            Id = Guid.NewGuid(), RiskDecisionId = decision.Id, EventKind = "protected-operation-control-plane",
            OperationFingerprint = request.OperationFingerprint,
            EvidenceHash = EconomyProtectedRiskDecisionIssuerSupport.Hash(string.Join('|',
                request.JurisdictionEvidenceHash, policy.PayloadHash, reserve.EvidenceHash, cluster.EvidenceHash)),
            Payload = JsonSerializer.Serialize(new
            {
                request.JurisdictionCode,
                jurisdictionEvidenceHash = request.JurisdictionEvidenceHash,
                policyVersion = policy.Version,
                policy.PayloadHash,
                reserveVersion = reserve.Version,
                reserve.AuthorizationEpoch,
                cluster.Version,
                cluster.EvidenceHash
            }),
            RecordedAt = request.Intent.RequestedAt
        });
        _db.Set<EconomyRiskAuditEvidenceRow>().AddRange(rows);
    }

    internal static void EnsureReplay(
        EconomyRiskDecisionRow existing,
        EconomyProtectedRiskDecisionRequest request,
        EconomyCapabilityPolicy policy,
        EconomyReserveHeadRow reserve,
        EconomyExternalRiskAssessment assessment)
    {
        if (existing.OperationFingerprint != request.OperationFingerprint ||
            existing.Outcome != assessment.Outcome || existing.PolicyVersion != policy.Version ||
            existing.ReserveVersion != reserve.Version || existing.TemplateKind != request.Intent.TemplateKind ||
            existing.AmountUnits != request.Intent.Amount.Units)
            throw new RiskDecisionReuseException(
                "The protected-operation idempotency key is bound to different authority inputs.");
    }

    internal static void Validate(EconomyProtectedRiskDecisionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Intent);
        if (request.TenantId == Guid.Empty || request.ActorId == Guid.Empty)
            throw new ArgumentException("Protected operation tenant and actor are required.", nameof(request));
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SubjectReference);
        _ = EconomyJurisdictionCode.Require(request.JurisdictionCode, nameof(request));
        ArgumentException.ThrowIfNullOrWhiteSpace(request.JurisdictionEvidenceHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationFingerprint);
    }

    internal static EconomyProtectedRiskDecision Rejected(
        EconomyProtectedOperationState state,
        string diagnostic) => new(Guid.Empty, RiskOutcome.Deny, state, null, [diagnostic]);
}
