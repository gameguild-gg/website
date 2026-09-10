using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.AdRewards;

public sealed record AdRewardDependencySnapshot(
    bool ProviderProofServiceAvailable,
    bool FraudDecisionAvailable,
    bool CounterStoreAvailable,
    bool RevenueReportsCurrent,
    bool LossBudgetAvailable,
    bool ReserveSnapshotAvailable,
    DateTimeOffset ObservedAt,
    DateTimeOffset ExpiresAt)
{
    public static AdRewardDependencySnapshot Healthy(DateTimeOffset observedAt, DateTimeOffset expiresAt) =>
        new(true, true, true, true, true, true, observedAt, expiresAt);

    public void EnsureReady(bool requiresProviderProof, DateTimeOffset now)
    {
        if (ObservedAt > now || ExpiresAt <= now || ExpiresAt <= ObservedAt ||
            !FraudDecisionAvailable || !CounterStoreAvailable || !RevenueReportsCurrent ||
            !LossBudgetAvailable || !ReserveSnapshotAvailable ||
            requiresProviderProof && !ProviderProofServiceAvailable)
            throw new AdRewardDependencyUnavailableException(
                "Ad reward issuance dependencies are unavailable or stale.");
    }
}

public sealed record AdRewardBudgetPolicy
{
    public AdRewardBudgetPolicy(
        long maximumUserSoftUnits,
        long maximumDeviceSoftUnits,
        long maximumNetworkSoftUnits,
        long maximumGlobalSoftUnits,
        long fundedLossBudgetUsdNanos,
        TimeSpan window)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumUserSoftUnits);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDeviceSoftUnits);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumNetworkSoftUnits);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumGlobalSoftUnits);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fundedLossBudgetUsdNanos);
        if (window <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(window));
        MaximumUserSoftUnits = maximumUserSoftUnits;
        MaximumDeviceSoftUnits = maximumDeviceSoftUnits;
        MaximumNetworkSoftUnits = maximumNetworkSoftUnits;
        MaximumGlobalSoftUnits = maximumGlobalSoftUnits;
        FundedLossBudgetUsdNanos = fundedLossBudgetUsdNanos;
        Window = window;
    }

    public long MaximumUserSoftUnits { get; }
    public long MaximumDeviceSoftUnits { get; }
    public long MaximumNetworkSoftUnits { get; }
    public long MaximumGlobalSoftUnits { get; }
    public long FundedLossBudgetUsdNanos { get; }
    public TimeSpan Window { get; }
}

public sealed record AdRewardBudgetConsumption(
    Guid SessionId,
    Guid UserId,
    string DeviceRiskHash,
    string Network,
    long SoftUnits,
    long LossBudgetUsdNanos,
    DateTimeOffset ConsumedAt);

public sealed record PendingAdRewardClaim(
    Guid SessionId,
    Guid UserId,
    WalletId WalletId,
    string Network,
    PolicyVersion PolicyVersion,
    SourceStampId SourceId,
    IdempotencyKey IdempotencyKey,
    DateTimeOffset CompletedAt);

public sealed record DeferredAdRewardConfirmationCommand(
    Guid SessionId,
    IdempotencyKey IdempotencyKey,
    VerifiedAdProviderReport Report,
    PostingId PostingId,
    CreditLotId OutputLotId,
    ProtectedOperationContext Context,
    RiskDecisionId RiskDecisionId,
    ProtectedIssuanceAuthorization Authorization,
    EntityRiskCluster EntityCluster,
    AdRewardDependencySnapshot Dependencies,
    AdRewardBudgetPolicy Budget,
    DateTimeOffset ConfirmedAt);

public enum AdRewardCompletionState
{
    Issued = 1,
    PendingProviderReport = 2,
    AccumulatedRemainder = 3
}

public sealed record AdRewardCompletionResult(
    AdRewardCompletionState State,
    Guid SessionId,
    AdRewardQuote? Quote,
    AdRewardIssuanceResult? Issuance,
    DateTimeOffset CompletedAt);

public sealed record AdRewardCompletionCommand(
    IdempotencyKey IdempotencyKey,
    SignedAdRewardSession Token,
    AdRewardSessionClaims Claims,
    AdPlaybackEvidence Playback,
    ProviderCompletionProof? Proof,
    SourceStampId SourceId,
    PostingId PostingId,
    CreditLotId OutputLotId,
    ProtectedOperationContext Context,
    RiskDecisionId? RiskDecisionId,
    ProtectedIssuanceAuthorization? Authorization,
    EntityRiskCluster EntityCluster,
    AdRewardDependencySnapshot Dependencies,
    AdRewardBudgetPolicy Budget,
    DateTimeOffset CompletedAt);

