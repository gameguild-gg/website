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
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.AdRewards;

public sealed class DurableAdRewardCompletionService : IDurableAdRewardCompletionService
{
    private const string RegisteredCapabilityName = "ad-reward-issuance";
    private const string GlobalCapSubject = "ad-rewards-global";
    private readonly DbContext _db;
    private readonly IDurableAdRewardPolicyReader _policies;
    private readonly IAdRewardSessionTokenProtector _tokens;
    private readonly IAdRewardProviderAdapterResolver _providerAdapters;
    private readonly IActorContextAccessor _actorContexts;
    private readonly IEconomyJurisdictionResolver _jurisdictions;
    private readonly IEconomyProtectedOperationOrchestrator _orchestrator;
    private readonly IRegisteredPostingCapabilityResolver _postingAuthority;
    private readonly IAdRewardIssuanceGateway _issuance;

    public DurableAdRewardCompletionService(
        IApplicationDbContext context,
        IDurableAdRewardPolicyReader policies,
        IAdRewardSessionTokenProtector tokens,
        IAdRewardProviderAdapterResolver providerAdapters,
        IActorContextAccessor actorContexts,
        IEconomyJurisdictionResolver jurisdictions,
        IEconomyProtectedOperationOrchestrator orchestrator,
        IRegisteredPostingCapabilityResolver postingAuthority,
        IAdRewardIssuanceGateway issuance)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(providerAdapters);
        ArgumentNullException.ThrowIfNull(actorContexts);
        ArgumentNullException.ThrowIfNull(jurisdictions);
        ArgumentNullException.ThrowIfNull(orchestrator);
        ArgumentNullException.ThrowIfNull(postingAuthority);
        ArgumentNullException.ThrowIfNull(issuance);
        _db = context as DbContext
            ?? throw new InvalidOperationException(
                "Durable ad reward completion requires the application's relational DbContext.");
        _policies = policies;
        _tokens = tokens;
        _providerAdapters = providerAdapters;
        _actorContexts = actorContexts;
        _jurisdictions = jurisdictions;
        _orchestrator = orchestrator;
        _postingAuthority = postingAuthority;
        _issuance = issuance;
    }

    public async ValueTask<DurableAdRewardCompletionResult> CompleteAsync(
        CompleteDurableAdRewardSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var actor = RequiredActor();
        var claims = await _tokens.UnprotectAsync(request.Token, request.CompletedAt, cancellationToken);
        EnsureActorScope(actor, claims);
        var policy = await _policies.GetVersionAsync(
            claims.TenantId, claims.Network, claims.PolicyVersion, cancellationToken);
        if (!policy.Policy.IsEffective(request.CompletedAt) || !policy.Policy.IsReportCurrent(request.CompletedAt) ||
            !policy.ProviderCertified || policy.Policy.IssuanceMode == AdRewardIssuanceMode.Disabled)
            throw new AdRewardDependencyUnavailableException(
                "The signed ad network policy or its reports are not current.");
        ValidatePlayback(claims, request.Playback, policy.Policy, request.CompletedAt);

        AdRewardProviderProofVerification? proofVerification = null;
        if (policy.Policy.IssuanceMode == AdRewardIssuanceMode.ImmediateProviderProof)
        {
            var providerProof = request.ProviderProof
                                ?? throw new AdProviderProofRequiredException(
                            "Independent provider completion proof is required.");
            proofVerification = await _providerAdapters.Resolve(claims.Network)
                .VerifyCompletionAsync(claims, providerProof, request.CompletedAt, cancellationToken);
            if (!proofVerification.IsValid ||
                !string.Equals(proofVerification.EvidenceHash, providerProof.EvidenceHash, StringComparison.Ordinal))
                throw new AdPlaybackVerificationException("Provider completion proof is invalid.");
        }
        else
        {
            _providerAdapters.Resolve(claims.Network);
        }

        var idempotencyHash = Hash(request.IdempotencyKey.Value);
        var requestHash = CompletionRequestHash(request, claims);
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async _ =>
        {
        var session = await _db.Set<AdRewardSessionRow>()
            .SingleOrDefaultAsync(row => row.Id == claims.SessionId, cancellationToken)
            ?? throw new AdRewardReplayException("Ad reward session was not found.");
        EnsurePersistedBinding(session, claims, request.Token);

        var existing = await _db.Set<AdRewardCompletionRow>()
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.SessionId == claims.SessionId, cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.IdempotencyKey, idempotencyHash, StringComparison.Ordinal))
                throw new AdRewardReplayException("Ad reward session was already completed.");
            return Map(existing, true);
        }
        if (session.State != DurableAdRewardSessionState.Issued)
            throw new AdRewardReplayException("Ad reward session is not consumable.");

        PersistMilestones(session.Id, request.Playback, requestHash);
        if (policy.Policy.IssuanceMode == AdRewardIssuanceMode.DeferredReport)
        {
            var pending = RecordDeferred(session, request, claims, idempotencyHash, requestHash);
            await _db.SaveChangesAsync(cancellationToken);
            return Map(pending, false);
        }

        var proof = request.ProviderProof!;
        PersistProviderProof(session, proof, proofVerification!, request.CompletedAt);
        var accumulator = await _db.Set<AdRewardAccumulatorRow>()
            .SingleOrDefaultAsync(
                row => row.TenantId == claims.TenantId &&
                       row.WalletId == claims.WalletId.Value &&
                       row.Network == claims.Network,
                cancellationToken);
        var previousRemainder = accumulator is null
            ? Int128.Zero
            : Int128.Parse(accumulator.RemainderNumerator, CultureInfo.InvariantCulture);
        var quote = AdRewardRationalAccumulator.Calculate(
            claims.WalletId, request.IdempotencyKey, policy.Policy, 1, previousRemainder);
        if (quote.RewardSoftUnits == 0)
        {
            UpsertAccumulator(accumulator, claims, quote, request.CompletedAt);
            var remainder = RecordCompletion(
                session, request, claims, quote, idempotencyHash, proof.ProviderEventId,
                null, null, null, DurableAdRewardSessionState.Posted,
                AdRewardCompletionState.AccumulatedRemainder);
            RecordAttribution(claims, quote, request.CompletedAt);
            await _db.SaveChangesAsync(cancellationToken);
            return Map(remainder, false);
        }

        await EnsureAndRecordCapsAsync(
            claims, policy, quote.RewardSoftUnits, request.CompletedAt, cancellationToken);
        var sourceStampId = new SourceStampId(DeterministicId(claims.SessionId, "source"));
        var postingId = new PostingId(DeterministicId(claims.SessionId, "posting"));
        var outputLotId = new CreditLotId(DeterministicId(claims.SessionId, "lot"));
        var destinationHash = Hash(claims.WalletId.Value.ToString("N"));
        var jurisdiction = await _jurisdictions.ResolveAsync(
            actor.TenantId,
            actor.ActorId,
            null,
            null,
            request.CompletedAt,
            cancellationToken);
        var intent = new EconomyProtectedOperationIntent(
            EconomyValueMovementCapability.IssueAdReward,
            PostingTemplateKind.AdRewardIssuance,
            claims.WalletId,
            claims.WalletId,
            new CoinAmount(CurrencyCode.SoftCoin, quote.RewardSoftUnits),
            [new RiskCurrencyLeg(CurrencyCode.SoftCoin, quote.RewardSoftUnits)],
            [sourceStampId],
            policy.ProviderHash,
            destinationHash,
            request.IdempotencyKey,
            request.CompletedAt,
            DestinationJurisdictionCode: jurisdiction.JurisdictionCode);
        return await _orchestrator.ExecuteAsync(intent, async (authorization, token) =>
        {
            EnsureAuthorization(authorization, actor, jurisdiction, policy);
            var receipt = authorization.Receipt;
            var authority = await _postingAuthority.ResolveAuthorityAsync(
                RegisteredCapabilityName,
                PostingTemplateKind.AdRewardIssuance,
                receipt,
                token);
            var posting = _issuance.Issue(new PersistedAdRewardIssuanceRequest(
                authority,
                postingId,
                request.IdempotencyKey,
                sourceStampId,
                outputLotId,
                claims.WalletId,
                quote.RewardSoftUnits,
                new PolicyVersion(receipt.PolicyVersion),
                new ReserveVersion(receipt.ReserveVersion),
                claims.Network,
                proof.ProviderEventId,
                proofVerification!.EvidenceHash,
                request.CompletedAt,
                receipt.ReceiptHash));
            if (posting.PostingId != postingId)
                throw new RegisteredPostingRejectedException(
                    "The ad reward writer returned an unexpected posting identity.");

            UpsertAccumulator(accumulator, claims, quote, request.CompletedAt);
            RecordBudgetConsumption(claims, quote.RewardSoftUnits, request.CompletedAt);
            RecordAttribution(claims, quote, request.CompletedAt);
            var completion = RecordCompletion(
                session,
                request,
                claims,
                quote,
                idempotencyHash,
                proof.ProviderEventId,
                postingId,
                outputLotId,
                receipt,
                DurableAdRewardSessionState.Posted,
                AdRewardCompletionState.Issued);
            await _db.SaveChangesAsync(token);
            return Map(completion, posting.IsDuplicate);
        }, cancellationToken);
        }, cancellationToken);
    }

    private void PersistMilestones(Guid sessionId, AdPlaybackEvidence playback, string evidenceHash)
    {
        var sequence = 1;
        foreach (var percentage in playback.Milestones)
        {
            _db.Set<AdRewardPlaybackMilestoneRow>().Add(new AdRewardPlaybackMilestoneRow
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                Sequence = sequence++,
                Percentage = percentage,
                ObservedAt = playback.StartedAt +
                             TimeSpan.FromTicks(playback.PlaybackDuration.Ticks * percentage / 100),
                EvidenceHash = evidenceHash
            });
        }
    }

    private void PersistProviderProof(
        AdRewardSessionRow session,
        ProviderCompletionProof proof,
        AdRewardProviderProofVerification verification,
        DateTimeOffset receivedAt)
    {
        _db.Set<AdRewardProviderProofInboxRow>().Add(new AdRewardProviderProofInboxRow
        {
            Id = Guid.NewGuid(),
            TenantId = session.TenantId,
            SessionId = session.Id,
            Network = session.Network,
            ProviderEventId = proof.ProviderEventId,
            PayloadHash = verification.PayloadHash,
            EvidenceHash = verification.EvidenceHash,
            SignatureVerified = true,
            ReceivedAt = receivedAt,
            ProcessedAt = receivedAt
        });
    }

    private AdRewardCompletionRow RecordDeferred(
        AdRewardSessionRow session,
        CompleteDurableAdRewardSessionRequest request,
        DurableAdRewardSessionClaims claims,
        string idempotencyHash,
        string requestHash)
    {
        _db.Set<AdRewardPendingClaimRow>().Add(new AdRewardPendingClaimRow
        {
            SessionId = session.Id,
            TenantId = session.TenantId,
            SourceStampId = DeterministicId(session.Id, "source"),
            CompletionIdempotencyKeyHash = idempotencyHash,
            CompletionRequestHash = requestHash,
            DeferredAt = request.CompletedAt
        });
        return RecordCompletion(
            session, request, claims, null, idempotencyHash, null, null, null, null,
            DurableAdRewardSessionState.Deferred,
            AdRewardCompletionState.PendingProviderReport);
    }

    private AdRewardCompletionRow RecordCompletion(
        AdRewardSessionRow session,
        CompleteDurableAdRewardSessionRequest request,
        DurableAdRewardSessionClaims claims,
        AdRewardQuote? quote,
        string idempotencyHash,
        string? providerEventId,
        PostingId? postingId,
        CreditLotId? outputLotId,
        CapabilityAuthorizationReceipt? receipt,
        DurableAdRewardSessionState sessionState,
        AdRewardCompletionState completionState)
    {
        session.State = sessionState;
        session.UpdatedAt = request.CompletedAt;
        session.Version++;
        var row = new AdRewardCompletionRow
        {
            SessionId = session.Id,
            TenantId = session.TenantId,
            UserId = session.UserId,
            WalletId = session.WalletId,
            Network = session.Network,
            PolicyVersion = session.PolicyVersion,
            IdempotencyKey = idempotencyHash,
            State = completionState,
            RewardSoftUnits = quote?.RewardSoftUnits ?? 0,
            SourceStampId = postingId is null ? null : DeterministicId(session.Id, "source"),
            PostingId = postingId?.Value,
            OutputLotId = outputLotId?.Value,
            ProviderEventId = providerEventId,
            CapabilityReceiptId = receipt?.Id,
            CapabilityReceiptHash = receipt?.ReceiptHash,
            ReserveVersion = receipt?.ReserveVersion,
            RiskDecisionId = receipt?.RiskDecisionId,
            KillSwitchEpoch = receipt?.KillSwitchEpoch,
            JurisdictionCode = receipt?.JurisdictionCode,
            ProviderHash = receipt?.ProviderHash,
            DestinationHash = receipt?.DestinationHash,
            EvidenceHashes = JsonSerializer.Serialize(receipt?.EvidenceHashes ?? []),
            CompletedAt = request.CompletedAt,
            Version = 1
        };
        _db.Set<AdRewardCompletionRow>().Add(row);
        _db.Set<AdRewardSessionEventRow>().Add(new AdRewardSessionEventRow
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Sequence = 2,
            State = sessionState,
            EvidenceHash = receipt?.ReceiptHash ?? CompletionRequestHash(request, claims),
            OccurredAt = request.CompletedAt
        });
        return row;
    }

    private void UpsertAccumulator(
        AdRewardAccumulatorRow? row,
        DurableAdRewardSessionClaims claims,
        AdRewardQuote quote,
        DateTimeOffset updatedAt)
    {
        if (row is null)
        {
            row = new AdRewardAccumulatorRow
            {
                TenantId = claims.TenantId,
                WalletId = claims.WalletId.Value,
                Network = claims.Network,
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

    private async Task EnsureAndRecordCapsAsync(
        DurableAdRewardSessionClaims claims,
        AdRewardNetworkPolicySnapshot policy,
        long softUnits,
        DateTimeOffset consumedAt,
        CancellationToken cancellationToken)
    {
        var startsAfter = consumedAt - policy.Budget.Window;
        var existing = await _db.Set<AdRewardCapConsumptionRow>()
            .AsNoTracking()
            .Where(row => row.TenantId == claims.TenantId && row.ConsumedAt > startsAfter)
            .ToArrayAsync(cancellationToken);
        var scopes = new[]
        {
            (AdRewardCapScope.User, Hash(claims.UserId.ToString("N")), policy.Budget.MaximumUserSoftUnits),
            (AdRewardCapScope.Device, claims.DeviceRiskHash, policy.Budget.MaximumDeviceSoftUnits),
            (AdRewardCapScope.Ip, claims.IpRiskHash, policy.MaximumIpSoftUnits),
            (AdRewardCapScope.Asn, claims.AsnRiskHash, policy.MaximumAsnSoftUnits),
            (AdRewardCapScope.Network, Hash(claims.Network), policy.Budget.MaximumNetworkSoftUnits),
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
                TenantId = claims.TenantId,
                SessionId = claims.SessionId,
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

    private void RecordBudgetConsumption(
        DurableAdRewardSessionClaims claims,
        long softUnits,
        DateTimeOffset consumedAt) =>
        _db.Set<AdRewardBudgetConsumptionRow>().Add(new AdRewardBudgetConsumptionRow
        {
            SessionId = claims.SessionId,
            TenantId = claims.TenantId,
            UserId = claims.UserId,
            DeviceRiskHash = claims.DeviceRiskHash,
            Network = claims.Network,
            SoftUnits = softUnits,
            LossBudgetUsdNanos = SoftFaceValueUsdNanos(softUnits),
            ConsumedAt = consumedAt
        });

    private void RecordAttribution(
        DurableAdRewardSessionClaims claims,
        AdRewardQuote quote,
        DateTimeOffset completedAt) =>
        _db.Set<AdRewardAttributionRow>().Add(new AdRewardAttributionRow
        {
            SessionId = claims.SessionId,
            TenantId = claims.TenantId,
            Network = claims.Network,
            PolicyVersion = quote.PolicyVersion.Value,
            ProviderBatchId = $"{claims.Network}:{completedAt:yyyyMMdd}:{quote.PolicyVersion.Value}",
            EstimatedRevenueUsdNanos = checked((long)((Int128)quote.EstimatedNetEcpmUsdNanos / 1_000)),
            RewardSoftUnits = quote.RewardSoftUnits,
            CompletedAt = completedAt
        });

    private static void EnsurePersistedBinding(
        AdRewardSessionRow session,
        DurableAdRewardSessionClaims claims,
        SignedAdRewardSession token)
    {
        if (session.TenantId != claims.TenantId || session.UserId != claims.UserId ||
            session.WalletId != claims.WalletId.Value || session.Network != claims.Network ||
            session.CreativeId != claims.CreativeId || session.DeviceRiskHash != claims.DeviceRiskHash ||
            session.IpRiskHash != claims.IpRiskHash || session.AsnRiskHash != claims.AsnRiskHash ||
            session.PolicyVersion != claims.PolicyVersion.Value || session.IssuedAt != claims.IssuedAt ||
            session.ExpiresAt != claims.ExpiresAt ||
            session.NonceHash != Hash(claims.Nonce) ||
            session.TokenHash != KmsAdRewardSessionTokenProtector.HashToken(token.Value))
            throw new AdRewardRiskBindingException(
                "Signed session claims do not match the durable session.");
    }

    private static void EnsureActorScope(
        ProtectedActor actor,
        DurableAdRewardSessionClaims claims)
    {
        if (actor.TenantId != claims.TenantId || actor.ActorId != claims.UserId)
            throw new AdRewardRiskBindingException(
                "The actor context does not own the durable ad reward session.");
    }

    private ProtectedActor RequiredActor()
    {
        var actor = _actorContexts.ActorContext;
        if (!actor.IsAuthenticated || actor.TenantId is not { } tenantId ||
            actor.SubjectIdAsGuid is not { } actorId)
            throw new UnauthorizedAccessException(
                "Ad reward completion requires an authenticated tenant actor.");
        return new ProtectedActor(tenantId, actorId);
    }

    private static void EnsureAuthorization(
        EconomyProtectedOperationAuthorization authorization,
        ProtectedActor actor,
        EconomyJurisdictionResolution jurisdiction,
        AdRewardNetworkPolicySnapshot policy)
    {
        if (authorization.TenantId != actor.TenantId || authorization.ActorId != actor.ActorId ||
            authorization.JurisdictionCode != jurisdiction.JurisdictionCode ||
            authorization.Receipt.PolicyVersion != policy.Policy.Version.Value ||
            authorization.Receipt.ProviderHash != policy.ProviderHash)
            throw new AdRewardRiskBindingException(
                "The protected operation authorization does not match the ad reward policy.");
    }

    private static void ValidatePlayback(
        DurableAdRewardSessionClaims claims,
        AdPlaybackEvidence evidence,
        AdNetworkPolicy policy,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (evidence.StartedAt < claims.IssuedAt || evidence.CompletedAt > now ||
            evidence.CompletedAt < evidence.StartedAt ||
            evidence.PlaybackDuration < claims.RequiredDuration ||
            evidence.VisibleDuration < TimeSpan.Zero || evidence.VisibleDuration > evidence.PlaybackDuration ||
            evidence.FocusLoss < TimeSpan.Zero || evidence.FocusLoss > policy.MaximumFocusLoss)
            throw new AdPlaybackVerificationException("Playback timing is not physically valid.");
        if ((decimal)evidence.VisibleDuration.Ticks * 1_000_000 <
            (decimal)evidence.PlaybackDuration.Ticks * policy.MinimumVisiblePpm)
            throw new AdPlaybackVerificationException("Playback visibility is below policy.");
        if (evidence.Milestones.Count < 2 || evidence.Milestones[0] != 0 ||
            evidence.Milestones[^1] != 100 || evidence.Milestones.Any(value => value is < 0 or > 100) ||
            evidence.Milestones.Where((value, index) => index > 0 && value <= evidence.Milestones[index - 1]).Any())
            throw new AdPlaybackVerificationException("Playback milestones are incomplete or unordered.");
    }

    private static void ValidateRequest(CompleteDurableAdRewardSessionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
    }

    private static string CompletionRequestHash(
        CompleteDurableAdRewardSessionRequest request,
        DurableAdRewardSessionClaims claims) => Hash(string.Join('|',
        claims.SessionId.ToString("N"),
        request.IdempotencyKey.Value,
        request.Playback.StartedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        request.Playback.CompletedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        request.Playback.PlaybackDuration.Ticks.ToString(CultureInfo.InvariantCulture),
        request.Playback.VisibleDuration.Ticks.ToString(CultureInfo.InvariantCulture),
        request.Playback.FocusLoss.Ticks.ToString(CultureInfo.InvariantCulture),
        string.Join(',', request.Playback.Milestones),
        request.ProviderProof?.ProviderEventId ?? string.Empty,
        request.ProviderProof?.EvidenceHash ?? string.Empty));

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

    private sealed record ProtectedActor(Guid TenantId, Guid ActorId);
}
