using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Commerce.Payments;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.Operations;

public enum LegacyEconomyShadowState
{
    Captured = 1,
    Backfilling = 2,
    Backfilled = 3,
    Reconciled = 4,
    CutoverProposed = 5,
    CutoverActive = 6,
    RolledBack = 7,
    Failed = 8
}

public sealed record CaptureLegacyEconomyShadowCommand(
    Guid BatchId,
    Guid TenantId,
    Guid ActorId,
    string JurisdictionCode,
    DateTimeOffset CapturedAt);

public sealed record BackfillLegacyEconomyWalletCommand(
    Guid BatchId,
    Guid TenantId,
    Guid ActorId,
    Guid LegacyWalletId,
    Guid RiskDecisionId,
    string OperationFingerprint,
    DateTimeOffset PostedAt);

public sealed record ReconcileLegacyEconomyShadowCommand(
    Guid BatchId,
    Guid TenantId,
    Guid ActorId,
    DateTimeOffset ReconciledAt);

public sealed record ProposeLegacyEconomyCutoverCommand(
    Guid BatchId,
    Guid TenantId,
    Guid ActorId,
    string Reason,
    string ReauthenticationHash,
    DateTimeOffset ProposedAt);

public sealed record ApproveLegacyEconomyCutoverCommand(
    Guid BatchId,
    Guid TenantId,
    Guid ActorId,
    string ReauthenticationHash,
    DateTimeOffset ApprovedAt);

public sealed record RollbackLegacyEconomyCutoverCommand(
    Guid BatchId,
    Guid TenantId,
    Guid ActorId,
    string Reason,
    string ReauthenticationHash,
    DateTimeOffset RolledBackAt);

public sealed record LegacyEconomyShadowWalletView
{
    public LegacyEconomyShadowWalletView(
        Guid legacyWalletId,
        Guid? economyWalletId,
        Guid ownerId,
        long legacyBalanceMinorUnits,
        long completedCreditsMinorUnits,
        long completedDebitsMinorUnits,
        int transactionCount,
        string state,
        Guid sourceStampId,
        Guid postingId,
        Guid creditLotId,
        long? journalSequence,
        string? failureCode)
    {
        LegacyWalletId = legacyWalletId;
        EconomyWalletId = economyWalletId;
        OwnerId = ownerId;
        LegacyBalanceMinorUnits = legacyBalanceMinorUnits;
        CompletedCreditsMinorUnits = completedCreditsMinorUnits;
        CompletedDebitsMinorUnits = completedDebitsMinorUnits;
        TransactionCount = transactionCount;
        State = state;
        SourceStampId = sourceStampId;
        PostingId = postingId;
        CreditLotId = creditLotId;
        JournalSequence = journalSequence;
        FailureCode = failureCode;
    }

    public Guid LegacyWalletId { get; }
    public Guid? EconomyWalletId { get; }
    public Guid OwnerId { get; }
    public long LegacyBalanceMinorUnits { get; }
    public long CompletedCreditsMinorUnits { get; }
    public long CompletedDebitsMinorUnits { get; }
    public int TransactionCount { get; }
    public string State { get; }
    public Guid SourceStampId { get; }
    public Guid PostingId { get; }
    public Guid CreditLotId { get; }
    public long? JournalSequence { get; }
    public string? FailureCode { get; }
}

public sealed record LegacyEconomyShadowBatchView(
    Guid Id,
    Guid TenantId,
    LegacyEconomyShadowState State,
    long PolicyVersion,
    int WalletCount,
    int TransactionCount,
    int FinancialLedgerEntryCount,
    long ExpectedHardUnits,
    long BackfilledHardUnits,
    long ReconciledHardUnits,
    string WalletSnapshotHash,
    string TransactionSnapshotHash,
    string FinancialLedgerSnapshotHash,
    string? FailureCode,
    IReadOnlyList<LegacyEconomyShadowWalletView> Wallets);

public interface ILegacyEconomyShadowMigration
{
    ValueTask<LegacyEconomyShadowBatchView> CaptureAsync(
        CaptureLegacyEconomyShadowCommand command,
        CancellationToken cancellationToken = default);

    ValueTask<LegacyEconomyShadowBatchView> BackfillAsync(
        BackfillLegacyEconomyWalletCommand command,
        CancellationToken cancellationToken = default);

    ValueTask<LegacyEconomyShadowBatchView> ReconcileAsync(
        ReconcileLegacyEconomyShadowCommand command,
        CancellationToken cancellationToken = default);

    ValueTask<LegacyEconomyShadowBatchView> ProposeCutoverAsync(
        ProposeLegacyEconomyCutoverCommand command,
        CancellationToken cancellationToken = default);

    ValueTask<LegacyEconomyShadowBatchView> ApproveCutoverAsync(
        ApproveLegacyEconomyCutoverCommand command,
        CancellationToken cancellationToken = default);

    ValueTask<LegacyEconomyShadowBatchView> RollbackCutoverAsync(
        RollbackLegacyEconomyCutoverCommand command,
        CancellationToken cancellationToken = default);

    ValueTask<LegacyEconomyShadowBatchView?> GetAsync(
        Guid tenantId,
        Guid batchId,
        CancellationToken cancellationToken = default);
}

public sealed class LegacyEconomyShadowMigrationException(string message) : InvalidOperationException(message);