public sealed class AdRewardCoordinator
{
    private const string GlobalLossBudgetSubject = "ad-rewards-global";
    private readonly object _gate = new();
    private readonly AdNetworkPolicyStore _policies;
    private readonly AdRewardControlState _controls;
    private readonly AdRewardSessionTokenService _tokens;
    private readonly AdPlaybackVerifier _playback;
    private readonly AdRewardRationalAccumulator _accumulator;
    private readonly TransactionalPostingService _posting;
    private readonly Dictionary<string, AdRewardCompletionResult> _idempotency = new(StringComparer.Ordinal);
    private readonly HashSet<Guid> _consumedSessions = [];
    private readonly HashSet<string> _consumedProofs = new(StringComparer.Ordinal);
    private readonly List<AdRewardCompletionResult> _completions = [];
    private readonly List<AdRewardBudgetConsumption> _budgets = [];
    private readonly List<PendingAdRewardClaim> _pending = [];
    private readonly Dictionary<Guid, AdRewardCompletionCommand> _pendingContexts = [];
    private readonly List<AdRewardAttribution> _attributions = [];

    public AdRewardCoordinator(
        AdNetworkPolicyStore policies,
        AdRewardControlState controls,
        AdRewardSessionTokenService tokens,
        AdPlaybackVerifier playback,
        AdRewardRationalAccumulator accumulator,
        TransactionalPostingService posting)
    {
        _policies = policies ?? throw new ArgumentNullException(nameof(policies));
        _controls = controls ?? throw new ArgumentNullException(nameof(controls));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _playback = playback ?? throw new ArgumentNullException(nameof(playback));
        _accumulator = accumulator ?? throw new ArgumentNullException(nameof(accumulator));
        _posting = posting ?? throw new ArgumentNullException(nameof(posting));
    }

    public IReadOnlyList<AdRewardCompletionResult> Completions
    {
        get { lock (_gate) return [.. _completions]; }
    }

    public IReadOnlyList<AdRewardBudgetConsumption> BudgetConsumptions
    {
        get { lock (_gate) return [.. _budgets]; }
    }

    public IReadOnlyList<PendingAdRewardClaim> PendingClaims
    {
        get { lock (_gate) return [.. _pending]; }
    }

    public IReadOnlyList<AdRewardAttribution> Attributions
    {
        get { lock (_gate) return [.. _attributions]; }
    }

    public AdRewardCompletionResult Complete(AdRewardCompletionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        lock (_gate)
        {
            if (_idempotency.TryGetValue(command.IdempotencyKey.Value, out var duplicate))
            {
                if (duplicate.SessionId == command.Claims.SessionId) return duplicate;
                throw new AdRewardIdempotencyConflictException(
                    "Ad reward idempotency key is bound to another session.");
            }

            var validatedClaims = _tokens.Validate(command.Token.Value, command.CompletedAt);
            if (validatedClaims != command.Claims)
                throw new AdRewardRiskBindingException("Signed session claims do not match the completion command.");
            var policy = _policies.Current(command.Claims.Network, command.CompletedAt);
            if (policy.Version != command.Claims.PolicyVersion)
                throw new AdRewardRiskBindingException("Session policy version is no longer the bound policy.");
            _controls.EnsureIssuanceEnabled(policy.Network);
            var immediate = policy.IssuanceMode == AdRewardIssuanceMode.ImmediateProviderProof;
            command.Dependencies.EnsureReady(immediate, command.CompletedAt);
            if (!policy.IsReportCurrent(command.CompletedAt))
                throw new AdRewardDependencyUnavailableException("Ad network revenue reports are stale.");
            EnsureSessionUnused(command.Claims.SessionId);
            if (command.Proof is not null && _consumedProofs.Contains(command.Proof.ProviderEventId))
                throw new AdProviderProofReplayException("Provider completion proof was already consumed.");
            var independentlyVerified = _playback.Verify(
                command.Claims, command.Playback, command.Proof, policy, command.CompletedAt);
            if (!independentlyVerified)
                return RecordPending(command, policy);

            var quote = _accumulator.Preview(
                command.Claims.WalletId, command.IdempotencyKey, policy, 1);
            if (quote.RewardSoftUnits == 0)
                return RecordRemainderOnly(command, policy);
            ValidateRiskBinding(command, quote);
            var lossBudget = SoftFaceValueUsdNanos(quote.RewardSoftUnits);
            EnsureBudgetAvailable(command, quote.RewardSoftUnits, lossBudget);

            var issuance = _posting.IssueAdReward(new IssueAdRewardCommand(
                command.PostingId,
                command.IdempotencyKey,
                command.SourceId,
                command.Claims.WalletId,
                command.OutputLotId,
                quote.RewardSoftUnits,
                command.Context.ReserveVersion,
                command.Context.PolicyVersion,
                command.Proof!.EvidenceHash,
                command.CompletedAt,
                command.Authorization!));
            var committedQuote = _accumulator.Accrue(
                command.Claims.WalletId, command.IdempotencyKey, policy, 1);
            var budget = new AdRewardBudgetConsumption(
                command.Claims.SessionId,
                command.Claims.UserId,
                command.Claims.DeviceRiskHash,
                command.Claims.Network,
                committedQuote.RewardSoftUnits,
                lossBudget,
                command.CompletedAt);
            _budgets.Add(budget);
            _consumedProofs.Add(command.Proof.ProviderEventId);
            _attributions.Add(CreateAttribution(command.Claims.SessionId, policy, committedQuote, command.CompletedAt));
            return RecordCompletion(new AdRewardCompletionResult(
                AdRewardCompletionState.Issued,
                command.Claims.SessionId,
                committedQuote,
                issuance,
                command.CompletedAt), command);
        }
    }

