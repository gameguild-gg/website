using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Finance.Economy.AdRewards.Persistence;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.AdRewards;

public sealed class DurableDeferredAdRewardService : IDurableDeferredAdRewardService
{
    private const string RegisteredCapabilityName = "ad-reward-issuance";
    private const string GlobalCapSubject = "ad-rewards-global";
    private readonly DbContext _db;
    private readonly IDurableAdRewardPolicyReader _policies;
    private readonly IEconomyCapabilityAuthorizationService _capabilities;
    private readonly IRegisteredPostingCapabilityResolver _postingAuthority;
    private readonly IAdRewardIssuanceGateway _issuance;

    public DurableDeferredAdRewardService(
        IApplicationDbContext context,
        IDurableAdRewardPolicyReader policies,
        IEconomyCapabilityAuthorizationService capabilities,
        IRegisteredPostingCapabilityResolver postingAuthority,
        IAdRewardIssuanceGateway issuance)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(postingAuthority);
        ArgumentNullException.ThrowIfNull(issuance);
        _db = context as DbContext
            ?? throw new InvalidOperationException(
                "Deferred ad reward confirmation requires the application's relational DbContext.");
        _policies = policies;
        _capabilities = capabilities;
        _postingAuthority = postingAuthority;
        _issuance = issuance;
    }

    public async ValueTask<DurableAdRewardCompletionResult> ConfirmAsync(
        ConfirmDeferredAdRewardRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);
        var idempotencyHash = Hash(request.IdempotencyKey.Value);
        var requestHash = Hash(string.Join('|',
            request.TenantId.ToString("N"),
            request.ActorId.ToString("N"),
            request.SessionId.ToString("N"),
            request.SubjectReference.Trim(),
            request.JurisdictionCode.Trim().ToUpperInvariant(),
            request.RiskDecisionId.ToString("N"),
            request.OperationFingerprint.Trim()));

        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async _ =>
        {
        var session = await _db.Set<AdRewardSessionRow>()
            .SingleOrDefaultAsync(
                row => row.Id == request.SessionId && row.TenantId == request.TenantId,
                cancellationToken)
            ?? throw new AdRewardReplayException("Deferred ad reward session was not found.");
        if (session.UserId != request.ActorId)
            throw new AdRewardRiskBindingException(
                "The actor context does not own the deferred ad reward session.");
        var pending = await _db.Set<AdRewardPendingClaimRow>()
            .SingleOrDefaultAsync(row => row.SessionId == request.SessionId, cancellationToken)
            ?? throw new AdRewardReplayException("Deferred ad reward claim was not found.");
        var completion = await _db.Set<AdRewardCompletionRow>()
            .SingleAsync(row => row.SessionId == request.SessionId, cancellationToken);

        if (pending.ConfirmationIdempotencyKeyHash is not null)
        {
            if (pending.ConfirmationIdempotencyKeyHash != idempotencyHash ||
                pending.ConfirmationRequestHash != requestHash)
                throw new AdRewardIdempotencyConflictException(
                    "The deferred confirmation idempotency key is bound to different inputs.");
            return Map(completion, true);
        }
        if (session.State != DurableAdRewardSessionState.Verified ||
            pending.ProviderReportId is null || pending.ConfirmedAt is null)
            throw new AdRewardReplayException(
                "Deferred ad reward claim has no verified provider report.");
        var report = await _db.Set<AdProviderReportRow>()
            .AsNoTracking()
            .SingleAsync(row => row.Id == pending.ProviderReportId.Value, cancellationToken);
        var policy = await _policies.GetVersionAsync(
            request.TenantId, session.Network, new PolicyVersion(session.PolicyVersion), cancellationToken);
        if (policy.Policy.IssuanceMode != AdRewardIssuanceMode.DeferredReport ||
            request.ConfirmedAt > report.PeriodEnd + policy.Policy.ReportStaleAfter ||
            !policy.ProviderCertified)
            throw new AdRewardDependencyUnavailableException(
                "The verified provider report or bound deferred policy is stale or unavailable.");

        var accumulator = await _db.Set<AdRewardAccumulatorRow>()
            .SingleOrDefaultAsync(
                row => row.TenantId == session.TenantId && row.WalletId == session.WalletId &&
                       row.Network == session.Network,
                cancellationToken);
        var previousRemainder = accumulator is null
            ? Int128.Zero
            : Int128.Parse(accumulator.RemainderNumerator, CultureInfo.InvariantCulture);
        var quote = AdRewardRationalAccumulator.Calculate(
            new WalletId(session.WalletId), request.IdempotencyKey, policy.Policy, 1, previousRemainder);
        UpsertAccumulator(accumulator, session, quote, request.ConfirmedAt);
        pending.ConfirmationIdempotencyKeyHash = idempotencyHash;
        pending.ConfirmationRequestHash = requestHash;

        if (quote.RewardSoftUnits == 0)
        {
            UpdateCompletion(
                completion, session, request, quote, null, null, null,
                AdRewardCompletionState.AccumulatedRemainder);
            RecordAttribution(session, quote, report.BatchId, request.ConfirmedAt);
            await _db.SaveChangesAsync(cancellationToken);
            return Map(completion, false);
        }

        await EnsureAndRecordCapsAsync(
            session, policy, quote.RewardSoftUnits, request.ConfirmedAt, cancellationToken);
        var sourceStampId = new SourceStampId(pending.SourceStampId);
        var postingId = new PostingId(DeterministicId(session.Id, "posting"));
        var outputLotId = new CreditLotId(DeterministicId(session.Id, "lot"));
        var receipt = await _capabilities.AuthorizeAndConsumeAsync(
            new EconomyCapabilityEvaluationContext(
                request.TenantId,
                request.ActorId,
                request.SubjectReference.Trim(),
                request.JurisdictionCode.Trim().ToUpperInvariant(),
                EconomyValueMovementCapability.IssueAdReward,
                request.RiskDecisionId,
                request.OperationFingerprint.Trim(),
                policy.ProviderHash,
                Hash(session.WalletId.ToString("N")),
                [Hash(sourceStampId.Value.ToString("N"))],
                request.ConfirmedAt),
            cancellationToken);
        var authority = await _postingAuthority.ResolveAuthorityAsync(
            RegisteredCapabilityName,
            PostingTemplateKind.AdRewardIssuance,
            receipt,
            cancellationToken);
        var posting = _issuance.Issue(new PersistedAdRewardIssuanceRequest(
            authority,
            postingId,
            request.IdempotencyKey,
            sourceStampId,
            outputLotId,
            new WalletId(session.WalletId),
            quote.RewardSoftUnits,
            new PolicyVersion(receipt.PolicyVersion),
            new ReserveVersion(receipt.ReserveVersion),
            session.Network,
            $"{report.ReportId}:{report.Version}:{session.Id:N}",
            report.EvidenceHash,
            request.ConfirmedAt,
            receipt.ReceiptHash));
        if (posting.PostingId != postingId)
            throw new RegisteredPostingRejectedException(
                "The deferred ad reward writer returned an unexpected posting identity.");

        RecordBudgetConsumption(session, quote.RewardSoftUnits, request.ConfirmedAt);
        RecordAttribution(session, quote, report.BatchId, request.ConfirmedAt);
        UpdateCompletion(
            completion, session, request, quote, postingId, outputLotId, receipt,
            AdRewardCompletionState.Issued);
        await _db.SaveChangesAsync(cancellationToken);
        return Map(completion, posting.IsDuplicate);
        }, cancellationToken);
    }

    private async Task EnsureAndRecordCapsAsync(
        AdRewardSessionRow session,
        AdRewardNetworkPolicySnapshot policy,
        long softUnits,
        DateTimeOffset consumedAt,
        CancellationToken cancellationToken)
    {
        var startsAfter = consumedAt - policy.Budget.Window;
        var existing = await _db.Set<AdRewardCapConsumptionRow>()
            .AsNoTracking()
            .Where(row => row.TenantId == session.TenantId && row.ConsumedAt > startsAfter)
            .ToArrayAsync(cancellationToken);
        var scopes = new[]
        {
            (AdRewardCapScope.User, Hash(session.UserId.ToString("N")), policy.Budget.MaximumUserSoftUnits),
            (AdRewardCapScope.Device, session.DeviceRiskHash, policy.Budget.MaximumDeviceSoftUnits),
            (AdRewardCapScope.Ip, session.IpRiskHash, policy.MaximumIpSoftUnits),
            (AdRewardCapScope.Asn, session.AsnRiskHash, policy.MaximumAsnSoftUnits),
            (AdRewardCapScope.Network, Hash(session.Network), policy.Budget.MaximumNetworkSoftUnits),
            (AdRewardCapScope.Global, GlobalCapSubject, policy.Budget.MaximumGlobalSoftUnits)
        };
        var lossBudget = SoftFaceValueUsdNanos(softUnits);
        foreach (var (scope, subject, maximum) in scopes)
        {
            var used = existing.Where(row => row.Scope == scope && row.SubjectHash == subject)
                .Sum(row => row.SoftUnits);
            if (softUnits > maximum - used)
                throw new AdRewardBudgetExceededException($"Ad reward {scope} cap was exceeded.");
        }
        var usedLossBudget = existing.Where(row => row.Scope == AdRewardCapScope.Global)
            .Sum(row => row.LossBudgetUsdNanos);
        if (lossBudget > policy.Budget.FundedLossBudgetUsdNanos - usedLossBudget)
            throw new AdRewardBudgetExceededException("Ad reward funded loss budget was exceeded.");
        foreach (var (scope, subject, _) in scopes)
        {
            _db.Set<AdRewardCapConsumptionRow>().Add(new AdRewardCapConsumptionRow
            {
                Id = Guid.NewGuid(),
                TenantId = session.TenantId,
                SessionId = session.Id,
                Scope = scope,
                SubjectHash = subject,
                WindowStartedAt = startsAfter,
                WindowEndsAt = consumedAt.Add(policy.Budget.Window),
                SoftUnits = softUnits,
                LossBudgetUsdNanos = scope == AdRewardCapScope.Global ? lossBudget : 0,
                ConsumedAt = consumedAt
            });
        }
    }

    private void UpsertAccumulator(
        AdRewardAccumulatorRow? row,
        AdRewardSessionRow session,
        AdRewardQuote quote,
        DateTimeOffset updatedAt)
    {
        if (row is null)
        {
            row = new AdRewardAccumulatorRow
            {
                TenantId = session.TenantId,
                WalletId = session.WalletId,
                Network = session.Network,
                CanonicalDenominator = AdRewardRationalAccumulator.CanonicalDenominator.ToString(CultureInfo.InvariantCulture),
                Version = 1
            };
            _db.Set<AdRewardAccumulatorRow>().Add(row);
        }
        else
        {
            row.Version++;
        }
        row.PolicyVersion = quote.PolicyVersion.Value;
        row.RemainderNumerator = quote.NextRemainder.ToString(CultureInfo.InvariantCulture);
        row.UpdatedAt = updatedAt;
    }

    private void UpdateCompletion(
        AdRewardCompletionRow completion,
        AdRewardSessionRow session,
        ConfirmDeferredAdRewardRequest request,
        AdRewardQuote quote,
        PostingId? postingId,
        CreditLotId? outputLotId,
        CapabilityAuthorizationReceipt? receipt,
        AdRewardCompletionState state)
    {
        completion.State = state;
        completion.RewardSoftUnits = quote.RewardSoftUnits;
        completion.SourceStampId = postingId is null ? null : DeterministicId(session.Id, "source");
        completion.PostingId = postingId?.Value;
        completion.OutputLotId = outputLotId?.Value;
        completion.CapabilityReceiptId = receipt?.Id;
        completion.CapabilityReceiptHash = receipt?.ReceiptHash;
        completion.ReserveVersion = receipt?.ReserveVersion;
        completion.RiskDecisionId = receipt?.RiskDecisionId;
        completion.KillSwitchEpoch = receipt?.KillSwitchEpoch;
        completion.JurisdictionCode = receipt?.JurisdictionCode;
        completion.ProviderHash = receipt?.ProviderHash;
        completion.DestinationHash = receipt?.DestinationHash;
        completion.EvidenceHashes = JsonSerializer.Serialize(receipt?.EvidenceHashes ?? []);
        completion.CompletedAt = request.ConfirmedAt;
        completion.Version++;
        session.State = DurableAdRewardSessionState.Posted;
        session.UpdatedAt = request.ConfirmedAt;
        session.Version++;
        _db.Set<AdRewardSessionEventRow>().Add(new AdRewardSessionEventRow
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Sequence = 4,
            State = DurableAdRewardSessionState.Posted,
            EvidenceHash = receipt?.ReceiptHash ?? Hash(request.IdempotencyKey.Value),
            OccurredAt = request.ConfirmedAt
        });
    }

    private void RecordBudgetConsumption(
        AdRewardSessionRow session,
        long softUnits,
        DateTimeOffset consumedAt) =>
        _db.Set<AdRewardBudgetConsumptionRow>().Add(new AdRewardBudgetConsumptionRow
        {
            SessionId = session.Id,
            TenantId = session.TenantId,
            UserId = session.UserId,
            DeviceRiskHash = session.DeviceRiskHash,
            Network = session.Network,
            SoftUnits = softUnits,
            LossBudgetUsdNanos = SoftFaceValueUsdNanos(softUnits),
            ConsumedAt = consumedAt
        });

    private void RecordAttribution(
        AdRewardSessionRow session,
        AdRewardQuote quote,
        string batchId,
        DateTimeOffset completedAt) =>
        _db.Set<AdRewardAttributionRow>().Add(new AdRewardAttributionRow
        {
            SessionId = session.Id,
            TenantId = session.TenantId,
            Network = session.Network,
            PolicyVersion = quote.PolicyVersion.Value,
            ProviderBatchId = batchId,
            EstimatedRevenueUsdNanos = checked((long)((Int128)quote.EstimatedNetEcpmUsdNanos / 1_000)),
            RewardSoftUnits = quote.RewardSoftUnits,
            CompletedAt = completedAt
        });

    private static void Validate(ConfirmDeferredAdRewardRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TenantId == Guid.Empty || request.ActorId == Guid.Empty ||
            request.SessionId == Guid.Empty || request.RiskDecisionId == Guid.Empty)
            throw new ArgumentException(
                "Tenant, actor, session and risk decision IDs are required.", nameof(request));
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SubjectReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.JurisdictionCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationFingerprint);
    }

    private static DurableAdRewardCompletionResult Map(AdRewardCompletionRow row, bool duplicate) => new(
        row.SessionId,
        row.State,
        row.RewardSoftUnits,
        row.PostingId.HasValue ? new PostingId(row.PostingId.Value) : null,
        row.OutputLotId.HasValue ? new CreditLotId(row.OutputLotId.Value) : null,
        duplicate,
        row.CompletedAt);

    private static Guid DeterministicId(Guid sessionId, string purpose)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{sessionId:N}:ad-reward:{purpose}"));
        return new Guid(bytes.AsSpan(0, 16));
    }

    private static long SoftFaceValueUsdNanos(long softUnits)
    {
        var numerator = checked((Int128)softUnits * 1_000_000_000);
        return checked((long)((numerator + AdRewardRationalAccumulator.SoftCoinsPerUsd - 1) /
                              AdRewardRationalAccumulator.SoftCoinsPerUsd));
    }

    private static string Hash(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