public sealed class PostgreSqlLegacyEconomyShadowMigration : ILegacyEconomyShadowMigration
{
    private const string CapabilityName = "legacy-balance-backfill";
    private const string ExpectedProvider = "legacy-shadow-v1";
    private readonly DbContext _db;
    private readonly IEconomyCapabilityPolicyStore _policies;
    private readonly IEconomyCapabilityAuthorizationService _capabilities;
    private readonly IRegisteredPostingCapabilityResolver _postingAuthorities;
    private readonly IEconomyWalletProvisioner _walletProvisioner;
    private readonly ILegacyBalanceBackfillGateway _backfill;

    public PostgreSqlLegacyEconomyShadowMigration(
        IApplicationDbContext context,
        IEconomyCapabilityPolicyStore policies,
        IEconomyCapabilityAuthorizationService capabilities,
        IRegisteredPostingCapabilityResolver postingAuthorities,
        IEconomyWalletProvisioner walletProvisioner,
        ILegacyBalanceBackfillGateway backfill)
    {
        ArgumentNullException.ThrowIfNull(context);
        _db = context as DbContext ?? throw new InvalidOperationException(
            "Legacy Economy shadow migration requires the application's relational DbContext.");
        _policies = policies ?? throw new ArgumentNullException(nameof(policies));
        _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        _postingAuthorities = postingAuthorities ?? throw new ArgumentNullException(nameof(postingAuthorities));
        _walletProvisioner = walletProvisioner ?? throw new ArgumentNullException(nameof(walletProvisioner));
        _backfill = backfill ?? throw new ArgumentNullException(nameof(backfill));
    }

