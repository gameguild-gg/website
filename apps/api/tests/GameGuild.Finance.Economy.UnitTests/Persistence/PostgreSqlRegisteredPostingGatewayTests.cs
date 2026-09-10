using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.TestSupport.Finance.Economy;
using GameGuild.Finance.Economy.Ledger;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace GameGuild.Finance.Economy.UnitTests.Persistence;

public sealed class PostgreSqlRegisteredPostingGatewayTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 10, 22, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Post_PersistsAuthorizedSpendInTheImmutableJournalAndReplaysIdempotently()
    {
        await using var database = await CreateDatabaseAsync();
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.MigrateAsync();

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        var sourceWallet = Guid.NewGuid();
        var destinationWallet = Guid.NewGuid();
        var capability = Guid.NewGuid();
        var riskDecision = Guid.NewGuid();
        var counter = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        await SeedAsync(connection, sourceWallet, destinationWallet, capability, riskDecision, counter, actor, tenant);
        await ReserveRiskCounterAsync(connection, riskDecision, counter, 50);

        var request = new RegisteredPostingRequest(
            new RegisteredPostingAuthority(capability, actor, tenant, riskDecision, "durable-spend-posting", 1),
            new PostingRequest(
                PostingId.New(),
                new PostingTemplate(PostingTemplateKind.Spend, PostingTemplate.CurrentVersion),
                new IdempotencyKey("durable-registered-spend"),
                PostingAuthority.WalletOwner,
                new ReserveVersion(1),
                new PolicyVersion(1),
                null,
                Now,
                [
                    new PostingLine(1, EntrySide.Debit, EconomyAccountCode.SoftCoinLiability,
                        new CoinAmount(CurrencyCode.SoftCoin, 50), new WalletId(sourceWallet), null, ProvenanceKind.ConvertedSoft),
                    new PostingLine(2, EntrySide.Credit, EconomyAccountCode.SoftCoinLiability,
                        new CoinAmount(CurrencyCode.SoftCoin, 50), new WalletId(destinationWallet), null, ProvenanceKind.ConvertedSoft)
                ]));
        var gateway = new PostgreSqlRegisteredPostingGateway(context);

        var receipt = gateway.Post(request);
        receipt.IsDuplicate.Should().BeFalse();
        receipt.JournalSequence.Should().BePositive();
        receipt.JournalHash.Should().HaveLength(64);
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM public.economy_posting_groups;"))
            .Should().Be(1);
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM public.economy_journal_lines;"))
            .Should().Be(2);
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM public.economy_risk_decision_consumptions;"))
            .Should().Be(1);
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM public.economy_outbox_messages;"))
            .Should().Be(1);

        var replay = gateway.Post(request);
        replay.IsDuplicate.Should().BeTrue();
        replay.JournalSequence.Should().Be(receipt.JournalSequence);
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM public.economy_outbox_messages;"))
            .Should().Be(1);
    }

    [Fact]
    public async Task Post_RejectsAReferenceToAnUnprovisionedAccount()
    {
        await using var database = await CreateDatabaseAsync();
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.MigrateAsync();

        var request = new RegisteredPostingRequest(
            new RegisteredPostingAuthority(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "missing-account", 1),
            new PostingRequest(
                PostingId.New(),
                new PostingTemplate(PostingTemplateKind.Spend, PostingTemplate.CurrentVersion),
                new IdempotencyKey("missing-registered-account"),
                PostingAuthority.WalletOwner,
                new ReserveVersion(1),
                new PolicyVersion(1),
                null,
                Now,
                [
                    new PostingLine(1, EntrySide.Debit, EconomyAccountCode.SoftCoinLiability,
                        new CoinAmount(CurrencyCode.SoftCoin, 1), WalletId.New(), null, ProvenanceKind.ConvertedSoft),
                    new PostingLine(2, EntrySide.Credit, EconomyAccountCode.SoftCoinLiability,
                        new CoinAmount(CurrencyCode.SoftCoin, 1), WalletId.New(), null, ProvenanceKind.ConvertedSoft)
                ]));

        FluentActions.Invoking(() => new PostgreSqlRegisteredPostingGateway(context).Post(request))
            .Should().Throw<RegisteredPostingRejectedException>()
            .WithMessage("*not provisioned*");
    }

    private static Task<EconomyPostgreSqlTestDatabase> CreateDatabaseAsync() =>
        EconomyPostgreSqlTestDatabase.CreateAsync("registered_posting_gateway");

    private static ApplicationDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options);

    private static Task SeedAsync(
        NpgsqlConnection connection,
        Guid sourceWallet,
        Guid destinationWallet,
        Guid capability,
        Guid riskDecision,
        Guid counter,
        Guid actor,
        Guid tenant) =>
        ExecuteAsync(connection, $"""
            INSERT INTO public.economy_wallets ("Id", "OwnerId", "TenantId", "State", "CreatedAt") VALUES
                ('{sourceWallet}', '{actor}', '{tenant}', 1, '{Now.AddMinutes(-2):O}'),
                ('{destinationWallet}', '{Guid.NewGuid()}', '{Guid.NewGuid()}', 1, '{Now.AddMinutes(-2):O}');
            INSERT INTO public.economy_accounts ("Id", "WalletId", "Code", "Currency", "Provenance", "CreatedAt") VALUES
                ('{Guid.NewGuid()}', '{sourceWallet}', 4, 2, 3, '{Now.AddMinutes(-2):O}'),
                ('{Guid.NewGuid()}', '{destinationWallet}', 4, 2, 3, '{Now.AddMinutes(-2):O}');
            INSERT INTO public.economy_registered_capabilities ("Id", "Name", "AllowedTemplateKinds", "IsEnabled", "CreatedAt", "RevokedAt")
            VALUES ('{capability}', 'registered-spend', '[4]'::jsonb, true, '{Now.AddMinutes(-2):O}', NULL);
            INSERT INTO public.economy_risk_counters (
                "Id", "Dimension", "SubjectHash", "Operation", "Currency", "WindowStartedAt", "WindowEndsAt",
                "CounterVersion", "MaxUnits", "UsedUnits", "UpdatedAt")
            VALUES ('{counter}', 1, 'registered-spend', 1, 2, '{Now.AddHours(-1):O}', '{Now.AddHours(1):O}', 1, 100, 0, '{Now.AddHours(-1):O}');
            INSERT INTO public.economy_risk_decisions (
                "Id", "Outcome", "OperationFingerprint", "ActorHash", "TemplateKind", "SourceWalletId", "DestinationWalletId",
                "Currency", "AmountUnits", "CurrencyLegs", "SourceRoots", "ProviderReferenceHash", "PolicyVersion",
                "ReserveVersion", "ReserveAuthorizationEpoch", "FeatureVersion", "KillSwitchEpoch", "CounterVersion", "EntityGraphVersion",
                "EntityGraphEvidenceHash", "ReasonCodes", "IssuedAt", "ExpiresAt")
            VALUES ('{riskDecision}', 1, 'durable-spend-posting', 'actor-hash', 4, '{sourceWallet}', '{destinationWallet}',
                2, 50, '[]', '[]', 'provider-hash', 1, 1, 1, 1, 0, 1, 0, 'graph-hash', '[]', '{Now.AddMinutes(-1):O}', '{Now.AddMinutes(5):O}');
            """);

    private static async Task ReserveRiskCounterAsync(NpgsqlConnection connection, Guid riskDecision, Guid counter, long amount)
    {
        await ExecuteAsync(connection, "SET ROLE gameguild_economy_writer;");
        await ScalarAsync<bool>(connection, $"SELECT economy_private.reserve_risk_counter_v1('{Guid.NewGuid()}', '{riskDecision}', '{counter}', 1, {amount}, '{Now:O}');");
        await ExecuteAsync(connection, "RESET ROLE;");
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

}
