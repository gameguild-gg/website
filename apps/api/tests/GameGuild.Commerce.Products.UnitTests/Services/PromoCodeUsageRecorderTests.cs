using FluentAssertions;
using GameGuild.Commerce.Products;
using GameGuild.Tests.Commerce.Products.Unit.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameGuild.Tests.Commerce.Products.Unit.Services;

/// <summary>
/// Relational (SQLite) tests for <see cref="PromoCodeUsageRecorder"/>: atomic limit
/// enforcement under concurrency, idempotency-key replay protection, per-user limits,
/// and campaign aggregation.
/// </summary>
public class PromoCodeUsageRecorderTests : IDisposable
{
    private readonly DbConnectionKeeper _keeper = new();

    private sealed class DbConnectionKeeper : IDisposable
    {
        private readonly List<System.Data.Common.DbConnection> _connections = new();

        public System.Data.Common.DbConnection Track(System.Data.Common.DbConnection connection)
        {
            _connections.Add(connection);
            return connection;
        }

        public void Dispose()
        {
            foreach (var connection in _connections)
            {
                connection.Dispose();
            }
        }
    }

    private PromoCodeRepository CreateRepository(System.Data.Common.DbConnection connection) =>
        new(PromoSqliteTestSupport.CreateContext(connection));

    [Fact]
    public async Task RecordAsync_PersistsActualDiscount_WithIdempotencyKey()
    {
        var connection = _keeper.Track(PromoSqliteTestSupport.CreatePrivateConnection());
        var promo = PromoSqliteTestSupport.SeedPromoCode(connection, p =>
        {
            p.DiscountPercentage = 20m;
            p.MaxUses = 10;
        });

        var recorder = new PromoCodeUsageRecorder(CreateRepository(connection));

        var (status, usage) = await recorder.RecordAsync(promo, Guid.NewGuid(), 20m, "USD", "op-1");

        status.Should().Be(PromoUsageRecordingStatus.Recorded);
        usage.Should().NotBeNull();
        usage!.DiscountApplied.Should().Be(20m);
        usage.IdempotencyKey.Should().Be("op-1");

        await using var verify = PromoSqliteTestSupport.CreateContext(connection);
        var stored = await verify.Set<PromoCodeUse>().SingleAsync(u => u.PromoCodeId == promo.Id);
        stored.DiscountApplied.Should().Be(20m);
        (await verify.Set<PromoCode>().SingleAsync(p => p.Id == promo.Id)).CurrentUses.Should().Be(1);
    }

    [Fact]
    public async Task RecordAsync_ReplayedIdempotencyKey_DoesNotCountTwice()
    {
        var connection = _keeper.Track(PromoSqliteTestSupport.CreatePrivateConnection());
        var promo = PromoSqliteTestSupport.SeedPromoCode(connection, p => p.MaxUses = 1);
        var userId = Guid.NewGuid();
        var recorder = new PromoCodeUsageRecorder(CreateRepository(connection));

        var first = await recorder.RecordAsync(promo, userId, 10m, "USD", "same-key");
        var second = await recorder.RecordAsync(promo, userId, 10m, "USD", "same-key");

        first.Status.Should().Be(PromoUsageRecordingStatus.Recorded);
        second.Status.Should().Be(PromoUsageRecordingStatus.Replay);
        second.Usage!.Id.Should().Be(first.Usage!.Id, "the replay returns the original usage row");

        await using var verify = PromoSqliteTestSupport.CreateContext(connection);
        (await verify.Set<PromoCodeUse>().CountAsync(u => u.PromoCodeId == promo.Id)).Should().Be(1);
        (await verify.Set<PromoCode>().SingleAsync(p => p.Id == promo.Id)).CurrentUses.Should().Be(1);
    }

