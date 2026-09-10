using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Finance.Economy.UnitTests.Funding;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace GameGuild.Finance.Economy.UnitTests.Persistence;

public sealed class PostgreSqlHardCoinFundingGatewayTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 10, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ObserveThenConfirm_PersistsAnAuthorizedMintAndReplaysIdempotently()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("hard_coin_funding_gateway");

        await using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(database.ConnectionString)
                .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
                .Options);
        await context.Database.MigrateAsync();

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        var wallet = Guid.NewGuid();
        var capability = Guid.NewGuid();
        var riskDecision = Guid.NewGuid();
        var counter = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var source = new SourceStampId(Guid.NewGuid());
        var posting = new PostingId(Guid.NewGuid());
        var lot = new CreditLotId(Guid.NewGuid());
        await SeedAsync(connection, wallet, capability, riskDecision, counter, actor, tenant);

        var gateway = new PostgreSqlHardCoinFundingGateway(context);
        var observed = gateway.Observe(new PersistedHardCoinFundingObservation(
            new ObserveHardCoinTopUpCommand(
                source,
                new WalletId(wallet),
                new ProviderMonetaryLeg("stripe", "test", "platform", "pi_gateway", "principal"),
                "provider-observation",
                100,
                Now),
            actor,
            tenant,
            new PolicyVersion(1)));
        observed.State.Should().Be(SourceConfirmationState.Observed);
        observed.Amount.Should().Be(new CoinAmount(CurrencyCode.HardCoin, 100));

        await ExecuteAsync(connection, "SET ROLE gameguild_economy_writer;");
        await ScalarAsync<bool>(connection, $"""
            SELECT economy_private.reserve_risk_counter_v1(
                '{Guid.NewGuid()}', '{riskDecision}', '{counter}', 1, 100, '{Now.AddMinutes(1):O}');
            """);
        await ExecuteAsync(connection, "RESET ROLE;");

        var idempotencyKey = new IdempotencyKey("funding-gateway-confirmation");
        var authority = new RegisteredPostingAuthority(
            capability,
            actor,
            tenant,
            riskDecision,
            "funding-gateway-confirmation",
            1);
        var confirmation = new ConfirmObservedTopUpCommand(
            posting,
            idempotencyKey,
            source,
            lot,
            new ReserveVersion(1),
            new PolicyVersion(1),
            "provider-confirmation",
            Now.AddMinutes(1),
            FundingAuthorizationFixture.Create(
                PostingTemplateKind.ConfirmedTopUpMint,
                idempotencyKey,
                new WalletId(wallet),
                new CoinAmount(CurrencyCode.HardCoin, 100),
                [source],
                Now.AddMinutes(1)));

        var receipt = gateway.Confirm(new PersistedHardCoinFundingConfirmation(confirmation, authority));
        receipt.PostingId.Should().Be(posting);
        receipt.IsDuplicate.Should().BeFalse();
        receipt.JournalSequence.Should().BePositive();

        var replay = gateway.Confirm(new PersistedHardCoinFundingConfirmation(confirmation, authority));
        replay.IsDuplicate.Should().BeTrue();
        replay.JournalSequence.Should().Be(receipt.JournalSequence);
        var durableReceipt = new CapabilityAuthorizationReceipt(
            Guid.NewGuid(), tenant, actor, "top-up", "USA",
            EconomyValueMovementCapability.ConfirmHardCoinFunding,
            authority.RiskOperationFingerprint, 1, 1, riskDecision, 0,
            "provider", "destination", [], ["evidence"],
            Now, Now.AddMinutes(5), "receipt", "key", "signature");
        var durableReplay = gateway.ConfirmDurable(new PersistedDurableHardCoinFundingConfirmation(
            posting,
            idempotencyKey,
            source,
            lot,
            "provider-confirmation",
            Now.AddMinutes(1),
            durableReceipt,
            authority));
        durableReplay.IsDuplicate.Should().BeTrue();
        durableReplay.JournalSequence.Should().Be(receipt.JournalSequence);
        (await ScalarAsync<long>(connection, $"SELECT count(*) FROM public.economy_credit_lots WHERE \"Id\" = '{lot.Value}';"))
            .Should().Be(1);
        (await ScalarAsync<long>(connection, $"SELECT \"PurchasedHard\" FROM public.economy_wallet_balance_projections WHERE \"WalletId\" = '{wallet}';"))
            .Should().Be(100);
        (await ScalarAsync<long>(connection, $"""
            SELECT count(*)
            FROM public.economy_funding_claims
            WHERE "SourceStampId" = '{source.Value}'
              AND "State" = 2
              AND "PostingGroupId" = '{posting.Value}'
              AND "RootCreditLotId" = '{lot.Value}';
            """))
            .Should().Be(1);
        (await ScalarAsync<int>(connection, $"SELECT \"State\" FROM public.economy_source_stamps WHERE \"Id\" = '{source.Value}';"))
            .Should().Be(1, "the immutable provider source is evidenced by an append-only confirmation event, not a mutable source update");
        (await ScalarAsync<long>(connection, $"""
            SELECT count(*)
            FROM public.economy_source_stamp_events
            WHERE "SourceStampId" = '{source.Value}'
              AND "Sequence" = 2
              AND "State" = 2;
            """))
            .Should().Be(1);
    }

    [Fact]
    public void ConfirmDurable_RejectsNullOrIncompleteAuthorityBeforeReadingPersistence()
    {
        using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options);
        var gateway = new PostgreSqlHardCoinFundingGateway(context);
        var request = DurableRequest();

        FluentActions.Invoking(() => gateway.ConfirmDurable(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => gateway.ConfirmDurable(request with { Receipt = null! }))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => gateway.ConfirmDurable(request with { Authority = null! }))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => gateway.ConfirmDurable(request with { Evidence = "" }))
            .Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void ConfirmDurable_RejectsEveryReceiptAuthorityMismatch(int mismatch)
    {
        using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options);
        var gateway = new PostgreSqlHardCoinFundingGateway(context);
        var request = DurableRequest();
        var changedReceipt = mismatch switch
        {
            0 => request.Receipt with { Capability = EconomyValueMovementCapability.Transfer },
            1 => request.Receipt with { ActorId = Guid.NewGuid() },
            2 => request.Receipt with { TenantId = Guid.NewGuid() },
            3 => request.Receipt with { RiskDecisionId = Guid.NewGuid() },
            4 => request.Receipt with { OperationFingerprint = "other" },
            5 => request.Receipt with { IssuedAt = request.ConfirmedAt.AddTicks(1) },
            _ => request.Receipt with { ExpiresAt = request.ConfirmedAt }
        };

        FluentActions.Invoking(() => gateway.ConfirmDurable(request with { Receipt = changedReceipt }))
            .Should().Throw<RegisteredPostingRejectedException>()
            .WithMessage("*does not authorize*");
    }

    [Fact]
    public void ConfirmDurable_FailsClosedWhenObservedSourceOrFundingClaimIsMissing()
    {
        var request = DurableRequest();
        using var missingSourceContext = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options);
        FluentActions.Invoking(() =>
                new PostgreSqlHardCoinFundingGateway(missingSourceContext).ConfirmDurable(request))
            .Should().Throw<RegisteredPostingRejectedException>()
            .WithMessage("*source was not found*");

        using var missingClaimContext = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options);
        missingClaimContext.Set<EconomySourceStampRow>().Add(new EconomySourceStampRow
        {
            Id = request.SourceId.Value,
            SourceKind = "provider",
            InternalSourceId = "top-up",
            SourceLegId = "capture",
            EvidenceHash = "evidence",
            Provenance = ProvenanceKind.PurchasedHard,
            State = SourceConfirmationState.Observed,
            ActorId = request.Authority.ActorId,
            TenantId = request.Authority.TenantId,
            PolicyVersion = 1,
            AuthoritativeUnits = 100,
            ObservedAt = Now
        });
        missingClaimContext.SaveChanges();

        FluentActions.Invoking(() =>
                new PostgreSqlHardCoinFundingGateway(missingClaimContext).ConfirmDurable(request))
            .Should().Throw<RegisteredPostingRejectedException>()
            .WithMessage("*funding claim was not found*");
    }

    private static Task SeedAsync(
        NpgsqlConnection connection,
        Guid wallet,
        Guid capability,
        Guid riskDecision,
        Guid counter,
        Guid actor,
        Guid tenant) =>
        ExecuteAsync(connection, $"""
            INSERT INTO public.economy_wallets ("Id", "OwnerId", "TenantId", "State", "CreatedAt")
            VALUES ('{wallet}', '{actor}', '{tenant}', 1, '{Now.AddMinutes(-2):O}');
            INSERT INTO public.economy_accounts ("Id", "WalletId", "Code", "Currency", "Provenance", "CreatedAt") VALUES
                ('{Guid.NewGuid()}', NULL, 1, 1, NULL, '{Now.AddMinutes(-2):O}'),
                ('{Guid.NewGuid()}', '{wallet}', 2, 1, 1, '{Now.AddMinutes(-2):O}');
            INSERT INTO public.economy_registered_capabilities ("Id", "Name", "AllowedTemplateKinds", "IsEnabled", "CreatedAt", "RevokedAt")
            VALUES ('{capability}', 'funding-gateway', '[1]'::jsonb, true, '{Now.AddMinutes(-2):O}', NULL);
            INSERT INTO public.economy_risk_counters (
                "Id", "Dimension", "SubjectHash", "Operation", "Currency", "WindowStartedAt", "WindowEndsAt",
                "CounterVersion", "MaxUnits", "UsedUnits", "UpdatedAt")
            VALUES ('{counter}', 1, 'funding-gateway', 1, 1, '{Now.AddHours(-1):O}', '{Now.AddHours(1):O}', 1, 1_000, 0, '{Now.AddHours(-1):O}');
            INSERT INTO public.economy_risk_decisions (
                "Id", "Outcome", "OperationFingerprint", "ActorHash", "TemplateKind", "SourceWalletId", "DestinationWalletId",
                "Currency", "AmountUnits", "CurrencyLegs", "SourceRoots", "ProviderReferenceHash", "PolicyVersion",
                "ReserveVersion", "ReserveAuthorizationEpoch", "FeatureVersion", "KillSwitchEpoch", "CounterVersion", "EntityGraphVersion",
                "EntityGraphEvidenceHash", "ReasonCodes", "IssuedAt", "ExpiresAt")
            VALUES ('{riskDecision}', 1, 'funding-gateway-confirmation', 'actor-hash', 1, '{wallet}', '{wallet}',
                1, 100, '[]', '[]', 'provider-hash', 1, 1, 1, 1, 0, 1, 0, 'graph-hash', '[]', '{Now.AddMinutes(-1):O}', '{Now.AddMinutes(5):O}');
            """);

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    private static PersistedDurableHardCoinFundingConfirmation DurableRequest()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var riskDecisionId = Guid.NewGuid();
        var confirmedAt = Now.AddMinutes(1);
        const string fingerprint = "durable-funding";
        var receipt = new CapabilityAuthorizationReceipt(
            Guid.NewGuid(), tenantId, actorId, "top-up", "USA",
            EconomyValueMovementCapability.ConfirmHardCoinFunding,
            fingerprint, 1, 1, riskDecisionId, 0,
            "provider", "destination", [], ["evidence"],
            Now, confirmedAt.AddMinutes(1), "receipt", "key", "signature");
        var authority = new RegisteredPostingAuthority(
            Guid.NewGuid(), actorId, tenantId, riskDecisionId, fingerprint, 1);
        return new PersistedDurableHardCoinFundingConfirmation(
            PostingId.New(),
            new IdempotencyKey("durable-funding"),
            SourceStampId.New(),
            CreditLotId.New(),
            "evidence",
            confirmedAt,
            receipt,
            authority);
    }

}
