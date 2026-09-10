using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GameGuild.Finance.Economy.UnitTests.Funding;

public sealed class PostgreSqlAdRewardIssuanceGatewayTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 19, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task IssuePersistsSoftRewardAndReplaysIdempotently()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("ad_reward_issuance");
        await using var context = CreateContext(database.ConnectionString);
        var request = Request();
        await SeedAsync(database.ConnectionString, request);
        var gateway = new PostgreSqlAdRewardIssuanceGateway(context);

        var receipt = gateway.Issue(request);
        var replay = gateway.Issue(request);

        receipt.PostingId.Should().Be(request.PostingId);
        receipt.JournalSequence.Should().BePositive();
        receipt.JournalHash.Should().HaveLength(64);
        receipt.IsDuplicate.Should().BeFalse();
        replay.PostingId.Should().Be(receipt.PostingId);
        replay.JournalSequence.Should().Be(receipt.JournalSequence);
        replay.JournalHash.Should().Be(receipt.JournalHash);
        replay.IsDuplicate.Should().BeTrue();
        (await context.Set<EconomyCreditLotRow>().AsNoTracking()
            .SingleAsync(row => row.Id == request.OutputLotId.Value)).AmountUnits.Should().Be(request.SoftUnits);
        (await context.Set<EconomySourceStampRow>().AsNoTracking()
            .SingleAsync(row => row.Id == request.SourceStampId.Value)).Provider.Should().Be("ad-network");
    }

    [Fact]
    public async Task DatabaseFailuresAreTranslatedToPostingRejections()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("ad_reward_rejection");
        await using var context = CreateContext(database.ConnectionString);
        var gateway = new PostgreSqlAdRewardIssuanceGateway(context);

        FluentActions.Invoking(() => gateway.Issue(Request()))
            .Should().Throw<RegisteredPostingRejectedException>()
            .WithMessage("*ad reward writer rejected*")
            .WithInnerException<NpgsqlException>();
    }

    [Fact]
    public void BoundaryValidationAndConstructionFailClosed()
    {
        Action nullContext = () => new PostgreSqlAdRewardIssuanceGateway(null!);
        Action nonRelational = () => new PostgreSqlAdRewardIssuanceGateway(new StubApplicationDbContext());
        nullContext.Should().Throw<ArgumentNullException>();
        nonRelational.Should().Throw<InvalidOperationException>().WithMessage("*relational DbContext*");

        using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql("Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=1")
                .Options);
        var gateway = new PostgreSqlAdRewardIssuanceGateway(context);
        var valid = Request();
        FluentActions.Invoking(() => gateway.Issue(null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => gateway.Issue(valid with { Authority = null! })).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => gateway.Issue(valid with { SoftUnits = 0 })).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => gateway.Issue(valid with { Network = " " })).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => gateway.Issue(valid with { ProviderEventReference = " " })).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => gateway.Issue(valid with { EvidenceHash = " " })).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => gateway.Issue(valid with { CapabilityReceiptHash = " " })).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => gateway.Issue(valid with { CapabilityReceiptHash = new string('a', 129) }))
            .Should().Throw<ArgumentException>().WithMessage("*cannot exceed 128*");
    }

    private static PersistedAdRewardIssuanceRequest Request()
    {
        var actor = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var risk = Guid.NewGuid();
        return new PersistedAdRewardIssuanceRequest(
            new RegisteredPostingAuthority(Guid.NewGuid(), actor, tenant, risk, "ad-reward-operation", 1),
            PostingId.New(),
            new IdempotencyKey($"ad-reward-{Guid.NewGuid():N}"),
            SourceStampId.New(),
            CreditLotId.New(),
            WalletId.New(),
            25,
            new PolicyVersion(1),
            new ReserveVersion(1),
            " ad-network ",
            " provider-event ",
            " evidence-hash ",
            Now,
            " receipt-hash ");
    }

    private static async Task SeedAsync(string connectionString, PersistedAdRewardIssuanceRequest request)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var counter = Guid.NewGuid();
        var receipt = Guid.NewGuid();
        await using var command = new NpgsqlCommand($"""
            INSERT INTO public.economy_wallets ("Id", "OwnerId", "TenantId", "State", "CreatedAt")
            VALUES ('{request.WalletId.Value}', '{request.Authority.ActorId}', '{request.Authority.TenantId}', 1, '{Now.AddMinutes(-10):O}');
            INSERT INTO public.economy_accounts ("Id", "WalletId", "Code", "Currency", "Provenance", "CreatedAt") VALUES
                ('{Guid.NewGuid()}', NULL, 6, 2, NULL, '{Now.AddMinutes(-10):O}'),
                ('{Guid.NewGuid()}', '{request.WalletId.Value}', 4, 2, 4, '{Now.AddMinutes(-10):O}');
            INSERT INTO public.economy_registered_capabilities
                ("Id", "Name", "AllowedTemplateKinds", "IsEnabled", "CreatedAt", "RevokedAt")
            VALUES ('{request.Authority.CapabilityId}', 'ad-reward-writer', '[21]'::jsonb, true, '{Now.AddMinutes(-10):O}', NULL);
            INSERT INTO public.economy_risk_counters
                ("Id", "Dimension", "SubjectHash", "Operation", "Currency", "WindowStartedAt", "WindowEndsAt",
                 "CounterVersion", "MaxUnits", "UsedUnits", "UpdatedAt")
            VALUES ('{counter}', 1, 'ad-reward', 1, 2, '{Now.AddHours(-1):O}', '{Now.AddHours(1):O}',
                1, 1000, 0, '{Now.AddMinutes(-10):O}');
            INSERT INTO public.economy_risk_decisions
                ("Id", "Outcome", "OperationFingerprint", "ActorHash", "TemplateKind", "SourceWalletId",
                 "DestinationWalletId", "Currency", "AmountUnits", "CurrencyLegs", "SourceRoots",
                 "ProviderReferenceHash", "PolicyVersion", "ReserveVersion", "ReserveAuthorizationEpoch",
                 "FeatureVersion", "KillSwitchEpoch", "CounterVersion", "EntityGraphVersion",
                 "EntityGraphEvidenceHash", "ReasonCodes", "IssuedAt", "ExpiresAt")
            VALUES ('{request.Authority.RiskDecisionId}', 1, '{request.Authority.RiskOperationFingerprint}', 'actor', 21,
                '{request.WalletId.Value}', '{request.WalletId.Value}', 2, {request.SoftUnits}, '[]', '[]', 'provider',
                1, 1, 1, 1, 0, 1, 1, 'graph', '[]', '{Now.AddMinutes(-5):O}', '{Now.AddMinutes(10):O}');
            INSERT INTO public.economy_risk_counter_reservations
                ("Id", "RiskDecisionId", "RiskCounterId", "AmountUnits", "ReservedAt", "ExpiresAt",
                 "InputFingerprint", "ReservationGroupId", "Status")
            VALUES ('{Guid.NewGuid()}', '{request.Authority.RiskDecisionId}', '{counter}', {request.SoftUnits},
                '{Now.AddMinutes(-4):O}', '{Now.AddMinutes(10):O}', 'ad-reservation', '{Guid.NewGuid()}', 1);
            INSERT INTO public.economy_capability_receipts
                ("Id", "TenantId", "ActorId", "SubjectReference", "JurisdictionCode", "Capability",
                 "OperationFingerprint", "PolicyVersion", "ReserveVersion", "RiskDecisionId", "KillSwitchEpoch",
                 "ProviderHash", "DestinationHash", "SourceRootHashes", "EvidenceHashes", "IssuedAt", "ExpiresAt",
                 "ReceiptHash", "KeyId", "Signature")
            VALUES ('{receipt}', '{request.Authority.TenantId}', '{request.Authority.ActorId}', 'ad-subject', 'BR', 5,
                '{request.Authority.RiskOperationFingerprint}', 1, 1, '{request.Authority.RiskDecisionId}', 0,
                'provider', 'destination', '[]', '["evidence"]', '{Now.AddMinutes(-1):O}', '{Now.AddMinutes(5):O}',
                'receipt-hash', 'key', 'signature');
            INSERT INTO public.economy_capability_receipt_consumptions
                ("Id", "ReceiptId", "TenantId", "ActorId", "OperationFingerprint", "KillSwitchEpoch", "ConsumedAt")
            VALUES ('{Guid.NewGuid()}', '{receipt}', '{request.Authority.TenantId}', '{request.Authority.ActorId}',
                '{request.Authority.RiskOperationFingerprint}', 0, '{Now:O}');
            """, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static ApplicationDbContext CreateContext(string connectionString) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connectionString).Options);

    private sealed class StubApplicationDbContext : IApplicationDbContext
    {
        public DbSet<T> Set<T>() where T : class => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