    [Fact]
    public async Task RecordAsync_MaxUsesOne_ConcurrentRedemptions_CountExactlyOnce()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"promo-race-{Guid.NewGuid():N}.db");
        var keeperConnection = _keeper.Track(PromoSqliteTestSupport.CreateFileConnection(dbPath));

        var promo = PromoSqliteTestSupport.SeedPromoCode(keeperConnection, p =>
        {
            p.Code = "RACE1";
            p.MaxUses = 1;
            p.MaxUsesPerUser = null;
        });

        var userIds = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToList();
        var tasks = userIds.Select(userId => Task.Run(async () =>
        {
            using var connection = PromoSqliteTestSupport.CreateFileConnection(dbPath);
            var repository = new PromoCodeRepository(PromoSqliteTestSupport.CreateContext(connection, ensureCreated: false));
            var recorder = new PromoCodeUsageRecorder(repository);
            try
            {
                var (status, _) = await recorder.RecordAsync(promo, userId, 5m, "USD", $"race-{userId}");
                return status;
            }
            catch (DbUpdateException)
            {
                // Lost the race on a unique constraint: counts as not-recorded.
                return PromoUsageRecordingStatus.RejectedTotalLimit;
            }
            catch (Microsoft.Data.Sqlite.SqliteException)
            {
                // SQLite lock contention beyond the retry window: the fail-closed direction.
                return PromoUsageRecordingStatus.RejectedTotalLimit;
            }
        })).ToList();

        var results = await Task.WhenAll(tasks);

        results.Count(r => r == PromoUsageRecordingStatus.Recorded).Should().Be(1, "the atomic guard admits exactly one redemption");
        results.Count(r => r == PromoUsageRecordingStatus.RejectedTotalLimit).Should().Be(3, "everyone else fails closed");

        await using var verify = PromoSqliteTestSupport.CreateContext(keeperConnection, ensureCreated: false);
        (await verify.Set<PromoCodeUse>().CountAsync(u => u.PromoCodeId == promo.Id)).Should().Be(1);
        (await verify.Set<PromoCode>().SingleAsync(p => p.Id == promo.Id)).CurrentUses.Should().Be(1);

        keeperConnection.Close();
        try { File.Delete(dbPath); } catch (IOException) { }
    }

    [Fact]
    public async Task RecordAsync_MaxUses_Exhausted_FailsClosed()
    {
        var connection = _keeper.Track(PromoSqliteTestSupport.CreatePrivateConnection());
        var promo = PromoSqliteTestSupport.SeedPromoCode(connection, p => p.MaxUses = 2);
        var recorder = new PromoCodeUsageRecorder(CreateRepository(connection));

        (await recorder.RecordAsync(promo, Guid.NewGuid(), 1m, "USD", "a")).Status.Should().Be(PromoUsageRecordingStatus.Recorded);
        (await recorder.RecordAsync(promo, Guid.NewGuid(), 1m, "USD", "b")).Status.Should().Be(PromoUsageRecordingStatus.Recorded);
        (await recorder.RecordAsync(promo, Guid.NewGuid(), 1m, "USD", "c")).Status.Should().Be(PromoUsageRecordingStatus.RejectedTotalLimit);

        await using var verify = PromoSqliteTestSupport.CreateContext(connection);
        (await verify.Set<PromoCodeUse>().CountAsync(u => u.PromoCodeId == promo.Id)).Should().Be(2, "the rejected attempt never persists a row");
    }

    [Fact]
    public async Task RecordAsync_PerUserLimit_BindsAtomically()
    {
        var connection = _keeper.Track(PromoSqliteTestSupport.CreatePrivateConnection());
        var promo = PromoSqliteTestSupport.SeedPromoCode(connection, p =>
        {
            p.MaxUses = 100;
            p.MaxUsesPerUser = 2;
        });
        var user = Guid.NewGuid();
        var recorder = new PromoCodeUsageRecorder(CreateRepository(connection));

        (await recorder.RecordAsync(promo, user, 1m, "USD", "u1")).Status.Should().Be(PromoUsageRecordingStatus.Recorded);
        (await recorder.RecordAsync(promo, user, 1m, "USD", "u2")).Status.Should().Be(PromoUsageRecordingStatus.Recorded);
        (await recorder.RecordAsync(promo, user, 1m, "USD", "u3")).Status.Should().Be(PromoUsageRecordingStatus.RejectedPerUserLimit);

        // Other users are unaffected.
        (await recorder.RecordAsync(promo, Guid.NewGuid(), 1m, "USD", "other")).Status.Should().Be(PromoUsageRecordingStatus.Recorded);

        await using var verify = PromoSqliteTestSupport.CreateContext(connection);
        var counter = await verify.Set<PromoCodeUserUsageCounter>().SingleAsync(c => c.PromoCodeId == promo.Id && c.UserId == user);
        counter.UseCount.Should().Be(2);
    }

    [Fact]
    public async Task RecordAsync_InactiveCode_FailsClosed()
    {
        var connection = _keeper.Track(PromoSqliteTestSupport.CreatePrivateConnection());
        var promo = PromoSqliteTestSupport.SeedPromoCode(connection, p => p.IsActive = false);
        var recorder = new PromoCodeUsageRecorder(CreateRepository(connection));

        (await recorder.RecordAsync(promo, Guid.NewGuid(), 1m, "USD", "x")).Status.Should().Be(PromoUsageRecordingStatus.RejectedTotalLimit);

        await using var verify = PromoSqliteTestSupport.CreateContext(connection);
        (await verify.Set<PromoCodeUse>().AnyAsync(u => u.PromoCodeId == promo.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task RecordAsync_OrderLinkage_IsPersisted()
    {
        var connection = _keeper.Track(PromoSqliteTestSupport.CreatePrivateConnection());
        var promo = PromoSqliteTestSupport.SeedPromoCode(connection);
        var recorder = new PromoCodeUsageRecorder(CreateRepository(connection));
        var orderId = Guid.NewGuid();
        var lineItemId = Guid.NewGuid();

        var (_, usage) = await recorder.RecordAsync(promo, Guid.NewGuid(), 5m, "BRL", "order-1", orderId, lineItemId);

        usage!.OrderId.Should().Be(orderId);
        usage.OrderLineItemId.Should().Be(lineItemId);
        usage.Currency.Should().Be("BRL");
    }

    [Fact]
    public async Task CampaignPerformance_AggregatesUsesDiscountsAndTopCodes()
    {
        var connection = _keeper.Track(PromoSqliteTestSupport.CreatePrivateConnection());
        await using (var seed = PromoSqliteTestSupport.CreateContext(connection))
        {
            var summerA = new PromoCode { Id = Guid.NewGuid(), Code = "SUMMER-A", Name = "A", Type = PromoCodeType.PercentageOff, DiscountPercentage = 10m, Currency = "USD", IsActive = true, CampaignId = "summer-2026", MaxUses = 10 };
            var summerB = new PromoCode { Id = Guid.NewGuid(), Code = "SUMMER-B", Name = "B", Type = PromoCodeType.PercentageOff, DiscountPercentage = 20m, Currency = "USD", IsActive = true, CampaignId = "summer-2026", MaxUses = 10 };
            var other = new PromoCode { Id = Guid.NewGuid(), Code = "OTHER", Name = "O", Type = PromoCodeType.PercentageOff, DiscountPercentage = 10m, Currency = "USD", IsActive = true, CampaignId = "winter-2026" };
            seed.Set<PromoCode>().AddRange(summerA, summerB, other);

            var user = Guid.NewGuid();
            var now = SystemClock.UtcNow;
            seed.Set<PromoCodeUse>().AddRange(
                new PromoCodeUse { Id = Guid.NewGuid(), PromoCodeId = summerA.Id, UserId = user, DiscountApplied = 10m, UsedAt = now.AddHours(-2) },
                new PromoCodeUse { Id = Guid.NewGuid(), PromoCodeId = summerA.Id, UserId = Guid.NewGuid(), DiscountApplied = 10m, UsedAt = now.AddHours(-1) },
                new PromoCodeUse { Id = Guid.NewGuid(), PromoCodeId = summerB.Id, UserId = Guid.NewGuid(), DiscountApplied = 40m, UsedAt = now.AddDays(-3) },
                new PromoCodeUse { Id = Guid.NewGuid(), PromoCodeId = other.Id, UserId = Guid.NewGuid(), DiscountApplied = 99m, UsedAt = now });
            seed.SaveChanges();
        }

        var repository = CreateRepository(connection);
        var summary = await repository.GetCampaignPerformanceAsync("summer-2026");

        summary.TotalCodes.Should().Be(2);
        summary.ActiveCodes.Should().Be(2);
        summary.TotalUses.Should().Be(3);
        summary.UniqueUsers.Should().Be(3);
        summary.TotalDiscountGiven.Should().Be(60m);
        summary.AverageDiscountPerUse.Should().Be(20m);
        summary.RedemptionRate.Should().Be(0.15m, "3 uses of a 20-use capacity");
        summary.TopCodes.First().Code.Should().Be("SUMMER-A");
        summary.TopCodes.First().Uses.Should().Be(2);

        // Windowed: only usages from the last 150 minutes (excludes the 3-day-old one).
        var windowed = await repository.GetCampaignPerformanceAsync("summer-2026", SystemClock.UtcNow.AddMinutes(-150), null);
        windowed.TotalUses.Should().Be(2);
        windowed.TotalDiscountGiven.Should().Be(20m);
    }

    public void Dispose() => _keeper.Dispose();
}