    public AdRewardCompletionResult ConfirmDeferred(DeferredAdRewardConfirmationCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        lock (_gate)
        {
            if (_idempotency.TryGetValue(command.IdempotencyKey.Value, out var duplicate))
            {
                if (duplicate.SessionId == command.SessionId) return duplicate;
                throw new AdRewardIdempotencyConflictException(
                    "Deferred confirmation idempotency key is bound to another session.");
            }
            if (!_pendingContexts.TryGetValue(command.SessionId, out var pending))
                throw new AdRewardReplayException("Deferred ad reward claim is not pending.");
            if (command.Report.Network != pending.Claims.Network ||
                !command.Report.VerifiedSessionIds.Contains(command.SessionId) ||
                command.Report.ImportedAt > command.ConfirmedAt)
                throw new AdProviderReportVerificationException(
                    "Verified provider report does not authorize this deferred session.");
            _controls.EnsureIssuanceEnabled(pending.Claims.Network);
            command.Dependencies.EnsureReady(false, command.ConfirmedAt);
            var policy = _policies.Get(pending.Claims.Network, pending.Claims.PolicyVersion);
            if (command.ConfirmedAt > command.Report.PeriodEnd + policy.ReportStaleAfter)
                throw new AdRewardDependencyUnavailableException("Deferred provider report is stale.");
            var quote = _accumulator.Preview(
                pending.Claims.WalletId, command.IdempotencyKey, policy, 1);
            var synthetic = pending with
            {
                IdempotencyKey = command.IdempotencyKey,
                PostingId = command.PostingId,
                OutputLotId = command.OutputLotId,
                Context = command.Context,
                RiskDecisionId = command.RiskDecisionId,
                Authorization = command.Authorization,
                EntityCluster = command.EntityCluster,
                Dependencies = command.Dependencies,
                Budget = command.Budget,
                CompletedAt = command.ConfirmedAt
            };
            if (quote.RewardSoftUnits == 0)
                return RecordDeferredRemainderOnly(synthetic, policy, command.Report);
            ValidateRiskBinding(synthetic, quote);
            var lossBudget = SoftFaceValueUsdNanos(quote.RewardSoftUnits);
            EnsureBudgetAvailable(synthetic, quote.RewardSoftUnits, lossBudget);
            var issuance = _posting.IssueAdReward(new IssueAdRewardCommand(
                command.PostingId,
                command.IdempotencyKey,
                pending.SourceId,
                pending.Claims.WalletId,
                command.OutputLotId,
                quote.RewardSoftUnits,
                command.Context.ReserveVersion,
                command.Context.PolicyVersion,
                command.Report.EvidenceHash,
                command.ConfirmedAt,
                command.Authorization));
            var committedQuote = _accumulator.Accrue(
                pending.Claims.WalletId, command.IdempotencyKey, policy, 1);
            _budgets.Add(new AdRewardBudgetConsumption(
                pending.Claims.SessionId,
                pending.Claims.UserId,
                pending.Claims.DeviceRiskHash,
                pending.Claims.Network,
                committedQuote.RewardSoftUnits,
                lossBudget,
                command.ConfirmedAt));
            _attributions.Add(new AdRewardAttribution(
                pending.Claims.SessionId,
                pending.Claims.Network,
                policy.Version,
                command.Report.BatchId,
                EstimatedRevenueUsdNanos(committedQuote),
                committedQuote.RewardSoftUnits,
                command.ConfirmedAt));
            _pending.RemoveAll(item => item.SessionId == command.SessionId);
            _pendingContexts.Remove(command.SessionId);
            return RecordCompletion(new AdRewardCompletionResult(
                AdRewardCompletionState.Issued,
                command.SessionId,
                committedQuote,
                issuance,
                command.ConfirmedAt), synthetic);
        }
    }