    public async ValueTask<LegacyEconomyShadowBatchView> CaptureAsync(
        CaptureLegacyEconomyShadowCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(command.BatchId, command.TenantId, command.ActorId);
        var jurisdiction = Required(command.JurisdictionCode, nameof(command.JurisdictionCode)).ToUpperInvariant();
        var policy = await CurrentPolicyAsync(command.TenantId, jurisdiction, command.CapturedAt, cancellationToken);
        ValidateMigrationPolicy(policy);
        var requestHash = Hash(string.Join('|', command.BatchId.ToString("N"), command.TenantId.ToString("N"),
            command.ActorId.ToString("N"), jurisdiction, command.CapturedAt.UtcTicks, policy.Version, policy.PayloadHash));

        var replay = await _db.Set<EconomyLegacyShadowBatchRow>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == command.BatchId, cancellationToken);
        if (replay is not null)
        {
            if (replay.RequestHash != requestHash)
                throw new LegacyEconomyShadowMigrationException(
                    "The legacy shadow batch ID is already bound to different capture inputs.");
            return await RequiredBatchAsync(command.TenantId, command.BatchId, cancellationToken);
        }

        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async _ =>
        {
        var wallets = await _db.Set<UserWallet>().AsNoTracking()
            .Where(wallet => wallet.TenantId == command.TenantId && wallet.DeletedAt == null)
            .OrderBy(wallet => wallet.Id)
            .ToArrayAsync(cancellationToken);
        var walletIds = wallets.Select(wallet => wallet.Id).ToArray();
        var transactions = walletIds.Length == 0
            ? []
            : await _db.Set<WalletTransaction>().AsNoTracking()
                .Where(transaction => walletIds.Contains(transaction.WalletId) && transaction.DeletedAt == null)
                .OrderBy(transaction => transaction.WalletId)
                .ThenBy(transaction => transaction.CreatedAt)
                .ThenBy(transaction => transaction.Id)
                .ToArrayAsync(cancellationToken);
        var financialLedger = await ReadFinancialLedgerSnapshotAsync(command.TenantId, cancellationToken);
        var transactionGroups = transactions.GroupBy(transaction => transaction.WalletId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var itemRows = new List<EconomyLegacyShadowWalletRow>(wallets.Length);
        foreach (var wallet in wallets)
        {
            var walletTransactions = transactionGroups.GetValueOrDefault(wallet.Id) ?? [];
            var assessment = Assess(wallet, walletTransactions);
            Guid? economyWalletId = null;
            if (assessment.FailureCode is null)
            {
                var provisioned = await _walletProvisioner.ProvisionAsync(
                    command.TenantId, wallet.UserId, command.CapturedAt, cancellationToken);
                economyWalletId = provisioned.WalletId.Value;
            }

            itemRows.Add(new EconomyLegacyShadowWalletRow
            {
                Id = DeterministicId(command.BatchId, $"legacy-item:{wallet.Id:N}"),
                BatchId = command.BatchId,
                TenantId = command.TenantId,
                LegacyWalletId = wallet.Id,
                EconomyWalletId = economyWalletId,
                OwnerId = wallet.UserId,
                LegacyBalanceMinorUnits = assessment.BalanceMinorUnits,
                CompletedCreditsMinorUnits = assessment.CreditsMinorUnits,
                CompletedDebitsMinorUnits = assessment.DebitsMinorUnits,
                TransactionCount = walletTransactions.Length,
                SnapshotHash = assessment.SnapshotHash,
                SourceStampId = DeterministicId(command.BatchId, $"legacy-source:{wallet.Id:N}"),
                PostingId = DeterministicId(command.BatchId, $"legacy-posting:{wallet.Id:N}"),
                CreditLotId = DeterministicId(command.BatchId, $"legacy-lot:{wallet.Id:N}"),
                State = assessment.FailureCode is null
                    ? EconomyLegacyShadowItemState.Captured
                    : EconomyLegacyShadowItemState.Blocked,
                FailureCode = assessment.FailureCode,
                CapturedAt = command.CapturedAt,
                Version = 1
            });
        }

        var walletSnapshotHash = Hash(string.Join('\n', itemRows.Select(row =>
            $"{row.LegacyWalletId:N}|{row.OwnerId:N}|{row.LegacyBalanceMinorUnits}|{row.SnapshotHash}")));
        var transactionSnapshotHash = Hash(string.Join('\n', transactions.Select(CanonicalTransaction)));
        var blocked = itemRows.Count(row => row.State == EconomyLegacyShadowItemState.Blocked);
        var batch = new EconomyLegacyShadowBatchRow
        {
            Id = command.BatchId,
            TenantId = command.TenantId,
            RequestedBy = command.ActorId,
            JurisdictionCode = jurisdiction,
            PolicyVersion = policy.Version,
            State = blocked == 0 ? EconomyLegacyShadowBatchState.Captured : EconomyLegacyShadowBatchState.Failed,
            WalletCount = itemRows.Count,
            TransactionCount = transactions.Length,
            FinancialLedgerEntryCount = financialLedger.Count,
            ExpectedHardUnits = itemRows.Sum(row => row.LegacyBalanceMinorUnits),
            WalletSnapshotHash = walletSnapshotHash,
            TransactionSnapshotHash = transactionSnapshotHash,
            FinancialLedgerSnapshotHash = financialLedger.Hash,
            RequestHash = requestHash,
            FailureCode = blocked == 0 ? null : $"blocked-wallets:{blocked}",
            CapturedAt = command.CapturedAt,
            UpdatedAt = command.CapturedAt,
            Version = 1
        };
        _db.Set<EconomyLegacyShadowBatchRow>().Add(batch);
        _db.Set<EconomyLegacyShadowWalletRow>().AddRange(itemRows);
        await _db.SaveChangesAsync(cancellationToken);
        return await RequiredBatchAsync(command.TenantId, command.BatchId, cancellationToken);
        }, cancellationToken);
    }

    public async ValueTask<LegacyEconomyShadowBatchView> BackfillAsync(
        BackfillLegacyEconomyWalletCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(command.BatchId, command.TenantId, command.ActorId);
        if (command.LegacyWalletId == Guid.Empty)
            throw new ArgumentException("Legacy wallet ID is required.", nameof(command));
        if (command.RiskDecisionId == Guid.Empty)
            throw new ArgumentException("Risk decision ID is required.", nameof(command));
        var fingerprint = Required(command.OperationFingerprint, nameof(command.OperationFingerprint));
        var batch = await _db.Set<EconomyLegacyShadowBatchRow>()
            .SingleOrDefaultAsync(row => row.Id == command.BatchId && row.TenantId == command.TenantId,
                cancellationToken) ?? throw new KeyNotFoundException("Legacy shadow batch was not found.");
        if (batch.State is EconomyLegacyShadowBatchState.Failed or > EconomyLegacyShadowBatchState.Backfilled)
            throw new LegacyEconomyShadowMigrationException("The legacy shadow batch is not open for backfill.");
        var item = await _db.Set<EconomyLegacyShadowWalletRow>()
            .SingleOrDefaultAsync(row => row.BatchId == command.BatchId &&
                                         row.LegacyWalletId == command.LegacyWalletId &&
                                         row.TenantId == command.TenantId, cancellationToken)
            ?? throw new KeyNotFoundException("Legacy shadow wallet was not found.");
        if (item.State is EconomyLegacyShadowItemState.Posted or EconomyLegacyShadowItemState.Reconciled)
        {
            await RefreshBatchStateAsync(batch, command.PostedAt, cancellationToken);
            return await RequiredBatchAsync(command.TenantId, command.BatchId, cancellationToken);
        }
        if (item.State == EconomyLegacyShadowItemState.Blocked || item.EconomyWalletId is null)
            throw new LegacyEconomyShadowMigrationException("A blocked legacy wallet cannot be backfilled.");
        if (item.LegacyBalanceMinorUnits == 0)
        {
            item.State = EconomyLegacyShadowItemState.Reconciled;
            item.ReconciledAt = command.PostedAt;
            item.ReconciliationHash = Hash($"zero|{item.SnapshotHash}");
            item.Version++;
            await _db.SaveChangesAsync(cancellationToken);
            await RefreshBatchStateAsync(batch, command.PostedAt, cancellationToken);
            return await RequiredBatchAsync(command.TenantId, command.BatchId, cancellationToken);
        }

        var policy = await CurrentPolicyAsync(
            command.TenantId, batch.JurisdictionCode, command.PostedAt, cancellationToken);
        ValidateMigrationPolicy(policy);
        if (policy.Version != batch.PolicyVersion)
            throw new LegacyEconomyShadowMigrationException(
                "The active legacy migration policy changed after capture; create a new batch.");
        var providerHash = policy.PayloadHash;
        var destinationHash = Hash(item.EconomyWalletId.Value.ToString("N"));
        var sourceRootHash = Hash(item.SourceStampId.ToString("N"));
        var receipt = await _capabilities.AuthorizeAndConsumeAsync(
            new EconomyCapabilityEvaluationContext(
                command.TenantId,
                command.ActorId,
                $"legacy-wallet:{item.LegacyWalletId:N}",
                batch.JurisdictionCode,
                EconomyValueMovementCapability.LegacyBalanceBackfill,
                command.RiskDecisionId,
                fingerprint,
                providerHash,
                destinationHash,
                [sourceRootHash],
                command.PostedAt), cancellationToken);
        if (receipt.PolicyVersion != batch.PolicyVersion)
            throw new LegacyEconomyShadowMigrationException("Capability receipt policy does not match the captured batch.");
        var authority = await _postingAuthorities.ResolveAuthorityAsync(
            CapabilityName, PostingTemplateKind.ConfirmedTopUpMint, receipt, cancellationToken);
        var posting = _backfill.Post(new LegacyBalanceBackfillPostingRequest(
            authority,
            receipt,
            item.LegacyWalletId,
            new WalletId(item.EconomyWalletId.Value),
            new SourceStampId(item.SourceStampId),
            new PostingId(item.PostingId),
            new CreditLotId(item.CreditLotId),
            new IdempotencyKey($"legacy-shadow:{batch.Id:N}:{item.LegacyWalletId:N}"),
            item.LegacyBalanceMinorUnits,
            item.SnapshotHash,
            providerHash,
            destinationHash,
            sourceRootHash,
            command.PostedAt));
        await _db.Entry(item).ReloadAsync(cancellationToken);
        if (item.State != EconomyLegacyShadowItemState.Posted ||
            item.JournalSequence != posting.JournalSequence || item.JournalHash != posting.JournalHash)
            throw new LegacyEconomyShadowMigrationException(
                "The protected legacy writer did not persist the shadow-item posting binding.");
        await RefreshBatchStateAsync(batch, command.PostedAt, cancellationToken);
        return await RequiredBatchAsync(command.TenantId, command.BatchId, cancellationToken);
    }

    public async ValueTask<LegacyEconomyShadowBatchView> ReconcileAsync(
        ReconcileLegacyEconomyShadowCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(command.BatchId, command.TenantId, command.ActorId);
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async _ =>
        {
        var batch = await _db.Set<EconomyLegacyShadowBatchRow>()
            .SingleOrDefaultAsync(row => row.Id == command.BatchId && row.TenantId == command.TenantId,
                cancellationToken) ?? throw new KeyNotFoundException("Legacy shadow batch was not found.");
        if (batch.State is EconomyLegacyShadowBatchState.Failed or EconomyLegacyShadowBatchState.Captured or EconomyLegacyShadowBatchState.Backfilling)
            throw new LegacyEconomyShadowMigrationException("All non-zero legacy wallets must be posted before reconciliation.");
        var items = await _db.Set<EconomyLegacyShadowWalletRow>()
            .Where(row => row.BatchId == command.BatchId && row.TenantId == command.TenantId)
            .OrderBy(row => row.LegacyWalletId)
            .ToArrayAsync(cancellationToken);
        var source = await ReadCurrentLegacySourceAsync(command.TenantId, items, cancellationToken);
        if (source.WalletHash != batch.WalletSnapshotHash || source.TransactionHash != batch.TransactionSnapshotHash)
            throw new LegacyEconomyShadowMigrationException(
                "Legacy wallet or transaction data changed after capture; reconciliation is fail-closed.");
        var financialLedger = await ReadFinancialLedgerSnapshotAsync(command.TenantId, cancellationToken);
        if (financialLedger.Count != batch.FinancialLedgerEntryCount || financialLedger.Hash != batch.FinancialLedgerSnapshotHash)
            throw new LegacyEconomyShadowMigrationException(
                "The legacy financial ledger changed after capture; reconciliation is fail-closed.");

        foreach (var item in items)
        {
            if (item.State == EconomyLegacyShadowItemState.Reconciled) continue;
            if (item.State != EconomyLegacyShadowItemState.Posted || item.EconomyWalletId is null)
                throw new LegacyEconomyShadowMigrationException("A legacy shadow item has not reached a reconcilable state.");
            var proof = await (from lot in _db.Set<EconomyCreditLotRow>().AsNoTracking()
                               join posting in _db.Set<EconomyPostingGroupRow>().AsNoTracking()
                                   on item.PostingId equals posting.Id
                               join journal in _db.Set<EconomyJournalEntryRow>().AsNoTracking()
                                   on posting.Id equals journal.PostingGroupId
                               where lot.Id == item.CreditLotId &&
                                     lot.WalletId == item.EconomyWalletId.Value &&
                                     lot.RootSourceStampId == item.SourceStampId &&
                                     lot.Currency == CurrencyCode.HardCoin &&
                                     lot.Provenance == ProvenanceKind.PurchasedHard &&
                                     lot.AmountUnits == item.LegacyBalanceMinorUnits &&
                                     posting.TenantId == command.TenantId &&
                                     posting.PolicyVersion == batch.PolicyVersion &&
                                     posting.TemplateKind == PostingTemplateKind.ConfirmedTopUpMint
                               select new { lot.JournalSequence, journal.Hash })
                .SingleOrDefaultAsync(cancellationToken);
            if (proof is null || proof.JournalSequence != item.JournalSequence || proof.Hash != item.JournalHash)
                throw new LegacyEconomyShadowMigrationException(
                    "A legacy backfill posting is absent or no longer matches its captured provenance.");
            item.State = EconomyLegacyShadowItemState.Reconciled;
            item.ReconciledAt = command.ReconciledAt;
            item.ReconciliationHash = Hash(string.Join('|', item.SnapshotHash, item.SourceStampId.ToString("N"),
                item.PostingId.ToString("N"), item.CreditLotId.ToString("N"), proof.JournalSequence, proof.Hash));
            item.Version++;
        }

        batch.State = EconomyLegacyShadowBatchState.Reconciled;
        batch.BackfilledHardUnits = items.Sum(item => item.State == EconomyLegacyShadowItemState.Reconciled
            ? item.LegacyBalanceMinorUnits : 0);
        batch.ReconciledHardUnits = batch.BackfilledHardUnits;
        if (batch.ReconciledHardUnits != batch.ExpectedHardUnits)
            throw new LegacyEconomyShadowMigrationException("Legacy and Economy backfill totals do not reconcile.");
        batch.UpdatedAt = command.ReconciledAt;
        batch.Version++;
        await _db.SaveChangesAsync(cancellationToken);
        return await RequiredBatchAsync(command.TenantId, command.BatchId, cancellationToken);
        }, cancellationToken);
    }

    public async ValueTask<LegacyEconomyShadowBatchView> ProposeCutoverAsync(
        ProposeLegacyEconomyCutoverCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(command.BatchId, command.TenantId, command.ActorId);
        var reason = Required(command.Reason, nameof(command.Reason));
        var reauthentication = Required(command.ReauthenticationHash, nameof(command.ReauthenticationHash));
        var reauthenticationHash = Hash(reauthentication);
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async _ =>
        {
        var batch = await RequiredBatchRowAsync(command.TenantId, command.BatchId, cancellationToken);
        if (batch.State != EconomyLegacyShadowBatchState.Reconciled ||
            batch.ReconciledHardUnits != batch.ExpectedHardUnits)
            throw new LegacyEconomyShadowMigrationException("Only a fully reconciled batch can enter cutover.");
        if (await _db.Set<EconomyLegacyCutoverRow>().AnyAsync(row => row.TenantId == command.TenantId, cancellationToken))
            throw new LegacyEconomyShadowMigrationException("The tenant already has a legacy cutover record.");
        var cutover = new EconomyLegacyCutoverRow
        {
            TenantId = command.TenantId,
            BatchId = command.BatchId,
            State = EconomyLegacyCutoverState.Proposed,
            ProposedBy = command.ActorId,
            ReauthenticationHash = reauthenticationHash,
            Reason = reason,
            ProposedAt = command.ProposedAt,
            Epoch = 1,
            Version = 1
        };
        _db.Set<EconomyLegacyCutoverRow>().Add(cutover);
        AddAudit(cutover, command.ActorId, reason, reauthenticationHash, command.ProposedAt, 1);
        batch.State = EconomyLegacyShadowBatchState.CutoverProposed;
        batch.UpdatedAt = command.ProposedAt;
        batch.Version++;
        await _db.SaveChangesAsync(cancellationToken);
        return await RequiredBatchAsync(command.TenantId, command.BatchId, cancellationToken);
        }, cancellationToken);
    }

    public async ValueTask<LegacyEconomyShadowBatchView> ApproveCutoverAsync(
        ApproveLegacyEconomyCutoverCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(command.BatchId, command.TenantId, command.ActorId);
        var reauthentication = Required(command.ReauthenticationHash, nameof(command.ReauthenticationHash));
        var reauthenticationHash = Hash(reauthentication);
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async _ =>
        {
        var batch = await RequiredBatchRowAsync(command.TenantId, command.BatchId, cancellationToken);
        var cutover = await _db.Set<EconomyLegacyCutoverRow>()
            .SingleOrDefaultAsync(row => row.TenantId == command.TenantId && row.BatchId == command.BatchId,
                cancellationToken) ?? throw new KeyNotFoundException("Legacy cutover proposal was not found.");
        if (command.ActorId == cutover.ProposedBy || command.ActorId == cutover.FirstApprovedBy)
            throw new LegacyEconomyShadowMigrationException(
                "Cutover requires the proposer and two different approving administrators.");
        var sequence = await NextAuditSequenceAsync(command.TenantId, cancellationToken);
        if (cutover.State == EconomyLegacyCutoverState.Proposed)
        {
            cutover.State = EconomyLegacyCutoverState.FirstApproved;
            cutover.FirstApprovedBy = command.ActorId;
            cutover.FirstApprovedAt = command.ApprovedAt;
            AddAudit(cutover, command.ActorId, "first-approval", reauthenticationHash, command.ApprovedAt, sequence);
        }
        else if (cutover.State == EconomyLegacyCutoverState.FirstApproved)
        {
            cutover.State = EconomyLegacyCutoverState.Active;
            cutover.SecondApprovedBy = command.ActorId;
            cutover.ActivatedAt = command.ApprovedAt;
            cutover.Epoch++;
            batch.State = EconomyLegacyShadowBatchState.CutoverActive;
            AddAudit(cutover, command.ActorId, "cutover-activated", reauthenticationHash, command.ApprovedAt, sequence);
        }
        else
        {
            throw new LegacyEconomyShadowMigrationException("The legacy cutover cannot accept another approval.");
        }
        cutover.ReauthenticationHash = reauthenticationHash;
        cutover.Version++;
        batch.UpdatedAt = command.ApprovedAt;
        batch.Version++;
        await _db.SaveChangesAsync(cancellationToken);
        return await RequiredBatchAsync(command.TenantId, command.BatchId, cancellationToken);
        }, cancellationToken);
    }

    public async ValueTask<LegacyEconomyShadowBatchView> RollbackCutoverAsync(
        RollbackLegacyEconomyCutoverCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(command.BatchId, command.TenantId, command.ActorId);
        var reason = Required(command.Reason, nameof(command.Reason));
        var reauthentication = Required(command.ReauthenticationHash, nameof(command.ReauthenticationHash));
        var reauthenticationHash = Hash(reauthentication);
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async _ =>
        {
        var batch = await RequiredBatchRowAsync(command.TenantId, command.BatchId, cancellationToken);
        var cutover = await _db.Set<EconomyLegacyCutoverRow>()
            .SingleOrDefaultAsync(row => row.TenantId == command.TenantId && row.BatchId == command.BatchId,
                cancellationToken) ?? throw new KeyNotFoundException("Legacy cutover was not found.");
        if (cutover.State != EconomyLegacyCutoverState.Active)
            throw new LegacyEconomyShadowMigrationException("Only an active legacy cutover can be rolled back.");
        cutover.State = EconomyLegacyCutoverState.RolledBack;
        cutover.RolledBackBy = command.ActorId;
        cutover.RolledBackAt = command.RolledBackAt;
        cutover.ReauthenticationHash = reauthenticationHash;
        cutover.Reason = reason;
        cutover.Epoch++;
        cutover.Version++;
        batch.State = EconomyLegacyShadowBatchState.RolledBack;
        batch.UpdatedAt = command.RolledBackAt;
        batch.Version++;
        AddAudit(cutover, command.ActorId, reason, reauthenticationHash, command.RolledBackAt,
            await NextAuditSequenceAsync(command.TenantId, cancellationToken));
        await _db.SaveChangesAsync(cancellationToken);
        return await RequiredBatchAsync(command.TenantId, command.BatchId, cancellationToken);
        }, cancellationToken);
    }

    public async ValueTask<LegacyEconomyShadowBatchView?> GetAsync(
        Guid tenantId,
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || batchId == Guid.Empty)
            throw new ArgumentException("Tenant and batch IDs are required.");
        var exists = await _db.Set<EconomyLegacyShadowBatchRow>().AsNoTracking()
            .AnyAsync(row => row.Id == batchId && row.TenantId == tenantId, cancellationToken);
        return exists ? await RequiredBatchAsync(tenantId, batchId, cancellationToken) : null;
    }

    private async ValueTask<EconomyCapabilityPolicy> CurrentPolicyAsync(
        Guid tenantId,
        string jurisdiction,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var policy = await _policies.CurrentAsync(
                         tenantId, EconomyValueMovementCapability.LegacyBalanceBackfill, jurisdiction, cancellationToken)
                     ?? await _policies.CurrentAsync(
                         null, EconomyValueMovementCapability.LegacyBalanceBackfill, jurisdiction, cancellationToken);
        if (policy is null || policy.State != EconomyCapabilityPolicyState.Active ||
            policy.EffectiveAt > at || policy.ExpiresAt <= at || !policy.ProviderReady)
            throw new LegacyEconomyShadowMigrationException(
                "An active, signed and provider-ready LegacyBalanceBackfill policy is required.");
        return policy;
    }

    private static void ValidateMigrationPolicy(EconomyCapabilityPolicy policy)
    {
        bool valid;
        try
        {
            using var document = JsonDocument.Parse(policy.CanonicalPayload);
            var root = document.RootElement;
            valid = root.ValueKind == JsonValueKind.Object &&
                    root.GetProperty("sourceCurrency").GetString() == "USD" &&
                    root.GetProperty("provenance").GetString() == nameof(ProvenanceKind.PurchasedHard) &&
                    root.GetProperty("minorUnitsPerHardUnit").GetInt64() == 1 &&
                    root.GetProperty("provider").GetString() == ExpectedProvider;
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new LegacyEconomyShadowMigrationException(
                "Legacy migration policy payload is missing its fail-closed classification fields.");
        }
        if (!valid)
            throw new LegacyEconomyShadowMigrationException(
                "Legacy migration policy must explicitly bind USD minor units 1:1 to PurchasedHard through legacy-shadow-v1.");
    }

    private async ValueTask RefreshBatchStateAsync(
        EconomyLegacyShadowBatchRow batch,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var items = await _db.Set<EconomyLegacyShadowWalletRow>().AsNoTracking()
            .Where(row => row.BatchId == batch.Id)
            .ToArrayAsync(cancellationToken);
        batch.BackfilledHardUnits = items.Sum(item => item.State is EconomyLegacyShadowItemState.Posted or EconomyLegacyShadowItemState.Reconciled
            ? item.LegacyBalanceMinorUnits : 0);
        batch.State = items.All(item => item.State is EconomyLegacyShadowItemState.Posted or EconomyLegacyShadowItemState.Reconciled)
            ? EconomyLegacyShadowBatchState.Backfilled
            : EconomyLegacyShadowBatchState.Backfilling;
        batch.UpdatedAt = at;
        batch.Version++;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async ValueTask<CurrentLegacySource> ReadCurrentLegacySourceAsync(
        Guid tenantId,
        IReadOnlyCollection<EconomyLegacyShadowWalletRow> items,
        CancellationToken cancellationToken)
    {
        var legacyIds = items.Select(item => item.LegacyWalletId).ToArray();
        var wallets = legacyIds.Length == 0
            ? []
            : await _db.Set<UserWallet>().AsNoTracking()
                .Where(wallet => legacyIds.Contains(wallet.Id) && wallet.TenantId == tenantId && wallet.DeletedAt == null)
                .OrderBy(wallet => wallet.Id)
                .ToArrayAsync(cancellationToken);
        if (wallets.Length != items.Count)
            throw new LegacyEconomyShadowMigrationException("A captured legacy wallet is missing or outside the tenant.");
        var transactions = legacyIds.Length == 0
            ? []
            : await _db.Set<WalletTransaction>().AsNoTracking()
                .Where(transaction => legacyIds.Contains(transaction.WalletId) && transaction.DeletedAt == null)
                .OrderBy(transaction => transaction.WalletId)
                .ThenBy(transaction => transaction.CreatedAt)
                .ThenBy(transaction => transaction.Id)
                .ToArrayAsync(cancellationToken);
        var groups = transactions.GroupBy(transaction => transaction.WalletId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var rows = wallets.Select(wallet =>
        {
            var assessment = Assess(wallet, groups.GetValueOrDefault(wallet.Id) ?? []);
            return $"{wallet.Id:N}|{wallet.UserId:N}|{assessment.BalanceMinorUnits}|{assessment.SnapshotHash}";
        });
        return new CurrentLegacySource(
            Hash(string.Join('\n', rows)),
            Hash(string.Join('\n', transactions.Select(CanonicalTransaction))));
    }

    private async ValueTask<FinancialLedgerSnapshot> ReadFinancialLedgerSnapshotAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var connection = _db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);
        await using var exists = connection.CreateCommand();
        exists.Transaction = _db.Database.CurrentTransaction?.GetDbTransaction();
        exists.CommandText = "SELECT to_regclass('public.financial_ledger_entries') IS NOT NULL;";
        if (await exists.ExecuteScalarAsync(cancellationToken) is not true)
            return new FinancialLedgerSnapshot(0, Hash(string.Empty));

        await using var columns = connection.CreateCommand();
        columns.Transaction = _db.Database.CurrentTransaction?.GetDbTransaction();
        columns.CommandText = """
            SELECT count(*) FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'financial_ledger_entries'
              AND column_name IN ('Id', 'TenantId', 'DeletedAt', 'EntryType', 'DebitAccount',
                                  'CreditAccount', 'Amount', 'Currency', 'ReferenceNumber',
                                  'IsReconciled', 'CreatedAt');
            """;
        if (Convert.ToInt32(await columns.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) != 11)
            throw new LegacyEconomyShadowMigrationException(
                "The legacy financial ledger schema is unknown and cannot be captured safely.");
        await using var snapshot = connection.CreateCommand();
        snapshot.Transaction = _db.Database.CurrentTransaction?.GetDbTransaction();
        snapshot.CommandText = """
            SELECT count(*)::integer,
                   encode(digest(convert_to(COALESCE(string_agg(concat_ws('|',
                       "Id"::text, "EntryType"::text, "DebitAccount", "CreditAccount",
                       "Amount"::text, "Currency", COALESCE("ReferenceNumber", ''),
                       "IsReconciled"::text, "CreatedAt"::text), E'\n' ORDER BY "Id"), ''), 'UTF8'), 'sha256'), 'hex')
            FROM public.financial_ledger_entries
            WHERE "TenantId" = @tenant_id AND "DeletedAt" IS NULL;
            """;
        var parameter = snapshot.CreateParameter();
        parameter.ParameterName = "tenant_id";
        parameter.Value = tenantId;
        snapshot.Parameters.Add(parameter);
        await using var reader = await snapshot.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return new FinancialLedgerSnapshot(reader.GetInt32(0), reader.GetString(1));
    }

    private static LegacyWalletAssessment Assess(UserWallet wallet, IReadOnlyCollection<WalletTransaction> transactions)
    {
        var snapshot = Hash(string.Join('|', wallet.Id.ToString("N"), wallet.UserId.ToString("N"),
            wallet.TenantId?.ToString("N") ?? string.Empty, wallet.Balance.ToString(CultureInfo.InvariantCulture),
            wallet.Currency, wallet.IsActive, wallet.IsLocked, wallet.LastTransactionAt?.ToUniversalTime().Ticks ?? 0,
            string.Join('\n', transactions.Select(CanonicalTransaction))));
        if (!wallet.IsActive) return new LegacyWalletAssessment(0, 0, 0, snapshot, "legacy-wallet-inactive");
        if (wallet.IsLocked) return new LegacyWalletAssessment(0, 0, 0, snapshot, "legacy-wallet-locked");
        if (!StringComparer.Ordinal.Equals(wallet.Currency, "USD"))
            return new LegacyWalletAssessment(0, 0, 0, snapshot, "legacy-wallet-currency-unsupported");
        if (!TryMinorUnits(wallet.Balance, out var balance))
            return new LegacyWalletAssessment(0, 0, 0, snapshot, "legacy-wallet-precision-invalid");
        if (transactions.Any(transaction => transaction.TenantId is { } tenant && tenant != wallet.TenantId))
            return new LegacyWalletAssessment(balance, 0, 0, snapshot, "legacy-transaction-tenant-mismatch");
        if (transactions.Any(transaction => transaction.Type == WalletTransactionType.Adjustment))
            return new LegacyWalletAssessment(balance, 0, 0, snapshot, "legacy-adjustment-unclassified");
        var completed = transactions.Where(transaction => transaction.Status == TransactionStatus.Completed).ToArray();
        if (completed.Length > 0 && completed[^1].BalanceAfter != wallet.Balance)
            return new LegacyWalletAssessment(balance, 0, 0, snapshot, "legacy-balance-after-mismatch");
        long credits = 0;
        long debits = 0;
        foreach (var transaction in completed)
        {
            if (!TryMinorUnits(transaction.Amount, out var units))
                return new LegacyWalletAssessment(balance, credits, debits, snapshot, "legacy-transaction-precision-invalid");
            if (transaction.Type is WalletTransactionType.Credit or WalletTransactionType.TransferIn or WalletTransactionType.Refund)
                credits = checked(credits + units);
            else if (transaction.Type is WalletTransactionType.Debit or WalletTransactionType.TransferOut or WalletTransactionType.Fee)
                debits = checked(debits + units);
        }
        return new LegacyWalletAssessment(balance, credits, debits, snapshot, null);
    }

    private async ValueTask<EconomyLegacyShadowBatchRow> RequiredBatchRowAsync(
        Guid tenantId,
        Guid batchId,
        CancellationToken cancellationToken) =>
        await _db.Set<EconomyLegacyShadowBatchRow>()
            .SingleOrDefaultAsync(row => row.Id == batchId && row.TenantId == tenantId, cancellationToken)
        ?? throw new KeyNotFoundException("Legacy shadow batch was not found.");

    private async ValueTask<LegacyEconomyShadowBatchView> RequiredBatchAsync(
        Guid tenantId,
        Guid batchId,
        CancellationToken cancellationToken)
    {
        var batch = await _db.Set<EconomyLegacyShadowBatchRow>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == batchId && row.TenantId == tenantId, cancellationToken)
            ?? throw new KeyNotFoundException("Legacy shadow batch was not found.");
        var items = await _db.Set<EconomyLegacyShadowWalletRow>().AsNoTracking()
            .Where(row => row.BatchId == batchId && row.TenantId == tenantId)
            .OrderBy(row => row.LegacyWalletId)
            .Select(row => new LegacyEconomyShadowWalletView(
                row.LegacyWalletId, row.EconomyWalletId, row.OwnerId, row.LegacyBalanceMinorUnits,
                row.CompletedCreditsMinorUnits, row.CompletedDebitsMinorUnits, row.TransactionCount,
                row.State.ToString(), row.SourceStampId, row.PostingId, row.CreditLotId,
                row.JournalSequence, row.FailureCode))
            .ToArrayAsync(cancellationToken);
        return new LegacyEconomyShadowBatchView(
            batch.Id, batch.TenantId, (LegacyEconomyShadowState)(int)batch.State, batch.PolicyVersion,
            batch.WalletCount, batch.TransactionCount, batch.FinancialLedgerEntryCount,
            batch.ExpectedHardUnits, batch.BackfilledHardUnits, batch.ReconciledHardUnits,
            batch.WalletSnapshotHash, batch.TransactionSnapshotHash, batch.FinancialLedgerSnapshotHash,
            batch.FailureCode, items);
    }

    private void AddAudit(
        EconomyLegacyCutoverRow cutover,
        Guid actorId,
        string reason,
        string reauthenticationHash,
        DateTimeOffset at,
        long sequence)
    {
        _db.Set<EconomyLegacyCutoverAuditRow>().Add(new EconomyLegacyCutoverAuditRow
        {
            Id = Guid.NewGuid(),
            TenantId = cutover.TenantId,
            BatchId = cutover.BatchId,
            Sequence = sequence,
            State = cutover.State,
            ActorId = actorId,
            Reason = reason,
            ReauthenticationHash = reauthenticationHash,
            EvidenceHash = Hash(string.Join('|', cutover.TenantId.ToString("N"), cutover.BatchId.ToString("N"),
                sequence, (int)cutover.State, actorId.ToString("N"), reason, reauthenticationHash,
                at.UtcTicks, cutover.Epoch)),
            RecordedAt = at
        });
    }

    private async ValueTask<long> NextAuditSequenceAsync(Guid tenantId, CancellationToken cancellationToken) =>
        checked((await _db.Set<EconomyLegacyCutoverAuditRow>()
            .Where(row => row.TenantId == tenantId)
            .MaxAsync(row => (long?)row.Sequence, cancellationToken) ?? 0) + 1);

    private static string CanonicalTransaction(WalletTransaction transaction) => string.Join('|',
        transaction.Id.ToString("N"), transaction.WalletId.ToString("N"),
        transaction.TenantId?.ToString("N") ?? string.Empty, (int)transaction.Type,
        transaction.Amount.ToString(CultureInfo.InvariantCulture),
        transaction.BalanceAfter.ToString(CultureInfo.InvariantCulture), (int)transaction.Status,
        transaction.ReferenceId ?? string.Empty, transaction.CreatedAt.ToUniversalTime().Ticks,
        transaction.ProcessedAt?.ToUniversalTime().Ticks ?? 0);

    private static bool TryMinorUnits(decimal value, out long minorUnits)
    {
        var scaled = value * 100m;
        if (value < 0 || decimal.Truncate(scaled) != scaled || scaled > long.MaxValue)
        {
            minorUnits = 0;
            return false;
        }
        minorUnits = decimal.ToInt64(scaled);
        return true;
    }

    private static Guid DeterministicId(Guid scope, string discriminator)
    {
        var scopeBytes = scope.ToByteArray();
        var valueBytes = Encoding.UTF8.GetBytes(discriminator);
        var payload = new byte[scopeBytes.Length + valueBytes.Length];
        scopeBytes.CopyTo(payload, 0);
        valueBytes.CopyTo(payload, scopeBytes.Length);
        return new Guid(SHA256.HashData(payload).AsSpan(0, 16));
    }

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string Required(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }

    private static void ValidateIdentity(Guid batchId, Guid tenantId, Guid actorId)
    {
        if (batchId == Guid.Empty) throw new ArgumentException("Batch ID is required.", nameof(batchId));
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        if (actorId == Guid.Empty) throw new ArgumentException("Actor ID is required.", nameof(actorId));
    }

    private sealed record LegacyWalletAssessment(
        long BalanceMinorUnits,
        long CreditsMinorUnits,
        long DebitsMinorUnits,
        string SnapshotHash,
        string? FailureCode);

    private sealed record FinancialLedgerSnapshot(int Count, string Hash);
    private sealed record CurrentLegacySource(string WalletHash, string TransactionHash);
}
