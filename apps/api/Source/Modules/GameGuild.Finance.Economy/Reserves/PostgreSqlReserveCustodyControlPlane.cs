using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Reserves;

public sealed record CustodyObservationCommand(
    Guid Id,
    string Provider,
    string AssetKey,
    ReserveBackingPurpose Purpose,
    long Version,
    long EligibleUsdNanos,
    DateTimeOffset ObservedAt,
    DateTimeOffset ExpiresAt,
    string PayloadHash,
    string KeyId,
    string Signature);

public sealed record DurableCustodyObservation(
    Guid Id,
    string Provider,
    string AssetKey,
    ReserveBackingPurpose Purpose,
    long Version,
    long EligibleUsdNanos,
    DateTimeOffset ObservedAt,
    DateTimeOffset ExpiresAt,
    string PayloadHash,
    string KeyId,
    string Signature);

public sealed record EconomyLiabilitySnapshot(
    long JournalSequence,
    string JournalHash,
    long OutstandingHardUnits,
    long OutstandingSoftUnits,
    long LiabilityUsdNanos);

public sealed record DurableReserveProposalCommand(
    Guid Id,
    long Version,
    long? ExpectedActiveVersion,
    long PolicyVersion,
    long AuthorizationEpoch,
    DateTimeOffset ObservedAt,
    DateTimeOffset ExpiresAt,
    ReserveBufferPosition Buffers,
    IReadOnlyCollection<ReserveServiceObservation> Services,
    IReadOnlyCollection<Guid> CustodyObservationIds,
    long IrreversibleInFlightProviderCostUsdNanos,
    Guid ProposedBy,
    DateTimeOffset ProposedAt);

public sealed record DurableReserveProposalState(
    Guid Id,
    long Version,
    long PolicyVersion,
    long AuthorizationEpoch,
    long LiabilityUsdNanos,
    long EligibleAssetUsdNanos,
    ReserveCoverageState Coverage,
    string SnapshotHash,
    Guid ProposedBy,
    Guid? ApprovedBy,
    string Status,
    DateTimeOffset ObservedAt,
    DateTimeOffset ExpiresAt);

public interface IEconomyReserveCustodyControlPlane
{
    ValueTask<DurableCustodyObservation> IngestObservationAsync(
        CustodyObservationCommand command,
        CancellationToken cancellationToken);

    ValueTask<EconomyLiabilitySnapshot> CalculateLiabilitiesAsync(CancellationToken cancellationToken);

    ValueTask<DurableReserveProposalState> ProposeAsync(
        DurableReserveProposalCommand command,
        CancellationToken cancellationToken);

    ValueTask<ReserveHead> ApproveAndActivateAsync(
        Guid proposalId,
        Guid actorId,
        string reauthenticationHash,
        DateTimeOffset approvedAt,
        CancellationToken cancellationToken);

    ValueTask<ReserveHead> CurrentHeadAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken);

    ValueTask<ReservePostingAuthorization> AuthorizeAsync(
        ReserveVersion version,
        long authorizationEpoch,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}

public sealed class PostgreSqlReserveCustodyControlPlane : IEconomyReserveCustodyControlPlane
{
    private const long UsdNanosPerCent = 10_000_000;
    private readonly DbContext _db;
    private readonly ICapabilityPolicySignatureVerifier _signatureVerifier;

    public PostgreSqlReserveCustodyControlPlane(
        IApplicationDbContext context,
        ICapabilityPolicySignatureVerifier signatureVerifier)
    {
        _db = PostgreSqlEntityRiskGraphStore.RequireRelationalContext(context);
        ArgumentNullException.ThrowIfNull(signatureVerifier);
        _signatureVerifier = signatureVerifier;
    }

