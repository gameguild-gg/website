using FluentAssertions;
using System.Data;
using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Transfers;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace GameGuild.Finance.Economy.UnitTests.Transfers;

public sealed class PostgreSqlSelfServiceEconomyTransferIntentStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 15, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.Parse("a2000000-0000-0000-0000-000000000001");
    private static readonly Guid ActorId = Guid.Parse("a2000000-0000-0000-0000-000000000002");
    private static readonly Guid RecipientId = Guid.Parse("a2000000-0000-0000-0000-000000000003");

    [Fact]
    public async Task PrepareAsync_IsAppendOnlyAndReusesTheFirstServerTimestampOnReplay()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("transfer_intent_replay");
        await using var context = Context(database.ConnectionString);
        await context.Database.MigrateAsync();
        var store = new PostgreSqlSelfServiceEconomyTransferIntentStore(context);
        var draft = Draft("replay-key", Now);

        var first = await store.PrepareAsync(draft);
        var replay = await store.PrepareAsync(draft with { RequestedAt = Now.AddMinutes(3) });

        replay.Should().Be(first);
        first.RequestedAt.Should().Be(Now);
        first.RequestHash.Should().HaveLength(64);
        first.ProviderReferenceHash.Should().HaveLength(64);
        first.DestinationHash.Should().HaveLength(64);

        var update = async () => await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE public.economy_self_service_transfer_intents
            SET "AmountUnits" = 999
            WHERE "Id" = {first.PostingId.Value}
            """);
        (await update.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("42501");
    }

    [Fact]
    public async Task PrepareAsync_RejectsAConflictingRequestForTheSameActorKey()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("transfer_intent_conflict");
        await using var context = Context(database.ConnectionString);
        await context.Database.MigrateAsync();
        var store = new PostgreSqlSelfServiceEconomyTransferIntentStore(context);
        var draft = Draft("conflict-key", Now);
        await store.PrepareAsync(draft);

        var conflict = () => store.PrepareAsync(draft with { AmountUnits = 12 }).AsTask();

        await conflict.Should().ThrowAsync<SelfServiceEconomyTransferException>()
            .WithMessage("The transfer idempotency key is already bound to another request.");
    }

    [Fact]
    public async Task PrepareAsync_SerializesConcurrentRetriesThroughTheRuntimeRole()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("transfer_intent_concurrent");
        await using (var migrationContext = Context(database.ConnectionString))
            await migrationContext.Database.MigrateAsync();

        await using var firstContext = Context(database.ConnectionString);
        await using var secondContext = Context(database.ConnectionString);
        await firstContext.Database.OpenConnectionAsync();
        await secondContext.Database.OpenConnectionAsync();
        await firstContext.Database.ExecuteSqlRawAsync("SET ROLE gameguild_economy_runtime;");
        await secondContext.Database.ExecuteSqlRawAsync("SET ROLE gameguild_economy_runtime;");
        var draft = Draft("concurrent-key", Now);

        var results = await Task.WhenAll(
            new PostgreSqlSelfServiceEconomyTransferIntentStore(firstContext)
                .PrepareAsync(draft).AsTask(),
            new PostgreSqlSelfServiceEconomyTransferIntentStore(secondContext)
                .PrepareAsync(draft with { RequestedAt = Now.AddSeconds(1) }).AsTask());

        results[1].Should().Be(results[0]);
        await firstContext.Database.ExecuteSqlRawAsync("RESET ROLE;");
        await secondContext.Database.ExecuteSqlRawAsync("RESET ROLE;");
    }

    [Fact]
    public async Task SourceRootPlanner_ReservesExactFifoRootsThroughTheRuntimeRole()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("transfer_root_plan");
        await using var context = Context(database.ConnectionString);
        await context.Database.MigrateAsync();
        var sourceWallet = Guid.NewGuid();
        var destinationWallet = Guid.NewGuid();
        var firstRoot = Guid.NewGuid();
        var secondRoot = Guid.NewGuid();
        await SeedTransferLotsAsync(
            context, sourceWallet, destinationWallet, firstRoot, secondRoot);
        await context.Database.OpenConnectionAsync();
        await context.Database.ExecuteSqlRawAsync("SET ROLE gameguild_economy_runtime;");

        IReadOnlyList<SourceStampId>? roots = null;
        await using (var transaction = await context.Database.BeginTransactionAsync(
                         IsolationLevel.Serializable))
        {
            var prepared = await new PostgreSqlSelfServiceEconomyTransferIntentStore(context)
                .PrepareAsync(Draft("root-plan", Now));
            roots = await new PostgreSqlSelfServiceEconomyTransferSourceRootPlanner(context)
                .ReserveAsync(new SelfServiceEconomyTransferSourceRootRequest(
                    prepared.PostingId,
                    TenantId,
                    ActorId,
                    new WalletId(sourceWallet),
                    new WalletId(destinationWallet)));
            await transaction.CommitAsync();
        }

        roots.Select(root => root.Value).Should().Equal(new[] { firstRoot, secondRoot }.Order());
        await context.Database.ExecuteSqlRawAsync("RESET ROLE;");
        var reservedUnits = await context.Database.SqlQuery<long>($"""
            SELECT COALESCE(sum(("EndExclusive" - "StartInclusive") / 1000), 0)::bigint AS "Value"
            FROM public.economy_fragment_reservations
            WHERE "RootSourceStampId" IN ({firstRoot}, {secondRoot}) AND "Status" = 1
            """).SingleAsync();
        reservedUnits.Should().Be(11);
    }

    [Fact]
    public async Task SourceRootPlanner_RollsBackIntentAndReservationsWhenTheProtectedOperationFails()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("transfer_root_rollback");
        await using var context = Context(database.ConnectionString);
        await context.Database.MigrateAsync();
        var sourceWallet = Guid.NewGuid();
        var destinationWallet = Guid.NewGuid();
        var firstRoot = Guid.NewGuid();
        var secondRoot = Guid.NewGuid();
        await SeedTransferLotsAsync(
            context, sourceWallet, destinationWallet, firstRoot, secondRoot);

        var operation = async () => await PostgreSqlTransactionExecutor.ExecuteAsync(
            context,
            IsolationLevel.Serializable,
            async token =>
            {
                var prepared = await new PostgreSqlSelfServiceEconomyTransferIntentStore(context)
                    .PrepareAsync(Draft("root-rollback", Now), token);
                await new PostgreSqlSelfServiceEconomyTransferSourceRootPlanner(context)
                    .ReserveAsync(new SelfServiceEconomyTransferSourceRootRequest(
                        prepared.PostingId,
                        TenantId,
                        ActorId,
                        new WalletId(sourceWallet),
                        new WalletId(destinationWallet)), token);
                throw new InvalidOperationException("simulated protected-operation rejection");
            },
            CancellationToken.None);

        await operation.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("simulated protected-operation rejection");
        (await context.Database.SqlQuery<long>($"""
            SELECT count(*)::bigint AS "Value"
            FROM public.economy_self_service_transfer_intents
            WHERE "IdempotencyKey" = {"root-rollback"}
            """).SingleAsync()).Should().Be(0);
        (await context.Database.SqlQuery<long>($"""
            SELECT count(*)::bigint AS "Value"
            FROM public.economy_fragment_reservations
            WHERE "RootSourceStampId" IN ({firstRoot}, {secondRoot})
            """).SingleAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SourceRootPlanner_RedactsDatabaseRejections()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("transfer_root_rejection");
        await using var context = Context(database.ConnectionString);
        await context.Database.MigrateAsync();
        var planner = new PostgreSqlSelfServiceEconomyTransferSourceRootPlanner(context);

        var reserve = () => planner.ReserveAsync(new SelfServiceEconomyTransferSourceRootRequest(
            new PostingId(Guid.NewGuid()),
            TenantId,
            ActorId,
            new WalletId(Guid.NewGuid()),
            new WalletId(Guid.NewGuid()))).AsTask();

        var exception = await reserve.Should().ThrowAsync<SelfServiceEconomyTransferException>()
            .WithMessage("The persistent Economy transfer planner rejected the request.");
        exception.Which.InnerException.Should().BeOfType<PostgresException>();
    }

    [Fact]
    public async Task FifoWriter_RejectsMismatchedRootsAndAcceptsTheExactRiskBinding()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("transfer_root_binding");
        await using var context = Context(database.ConnectionString);
        await context.Database.MigrateAsync();
        var sourceWallet = Guid.NewGuid();
        var destinationWallet = Guid.NewGuid();
        var firstRoot = Guid.NewGuid();
        var secondRoot = Guid.NewGuid();
        await SeedTransferLotsAsync(
            context, sourceWallet, destinationWallet, firstRoot, secondRoot);
        await SeedTransferAccountsAsync(context, sourceWallet, destinationWallet);

        var mismatch = () => ExecuteProtectedPostingAsync(
            context,
            sourceWallet,
            destinationWallet,
            [firstRoot],
            "root-binding");
        await mismatch.Should().ThrowAsync<RegisteredPostingRejectedException>()
            .WithMessage("The persistent Economy FIFO transfer writer rejected the request.");
        (await CountAsync(context, "economy_self_service_transfer_intents")).Should().Be(0);
        (await CountAsync(context, "economy_fragment_reservations")).Should().Be(0);
        (await CountAsync(context, "economy_posting_groups")).Should().Be(0);

        var receipt = await ExecuteProtectedPostingAsync(
            context,
            sourceWallet,
            destinationWallet,
            [firstRoot, secondRoot],
            "root-binding");
        receipt.IsDuplicate.Should().BeFalse();
        (await CountAsync(context, "economy_self_service_transfer_intents")).Should().Be(1);
        (await CountAsync(context, "economy_fifo_transfer_operations")).Should().Be(1);
        (await context.Database.SqlQuery<long>($"""
            SELECT count(*)::bigint AS "Value"
            FROM public.economy_fragment_reservations
            WHERE "OperationId" = {receipt.PostingId.Value} AND "Status" = 3
            """).SingleAsync()).Should().Be(2);
    }

    private static SelfServiceEconomyTransferIntentDraft Draft(string key, DateTimeOffset requestedAt) => new(
        TenantId,
        ActorId,
        RecipientId,
        SelfServiceEconomyTransferType.Gift,
        CurrencyCode.HardCoin,
        ProvenanceKind.PurchasedHard,
        11,
        new IdempotencyKey(key),
        requestedAt);

    private static async Task SeedTransferLotsAsync(
        ApplicationDbContext context,
        Guid sourceWallet,
        Guid destinationWallet,
        Guid firstRoot,
        Guid secondRoot)
    {
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.economy_wallets ("Id", "OwnerId", "TenantId", "State", "CreatedAt") VALUES
                ({sourceWallet}, {ActorId}, {TenantId}, 1, {Now.AddMinutes(-3)}),
                ({destinationWallet}, {RecipientId}, {TenantId}, 1, {Now.AddMinutes(-3)});
            """);
        await SeedLotAsync(context, sourceWallet, firstRoot, 7, Now.AddMinutes(-2), 1);
        await SeedLotAsync(context, sourceWallet, secondRoot, 7, Now.AddMinutes(-1), 2);
    }

    private static Task SeedLotAsync(
        ApplicationDbContext context,
        Guid walletId,
        Guid rootId,
        long amount,
        DateTimeOffset confirmedAt,
        long sequence)
    {
        var lotId = Guid.NewGuid();
        return context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.economy_source_stamps (
                "Id", "SourceKind", "InternalSourceId", "SourceLegId", "Provider", "ProviderReference", "EvidenceHash",
                "Provenance", "State", "ActorId", "TenantId", "PostingReferenceId", "PolicyVersion", "AuthoritativeUnits", "ObservedAt", "ConfirmedAt")
            VALUES ({rootId}, {"test"}, {rootId.ToString("N")}, {"leg"}, NULL, NULL, {"evidence-" + rootId.ToString("N")}, 1, 2,
                {ActorId}, {TenantId}, NULL, 1, {amount}, {confirmedAt}, {confirmedAt});
            INSERT INTO public.economy_credit_lots (
                "Id", "WalletId", "RootSourceStampId", "Currency", "AmountUnits", "Provenance", "CreditedAt", "ConfirmedAt",
                "OriginalMaturesAt", "CashOutEligible", "JournalSequence", "State", "ReversalEpoch")
            VALUES ({lotId}, {walletId}, {rootId}, 1, {amount}, 1, {confirmedAt}, {confirmedAt},
                {confirmedAt}, false, {sequence}, 1, 0);
            INSERT INTO public.economy_root_reversal_states (
                "RootSourceStampId", "Epoch", "CumulativeProviderUnits", "ReversedUnits", "State", "TargetedRanges", "UpdatedAt")
            VALUES ({rootId}, 0, 0, 0, {"active"}, {"[]"}::jsonb, {confirmedAt});
            INSERT INTO public.economy_fragment_root_ranges (
                "Id", "RootSourceStampId", "CreditLotId", "EntryAllocationId", "StartInclusive", "EndExclusive", "ReversalEpoch")
            VALUES ({Guid.NewGuid()}, {rootId}, {lotId}, NULL, 0, {amount * 1000}, 0);
            """);
    }

    private static Task SeedTransferAccountsAsync(
        ApplicationDbContext context,
        Guid sourceWallet,
        Guid destinationWallet) =>
        context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.economy_accounts ("Id", "WalletId", "Code", "Currency", "Provenance", "CreatedAt") VALUES
                ({Guid.NewGuid()}, {sourceWallet}, 2, 1, 1, {Now.AddMinutes(-3)}),
                ({Guid.NewGuid()}, {destinationWallet}, 2, 1, 1, {Now.AddMinutes(-3)});
            """);

    private static Task<RegisteredPostingReceipt> ExecuteProtectedPostingAsync(
        ApplicationDbContext context,
        Guid sourceWallet,
        Guid destinationWallet,
        IReadOnlyList<Guid> authorizedRoots,
        string idempotencyKey) =>
        PostgreSqlTransactionExecutor.ExecuteAsync(
            (DbContext)context,
            IsolationLevel.Serializable,
            async token =>
            {
                var prepared = await new PostgreSqlSelfServiceEconomyTransferIntentStore(context)
                    .PrepareAsync(Draft(idempotencyKey, Now), token);
                await new PostgreSqlSelfServiceEconomyTransferSourceRootPlanner(context)
                    .ReserveAsync(new SelfServiceEconomyTransferSourceRootRequest(
                        prepared.PostingId,
                        TenantId,
                        ActorId,
                        new WalletId(sourceWallet),
                        new WalletId(destinationWallet)), token);
                var capabilityId = await context.Database.SqlQuery<Guid>($"""
                    SELECT "Id" AS "Value"
                    FROM public.economy_registered_capabilities
                    WHERE "Name" = {"fifo-transfer"}
                    """).SingleAsync(token);
                var riskDecisionId = Guid.NewGuid();
                var riskCounterId = Guid.NewGuid();
                var operationFingerprint = $"self-service-transfer-{riskDecisionId:N}";
                var sourceRoots = JsonSerializer.Serialize(authorizedRoots);
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO public.economy_risk_counters (
                        "Id", "Dimension", "SubjectHash", "Operation", "Currency", "WindowStartedAt", "WindowEndsAt",
                        "CounterVersion", "MaxUnits", "UsedUnits", "UpdatedAt")
                    VALUES ({riskCounterId}, 1, {"self-service-transfer-source"}, 1, 1,
                        {Now.AddHours(-1)}, {Now.AddHours(1)}, 1, 100, 0, {Now.AddHours(-1)});
                    INSERT INTO public.economy_risk_decisions (
                        "Id", "Outcome", "OperationFingerprint", "IdempotencyKey", "ActorHash", "TemplateKind",
                        "SourceWalletId", "DestinationWalletId", "Currency", "AmountUnits", "CurrencyLegs", "SourceRoots",
                        "ProviderReferenceHash", "PolicyVersion", "ReserveVersion", "ReserveAuthorizationEpoch", "FeatureVersion",
                        "KillSwitchEpoch", "CounterVersion", "EntityGraphVersion", "EntityGraphEvidenceHash", "ReasonCodes",
                        "IssuedAt", "ExpiresAt")
                    VALUES ({riskDecisionId}, 1, {operationFingerprint}, {idempotencyKey}, {"actor-hash"}, 4,
                        {sourceWallet}, {destinationWallet}, 1, 11, {"[{\"currency\":1,\"units\":11}]"}::jsonb,
                        {sourceRoots}::jsonb, {prepared.ProviderReferenceHash}, 1, 1, 1, 1, 0, 1, 0,
                        {"graph-hash"}, {"[]"}::jsonb, {Now}, {Now.AddMinutes(5)});
                    SELECT economy_private.reserve_risk_counter_v1(
                        {Guid.NewGuid()}, {riskDecisionId}, {riskCounterId}, 1, 11, {Now});
                    """, token);
                return new PostgreSqlFifoTransferGateway(context).Transfer(
                    new PersistedFifoTransferRequest(
                        new TransferFragmentsCommand(
                            prepared.PostingId,
                            new IdempotencyKey(idempotencyKey),
                            new WalletId(sourceWallet),
                            new WalletId(destinationWallet),
                            new CoinAmount(CurrencyCode.HardCoin, 11),
                            ProvenanceKind.PurchasedHard,
                            new ReserveVersion(1),
                            new PolicyVersion(1),
                            Now),
                        new RegisteredPostingAuthority(
                            capabilityId,
                            ActorId,
                            TenantId,
                            riskDecisionId,
                            operationFingerprint,
                            1),
                        prepared.RequestHash));
            },
            CancellationToken.None);

    private static Task<long> CountAsync(ApplicationDbContext context, string tableName)
    {
        return tableName switch
        {
            "economy_self_service_transfer_intents" => context.Database.SqlQuery<long>(
                $"SELECT count(*)::bigint AS \"Value\" FROM public.economy_self_service_transfer_intents")
                .SingleAsync(),
            "economy_fragment_reservations" => context.Database.SqlQuery<long>(
                $"SELECT count(*)::bigint AS \"Value\" FROM public.economy_fragment_reservations")
                .SingleAsync(),
            "economy_posting_groups" => context.Database.SqlQuery<long>(
                $"SELECT count(*)::bigint AS \"Value\" FROM public.economy_posting_groups")
                .SingleAsync(),
            "economy_fifo_transfer_operations" => context.Database.SqlQuery<long>(
                $"SELECT count(*)::bigint AS \"Value\" FROM public.economy_fifo_transfer_operations")
                .SingleAsync(),
            _ => throw new ArgumentOutOfRangeException(nameof(tableName))
        };
    }

    private static ApplicationDbContext Context(string connectionString) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options);
}
