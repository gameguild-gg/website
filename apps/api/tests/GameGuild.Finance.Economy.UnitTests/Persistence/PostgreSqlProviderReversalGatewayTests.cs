using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.TestSupport.Finance.Economy;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Ledger;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace GameGuild.Finance.Economy.UnitTests.Persistence;

public sealed class PostgreSqlProviderReversalGatewayTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task WriterConsumesPersistedFractionsAndReplaysIdempotently()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("provider_reversal_writer");

        await using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(database.ConnectionString)
                .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
                .Options);
        await context.Database.MigrateAsync();

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        var wallet = Guid.NewGuid();
        var root = Guid.NewGuid();
        var lot = Guid.NewGuid();
        var capability = Guid.NewGuid();
        var originalPosting = Guid.NewGuid();
        var originalRisk = Guid.NewGuid();
        var riskDecision = Guid.NewGuid();
        var counter = Guid.NewGuid();
        var operation = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        await SeedAsync(connection, wallet, root, lot, capability, originalPosting, originalRisk, riskDecision, counter, actor, tenant);

        await ExecuteAsync(connection, "SET ROLE gameguild_economy_writer;");
        await ScalarAsync<bool>(connection, $"""
            SELECT economy_private.reserve_risk_counter_v1(
                '{Guid.NewGuid()}', '{riskDecision}', '{counter}', 1, 10, '{Now:O}');
            """);
        await ExecuteAsync(connection, "RESET ROLE;");
        var gateway = new PostgreSqlProviderReversalGateway(context);
        var authority = new RegisteredPostingAuthority(
            capability,
            actor,
            tenant,
            riskDecision,
            "provider-reversal-operation",
            1);
        var command = new ReverseTopUpCommand(
            new PostingId(operation),
            new IdempotencyKey("provider-reversal-1"),
            new SourceStampId(root),
            10,
            ProviderReversalDisposition.ResponsibleDebt,
            "provider-evidence",
            new ReserveVersion(1),
            new PolicyVersion(1),
            Now);
        var first = gateway.Reverse(new PersistedProviderReversal(command, authority));
        first.RecoveredHardUnits.Should().Be(10);
        first.RecoveredConvertedSoftUnits.Should().Be(0);
        first.ResponsibleDebtHardUnits.Should().Be(0);
        first.PlatformLossHardUnits.Should().Be(0);
        first.IsDuplicate.Should().BeFalse();

        var replay = gateway.Reverse(new PersistedProviderReversal(command, authority));
        replay.IsDuplicate.Should().BeTrue();
        replay.RecoveredHardUnits.Should().Be(10);

        (await ScalarAsync<long>(connection, $"SELECT count(*) FROM public.economy_entry_allocations WHERE \"ParentLotId\" = '{lot}';"))
            .Should().Be(1);
        (await ScalarAsync<long>(connection, $"SELECT \"CumulativeProviderUnits\" FROM public.economy_root_reversal_states WHERE \"RootSourceStampId\" = '{root}';"))
            .Should().Be(10);
        (await ScalarAsync<string>(connection, $"SELECT \"State\" FROM public.economy_root_reversal_states WHERE \"RootSourceStampId\" = '{root}';"))
            .Should().Be("reversed");
        (await ScalarAsync<long>(connection, $"SELECT \"CumulativeProviderReversalUnits\" FROM public.economy_funding_claims WHERE \"SourceStampId\" = '{root}';"))
            .Should().Be(10);
        (await ScalarAsync<long>(connection, $"SELECT count(*) FROM public.economy_provider_reversal_operations WHERE \"Id\" = '{operation}';"))
            .Should().Be(1);
    }

    private static Task SeedAsync(
        NpgsqlConnection connection,
        Guid wallet,
        Guid root,
        Guid lot,
        Guid capability,
        Guid originalPosting,
        Guid originalRisk,
        Guid riskDecision,
        Guid counter,
        Guid actor,
        Guid tenant) =>
        ExecuteAsync(connection, $"""
            INSERT INTO public.economy_wallets ("Id", "OwnerId", "TenantId", "State", "CreatedAt")
            VALUES ('{wallet}', '{Guid.NewGuid()}', '{tenant}', 1, '{Now.AddHours(-1):O}');
            INSERT INTO public.economy_accounts ("Id", "WalletId", "Code", "Currency", "Provenance", "CreatedAt") VALUES
                ('{Guid.NewGuid()}', NULL, 1, 1, NULL, '{Now.AddHours(-1):O}'),
                ('{Guid.NewGuid()}', '{wallet}', 2, 1, 1, '{Now.AddHours(-1):O}');
            INSERT INTO public.economy_registered_capabilities ("Id", "Name", "AllowedTemplateKinds", "IsEnabled", "CreatedAt", "RevokedAt")
            VALUES ('{capability}', 'provider-reversal-test', '[2,3,18,19,20]'::jsonb, true, '{Now.AddHours(-1):O}', NULL);
            INSERT INTO public.economy_risk_counters (
                "Id", "Dimension", "SubjectHash", "Operation", "Currency", "WindowStartedAt", "WindowEndsAt",
                "CounterVersion", "MaxUnits", "UsedUnits", "UpdatedAt")
            VALUES ('{counter}', 1, 'provider-reversal', 2, 1, '{Now.AddHours(-1):O}', '{Now.AddHours(1):O}', 1, 100, 0, '{Now.AddHours(-1):O}');
            INSERT INTO public.economy_risk_decisions (
                "Id", "Outcome", "OperationFingerprint", "ActorHash", "TemplateKind", "SourceWalletId", "DestinationWalletId",
                "Currency", "AmountUnits", "CurrencyLegs", "SourceRoots", "ProviderReferenceHash", "PolicyVersion",
                "ReserveVersion", "ReserveAuthorizationEpoch", "FeatureVersion", "KillSwitchEpoch", "CounterVersion", "EntityGraphVersion",
                "EntityGraphEvidenceHash", "ReasonCodes", "IssuedAt", "ExpiresAt")
            VALUES
                ('{originalRisk}', 1, 'original-provider-mint', 'actor-hash', 1, '{wallet}', '{wallet}', 1, 10, '[]', '[]', 'provider-hash', 1, 1, 1, 1, 0, 1, 0, 'graph-hash', '[]', '{Now.AddMinutes(-1):O}', '{Now.AddMinutes(5):O}'),
                ('{riskDecision}', 1, 'provider-reversal-operation', 'actor-hash', 2, '{wallet}', '{wallet}', 1, 10, '[]', '[]', 'provider-hash', 1, 1, 1, 1, 0, 1, 0, 'graph-hash', '[]', '{Now.AddMinutes(-1):O}', '{Now.AddMinutes(5):O}');
                        INSERT INTO public.economy_source_stamps (
                "Id", "SourceKind", "InternalSourceId", "SourceLegId", "Provider", "ProviderReference", "EvidenceHash",
                "Provenance", "State", "ActorId", "TenantId", "PostingReferenceId", "PolicyVersion", "AuthoritativeUnits", "ObservedAt", "ConfirmedAt")
            VALUES ('{root}', 'provider-test', '{root:N}', 'leg', 'test', 'provider-ref', 'source-evidence', 1, 2,
                '{actor}', '{tenant}', NULL, 1, 10, '{Now.AddHours(-1):O}', '{Now.AddHours(-1):O}');
            INSERT INTO public.economy_posting_groups (
                "Id", "IdempotencyKey", "TemplateKind", "TemplateVersion", "Authority", "Status", "CapabilityId", "ActorId", "TenantId",
                "RiskDecisionId", "PolicyVersion", "ReserveVersion", "ReserveAuthorizationEpoch", "SourceStampId", "RecordedAt")
            VALUES ('{originalPosting}', 'original-provider-mint', 1, 1, 1, 1, '{capability}', '{actor}', '{tenant}', '{originalRisk}', 1, 1, 1, '{root}', '{Now.AddHours(-1):O}');
            INSERT INTO public.economy_credit_lots (
                "Id", "WalletId", "RootSourceStampId", "Currency", "AmountUnits", "Provenance", "CreditedAt", "ConfirmedAt",
                "OriginalMaturesAt", "CashOutEligible", "JournalSequence", "State", "ReversalEpoch")
            VALUES ('{lot}', '{wallet}', '{root}', 1, 10, 1, '{Now.AddHours(-1):O}', '{Now.AddHours(-1):O}',
                '{Now.AddHours(-1):O}', false, 1, 1, 0);
            INSERT INTO public.economy_funding_claims (
                "SourceStampId", "WalletId", "Provider", "Environment", "ConnectedAccount", "ProviderObject", "ProviderMonetaryLeg",
                "AuthoritativeUsdMinorUnits", "State", "ObservedAt", "ConfirmedAt", "StateChangedAt", "PostingGroupId", "RootCreditLotId", "CumulativeProviderReversalUnits", "Version")
            VALUES ('{root}', '{wallet}', 'test', 'test', 'account', 'object', 'leg', 10, 2,
                '{Now.AddHours(-1):O}', '{Now.AddHours(-1):O}', '{Now.AddHours(-1):O}', '{originalPosting}', '{lot}', 0, 1);
            INSERT INTO public.economy_root_reversal_states (
                "RootSourceStampId", "Epoch", "CumulativeProviderUnits", "ReversedUnits", "State", "TargetedRanges", "UpdatedAt")
            VALUES ('{root}', 0, 0, 0, 'active', '[]'::jsonb, '{Now.AddHours(-1):O}');
            INSERT INTO public.economy_fragment_root_ranges (
                "Id", "RootSourceStampId", "CreditLotId", "EntryAllocationId", "StartInclusive", "EndExclusive", "ReversalEpoch")
            VALUES ('{Guid.NewGuid()}', '{root}', '{lot}', NULL, 0, 10000, 0);
            """);

    private static async Task<Receipt> ReverseAsync(
        NpgsqlConnection connection,
        Guid capability,
        Guid actor,
        Guid tenant,
        Guid riskDecision,
        Guid operation,
        Guid root,
        string idempotencyKey,
        long cumulativeHardUnits)
    {
        await using var command = new NpgsqlCommand($"""
            SELECT * FROM economy_private.post_provider_reversal_v2(
                '{capability}', '{actor}', '{tenant}', '{operation}', '{idempotencyKey}', '{root}',
                {cumulativeHardUnits}, 1, 'provider-evidence', 1, 1, '{riskDecision}', 'provider-reversal-operation', 1, '{Now:O}', NULL);
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        return new Receipt(reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3), reader.GetInt64(4), reader.GetBoolean(5));
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? default! : (T)value;
    }

    private sealed record Receipt(
        long RecoveredHardUnits,
        long RecoveredConvertedSoftUnits,
        long ResponsibleDebtHardUnits,
        long PlatformLossHardUnits,
        bool Duplicate);
}