    private AdRewardCompletionResult RecordPending(AdRewardCompletionCommand command, AdNetworkPolicy policy)
    {
        _pending.Add(new PendingAdRewardClaim(
            command.Claims.SessionId,
            command.Claims.UserId,
            command.Claims.WalletId,
            command.Claims.Network,
            policy.Version,
            command.SourceId,
            command.IdempotencyKey,
            command.CompletedAt));
        _pendingContexts.Add(command.Claims.SessionId, command);
        return RecordCompletion(new AdRewardCompletionResult(
            AdRewardCompletionState.PendingProviderReport,
            command.Claims.SessionId,
            null,
            null,
            command.CompletedAt), command);
    }

    private AdRewardCompletionResult RecordRemainderOnly(
        AdRewardCompletionCommand command,
        AdNetworkPolicy policy)
    {
        var quote = _accumulator.Accrue(
            command.Claims.WalletId, command.IdempotencyKey, policy, 1);
        _consumedProofs.Add(command.Proof!.ProviderEventId);
        _attributions.Add(CreateAttribution(command.Claims.SessionId, policy, quote, command.CompletedAt));
        return RecordCompletion(new AdRewardCompletionResult(
            AdRewardCompletionState.AccumulatedRemainder,
            command.Claims.SessionId,
            quote,
            null,
            command.CompletedAt), command);
    }

    private AdRewardCompletionResult RecordDeferredRemainderOnly(
        AdRewardCompletionCommand command,
        AdNetworkPolicy policy,
        VerifiedAdProviderReport report)
    {
        var quote = _accumulator.Accrue(
            command.Claims.WalletId, command.IdempotencyKey, policy, 1);
        _attributions.Add(new AdRewardAttribution(
            command.Claims.SessionId,
            command.Claims.Network,
            policy.Version,
            report.BatchId,
            EstimatedRevenueUsdNanos(quote),
            quote.RewardSoftUnits,
            command.CompletedAt));
        _pending.RemoveAll(item => item.SessionId == command.Claims.SessionId);
        _pendingContexts.Remove(command.Claims.SessionId);
        return RecordCompletion(new AdRewardCompletionResult(
            AdRewardCompletionState.AccumulatedRemainder,
            command.Claims.SessionId,
            quote,
            null,
            command.CompletedAt), command);
    }

    private AdRewardCompletionResult RecordCompletion(
        AdRewardCompletionResult result,
        AdRewardCompletionCommand command)
    {
        _consumedSessions.Add(command.Claims.SessionId);
        _idempotency.Add(command.IdempotencyKey.Value, result);
        _completions.Add(result);
        return result;
    }

    private void EnsureSessionUnused(Guid sessionId)
    {
        if (_consumedSessions.Contains(sessionId))
            throw new AdRewardReplayException("Ad reward session was already completed.");
    }

