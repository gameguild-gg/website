using FluentAssertions;
using GameGuild.Finance.Economy.Integrations.AI;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Xunit;

namespace GameGuild.Finance.Economy.UnitTests.Integrations.AI;

public sealed class AiCreditReservationTests
{
    [Fact]
    public void Settle_ChargesActualUsageAndReleasesUnusedMaximum()
    {
        var reservation = AiCreditReservation.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "lesson-authoring", "OpenAi", "gpt-test", 100, "reserve-1", DateTimeOffset.UtcNow);

        reservation.Settle(120, 30, 35, "provider-usage-1", "settle-1", DateTimeOffset.UtcNow);

        reservation.Status.Should().Be(AiCreditReservationStatus.Settled);
        reservation.SettledSoftUnits.Should().Be(35);
        reservation.ReleasedSoftUnits.Should().Be(65);
        reservation.InputTokens.Should().Be(120);
        reservation.OutputTokens.Should().Be(30);
    }

    [Fact]
    public void Settle_RetryWithSameIdempotencyKey_DoesNotChargeTwice()
    {
        var reservation = AiCreditReservation.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "lesson-authoring", "OpenAi", "gpt-test", 80, "reserve-2", DateTimeOffset.UtcNow);
        reservation.Settle(10, 5, 20, "usage-2", "settle-2", DateTimeOffset.UtcNow);

        var act = () => reservation.Settle(10, 5, 20, "usage-2", "settle-2", DateTimeOffset.UtcNow);

        act.Should().NotThrow();
        reservation.SettledSoftUnits.Should().Be(20);
    }

    [Fact]
    public void Release_BeforeProviderConsumption_ReturnsEntireReservation()
    {
        var reservation = AiCreditReservation.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "lesson-authoring", "OpenAi", "gpt-test", 55, "reserve-3", DateTimeOffset.UtcNow);

        reservation.Release("provider failed", "release-3", DateTimeOffset.UtcNow);

        reservation.Status.Should().Be(AiCreditReservationStatus.Released);
        reservation.SettledSoftUnits.Should().Be(0);
        reservation.ReleasedSoftUnits.Should().Be(55);
    }

    [Fact]
    public void Create_RequiresExplicitActorAndTenant()
    {
        var act = () => AiCreditReservation.Create(
            Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Guid.NewGuid(),
            "lesson-authoring", "OpenAi", "gpt-test", 55, "reserve-4", DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task Settlement_ChargesOnlyTheAuthenticatedActorWallet()
    {
        await using var context = new AiCreditTestContext(
            new DbContextOptionsBuilder<AiCreditTestContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var tenantId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        SeedWallet(context, tenantId, teacherId, 100);
        SeedWallet(context, tenantId, studentId, 100);
        await context.SaveChangesAsync();
        var service = new AiCreditWalletService(
            context,
            Options.Create(new AiCreditPricingOptions
            {
                DefaultInputSoftUnitsPerMillion = 1_000_000,
                DefaultOutputSoftUnitsPerMillion = 1_000_000,
            }),
            TimeProvider.System);
        var quote = await service.QuoteAsync("lesson-authoring", "OpenAi", "test", 5, 5);
        var runId = Guid.NewGuid();

        await service.ReserveAsync(runId, tenantId, teacherId, "lesson-authoring", quote, "teacher-run");
        await service.SettleAsync(runId, 3, 2, "provider-usage", "teacher-settlement");

        var teacher = await service.GetBalanceAsync(tenantId, teacherId);
        var student = await service.GetBalanceAsync(tenantId, studentId);
        teacher.AvailableSoftUnits.Should().Be(95);
        student.AvailableSoftUnits.Should().Be(100);
    }

    [Fact]
    public async Task Quote_ReusesThePersistedRateCardForTheSameProviderModelAndService()
    {
        await using var context = new AiCreditTestContext(
            new DbContextOptionsBuilder<AiCreditTestContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var service = new AiCreditWalletService(
            context,
            Options.Create(new AiCreditPricingOptions()),
            TimeProvider.System);

        var first = await service.QuoteAsync("lesson-authoring", "OpenAi", "test", 5, 5);
        var second = await service.QuoteAsync("lesson-authoring", "OpenAi", "test", 10, 10);

        first.RateCardVersion.Should().Be(second.RateCardVersion);
        (await context.Set<AiCreditRateCard>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Reserve_IdempotencyKeyCannotBeReusedWithAnotherPriceSnapshot()
    {
        await using var context = new AiCreditTestContext(
            new DbContextOptionsBuilder<AiCreditTestContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        SeedWallet(context, tenantId, actorId, 100);
        await context.SaveChangesAsync();
        var service = new AiCreditWalletService(
            context,
            Options.Create(new AiCreditPricingOptions()),
            TimeProvider.System);
        var runId = Guid.NewGuid();
        var quote = new AiCreditQuote("rate-v1", "OpenAi", "model-a", 10, 10, 25, 100, 200);
        await service.ReserveAsync(runId, tenantId, actorId, "lesson-authoring", quote, "same-key");

        var replay = () => service.ReserveAsync(
            runId,
            tenantId,
            actorId,
            "lesson-authoring",
            quote with { MaximumSoftUnits = 26 },
            "same-key");

        await replay.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*another reservation request*");
        (await context.Set<AiCreditReservation>().CountAsync()).Should().Be(1);
    }

    private static void SeedWallet(AiCreditTestContext context, Guid tenantId, Guid actorId, long softBalance)
    {
        var walletId = Guid.NewGuid();
        context.Set<EconomyWalletRow>().Add(new EconomyWalletRow
        {
            Id = walletId,
            TenantId = tenantId,
            OwnerId = actorId,
            State = WalletLifecycleState.Active,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        context.Set<EconomyWalletBalanceProjectionRow>().Add(new EconomyWalletBalanceProjectionRow
        {
            WalletId = walletId,
            Soft = softBalance,
            AvailableSoftToSpend = softBalance,
            ProjectionHash = "test",
            RebuiltAt = DateTimeOffset.UtcNow,
        });
    }

    private sealed class AiCreditTestContext(DbContextOptions<AiCreditTestContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EconomyWalletRow>().HasKey(item => item.Id);
            modelBuilder.Entity<EconomyWalletBalanceProjectionRow>().HasKey(item => item.WalletId);
            modelBuilder.Entity<AiCreditReservation>().HasKey(item => item.Id);
            modelBuilder.Entity<AiCreditRateCard>().HasKey(item => item.Id);
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
