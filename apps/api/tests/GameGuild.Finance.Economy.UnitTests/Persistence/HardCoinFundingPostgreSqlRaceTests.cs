using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GameGuild.Finance.Economy.UnitTests.Persistence;

public sealed class HardCoinFundingPostgreSqlRaceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset ObservedAt = DateTimeOffset.Parse("2026-07-18T12:00:00Z");
    private EconomyPostgreSqlTestDatabase? _database;

    public async Task InitializeAsync()
    {
        _database = await EconomyPostgreSqlTestDatabase.CreateAsync("funding_races");
        await ResetSchemaAsync();
    }

    public async Task DisposeAsync()
    {
        if (_database is not null)
            await _database.DisposeAsync();
    }

    [Fact]
    public async Task ConfirmationVersusFailure_HasOneTerminalWinnerAndAtMostOneMintRoot()
    {
        await ResetSchemaAsync();
        var fixture = await SeedObservedClaimAsync("confirmation-race");
        await using var confirmationContext = CreateContext();
        await using var failureContext = CreateContext();
        var confirmation = await confirmationContext.Set<EconomyFundingClaimRow>()
            .SingleAsync(row => row.SourceStampId == fixture.SourceId);
        var failure = await failureContext.Set<EconomyFundingClaimRow>()
            .SingleAsync(row => row.SourceStampId == fixture.SourceId);

        StageConfirmation(confirmationContext, confirmation, fixture);
        StageFailure(failureContext, failure);

        var outcomes = await Task.WhenAll(
            CaptureAsync(() => confirmationContext.SaveChangesAsync()),
            CaptureAsync(() => failureContext.SaveChangesAsync()));
        var unexpectedFailures = outcomes
            .Where(outcome => outcome is not null && !IsSerializedLoser(outcome))
            .Select(outcome => DescribeException(outcome!))
            .ToArray();

        outcomes.Count(outcome => outcome is null).Should().Be(1);
        outcomes.Count(IsSerializedLoser).Should().Be(
            1,
            "the database must roll back the losing transaction through optimistic concurrency, the immutable event sequence, or transaction serialization; unexpected failures: {0}",
            string.Join(" | ", unexpectedFailures));

        await using var observer = CreateContext();
        var persisted = await observer.Set<EconomyFundingClaimRow>()
            .AsNoTracking()
            .SingleAsync(row => row.SourceStampId == fixture.SourceId);
        persisted.State.Should().BeOneOf(SourceConfirmationState.Confirmed, SourceConfirmationState.Failed);
        persisted.Version.Should().Be(2);

        var postings = await observer.Set<EconomyPostingGroupRow>()
            .CountAsync(row => row.SourceStampId == fixture.SourceId);
        var roots = await observer.Set<EconomyCreditLotRow>()
            .CountAsync(row => row.RootSourceStampId == fixture.SourceId);
        var terminalEvents = await observer.Set<EconomySourceStampEventRow>()
            .Where(row => row.SourceStampId == fixture.SourceId && row.Sequence == 2)
            .ToArrayAsync();
        terminalEvents.Should().ContainSingle().Which.State.Should().Be(persisted.State);
        if (persisted.State == SourceConfirmationState.Confirmed)
        {
            postings.Should().Be(1);
            roots.Should().Be(1);
        }
        else
        {
            postings.Should().Be(0);
            roots.Should().Be(0);
        }
    }

    [Fact]
    public async Task ProviderLegAndCumulativeReversal_AreEnforcedByPostgreSql()
    {
        await ResetSchemaAsync();
        var first = await SeedObservedClaimAsync("provider-leg");
        await using var context = CreateContext();
        var duplicateSource = Guid.NewGuid();
        var duplicateWallet = Guid.NewGuid();
        context.Add(Wallet(duplicateWallet));
        context.Add(Source(duplicateSource));
        context.Add(Claim(duplicateSource, duplicateWallet, first.ProviderMonetaryLeg));

        var duplicate = () => context.SaveChangesAsync();
        (await duplicate.Should().ThrowAsync<DbUpdateException>())
            .Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);

        await ResetSchemaAsync();
        var bounds = await SeedObservedClaimAsync("reversal-bounds");
        await using var boundsContext = CreateContext();
        var claim = await boundsContext.Set<EconomyFundingClaimRow>()
            .SingleAsync(row => row.SourceStampId == bounds.SourceId);
        claim.CumulativeProviderReversalUnits = claim.AuthoritativeUsdMinorUnits + 1;

        var invalidCumulative = () => boundsContext.SaveChangesAsync();
        (await invalidCumulative.Should().ThrowAsync<DbUpdateException>())
            .Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
    }

    private async Task<FundingFixture> SeedObservedClaimAsync(string providerLeg)
    {
        await using var context = CreateContext();
        var sourceId = Guid.NewGuid();
        var walletId = Guid.NewGuid();
        context.Add(Wallet(walletId));
        context.Add(Source(sourceId));
        context.Add(Claim(sourceId, walletId, providerLeg));
        await context.SaveChangesAsync();
        return new FundingFixture(sourceId, walletId, providerLeg);
    }

    private static void StageConfirmation(
        FundingRaceDbContext context,
        EconomyFundingClaimRow claim,
        FundingFixture fixture)
    {
        var confirmedAt = ObservedAt.AddMinutes(1);
        var postingId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        claim.State = SourceConfirmationState.Confirmed;
        claim.ConfirmedAt = confirmedAt;
        claim.StateChangedAt = confirmedAt;
        claim.PostingGroupId = postingId;
        claim.RootCreditLotId = lotId;
        claim.Version++;
        context.Add(Event(fixture.SourceId, SourceConfirmationState.Confirmed, confirmedAt));
        context.Add(new EconomyPostingGroupRow
        {
            Id = postingId,
            IdempotencyKey = $"confirm:{fixture.SourceId:N}",
            TemplateKind = PostingTemplateKind.ConfirmedTopUpMint,
            TemplateVersion = 1,
            Authority = PostingAuthority.ProviderConfirmation,
            Status = PostingStatus.Accepted,
            CapabilityId = Guid.NewGuid(),
            ActorId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            RiskDecisionId = Guid.NewGuid(),
            PolicyVersion = 1,
            ReserveVersion = 1,
            ReserveAuthorizationEpoch = 1,
            SourceStampId = fixture.SourceId,
            RecordedAt = confirmedAt
        });
        context.Add(new EconomyCreditLotRow
        {
            Id = lotId,
            WalletId = fixture.WalletId,
            RootSourceStampId = fixture.SourceId,
            Currency = CurrencyCode.HardCoin,
            AmountUnits = 100,
            Provenance = ProvenanceKind.PurchasedHard,
            CreditedAt = confirmedAt,
            ConfirmedAt = confirmedAt,
            OriginalMaturesAt = confirmedAt,
            CashOutEligible = false,
            JournalSequence = 1,
            State = CreditLotState.Active,
            ReversalEpoch = 0
        });
    }

    private static void StageFailure(FundingRaceDbContext context, EconomyFundingClaimRow claim)
    {
        var failedAt = ObservedAt.AddMinutes(2);
        claim.State = SourceConfirmationState.Failed;
        claim.StateChangedAt = failedAt;
        claim.Version++;
        context.Add(Event(claim.SourceStampId, SourceConfirmationState.Failed, failedAt));
    }

    private static async Task<Exception?> CaptureAsync(Func<Task<int>> action)
    {
        try
        {
            await action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static bool IsSerializedLoser(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbUpdateConcurrencyException)
                return true;

            if (current is PostgresException postgresException && postgresException.SqlState is
                    PostgresErrorCodes.UniqueViolation or
                    PostgresErrorCodes.DeadlockDetected or
                    PostgresErrorCodes.SerializationFailure)
                return true;
        }

        return false;
    }

    private static string DescribeException(Exception exception)
    {
        var descriptions = new List<string>();
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            descriptions.Add(current is PostgresException postgresException
                ? $"{current.GetType().Name}(SqlState={postgresException.SqlState})"
                : current.GetType().Name);
        }

        return string.Join(" -> ", descriptions);
    }

    private async Task ResetSchemaAsync()
    {
        await (_database ?? throw new InvalidOperationException("The PostgreSQL fixture was not initialized.")).ResetAsync();
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
    }

    private FundingRaceDbContext CreateContext() => new(
        new DbContextOptionsBuilder<FundingRaceDbContext>()
            .UseNpgsql((_database ?? throw new InvalidOperationException("The PostgreSQL fixture was not initialized.")).ConnectionString)
            .Options);

    private static EconomyWalletRow Wallet(Guid id) => new()
    {
        Id = id,
        OwnerId = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        State = WalletLifecycleState.Active,
        CreatedAt = ObservedAt
    };

    private static EconomySourceStampRow Source(Guid id) => new()
    {
        Id = id,
        SourceKind = "provider_top_up",
        InternalSourceId = id.ToString("N"),
        SourceLegId = "principal",
        Provider = "stripe",
        ProviderReference = id.ToString("N"),
        EvidenceHash = new string('a', 64),
        Provenance = ProvenanceKind.PurchasedHard,
        State = SourceConfirmationState.Observed,
        ActorId = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        PolicyVersion = 1,
        AuthoritativeUnits = 100,
        ObservedAt = ObservedAt
    };

    private static EconomyFundingClaimRow Claim(Guid sourceId, Guid walletId, string providerLeg) => new()
    {
        SourceStampId = sourceId,
        WalletId = walletId,
        Provider = "stripe",
        Environment = "test",
        ConnectedAccount = "platform",
        ProviderObject = "pi_123",
        ProviderMonetaryLeg = providerLeg,
        AuthoritativeUsdMinorUnits = 100,
        State = SourceConfirmationState.Observed,
        ObservedAt = ObservedAt,
        StateChangedAt = ObservedAt,
        Version = 1
    };

    private static EconomySourceStampEventRow Event(
        Guid sourceId,
        SourceConfirmationState state,
        DateTimeOffset occurredAt) => new()
    {
        Id = Guid.NewGuid(),
        SourceStampId = sourceId,
        Sequence = 2,
        State = state,
        EvidenceHash = new string(state == SourceConfirmationState.Confirmed ? 'b' : 'c', 64),
        OccurredAt = occurredAt
    };

    private sealed record FundingFixture(Guid SourceId, Guid WalletId, string ProviderMonetaryLeg);

    private sealed class FundingRaceDbContext(DbContextOptions<FundingRaceDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            new EconomyModelConfiguration().Configure(modelBuilder);
    }
}