    public async ValueTask<DurableCustodyObservation> IngestObservationAsync(
        CustodyObservationCommand command,
        CancellationToken cancellationToken)
    {
        ValidateObservation(command);
        var canonical = CanonicalObservationPayload(command);
        var computedHash = Hash(canonical);
        if (!string.Equals(computedHash, command.PayloadHash, StringComparison.Ordinal))
            throw new CustodyObservationException("The custody payload hash does not match its canonical content.");
        if (!await _signatureVerifier.VerifyAsync(
                canonical, command.KeyId, command.Signature, cancellationToken))
            throw new CustodyObservationException("The custody observation signature is invalid.");

        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async _ =>
        {
        var replay = await _db.Set<EconomyCustodyObservationRow>()
            .SingleOrDefaultAsync(row => row.Id == command.Id, cancellationToken);
        if (replay is not null)
        {
            if (replay.PayloadHash != computedHash)
                throw new CustodyObservationException("A custody observation ID cannot be reused with different content.");
            return Map(replay);
        }

        var latestVersion = await _db.Set<EconomyCustodyObservationRow>()
            .Where(row => row.Provider == command.Provider.Trim() && row.AssetKey == command.AssetKey.Trim())
            .Select(row => (long?)row.Version)
            .MaxAsync(cancellationToken) ?? 0;
        if (command.Version != latestVersion + 1)
            throw new CustodyObservationException("Custody observation versions must be contiguous and monotonic.");
        var row = new EconomyCustodyObservationRow
        {
            Id = command.Id,
            Provider = command.Provider.Trim(),
            AssetKey = command.AssetKey.Trim(),
            Purpose = command.Purpose,
            Version = command.Version,
            EligibleUsdNanos = command.EligibleUsdNanos,
            ObservedAt = command.ObservedAt,
            ExpiresAt = command.ExpiresAt,
            PayloadHash = computedHash,
            KeyId = command.KeyId.Trim(),
            Signature = command.Signature.Trim()
        };
        _db.Set<EconomyCustodyObservationRow>().Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return Map(row);
        }, cancellationToken);
    }

    public async ValueTask<EconomyLiabilitySnapshot> CalculateLiabilitiesAsync(CancellationToken cancellationToken)
    {
        var head = await _db.Set<EconomyChainHeadRow>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == 1, cancellationToken)
            ?? throw new ReserveInputUnknownException("Journal liabilities cannot be calculated without a chain head.");
        var accounts = await _db.Set<EconomyAccountRow>().AsNoTracking()
            .Where(row => row.Code == EconomyAccountCode.PurchasedHardLiability ||
                          row.Code == EconomyAccountCode.EarnedHardLiability ||
                          row.Code == EconomyAccountCode.SoftCoinLiability)
            .ToDictionaryAsync(row => row.Id, cancellationToken);
        var accountIds = accounts.Keys.ToArray();
        var lines = await _db.Set<EconomyJournalLineRow>().AsNoTracking()
            .Where(row => accountIds.Contains(row.AccountId))
            .ToArrayAsync(cancellationToken);
        long hard = 0;
        long soft = 0;
        foreach (var line in lines)
        {
            var delta = line.Side == EntrySide.Credit ? line.AmountUnits : -line.AmountUnits;
            if (accounts[line.AccountId].Code == EconomyAccountCode.SoftCoinLiability)
                soft = checked(soft + delta);
            else
                hard = checked(hard + delta);
        }
        if (hard < 0 || soft < 0)
            throw new ReserveInputUnknownException("Journal liability accounts produced a negative balance.");
        var liabilityNanos = checked(hard * UsdNanosPerCent + ReserveFormula.SoftFaceValueUsdNanos(soft));
        return new EconomyLiabilitySnapshot(head.Sequence, head.Hash, hard, soft, liabilityNanos);
    }

    public async ValueTask<DurableReserveProposalState> ProposeAsync(
        DurableReserveProposalCommand command,
        CancellationToken cancellationToken)
    {
        ValidateProposal(command);
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async _ =>
        {
        var replay = await _db.Set<EconomyReserveProposalRow>()
            .SingleOrDefaultAsync(row => row.Id == command.Id, cancellationToken);
        var requestHash = Hash(CanonicalProposalRequest(command));
        if (replay is not null)
        {
            if (replay.RequestHash != requestHash)
                throw new ReserveVersionConflictException("A reserve proposal ID cannot be reused with different inputs.");
            return Map(replay);
        }

        var active = await _db.Set<EconomyReserveHeadRow>().SingleOrDefaultAsync(row => row.IsActive, cancellationToken);
        if (active?.Version != command.ExpectedActiveVersion || (active is null && command.ExpectedActiveVersion.HasValue))
            throw new ReserveVersionConflictException("The expected active reserve version is stale.");
        var maximumVersion = await _db.Set<EconomyReserveProposalRow>()
            .Select(row => (long?)row.Version).MaxAsync(cancellationToken) ?? 0;
        if (command.Version <= maximumVersion || (active is not null && command.Version <= active.Version))
            throw new ReserveVersionConflictException("Reserve versions must increase monotonically.");
        var maximumEpoch = await _db.Set<EconomyReserveHeadRow>()
            .Select(row => (long?)row.AuthorizationEpoch).MaxAsync(cancellationToken) ?? 0;
        if (command.AuthorizationEpoch <= maximumEpoch)
            throw new ReserveAuthorizationEpochException("Reserve authorization epochs must increase monotonically.");

        var liabilities = await CalculateLiabilitiesAsync(cancellationToken);
        var checkpointValid = await _db.Set<EconomyJournalVerificationCheckpointRow>().AsNoTracking()
            .AnyAsync(row => row.IsValid && row.ToSequence == liabilities.JournalSequence &&
                             row.CurrentHash == liabilities.JournalHash, cancellationToken);
        if (!checkpointValid)
            throw new ReserveInputUnknownException("Reserve proposals require a fully verified journal head.");
        var observations = await LoadAndValidateObservationsAsync(
            command.CustodyObservationIds, command.ProposedAt, cancellationToken);
        var assets = observations.Select(row => new ExternalReserveAsset(
            row.AssetKey, row.Purpose, row.EligibleUsdNanos)).ToArray();
        if (assets.Select(asset => asset.AssetKey).Distinct(StringComparer.Ordinal).Count() != assets.Length)
            throw new DuplicateReserveAssetException("A custody asset cannot back multiple pools in one reserve head.");

        var requirements = CalculateRequirements(command, liabilities);
        var hardBacking = SumBacking(assets, ReserveBackingPurpose.HardCoin);
        var softBacking = SumBacking(assets, ReserveBackingPurpose.SoftCoin);
        var coverage = checked(requirements.RequiredHardReserveUsdMinor * UsdNanosPerCent) <= hardBacking &&
                       requirements.RequiredSoftReserveUsdNanos <= softBacking
            ? ReserveCoverageState.Covered
            : ReserveCoverageState.Shortfall;
        var observationIds = observations.Select(row => row.Id).Order().ToArray();
        var allocationsJson = JsonSerializer.Serialize(assets.OrderBy(row => row.AssetKey, StringComparer.Ordinal));
        var observationIdsJson = JsonSerializer.Serialize(observationIds);
        var snapshotPayload = Canonicalize(new
        {
            command.Version,
            command.PolicyVersion,
            command.AuthorizationEpoch,
            liabilities,
            requirements,
            hard_backing_usd_nanos = hardBacking,
            soft_backing_usd_nanos = softBacking,
            coverage,
            observation_ids = observationIds,
            asset_allocations = assets.OrderBy(row => row.AssetKey, StringComparer.Ordinal),
            command.ObservedAt,
            command.ExpiresAt
        });
        var snapshotHash = Hash(snapshotPayload);
        var eligibleAssets = checked(hardBacking + softBacking);
        var row = new EconomyReserveProposalRow
        {
            Id = command.Id,
            Version = command.Version,
            PolicyVersion = command.PolicyVersion,
            ExpectedActiveVersion = command.ExpectedActiveVersion,
            AuthorizationEpoch = command.AuthorizationEpoch,
            SnapshotHash = snapshotHash,
            LiabilityUsdNanos = liabilities.LiabilityUsdNanos,
            EligibleAssetUsdNanos = eligibleAssets,
            HardFaceValueUsdMinor = requirements.HardFaceValueUsdMinor,
            RequiredHardReserveUsdMinor = requirements.RequiredHardReserveUsdMinor,
            SoftFaceValueUsdNanos = requirements.SoftFaceValueUsdNanos,
            StressedExpectedRedemptionCostUsdNanos = requirements.StressedExpectedRedemptionCostUsdNanos,
            RequiredSoftReserveUsdNanos = requirements.RequiredSoftReserveUsdNanos,
            HardBackingUsdNanos = hardBacking,
            SoftBackingUsdNanos = softBacking,
            Coverage = coverage,
            ObservationIds = observationIdsJson,
            AssetAllocations = allocationsJson,
            EvidenceHash = snapshotHash,
            RequestHash = requestHash,
            ProposedBy = command.ProposedBy,
            ProposedAt = command.ProposedAt,
            ObservedAt = command.ObservedAt,
            ExpiresAt = command.ExpiresAt,
            Status = "PendingApproval"
        };
        _db.Set<EconomyReserveProposalRow>().Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return Map(row);
        }, cancellationToken);
    }

    public async ValueTask<ReserveHead> ApproveAndActivateAsync(
        Guid proposalId,
        Guid actorId,
        string reauthenticationHash,
        DateTimeOffset approvedAt,
        CancellationToken cancellationToken)
    {
        if (proposalId == Guid.Empty) throw new ArgumentException("Proposal ID cannot be empty.", nameof(proposalId));
        if (actorId == Guid.Empty) throw new ArgumentException("Actor ID cannot be empty.", nameof(actorId));
        ArgumentException.ThrowIfNullOrWhiteSpace(reauthenticationHash);
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async _ =>
        {
        var proposal = await _db.Set<EconomyReserveProposalRow>()
            .SingleOrDefaultAsync(row => row.Id == proposalId, cancellationToken)
            ?? throw new KeyNotFoundException("Reserve proposal not found.");
        if (proposal.Status != "PendingApproval")
            throw new InvalidOperationException("Only pending reserve proposals can be approved.");
        if (proposal.ProposedBy == actorId)
            throw new InvalidOperationException("The reserve proposer cannot approve their own proposal.");
        if (approvedAt < proposal.ProposedAt || approvedAt >= proposal.ExpiresAt)
            throw new ReserveInputUnknownException("The reserve proposal is stale or approval predates proposal.");
        var observationIds = JsonSerializer.Deserialize<Guid[]>(proposal.ObservationIds) ?? [];
        await LoadAndValidateObservationsAsync(observationIds, approvedAt, cancellationToken);
        var active = await _db.Set<EconomyReserveHeadRow>().SingleOrDefaultAsync(row => row.IsActive, cancellationToken);
        if (active?.Version != proposal.ExpectedActiveVersion || (active is null && proposal.ExpectedActiveVersion.HasValue))
            throw new ReserveVersionConflictException("The active reserve head changed after proposal creation.");
        if (active is not null) active.IsActive = false;

        var assets = JsonSerializer.Deserialize<ExternalReserveAsset[]>(proposal.AssetAllocations) ?? [];
        var head = new EconomyReserveHeadRow
        {
            Version = proposal.Version,
            IsActive = true,
            PolicyVersion = proposal.PolicyVersion,
            AuthorizationEpoch = proposal.AuthorizationEpoch,
            ObservedAt = proposal.ObservedAt,
            ExpiresAt = proposal.ExpiresAt,
            HardFaceValueUsdMinor = proposal.HardFaceValueUsdMinor,
            RequiredHardReserveUsdMinor = proposal.RequiredHardReserveUsdMinor,
            SoftFaceValueUsdNanos = proposal.SoftFaceValueUsdNanos,
            StressedExpectedRedemptionCostUsdNanos = proposal.StressedExpectedRedemptionCostUsdNanos,
            RequiredSoftReserveUsdNanos = proposal.RequiredSoftReserveUsdNanos,
            HardBackingUsdNanos = proposal.HardBackingUsdNanos,
            SoftBackingUsdNanos = proposal.SoftBackingUsdNanos,
            Coverage = proposal.Coverage,
            EvidenceHash = proposal.EvidenceHash,
            ActivatedAt = approvedAt
        };
        _db.Set<EconomyReserveHeadRow>().Add(head);
        _db.Set<EconomyReserveAssetAllocationRow>().AddRange(assets.Select(asset => new EconomyReserveAssetAllocationRow
        {
            Id = Guid.NewGuid(), ReserveVersion = proposal.Version, AssetKey = asset.AssetKey,
            Purpose = asset.Purpose, EligibleUsdNanos = asset.EligibleUsdNanos
        }));
        _db.Set<EconomyCustodyReconciliationRow>().Add(new EconomyCustodyReconciliationRow
        {
            Id = Guid.NewGuid(),
            ReserveVersion = proposal.Version,
            ObservationIds = proposal.ObservationIds,
            LiabilityUsdNanos = proposal.LiabilityUsdNanos,
            EligibleAssetUsdNanos = proposal.EligibleAssetUsdNanos,
            VarianceUsdNanos = checked(proposal.EligibleAssetUsdNanos - proposal.LiabilityUsdNanos),
            IsReconciled = true,
            EvidenceHash = proposal.EvidenceHash,
            ReconciledBy = actorId,
            ReconciledAt = approvedAt
        });
        proposal.ApprovedBy = actorId;
        proposal.ApprovalReauthenticationHash = reauthenticationHash.Trim();
        proposal.ApprovedAt = approvedAt;
        proposal.Status = "Active";
        await _db.SaveChangesAsync(cancellationToken);
        return MapHead(head, assets);
        }, cancellationToken);
    }

    public async ValueTask<ReservePostingAuthorization> AuthorizeAsync(
        ReserveVersion version,
        long authorizationEpoch,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var head = await CurrentHeadAsync(now, cancellationToken).ConfigureAwait(false);
        if (head.Version != version)
            throw new ReserveAuthorizationException("The requested reserve version is not active.");
        if (head.AuthorizationEpoch != authorizationEpoch)
            throw new ReserveAuthorizationEpochException("The requested reserve authorization epoch is not active.");
        return new ReservePostingAuthorization(version, authorizationEpoch, now);
    }

    public async ValueTask<ReserveHead> CurrentHeadAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var head = await _db.Set<EconomyReserveHeadRow>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.IsActive, cancellationToken)
            ?? throw new ReserveAuthorizationException("No authoritative reserve head is active.");
        if (head.ObservedAt > now || head.ExpiresAt <= now)
            throw new ReserveInputUnknownException("The active reserve head is stale.");
        if (head.Coverage != ReserveCoverageState.Covered)
            throw new ReserveShortfallException("The active reserve head does not cover required liabilities and buffers.");
        var reconciliation = await _db.Set<EconomyCustodyReconciliationRow>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.ReserveVersion == head.Version, cancellationToken);
        if (reconciliation?.IsReconciled != true || reconciliation.EvidenceHash != head.EvidenceHash)
            throw new ReserveInputUnknownException("Custody is not reconciled for the active reserve head.");
        var observationIds = JsonSerializer.Deserialize<Guid[]>(reconciliation.ObservationIds) ?? [];
        _ = await LoadAndValidateObservationsAsync(observationIds, now, cancellationToken).ConfigureAwait(false);
        var assets = await _db.Set<EconomyReserveAssetAllocationRow>().AsNoTracking()
            .Where(row => row.ReserveVersion == head.Version)
            .OrderBy(row => row.Purpose).ThenBy(row => row.AssetKey)
            .Select(row => new ExternalReserveAsset(row.AssetKey, row.Purpose, row.EligibleUsdNanos))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var hardBacking = assets.Where(asset => asset.Purpose == ReserveBackingPurpose.HardCoin)
            .Aggregate(0L, (total, asset) => checked(total + asset.EligibleUsdNanos));
        var softBacking = assets.Where(asset => asset.Purpose == ReserveBackingPurpose.SoftCoin)
            .Aggregate(0L, (total, asset) => checked(total + asset.EligibleUsdNanos));
        if (assets.Length == 0 || hardBacking != head.HardBackingUsdNanos ||
            softBacking != head.SoftBackingUsdNanos ||
            checked(hardBacking + softBacking) != reconciliation.EligibleAssetUsdNanos)
            throw new ReserveInputUnknownException(
                "Reserve asset allocations do not match the reconciled custody snapshot.");
        return MapHead(head, assets);
    }

    public static string CanonicalObservationPayload(CustodyObservationCommand command) => Canonicalize(new
    {
        id = command.Id,
        provider = command.Provider.Trim(),
        asset_key = command.AssetKey.Trim(),
        purpose = command.Purpose,
        version = command.Version,
        eligible_usd_nanos = command.EligibleUsdNanos,
        observed_at = command.ObservedAt,
        expires_at = command.ExpiresAt
    });

    public static string Hash(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private async ValueTask<EconomyCustodyObservationRow[]> LoadAndValidateObservationsAsync(
        IReadOnlyCollection<Guid> ids,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Count == 0 || ids.Any(id => id == Guid.Empty) || ids.Distinct().Count() != ids.Count)
            throw new ReserveInputUnknownException("A reserve proposal requires unique custody observations.");
        var observations = await _db.Set<EconomyCustodyObservationRow>()
            .Where(row => ids.Contains(row.Id)).ToArrayAsync(cancellationToken);
        if (observations.Length != ids.Count)
            throw new ReserveInputUnknownException("A referenced custody observation does not exist.");
        foreach (var row in observations)
        {
            if (row.ObservedAt > now || row.ExpiresAt <= now)
                throw new ReserveInputUnknownException("A custody observation is stale.");
            var latest = await _db.Set<EconomyCustodyObservationRow>()
                .Where(item => item.Provider == row.Provider && item.AssetKey == row.AssetKey)
                .MaxAsync(item => item.Version, cancellationToken);
            if (row.Version != latest)
                throw new ReserveInputUnknownException("A custody observation was superseded.");
            var command = new CustodyObservationCommand(
                row.Id, row.Provider, row.AssetKey, row.Purpose, row.Version, row.EligibleUsdNanos,
                row.ObservedAt, row.ExpiresAt, row.PayloadHash, row.KeyId, row.Signature);
            var canonical = CanonicalObservationPayload(command);
            if (Hash(canonical) != row.PayloadHash || !await _signatureVerifier.VerifyAsync(
                    canonical, row.KeyId, row.Signature, cancellationToken))
                throw new ReserveInputUnknownException("A custody observation signature is invalid.");
        }
        return observations;
    }

    private static ReserveRequirementSnapshot CalculateRequirements(
        DurableReserveProposalCommand command,
        EconomyLiabilitySnapshot liabilities)
    {
        var hardFace = ReserveFormula.HardFaceValueUsdMinor(liabilities.OutstandingHardUnits);
        var requiredHard = ReserveFormula.RequiredHardReserveUsdMinor(
            hardFace,
            command.Buffers.ChargebackRefundBufferUsdMinor,
            command.Buffers.PayoutSettlementBufferUsdMinor,
            command.Buffers.HardOperatingLiquidityBufferUsdMinor);
        var softFace = ReserveFormula.SoftFaceValueUsdNanos(liabilities.OutstandingSoftUnits);
        var stressed = ReserveFormula.StressedExpectedRedemptionCostUsdNanos(
            liabilities.OutstandingSoftUnits,
            liabilities.OutstandingSoftUnits,
            command.IrreversibleInFlightProviderCostUsdNanos,
            command.Services,
            command.ProposedAt);
        var requiredSoft = ReserveFormula.RequiredSoftReserveUsdNanos(
            softFace,
            stressed,
            command.Buffers.AdEstimateVarianceBufferUsdNanos,
            command.Buffers.FraudLossBudgetUsdNanos,
            command.Buffers.ProviderFxBufferUsdNanos,
            command.Buffers.SoftOperatingLiquidityBufferUsdNanos);
        return new ReserveRequirementSnapshot(hardFace, requiredHard, softFace, stressed, requiredSoft);
    }

    private static long SumBacking(IEnumerable<ExternalReserveAsset> assets, ReserveBackingPurpose purpose) =>
        assets.Where(asset => asset.Purpose == purpose)
            .Aggregate(0L, (total, asset) => checked(total + asset.EligibleUsdNanos));

    private static string CanonicalProposalRequest(DurableReserveProposalCommand command) => Canonicalize(new
    {
        command.Id,
        command.Version,
        command.ExpectedActiveVersion,
        command.PolicyVersion,
        command.AuthorizationEpoch,
        command.ObservedAt,
        command.ExpiresAt,
        command.Buffers,
        services = command.Services.OrderBy(row => row.ServiceCode, StringComparer.Ordinal),
        custody_observation_ids = command.CustodyObservationIds.Order(),
        command.IrreversibleInFlightProviderCostUsdNanos,
        command.ProposedBy,
        command.ProposedAt
    });

    private static string Canonicalize<T>(T value)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value));
        return EconomyCanonicalJson.Serialize(document.RootElement);
    }

    private static void ValidateObservation(CustodyObservationCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Id == Guid.Empty) throw new ArgumentException("Observation ID cannot be empty.", nameof(command));
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.AssetKey);
        if (!Enum.IsDefined(command.Purpose)) throw new ArgumentOutOfRangeException(nameof(command));
        if (command.Version <= 0 || command.EligibleUsdNanos < 0 || command.ExpiresAt <= command.ObservedAt)
            throw new CustodyObservationException("Custody observation values or lifetime are invalid.");
        ArgumentException.ThrowIfNullOrWhiteSpace(command.PayloadHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.KeyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Signature);
    }

    private static void ValidateProposal(DurableReserveProposalCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Id == Guid.Empty || command.ProposedBy == Guid.Empty)
            throw new ArgumentException("Proposal and proposer IDs are required.", nameof(command));
        if (command.Version <= 0 || command.PolicyVersion <= 0 || command.AuthorizationEpoch <= 0)
            throw new ArgumentOutOfRangeException(nameof(command));
        if (command.ObservedAt > command.ProposedAt || command.ExpiresAt <= command.ProposedAt)
            throw new ReserveInputUnknownException("Reserve proposal evidence is stale or has an invalid window.");
        ArgumentNullException.ThrowIfNull(command.Buffers);
        ArgumentNullException.ThrowIfNull(command.Services);
        ArgumentNullException.ThrowIfNull(command.CustodyObservationIds);
        if (command.IrreversibleInFlightProviderCostUsdNanos < 0)
            throw new ArgumentOutOfRangeException(nameof(command));
    }

    private static DurableCustodyObservation Map(EconomyCustodyObservationRow row) => new(
        row.Id, row.Provider, row.AssetKey, row.Purpose, row.Version, row.EligibleUsdNanos,
        row.ObservedAt, row.ExpiresAt, row.PayloadHash, row.KeyId, row.Signature);

    private static DurableReserveProposalState Map(EconomyReserveProposalRow row) => new(
        row.Id, row.Version, row.PolicyVersion, row.AuthorizationEpoch, row.LiabilityUsdNanos,
        row.EligibleAssetUsdNanos, row.Coverage, row.SnapshotHash, row.ProposedBy, row.ApprovedBy,
        row.Status, row.ObservedAt, row.ExpiresAt);

    private static ReserveHead MapHead(EconomyReserveHeadRow row, IReadOnlyList<ExternalReserveAsset> assets) => new(
        new ReserveVersion(row.Version),
        new PolicyVersion(row.PolicyVersion),
        row.AuthorizationEpoch,
        row.ObservedAt,
        row.ExpiresAt,
        new ReserveRequirementSnapshot(
            row.HardFaceValueUsdMinor,
            row.RequiredHardReserveUsdMinor,
            row.SoftFaceValueUsdNanos,
            row.StressedExpectedRedemptionCostUsdNanos,
            row.RequiredSoftReserveUsdNanos),
        row.HardBackingUsdNanos,
        row.SoftBackingUsdNanos,
        row.Coverage,
        assets,
        row.EvidenceHash);
}

public sealed class CustodyObservationException(string message) : InvalidOperationException(message);
