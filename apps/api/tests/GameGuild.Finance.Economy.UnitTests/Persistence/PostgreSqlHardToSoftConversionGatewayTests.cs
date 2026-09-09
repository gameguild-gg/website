using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.TestSupport.Finance.Economy;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Finance.Economy.UnitTests.Funding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using System.Reflection;

namespace GameGuild.Finance.Economy.UnitTests.Persistence;

public sealed class PostgreSqlHardToSoftConversionGatewayTests
{
    internal static readonly DateTimeOffset Now = new(2026, 8, 10, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task IssueSelfServiceDecision_ReservesConfirmedFifoFragmentsAndAuditsEvidence()
    {
        await using var database = await CreateDatabaseAsync();
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.MigrateAsync();

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        var wallet = Guid.NewGuid();
        var root = Guid.NewGuid();
        var hardLot = Guid.NewGuid();
        var capability = Guid.NewGuid();
        var existingDecision = Guid.NewGuid();
        var existingCounter = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var operation = Guid.NewGuid();
        const string idempotencyKey = "issue-self-service-risk-decision";

        await SeedAsync(
            connection,
            wallet,
            root,
            hardLot,
            capability,
            existingDecision,
            existingCounter,
            actor,
            tenant,
            "seeded-decision-not-used-by-issuer");
        await InsertCurrentCoveredReserveHeadAsync(connection);

        var issuer = new PostgreSqlHardToSoftConversionRiskDecisionIssuer(context);
        var request = new HardToSoftConversionRiskDecisionRequest(
            actor,
            tenant,
            new WalletId(wallet),
            operation,
            new IdempotencyKey(idempotencyKey),
            0,
            10,
            100,
            300,
            "BRA",
            7,
            "signed-policy-hash",
            [
                new(ExternalRiskSource.FinancialCrime, 1, Now.AddMinutes(-1), Now.AddMinutes(5), ExternalRiskOutcome.Allow, "financial-crime-evidence"),
                new(ExternalRiskSource.TrustSafety, 1, Now.AddMinutes(-1), Now.AddMinutes(5), ExternalRiskOutcome.Allow, "trust-safety-evidence")
            ],
            Now);

        var issued = await issuer.IssueAsync(request, CancellationToken.None);

        issued.SourceRoots.Should().Equal(root);
        (await ScalarAsync<long>(connection, $"SELECT count(*) FROM public.economy_fragment_reservations WHERE \"OperationId\" = '{operation}' AND \"RootSourceStampId\" = '{root}' AND \"Purpose\" = 3;")).Should().Be(1);
        (await ScalarAsync<long>(connection, $"SELECT count(*) FROM public.economy_risk_counter_reservations WHERE \"RiskDecisionId\" = '{issued.Id}' AND \"AmountUnits\" = 10;")).Should().Be(1);
        (await ScalarAsync<long>(connection, $"SELECT count(*) FROM public.economy_risk_audit_evidence WHERE \"RiskDecisionId\" = '{issued.Id}';")).Should().Be(3);
        (await ScalarAsync<long>(connection, $"SELECT count(*) FROM public.economy_risk_audit_evidence WHERE \"RiskDecisionId\" = '{issued.Id}' AND \"EvidenceHash\" = 'signed-policy-hash' AND \"Payload\"->>'kind' = 'signed-capability-policy' AND \"Payload\"->>'jurisdictionCode' = 'BRA';")).Should().Be(1);

        var replay = await issuer.IssueAsync(request, CancellationToken.None);
        replay.Id.Should().Be(issued.Id);
        replay.SourceRoots.Should().Equal(root);
        (await ScalarAsync<long>(connection, $"SELECT count(*) FROM public.economy_fragment_reservations WHERE \"OperationId\" = '{operation}';")).Should().Be(1);
    }

    [Fact]
    public async Task Convert_ConsumesConfirmedHardCoinFifoAndPersistsSoftCoinIdempotently()
    {
        await using var database = await CreateDatabaseAsync();
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.MigrateAsync();

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        var wallet = Guid.NewGuid();
        var root = Guid.NewGuid();
        var hardLot = Guid.NewGuid();
        var capability = Guid.NewGuid();
        var riskDecision = Guid.NewGuid();
        var counter = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var posting = PostingId.New();
        var outputLot = CreditLotId.New();
        var key = new IdempotencyKey("durable-hard-to-soft-conversion");

        await SeedAsync(connection, wallet, root, hardLot, capability, riskDecision, counter, actor, tenant, key.Value);
        await ReserveRiskCounterAsync(connection, riskDecision, counter, 10);

        var command = new ConvertHardToSoftCommand(
            posting,
            default,
            key,
            new WalletId(wallet),
            outputLot,
            10,
            0,
            new ReserveVersion(1),
            new PolicyVersion(1),
            Now,
            FundingAuthorizationFixture.Create(
                PostingTemplateKind.HardToSoftConversion,
                key,
                new WalletId(wallet),
                new CoinAmount(CurrencyCode.HardCoin, 10),
                [new SourceStampId(root)],
                Now,
                new CoinAmount(CurrencyCode.SoftCoin, 10_000)));
        var authority = new RegisteredPostingAuthority(
            capability, actor, tenant, riskDecision, "durable-hard-to-soft-conversion", 1);
        var gateway = new PostgreSqlHardToSoftConversionGateway(context);

        var receipt = gateway.Convert(new PersistedHardToSoftConversion(command, authority));
        receipt.PrincipalPosting.PostingId.Should().Be(posting);
        receipt.PrincipalPosting.IsDuplicate.Should().BeFalse();
        receipt.FeePosting.Should().BeNull();

        (await ScalarAsync<long>(connection, $"SELECT \"AmountUnits\" FROM public.economy_credit_lots WHERE \"Id\" = '{outputLot.Value}';"))
            .Should().Be(10_000);
        (await ScalarAsync<long>(connection, $"SELECT \"PurchasedHard\" FROM public.economy_wallet_balance_projections WHERE \"WalletId\" = '{wallet}';"))
            .Should().Be(0);
        (await ScalarAsync<long>(connection, $"SELECT \"Soft\" FROM public.economy_wallet_balance_projections WHERE \"WalletId\" = '{wallet}';"))
            .Should().Be(10_000);
        (await ScalarAsync<long>(connection, $"SELECT count(*) FROM public.economy_fragment_reservations WHERE \"OperationId\" = '{posting.Value}' AND \"Status\" = 3;"))
            .Should().Be(1);

        var replay = gateway.Convert(new PersistedHardToSoftConversion(command, authority));
        replay.PrincipalPosting.IsDuplicate.Should().BeTrue();
        replay.PrincipalPosting.JournalSequence.Should().Be(receipt.PrincipalPosting.JournalSequence);
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM public.economy_lot_lineage_edges;"))
            .Should().Be(1);
        (await ScalarAsync<long>(connection, $"SELECT \"AmountUnits\" FROM public.economy_lot_lineage_edges WHERE \"ChildLotId\" = '{outputLot.Value}';"))
            .Should().Be(10_000);
        (await ScalarAsync<long>(connection, $"SELECT count(*) FROM public.economy_fragment_root_ranges WHERE \"CreditLotId\" = '{outputLot.Value}';"))
            .Should().Be(1);
        (await ScalarAsync<long>(connection, $"SELECT count(*) FROM public.economy_outbox_messages WHERE \"PostingGroupId\" = '{posting.Value}';"))
            .Should().Be(1);
    }

    [Fact]
    public async Task Convert_WithAFee_PersistsAndReturnsTheFeePostingReceipt()
    {
        await using var database = await CreateDatabaseAsync();
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.MigrateAsync();

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        var wallet = Guid.NewGuid();
        var root = Guid.NewGuid();
        var hardLot = Guid.NewGuid();
        var capability = Guid.NewGuid();
        var riskDecision = Guid.NewGuid();
        var counter = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var principalPosting = PostingId.New();
        var feePosting = PostingId.New();
        var outputLot = CreditLotId.New();
        var key = new IdempotencyKey("durable-hard-to-soft-conversion-fee");

        await SeedAsync(connection, wallet, root, hardLot, capability, riskDecision, counter, actor, tenant, key.Value, hardUnits: 11, operationFingerprint: "durable-hard-to-soft-conversion-fee");
        await ReserveRiskCounterAsync(connection, riskDecision, counter, 11);
        (await ScalarAsync<bool>(connection, $"""
            SELECT EXISTS (
                SELECT 1
                FROM public.economy_risk_decisions decision
                WHERE decision."Id" = '{riskDecision}'
                  AND decision."Outcome" = 1
                  AND decision."TemplateKind" = 5
                  AND decision."OperationFingerprint" = 'durable-hard-to-soft-conversion-fee'
                  AND decision."PolicyVersion" = 1
                  AND decision."ReserveVersion" = 1
                  AND decision."CounterVersion" = 1
                  AND decision."Currency" = 1
                  AND decision."AmountUnits" = 11
                  AND decision."IssuedAt" <= '{Now:O}'
                  AND decision."ExpiresAt" > '{Now:O}')
            AND NOT EXISTS (
                SELECT 1 FROM public.economy_risk_decision_consumptions
                WHERE "RiskDecisionId" = '{riskDecision}')
            AND EXISTS (
                SELECT 1
                FROM public.economy_risk_counter_reservations reservation
                JOIN public.economy_risk_counters risk_counter ON risk_counter."Id" = reservation."RiskCounterId"
                WHERE reservation."RiskDecisionId" = '{riskDecision}'
                  AND reservation."AmountUnits" = 11
                  AND risk_counter."CounterVersion" = 1);
            """)).Should().BeTrue();

        var command = new ConvertHardToSoftCommand(
            principalPosting,
            feePosting,
            key,
            new WalletId(wallet),
            outputLot,
            10,
            1,
            new ReserveVersion(1),
            new PolicyVersion(1),
            Now,
            FundingAuthorizationFixture.Create(
                PostingTemplateKind.HardToSoftConversion,
                key,
                new WalletId(wallet),
                new CoinAmount(CurrencyCode.HardCoin, 11),
                [new SourceStampId(root)],
                Now,
                new CoinAmount(CurrencyCode.SoftCoin, 10_000)));
        var authority = new RegisteredPostingAuthority(
            capability, actor, tenant, riskDecision, "durable-hard-to-soft-conversion-fee", 1);

        var receipt = new PostgreSqlHardToSoftConversionGateway(context)
            .Convert(new PersistedHardToSoftConversion(command, authority));

        receipt.PrincipalPosting.PostingId.Should().Be(principalPosting);
        receipt.FeePosting.Should().NotBeNull();
        receipt.FeePosting!.PostingId.Should().Be(feePosting);
        receipt.FeePosting.IsDuplicate.Should().BeFalse();
        (await ScalarAsync<long>(connection, $"SELECT count(*) FROM public.economy_journal_entries WHERE \"PostingGroupId\" = '{feePosting.Value}';"))
            .Should().Be(1);
        (await ScalarAsync<long>(connection, $"SELECT \"AmountUnits\" FROM public.economy_journal_lines WHERE \"JournalEntryId\" IN (SELECT \"Id\" FROM public.economy_journal_entries WHERE \"PostingGroupId\" = '{feePosting.Value}') AND \"AccountId\" IN (SELECT \"Id\" FROM public.economy_accounts WHERE \"Code\" = 14);"))
            .Should().Be(1);
    }

    [Fact]
    public async Task FeeReceiptReadRejectsWhenTheWriterDidNotPersistIt()
    {
        await using var database = await CreateDatabaseAsync();
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.MigrateAsync();
        var method = typeof(PostgreSqlHardToSoftConversionGateway).GetMethod(
            "ReadFeeReceipt", BindingFlags.NonPublic | BindingFlags.Instance)!;

        Action read = () => method.Invoke(
            new PostgreSqlHardToSoftConversionGateway(context),
            [PostingId.New()]);

        read.Should().Throw<TargetInvocationException>()
            .Which.InnerException.Should().BeOfType<RegisteredPostingRejectedException>()
            .Which.Message.Should().Contain("fee posting was not persisted");
    }
    internal static Task<EconomyPostgreSqlTestDatabase> CreateDatabaseAsync() =>
        EconomyPostgreSqlTestDatabase.CreateAsync("hard_to_soft_gateway");

    internal static ApplicationDbContext CreateContext(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString) { IncludeErrorDetail = true };
        return new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(builder.ConnectionString)
                .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
                .Options);
    }

    internal static async Task SeedAsync(
        NpgsqlConnection connection,
        Guid wallet,
        Guid root,
        Guid hardLot,
        Guid capability,
        Guid riskDecision,
        Guid counter,
        Guid actor,
        Guid tenant,
        string idempotencyKey,
        long hardUnits = 10,
        string operationFingerprint = "durable-hard-to-soft-conversion",
        DateTimeOffset? timestamp = null,
        string capabilityName = "hard-to-soft-gateway")
    {
        var now = timestamp ?? Now;
        await ExecuteAsync(connection, $"""
            INSERT INTO public.economy_wallets ("Id", "OwnerId", "TenantId", "State", "CreatedAt")
            VALUES ('{wallet}', '{actor}', '{tenant}', 1, '{now.AddMinutes(-3):O}');
            INSERT INTO public.economy_accounts ("Id", "WalletId", "Code", "Currency", "Provenance", "CreatedAt") VALUES
                ('{Guid.NewGuid()}', '{wallet}', 2, 1, 1, '{now.AddMinutes(-3):O}'),
                ('{Guid.NewGuid()}', NULL, 5, 1, NULL, '{now.AddMinutes(-3):O}'),
                ('{Guid.NewGuid()}', NULL, 6, 2, NULL, '{now.AddMinutes(-3):O}'),
                ('{Guid.NewGuid()}', NULL, 14, 1, NULL, '{now.AddMinutes(-3):O}'),
                ('{Guid.NewGuid()}', '{wallet}', 4, 2, 3, '{now.AddMinutes(-3):O}');
            INSERT INTO public.economy_registered_capabilities ("Id", "Name", "AllowedTemplateKinds", "IsEnabled", "CreatedAt", "RevokedAt")
            VALUES ('{capability}', '{capabilityName}', '[5,17]'::jsonb, true, '{now.AddMinutes(-3):O}', NULL);
            INSERT INTO public.economy_risk_counters (
                "Id", "Dimension", "SubjectHash", "Operation", "Currency", "WindowStartedAt", "WindowEndsAt",
                "CounterVersion", "MaxUnits", "UsedUnits", "UpdatedAt")
            VALUES ('{counter}', 1, 'hard-to-soft-gateway', 1, 1, '{now.AddHours(-1):O}', '{now.AddHours(1):O}', 1, 100, 0, '{now.AddHours(-1):O}');
            INSERT INTO public.economy_risk_decisions (
                "Id", "Outcome", "OperationFingerprint", "ActorHash", "TemplateKind", "SourceWalletId", "DestinationWalletId",
                "Currency", "AmountUnits", "CurrencyLegs", "SourceRoots", "ProviderReferenceHash", "IdempotencyKey", "PolicyVersion",
                "ReserveVersion", "ReserveAuthorizationEpoch", "FeatureVersion", "KillSwitchEpoch", "CounterVersion", "EntityGraphVersion",
                "EntityGraphEvidenceHash", "ReasonCodes", "IssuedAt", "ExpiresAt")
            VALUES ('{riskDecision}', 1, '{operationFingerprint}', 'actor-hash', 5, '{wallet}', '{wallet}',
                1, {hardUnits}, '[]', jsonb_build_array('{root}'::text), 'provider-hash', '{idempotencyKey}', 1, 1, 1, 1, 0, 1, 0, 'graph-hash', '[]', '{now.AddMinutes(-1):O}', '{now.AddMinutes(5):O}');
            INSERT INTO public.economy_source_stamps (
                "Id", "SourceKind", "InternalSourceId", "SourceLegId", "Provider", "ProviderReference", "EvidenceHash",
                "Provenance", "State", "ActorId", "TenantId", "PostingReferenceId", "PolicyVersion", "AuthoritativeUnits", "ObservedAt", "ConfirmedAt")
            VALUES ('{root}', 'test', '{root:N}', 'principal', NULL, NULL, 'evidence-{root:N}', 1, 2,
                '{actor}', '{tenant}', NULL, 1, {hardUnits}, '{now.AddMinutes(-2):O}', '{now.AddMinutes(-2):O}');
            INSERT INTO public.economy_credit_lots (
                "Id", "WalletId", "RootSourceStampId", "Currency", "AmountUnits", "Provenance", "CreditedAt", "ConfirmedAt",
                "OriginalMaturesAt", "CashOutEligible", "JournalSequence", "State", "ReversalEpoch")
            VALUES ('{hardLot}', '{wallet}', '{root}', 1, {hardUnits}, 1, '{now.AddMinutes(-2):O}', '{now.AddMinutes(-2):O}',
                '{now.AddMinutes(-2):O}', false, 1, 1, 0);
            INSERT INTO public.economy_root_reversal_states (
                "RootSourceStampId", "Epoch", "CumulativeProviderUnits", "ReversedUnits", "State", "TargetedRanges", "UpdatedAt")
            VALUES ('{root}', 0, 0, 0, 'active', '[]'::jsonb, '{now.AddMinutes(-2):O}');
            INSERT INTO public.economy_fragment_root_ranges (
                "Id", "RootSourceStampId", "CreditLotId", "EntryAllocationId", "StartInclusive", "EndExclusive", "ReversalEpoch")
            VALUES ('{Guid.NewGuid()}', '{root}', '{hardLot}', NULL, 0, {hardUnits * 1000}, 0);
            """);
    }

    internal static async Task ReserveRiskCounterAsync(NpgsqlConnection connection, Guid riskDecision, Guid counter, long amount, DateTimeOffset? timestamp = null)
    {
        var now = timestamp ?? Now;
        await ExecuteAsync(connection, "SET ROLE gameguild_economy_writer;");
        await ScalarAsync<bool>(connection, $"SELECT economy_private.reserve_risk_counter_v1('{Guid.NewGuid()}', '{riskDecision}', '{counter}', 1, {amount}, '{now:O}');");
        await ExecuteAsync(connection, "RESET ROLE;");
    }

    private static async Task InsertCurrentCoveredReserveHeadAsync(NpgsqlConnection connection)
    {
        await ExecuteAsync(connection, $"""
            INSERT INTO public.economy_reserve_heads (
                "Version", "IsActive", "PolicyVersion", "AuthorizationEpoch", "ObservedAt", "ExpiresAt",
                "HardFaceValueUsdMinor", "RequiredHardReserveUsdMinor", "SoftFaceValueUsdNanos",
                "StressedExpectedRedemptionCostUsdNanos", "RequiredSoftReserveUsdNanos", "HardBackingUsdNanos",
                "SoftBackingUsdNanos", "Coverage", "EvidenceHash", "ActivatedAt")
            VALUES (
                1, true, 1, 1, '{Now.AddMinutes(-1):O}', '{Now.AddMinutes(5):O}',
                0, 0, 0, 0, 0, 0, 0, 1, 'covered-reserve-evidence', '{Now:O}');
            """);
    }

    internal static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    internal static async Task<T> ScalarAsync<T>(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? default! : (T)value;
    }

}
