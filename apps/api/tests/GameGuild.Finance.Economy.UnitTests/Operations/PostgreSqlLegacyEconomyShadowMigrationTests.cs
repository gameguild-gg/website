using System.Security.Cryptography;
using System.Reflection;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Commerce.Payments;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Operations;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace GameGuild.Finance.Economy.UnitTests.Operations;

public sealed class PostgreSqlLegacyEconomyShadowMigrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 7, 0, 0, TimeSpan.Zero);
    private const string PolicyPayload =
        "{\"minorUnitsPerHardUnit\":1,\"provenance\":\"PurchasedHard\",\"provider\":\"legacy-shadow-v1\",\"sourceCurrency\":\"USD\"}";

    [Fact]
    public async Task ZeroBalanceMigration_IsIdempotentTenantScopedReconciledAndReversible()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("legacy_shadow_zero_cutover");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.MigrateAsync();
        var tenantId = Guid.NewGuid();
        var foreignTenantId = Guid.NewGuid();
        var legacyWalletId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var proposer = Guid.NewGuid();
        var firstApprover = Guid.NewGuid();
        var secondApprover = Guid.NewGuid();
        context.Set<UserWallet>().AddRange(
            Wallet(legacyWalletId, Guid.NewGuid(), tenantId, 0m),
            Wallet(Guid.NewGuid(), Guid.NewGuid(), foreignTenantId, 50m));
        await context.SaveChangesAsync();
        var migration = CreateService(context, Policy(tenantId));
        var captureCommand = new CaptureLegacyEconomyShadowCommand(
            batchId, tenantId, proposer, "br", Now);

        var captured = await migration.CaptureAsync(captureCommand);
        var replay = await migration.CaptureAsync(captureCommand);

        replay.Should().BeEquivalentTo(captured);
        captured.State.Should().Be(LegacyEconomyShadowState.Captured);
        captured.WalletCount.Should().Be(1);
        captured.ExpectedHardUnits.Should().Be(0);
        captured.Wallets.Should().ContainSingle().Which.LegacyWalletId.Should().Be(legacyWalletId);
        (await context.Set<EconomyWalletRow>().CountAsync()).Should().Be(1);

        var backfilled = await migration.BackfillAsync(new BackfillLegacyEconomyWalletCommand(
            batchId, tenantId, proposer, legacyWalletId, Guid.NewGuid(), "zero-wallet", Now.AddMinutes(1)));
        backfilled.State.Should().Be(LegacyEconomyShadowState.Backfilled);
        var reconciled = await migration.ReconcileAsync(new ReconcileLegacyEconomyShadowCommand(
            batchId, tenantId, proposer, Now.AddMinutes(2)));
        reconciled.State.Should().Be(LegacyEconomyShadowState.Reconciled);

        (await migration.ProposeCutoverAsync(new ProposeLegacyEconomyCutoverCommand(
            batchId, tenantId, proposer, "legacy-writes-disabled", "reauth-proposer", Now.AddMinutes(3))))
            .State.Should().Be(LegacyEconomyShadowState.CutoverProposed);
        (await migration.ApproveCutoverAsync(new ApproveLegacyEconomyCutoverCommand(
            batchId, tenantId, firstApprover, "reauth-first", Now.AddMinutes(4))))
            .State.Should().Be(LegacyEconomyShadowState.CutoverProposed);
        (await migration.ApproveCutoverAsync(new ApproveLegacyEconomyCutoverCommand(
            batchId, tenantId, secondApprover, "reauth-second", Now.AddMinutes(5))))
            .State.Should().Be(LegacyEconomyShadowState.CutoverActive);
        var activationAudit = await context.Set<EconomyLegacyCutoverAuditRow>().AsNoTracking()
            .OrderBy(row => row.Sequence).ToArrayAsync();
        activationAudit.Select(row => row.ReauthenticationHash).Should().Equal(
            Hash("reauth-proposer"), Hash("reauth-first"), Hash("reauth-second"));
        activationAudit.Select(row => row.EvidenceHash).Should().OnlyHaveUniqueItems();

        var legacy = await context.Set<UserWallet>().SingleAsync(row => row.Id == legacyWalletId);
        legacy.Balance = 1m;
        await FluentActions.Invoking(() => context.SaveChangesAsync())
            .Should().ThrowAsync<InvalidOperationException>()
            .Where(exception => exception.GetBaseException().GetType() == typeof(PostgresException) &&
                                ((PostgresException)exception.GetBaseException()).SqlState == "55000");
        context.ChangeTracker.Clear();

        (await migration.RollbackCutoverAsync(new RollbackLegacyEconomyCutoverCommand(
            batchId, tenantId, secondApprover, "reconciliation-reopened", "reauth-rollback", Now.AddMinutes(6))))
            .State.Should().Be(LegacyEconomyShadowState.RolledBack);
        legacy = await context.Set<UserWallet>().SingleAsync(row => row.Id == legacyWalletId);
        legacy.Balance = 1m;
        await context.SaveChangesAsync();
        (await context.Set<UserWallet>().SingleAsync(row => row.Id == legacyWalletId)).Balance.Should().Be(1m);
        (await context.Set<EconomyLegacyCutoverAuditRow>().AsNoTracking()
            .SingleAsync(row => row.Sequence == 4)).ReauthenticationHash.Should().Be(Hash("reauth-rollback"));
    }

    [Fact]
    public async Task CaptureAsync_BlocksUnsafeWalletAndRejectsChangedOrCrossTenantReplay()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("legacy_shadow_fail_closed");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.MigrateAsync();
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        context.Set<UserWallet>().Add(Wallet(Guid.NewGuid(), Guid.NewGuid(), tenantId, 10m, isLocked: true));
        await context.SaveChangesAsync();
        var migration = CreateService(context, Policy(tenantId));
        var command = new CaptureLegacyEconomyShadowCommand(batchId, tenantId, actorId, "BR", Now);

        var result = await migration.CaptureAsync(command);

        result.State.Should().Be(LegacyEconomyShadowState.Failed);
        result.FailureCode.Should().Be("blocked-wallets:1");
        result.Wallets.Should().ContainSingle().Which.FailureCode.Should().Be("legacy-wallet-locked");
        (await context.Set<EconomyWalletRow>().CountAsync()).Should().Be(0);
        await FluentActions.Invoking(() => migration.CaptureAsync(command with { ActorId = Guid.NewGuid() }).AsTask())
            .Should().ThrowAsync<LegacyEconomyShadowMigrationException>();
        (await migration.GetAsync(Guid.NewGuid(), batchId)).Should().BeNull();
        await FluentActions.Invoking(() => migration.BackfillAsync(new BackfillLegacyEconomyWalletCommand(
                batchId, tenantId, actorId, result.Wallets[0].LegacyWalletId, Guid.NewGuid(), "blocked", Now)).AsTask())
            .Should().ThrowAsync<LegacyEconomyShadowMigrationException>();
    }

    [Fact]
    public async Task NonZeroBackfill_PostsOnceThroughProtectedWriterAndReconcilesExactProvenance()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("legacy_shadow_protected_posting");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.MigrateAsync();
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var legacyWalletId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var riskDecisionId = Guid.NewGuid();
        const string fingerprint = "legacy-balance-backfill-operation";
        context.Set<UserWallet>().Add(Wallet(legacyWalletId, Guid.NewGuid(), tenantId, 12.34m));
        await context.SaveChangesAsync();
        var policy = Policy(tenantId);
        var captureService = CreateService(context, policy);
        var captured = await captureService.CaptureAsync(new CaptureLegacyEconomyShadowCommand(
            batchId, tenantId, actorId, "BR", Now));
        var item = await context.Set<EconomyLegacyShadowWalletRow>().AsNoTracking()
            .SingleAsync(row => row.BatchId == batchId);
        var providerHash = policy.PayloadHash;
        var destinationHash = Hash(item.EconomyWalletId!.Value.ToString("N"));
        var sourceRootHash = Hash(item.SourceStampId.ToString("N"));
        var receipt = new CapabilityAuthorizationReceipt(
            Guid.NewGuid(), tenantId, actorId, $"legacy-wallet:{legacyWalletId:N}", "BR",
            EconomyValueMovementCapability.LegacyBalanceBackfill, fingerprint, 1, 1,
            riskDecisionId, 0, providerHash, destinationHash, [sourceRootHash], ["legacy-evidence"],
            Now, Now.AddHours(1), "capability-receipt-hash", "kms-key", "signature");
        await SeedPostingAuthorizationAsync(
            context, receipt, item.EconomyWalletId.Value, captured.ExpectedHardUnits);
        var authority = new RegisteredPostingAuthority(
            Guid.Parse("32f578aa-a580-4d65-a978-53c0c59e50cc"), actorId, tenantId,
            riskDecisionId, fingerprint, 1);
        var service = new PostgreSqlLegacyEconomyShadowMigration(
            context, new PolicyStore(policy), new AuthorizationService(receipt),
            new PostingResolver(authority), new PostgreSqlEconomyWalletProvisioner(context),
            new PostgreSqlLegacyBalanceBackfillGateway(context));
        var command = new BackfillLegacyEconomyWalletCommand(
            batchId, tenantId, actorId, legacyWalletId, riskDecisionId, fingerprint, Now.AddMinutes(1));

        var posted = await service.BackfillAsync(command);
        var replay = await service.BackfillAsync(command);
        var reconciled = await service.ReconcileAsync(new ReconcileLegacyEconomyShadowCommand(
            batchId, tenantId, actorId, Now.AddMinutes(2)));

        posted.State.Should().Be(LegacyEconomyShadowState.Backfilled);
        replay.State.Should().Be(LegacyEconomyShadowState.Backfilled);
        reconciled.State.Should().Be(LegacyEconomyShadowState.Reconciled);
        reconciled.BackfilledHardUnits.Should().Be(1234);
        reconciled.ReconciledHardUnits.Should().Be(1234);
        (await context.Set<EconomyPostingGroupRow>().CountAsync(row => row.Id == item.PostingId)).Should().Be(1);
        var journal = await context.Set<EconomyJournalEntryRow>().AsNoTracking()
            .SingleAsync(row => row.PostingGroupId == item.PostingId);
        journal.CanonicalPayloadHash.Should().NotBeNullOrWhiteSpace();
        journal.HashAlgorithmVersion.Should().Be(2);
        var lot = await context.Set<EconomyCreditLotRow>().AsNoTracking()
            .SingleAsync(row => row.Id == item.CreditLotId);
        lot.RootSourceStampId.Should().Be(item.SourceStampId);
        lot.AmountUnits.Should().Be(1234);
        lot.Provenance.Should().Be(ProvenanceKind.PurchasedHard);
        (await context.Set<EconomyRiskDecisionConsumptionRow>()
            .CountAsync(row => row.RiskDecisionId == riskDecisionId)).Should().Be(1);
    }

    [Fact]
    public async Task UpgradeFromTreasurySnapshot_PreservesLegacyRowsAndInstallsTheReversibleControlPlane()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("legacy_shadow_snapshot_upgrade");
        await using var context = CreateContext(database.ConnectionString);
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync("20260825022708_AddTenantScopedTreasury");
        var tenantId = Guid.NewGuid();
        var legacyWalletId = Guid.NewGuid();
        context.Set<UserWallet>().Add(Wallet(legacyWalletId, Guid.NewGuid(), tenantId, 45.67m));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await migrator.MigrateAsync();

        (await context.Set<UserWallet>().AsNoTracking().SingleAsync(row => row.Id == legacyWalletId))
            .Balance.Should().Be(45.67m);
        (await context.Set<EconomyLegacyShadowBatchRow>().CountAsync()).Should().Be(0);
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT count(*)
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'economy_legacy_cutover_audit'
              AND column_name = 'ReauthenticationHash';
            """, connection);
        Convert.ToInt64(await command.ExecuteScalarAsync()).Should().Be(1);

        await migrator.MigrateAsync("20260825022708_AddTenantScopedTreasury");
        (await context.Set<UserWallet>().AsNoTracking().SingleAsync(row => row.Id == legacyWalletId))
            .Balance.Should().Be(45.67m);
    }

    [Fact]
    public void LegacyWalletAssessmentClassifiesEveryFailClosedShapeAndCanonicalTransactionType()
    {
        var tenantId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var walletId = Guid.NewGuid();

        var inactive = Wallet(Guid.NewGuid(), ownerId, tenantId, 1m);
        inactive.IsActive = false;
        AssessmentFailure(inactive, []).Should().Be("legacy-wallet-inactive");
        AssessmentFailure(Wallet(Guid.NewGuid(), ownerId, tenantId, 1m, isLocked: true), [])
            .Should().Be("legacy-wallet-locked");

        var unsupportedCurrency = Wallet(Guid.NewGuid(), ownerId, tenantId, 1m);
        unsupportedCurrency.Currency = "EUR";
        AssessmentFailure(unsupportedCurrency, []).Should().Be("legacy-wallet-currency-unsupported");
        AssessmentFailure(Wallet(Guid.NewGuid(), ownerId, tenantId, -1m), [])
            .Should().Be("legacy-wallet-precision-invalid");
        AssessmentFailure(Wallet(Guid.NewGuid(), ownerId, tenantId, 0.001m), [])
            .Should().Be("legacy-wallet-precision-invalid");
        AssessmentFailure(Wallet(Guid.NewGuid(), ownerId, tenantId, 100_000_000_000_000_000m), [])
            .Should().Be("legacy-wallet-precision-invalid");

        var tenantMismatchWallet = Wallet(walletId, ownerId, tenantId, 1m);
        AssessmentFailure(tenantMismatchWallet,
            [Transaction(walletId, Guid.NewGuid(), WalletTransactionType.Credit, 1m, 1m)]).Should()
            .Be("legacy-transaction-tenant-mismatch");
        AssessmentFailure(tenantMismatchWallet,
            [Transaction(walletId, tenantId, WalletTransactionType.Adjustment, 1m, 1m)]).Should()
            .Be("legacy-adjustment-unclassified");
        AssessmentFailure(tenantMismatchWallet,
            [Transaction(walletId, tenantId, WalletTransactionType.Credit, 1m, 0m)]).Should()
            .Be("legacy-balance-after-mismatch");
        AssessmentFailure(tenantMismatchWallet,
            [Transaction(walletId, tenantId, WalletTransactionType.Credit, 0.001m, 1m)]).Should()
            .Be("legacy-transaction-precision-invalid");

        var successfulWallet = Wallet(walletId, ownerId, tenantId, 6m);
        successfulWallet.LastTransactionAt = Now.UtcDateTime;
        var successful = Assess(successfulWallet,
        [
            Transaction(walletId, tenantId, WalletTransactionType.Credit, 1m, 1m,
                referenceId: "credit", processedAt: Now.UtcDateTime),
            Transaction(walletId, tenantId, WalletTransactionType.TransferIn, 1m, 2m),
            Transaction(walletId, tenantId, WalletTransactionType.Refund, 1m, 3m),
            Transaction(walletId, tenantId, WalletTransactionType.Debit, 1m, 4m),
            Transaction(walletId, tenantId, WalletTransactionType.TransferOut, 1m, 5m),
            Transaction(walletId, tenantId, WalletTransactionType.Fee, 1m, 6m),
            Transaction(walletId, null, WalletTransactionType.Credit, 1m, 6m,
                status: TransactionStatus.Pending)
        ]);
        AssessmentProperty<string?>(successful, "FailureCode").Should().BeNull();
        AssessmentProperty<long>(successful, "BalanceMinorUnits").Should().Be(600);
        AssessmentProperty<long>(successful, "CreditsMinorUnits").Should().Be(300);
        AssessmentProperty<long>(successful, "DebitsMinorUnits").Should().Be(300);

        var unknownWallet = Wallet(Guid.NewGuid(), ownerId, tenantId, 1m);
        var unknownType = Assess(unknownWallet,
            [Transaction(unknownWallet.Id, tenantId, (WalletTransactionType)999, 1m, 1m)]);
        AssessmentProperty<string?>(unknownType, "FailureCode").Should().BeNull();
        AssessmentProperty<long>(unknownType, "CreditsMinorUnits").Should().Be(0);
        AssessmentProperty<long>(unknownType, "DebitsMinorUnits").Should().Be(0);

        var tenantlessWallet = Wallet(Guid.NewGuid(), ownerId, tenantId, 0m);
        tenantlessWallet.TenantId = null;
        AssessmentFailure(tenantlessWallet, []).Should().BeNull();
    }

    [Fact]
    public async Task MigrationPolicyAndIdentityValidationExerciseEveryFailClosedBoundary()
    {
        await using var context = CreateInMemoryContext();
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        typeof(PostgreSqlLegacyEconomyShadowMigration)
            .GetMethod("ValidateMigrationPolicy", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [Policy(tenantId)]);
        string[] invalidPayloads =
        [
            "{",
            "{}",
            "[]",
            "{\"minorUnitsPerHardUnit\":1,\"provenance\":\"PurchasedHard\",\"provider\":\"legacy-shadow-v1\",\"sourceCurrency\":7}",
            "{\"minorUnitsPerHardUnit\":1.5,\"provenance\":\"PurchasedHard\",\"provider\":\"legacy-shadow-v1\",\"sourceCurrency\":\"USD\"}",
            "{\"minorUnitsPerHardUnit\":1,\"provenance\":\"PurchasedHard\",\"provider\":\"legacy-shadow-v1\",\"sourceCurrency\":\"EUR\"}",
            "{\"minorUnitsPerHardUnit\":1,\"provenance\":\"Unknown\",\"provider\":\"legacy-shadow-v1\",\"sourceCurrency\":\"USD\"}",
            "{\"minorUnitsPerHardUnit\":2,\"provenance\":\"PurchasedHard\",\"provider\":\"legacy-shadow-v1\",\"sourceCurrency\":\"USD\"}",
            "{\"minorUnitsPerHardUnit\":1,\"provenance\":\"PurchasedHard\",\"provider\":\"other\",\"sourceCurrency\":\"USD\"}"
        ];
        foreach (var payload in invalidPayloads)
        {
            var service = CreateService(context, Policy(tenantId) with { CanonicalPayload = payload });
            await FluentActions.Invoking(() => service.CaptureAsync(new CaptureLegacyEconomyShadowCommand(
                    Guid.NewGuid(), tenantId, actorId, "BR", Now)).AsTask())
                .Should().ThrowAsync<LegacyEconomyShadowMigrationException>();
        }

        var validService = CreateService(context, Policy(tenantId));
        await FluentActions.Invoking(() => validService.CaptureAsync(new CaptureLegacyEconomyShadowCommand(
                Guid.Empty, tenantId, actorId, "BR", Now)).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => validService.CaptureAsync(new CaptureLegacyEconomyShadowCommand(
                Guid.NewGuid(), Guid.Empty, actorId, "BR", Now)).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => validService.CaptureAsync(new CaptureLegacyEconomyShadowCommand(
                Guid.NewGuid(), tenantId, Guid.Empty, "BR", Now)).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => validService.CaptureAsync(new CaptureLegacyEconomyShadowCommand(
                Guid.NewGuid(), tenantId, actorId, " ", Now)).AsTask())
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CurrentMigrationPolicyMustBeActiveCurrentAndProviderReady()
    {
        await using var context = CreateInMemoryContext();
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        EconomyCapabilityPolicy?[] policies =
        [
            null,
            Policy(tenantId) with { State = EconomyCapabilityPolicyState.PendingApproval },
            Policy(tenantId) with { EffectiveAt = Now.AddMinutes(1) },
            Policy(tenantId) with { ExpiresAt = Now },
            Policy(tenantId) with { ProviderReady = false }
        ];

        foreach (var policy in policies)
        {
            var service = CreateService(context, policy);
            await FluentActions.Invoking(() => service.CaptureAsync(new CaptureLegacyEconomyShadowCommand(
                    Guid.NewGuid(), tenantId, actorId, "BR", Now)).AsTask())
                .Should().ThrowAsync<LegacyEconomyShadowMigrationException>();
        }
    }

    [Fact]
    public void ConstructorRequiresRelationalContextAndEveryDependency()
    {
        using var context = CreateInMemoryContext();
        var policy = new PolicyStore(Policy(Guid.NewGuid()));
        var capabilities = new UnusedAuthorizationService();
        var resolver = new UnusedPostingResolver();
        var provisioner = new PostgreSqlEconomyWalletProvisioner(context);
        var backfill = new UnusedBackfillGateway();
        var proxy = DispatchProxy.Create<IApplicationDbContext, NonRelationalContextProxy>();

        FluentActions.Invoking(() => new PostgreSqlLegacyEconomyShadowMigration(
                null!, policy, capabilities, resolver, provisioner, backfill))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlLegacyEconomyShadowMigration(
                proxy, policy, capabilities, resolver, provisioner, backfill))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => new PostgreSqlLegacyEconomyShadowMigration(
                context, null!, capabilities, resolver, provisioner, backfill))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlLegacyEconomyShadowMigration(
                context, policy, null!, resolver, provisioner, backfill))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlLegacyEconomyShadowMigration(
                context, policy, capabilities, null!, provisioner, backfill))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlLegacyEconomyShadowMigration(
                context, policy, capabilities, resolver, null!, backfill))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlLegacyEconomyShadowMigration(
                context, policy, capabilities, resolver, provisioner, null!))
            .Should().Throw<ArgumentNullException>();
    }

    private static async Task SeedPostingAuthorizationAsync(
        ApplicationDbContext context,
        CapabilityAuthorizationReceipt receipt,
        Guid walletId,
        long amountUnits)
    {
        var counterId = Guid.NewGuid();
        context.Set<EconomyRiskDecisionRow>().Add(new EconomyRiskDecisionRow
        {
            Id = receipt.RiskDecisionId,
            Outcome = RiskOutcome.Allow,
            OperationFingerprint = receipt.OperationFingerprint,
            IdempotencyKey = receipt.OperationFingerprint,
            ActorHash = "actor-hash",
            TemplateKind = PostingTemplateKind.ConfirmedTopUpMint,
            SourceWalletId = walletId,
            DestinationWalletId = walletId,
            Currency = CurrencyCode.HardCoin,
            AmountUnits = amountUnits,
            CurrencyLegs = JsonSerializer.Serialize(new[] { new { currency = 1, units = amountUnits } }),
            SourceRoots = "[]",
            ProviderReferenceHash = receipt.ProviderHash,
            PolicyVersion = receipt.PolicyVersion,
            ReserveVersion = receipt.ReserveVersion,
            ReserveAuthorizationEpoch = 1,
            FeatureVersion = 1,
            KillSwitchEpoch = receipt.KillSwitchEpoch,
            CounterVersion = 1,
            EntityGraphVersion = 1,
            EntityGraphEvidenceHash = "legacy-evidence",
            ReasonCodes = "[]",
            IssuedAt = receipt.IssuedAt,
            ExpiresAt = receipt.ExpiresAt
        });
        context.Set<EconomyRiskCounterRow>().Add(new EconomyRiskCounterRow
        {
            Id = counterId,
            TenantId = receipt.TenantId,
            Dimension = RiskLimitDimension.Wallet,
            SubjectHash = "legacy-wallet",
            Operation = PostingTemplateKind.ConfirmedTopUpMint,
            Currency = CurrencyCode.HardCoin,
            WindowStartedAt = receipt.IssuedAt,
            WindowEndsAt = receipt.ExpiresAt,
            CounterVersion = 1,
            MaxUnits = amountUnits,
            UsedUnits = amountUnits,
            UpdatedAt = receipt.IssuedAt
        });
        context.Set<EconomyRiskCounterReservationRow>().Add(new EconomyRiskCounterReservationRow
        {
            Id = Guid.NewGuid(),
            ReservationGroupId = Guid.NewGuid(),
            RiskDecisionId = receipt.RiskDecisionId,
            RiskCounterId = counterId,
            InputFingerprint = receipt.OperationFingerprint,
            AmountUnits = amountUnits,
            ReservedAt = receipt.IssuedAt,
            ExpiresAt = receipt.ExpiresAt,
            Status = RiskCounterReservationStatus.Reserved
        });
        context.Set<EconomyCapabilityReceiptRow>().Add(new EconomyCapabilityReceiptRow
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
        context.Set<EconomyCapabilityReceiptConsumptionRow>().Add(new EconomyCapabilityReceiptConsumptionRow
        {
            Id = Guid.NewGuid(),
            ReceiptId = receipt.Id,
            TenantId = receipt.TenantId,
            ActorId = receipt.ActorId,
            OperationFingerprint = receipt.OperationFingerprint,
            KillSwitchEpoch = receipt.KillSwitchEpoch,
            ConsumedAt = receipt.IssuedAt
        });
        await context.SaveChangesAsync();
    }

    private static PostgreSqlLegacyEconomyShadowMigration CreateService(
        ApplicationDbContext context,
        EconomyCapabilityPolicy? policy) => new(
        context,
        new PolicyStore(policy),
        new UnusedAuthorizationService(),
        new UnusedPostingResolver(),
        new PostgreSqlEconomyWalletProvisioner(context),
        new UnusedBackfillGateway());

    private static EconomyCapabilityPolicy Policy(Guid? tenantId) => new(
        Guid.NewGuid(), $"{tenantId:N}:13:BR", tenantId,
        EconomyValueMovementCapability.LegacyBalanceBackfill, "BR", 1,
        PolicyPayload, "policy-payload-hash", "kms-key", "signature",
        Guid.NewGuid(), Guid.NewGuid(), Now.AddHours(-2), Now.AddHours(-1),
        Now.AddHours(-1), Now.AddHours(1), true, EconomyCapabilityPolicyState.Active);

    private static UserWallet Wallet(
        Guid id,
        Guid ownerId,
        Guid tenantId,
        decimal balance,
        bool isLocked = false) => new()
    {
        Id = id,
        UserId = ownerId,
        TenantId = tenantId,
        Balance = balance,
        Currency = "USD",
        IsActive = true,
        IsLocked = isLocked,
        CreatedAt = Now.UtcDateTime,
        UpdatedAt = Now.UtcDateTime,
        Version = 1
    };

    private static ApplicationDbContext CreateContext(string connectionString) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options);

    private static ApplicationDbContext CreateInMemoryContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static object Assess(UserWallet wallet, IReadOnlyCollection<WalletTransaction> transactions) =>
        typeof(PostgreSqlLegacyEconomyShadowMigration)
            .GetMethod("Assess", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [wallet, transactions])!;

    private static string? AssessmentFailure(
        UserWallet wallet,
        IReadOnlyCollection<WalletTransaction> transactions) =>
        AssessmentProperty<string?>(Assess(wallet, transactions), "FailureCode");

    private static T AssessmentProperty<T>(object assessment, string propertyName) =>
        (T)assessment.GetType().GetProperty(propertyName)!.GetValue(assessment)!;

    private static WalletTransaction Transaction(
        Guid walletId,
        Guid? tenantId,
        WalletTransactionType type,
        decimal amount,
        decimal balanceAfter,
        string? referenceId = null,
        DateTime? processedAt = null,
        TransactionStatus status = TransactionStatus.Completed) => new()
    {
        Id = Guid.NewGuid(),
        WalletId = walletId,
        TenantId = tenantId,
        Type = type,
        Amount = amount,
        BalanceAfter = balanceAfter,
        Description = type.ToString(),
        ReferenceId = referenceId,
        Status = status,
        CreatedAt = Now.UtcDateTime,
        UpdatedAt = Now.UtcDateTime,
        ProcessedAt = processedAt
    };

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed class PolicyStore(EconomyCapabilityPolicy? policy) : IEconomyCapabilityPolicyStore
    {
        public ValueTask<EconomyCapabilityPolicy> ProposeAsync(
            EconomyCapabilityPolicyProposal proposal, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public ValueTask<EconomyCapabilityPolicy> ApproveAsync(
            Guid policyId, Guid actorId, string reauthenticationHash, DateTimeOffset approvedAt,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<int> ActivateDueAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public ValueTask<EconomyCapabilityPolicy?> CurrentAsync(
            Guid? tenantId, EconomyValueMovementCapability capability, string jurisdictionCode,
            CancellationToken cancellationToken) => ValueTask.FromResult<EconomyCapabilityPolicy?>(
            policy is not null && (tenantId is null || tenantId == policy.TenantId) &&
            capability == policy.Capability && jurisdictionCode == policy.JurisdictionCode ? policy : null);
    }

    private sealed class UnusedAuthorizationService : IEconomyCapabilityAuthorizationService
    {
        public ValueTask<CapabilityAuthorizationReceipt> AuthorizeAndConsumeAsync(
            EconomyCapabilityEvaluationContext context, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Zero-balance migration must not authorize value movement.");
    }

    private sealed class AuthorizationService(CapabilityAuthorizationReceipt receipt) :
        IEconomyCapabilityAuthorizationService
    {
        public ValueTask<CapabilityAuthorizationReceipt> AuthorizeAndConsumeAsync(
            EconomyCapabilityEvaluationContext context, CancellationToken cancellationToken)
        {
            context.TenantId.Should().Be(receipt.TenantId);
            context.ActorId.Should().Be(receipt.ActorId);
            context.Capability.Should().Be(EconomyValueMovementCapability.LegacyBalanceBackfill);
            context.OperationFingerprint.Should().Be(receipt.OperationFingerprint);
            context.ProviderHash.Should().Be(receipt.ProviderHash);
            context.DestinationHash.Should().Be(receipt.DestinationHash);
            context.SourceRootHashes.Should().Equal(receipt.SourceRootHashes);
            return ValueTask.FromResult(receipt);
        }
    }

    private sealed class UnusedPostingResolver : IRegisteredPostingCapabilityResolver
    {
        public Task<RegisteredPostingCapability> ResolveAsync(
            string capabilityName, PostingTemplateKind templateKind,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RegisteredPostingAuthority> ResolveAuthorityAsync(
            string capabilityName, PostingTemplateKind templateKind, CapabilityAuthorizationReceipt receipt,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class PostingResolver(RegisteredPostingAuthority authority) : IRegisteredPostingCapabilityResolver
    {
        public Task<RegisteredPostingCapability> ResolveAsync(
            string capabilityName, PostingTemplateKind templateKind,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RegisteredPostingAuthority> ResolveAuthorityAsync(
            string capabilityName, PostingTemplateKind templateKind, CapabilityAuthorizationReceipt receipt,
            CancellationToken cancellationToken = default)
        {
            capabilityName.Should().Be("legacy-balance-backfill");
            templateKind.Should().Be(PostingTemplateKind.ConfirmedTopUpMint);
            return Task.FromResult(authority);
        }
    }

    private sealed class UnusedBackfillGateway : ILegacyBalanceBackfillGateway
    {
        public RegisteredPostingReceipt Post(LegacyBalanceBackfillPostingRequest request) =>
            throw new NotSupportedException("Zero-balance migration must not post value.");
    }

    private class NonRelationalContextProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new NotSupportedException();
    }
}