    private static void ValidateRiskBinding(AdRewardCompletionCommand command, AdRewardQuote quote)
    {
        if (command.Authorization is null || command.RiskDecisionId is null ||
            command.Authorization.Risk.DecisionId != command.RiskDecisionId.Value.Value ||
            command.Context.Fingerprint() != command.Authorization.Risk.OperationFingerprint ||
            command.Context.Operation != PostingTemplateKind.AdRewardIssuance ||
            command.Context.IdempotencyKey != command.IdempotencyKey ||
            command.Context.ActorId != command.Claims.UserId ||
            command.Context.SourceWalletId != command.Claims.WalletId ||
            command.Context.DestinationWalletId != command.Claims.WalletId ||
            command.Context.Amount != new CoinAmount(CurrencyCode.SoftCoin, quote.RewardSoftUnits) ||
            command.Context.PolicyVersion != quote.PolicyVersion ||
            command.Context.EntityGraphVersion != command.EntityCluster.Version ||
            !string.Equals(command.Context.EntityGraphEvidenceHash, command.EntityCluster.EvidenceHash, StringComparison.Ordinal) ||
            !command.Context.SourceRoots.SequenceEqual([command.SourceId]))
            throw new AdRewardRiskBindingException("Ad reward risk authorization is not bound to the exact issuance.");

        var nodes = command.EntityCluster.Nodes;
        if (!nodes.Contains(new RiskEntityNode(RiskEntityType.Account, command.Claims.UserId.ToString("N"))) ||
            !nodes.Contains(new RiskEntityNode(RiskEntityType.DeviceRiskToken, command.Claims.DeviceRiskHash)))
            throw new AdRewardRiskBindingException("Entity graph does not contain the bound account and device.");

        var required = new[]
        {
            new RiskLimitKey(RiskLimitDimension.Wallet, command.Claims.WalletId.Value.ToString("N")),
            new RiskLimitKey(RiskLimitDimension.IdentityCluster, command.EntityCluster.Id),
            new RiskLimitKey(RiskLimitDimension.DeviceIpAsnCluster, command.Claims.DeviceRiskHash),
            new RiskLimitKey(RiskLimitDimension.ProviderAccount, command.Claims.Network),
            new RiskLimitKey(RiskLimitDimension.GlobalLossBudget, GlobalLossBudgetSubject),
            new RiskLimitKey(RiskLimitDimension.SourceRoot, command.SourceId.Value.ToString("N"))
        };
        var actual = command.Authorization.Counter.Allocations.Select(allocation => allocation.Key).ToHashSet();
        if (required.Any(key => !actual.Contains(key)))
            throw new AdRewardRiskBindingException("Ad reward authorization lacks required aggregate exposure limits.");
    }

    private void EnsureBudgetAvailable(AdRewardCompletionCommand command, long softUnits, long lossBudget)
    {
        var active = _budgets.Where(item => item.ConsumedAt > command.CompletedAt - command.Budget.Window).ToArray();
        EnsureLimit(active.Where(item => item.UserId == command.Claims.UserId).Sum(item => item.SoftUnits),
            softUnits, command.Budget.MaximumUserSoftUnits, "user");
        EnsureLimit(active.Where(item => item.DeviceRiskHash == command.Claims.DeviceRiskHash).Sum(item => item.SoftUnits),
            softUnits, command.Budget.MaximumDeviceSoftUnits, "device");
        EnsureLimit(active.Where(item => item.Network == command.Claims.Network).Sum(item => item.SoftUnits),
            softUnits, command.Budget.MaximumNetworkSoftUnits, "network");
        EnsureLimit(active.Sum(item => item.SoftUnits),
            softUnits, command.Budget.MaximumGlobalSoftUnits, "global");
        EnsureLimit(active.Sum(item => item.LossBudgetUsdNanos),
            lossBudget, command.Budget.FundedLossBudgetUsdNanos, "funded loss budget");
    }

    private static void EnsureLimit(long consumed, long requested, long maximum, string dimension)
    {
        if (requested > maximum - consumed)
            throw new AdRewardBudgetExceededException($"Ad reward {dimension} limit was exceeded.");
    }

    private static long SoftFaceValueUsdNanos(long softUnits)
    {
        var numerator = checked((Int128)softUnits * 1_000_000_000);
        return checked((long)((numerator + AdRewardRationalAccumulator.SoftCoinsPerUsd - 1) /
            AdRewardRationalAccumulator.SoftCoinsPerUsd));
    }

    private static AdRewardAttribution CreateAttribution(
        Guid sessionId,
        AdNetworkPolicy policy,
        AdRewardQuote quote,
        DateTimeOffset completedAt) => new(
        sessionId,
        policy.Network,
        policy.Version,
        $"{policy.Network}:{completedAt:yyyyMMdd}:{policy.Version.Value}",
        EstimatedRevenueUsdNanos(quote),
        quote.RewardSoftUnits,
        completedAt);

    private static long EstimatedRevenueUsdNanos(AdRewardQuote quote) =>
        checked((long)((Int128)quote.EstimatedNetEcpmUsdNanos * quote.ImpressionCount / 1_000));
}

public sealed class AdRewardDependencyUnavailableException(string message) : InvalidOperationException(message);
public sealed class AdRewardRiskBindingException(string message) : InvalidOperationException(message);
public sealed class AdRewardReplayException(string message) : InvalidOperationException(message);
public sealed class AdProviderProofReplayException(string message) : InvalidOperationException(message);
public sealed class AdRewardBudgetExceededException(string message) : InvalidOperationException(message);
