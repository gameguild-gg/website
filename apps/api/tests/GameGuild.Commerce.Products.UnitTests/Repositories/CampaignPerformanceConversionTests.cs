using FluentAssertions;
using GameGuild.Commerce.Products;
using Xunit;

namespace GameGuild.Tests.Commerce.Products.Unit.Repositories;

/// <summary>
/// Issue #418 conversion tracking: campaign performance must count recorded validation
/// attempts (the denominator) and compute uses/attempts as the conversion rate, within
/// the same window as the usage query.
/// </summary>
public class CampaignPerformanceConversionTests
{
    [Fact]
    public async Task GetCampaignPerformance_ComputesConversionRate_FromAttemptsAndUses()
    {
        using var connection = PromoSqliteTestSupport.CreatePrivateConnection();
        var campaignCode = PromoSqliteTestSupport.SeedPromoCode(connection, promo =>
        {
            promo.Code = "SPRING";
            promo.CampaignId = "spring-launch";
            promo.DiscountPercentage = 10m;
        });

        using (var context = PromoSqliteTestSupport.CreateContext(connection))
        {
            // 4 validation attempts (denominator) ...
            var attempts = Enumerable.Range(0, 4).Select(_ => new PromoCodeValidationAttempt
            {
                Id = Guid.NewGuid(),
                Code = "SPRING",
                PromoCodeId = campaignCode.Id,
                UserId = Guid.NewGuid(),
                Outcome = PromoCodeValidationOutcome.Valid,
                AttemptedAt = SystemClock.UtcNow.AddHours(-1)
            });
            context.Set<PromoCodeValidationAttempt>().AddRange(attempts);

            // ... and 1 recorded use (numerator).
            context.Set<PromoCodeUse>().Add(new PromoCodeUse
            {
                Id = Guid.NewGuid(),
                PromoCodeId = campaignCode.Id,
                UserId = Guid.NewGuid(),
                DiscountApplied = 10m,
                Currency = "USD",
                UsedAt = SystemClock.UtcNow.AddMinutes(-30)
            });
            context.SaveChanges();
        }

        var repository = new PromoCodeRepository(
            PromoSqliteTestSupport.CreateContext(connection, ensureCreated: false));

        var performance = await repository.GetCampaignPerformanceAsync("spring-launch");

        performance.TotalUses.Should().Be(1);
        performance.TotalValidationAttempts.Should().Be(4);
        performance.ConversionRate.Should().Be(0.25m);
    }

    [Fact]
    public async Task GetCampaignPerformance_WithoutAttempts_ReportsNullConversionRate()
    {
        using var connection = PromoSqliteTestSupport.CreatePrivateConnection();
        PromoSqliteTestSupport.SeedPromoCode(connection, promo =>
        {
            promo.Code = "QUIET";
            promo.CampaignId = "quiet-campaign";
        });

        var repository = new PromoCodeRepository(
            PromoSqliteTestSupport.CreateContext(connection, ensureCreated: false));

        var performance = await repository.GetCampaignPerformanceAsync("quiet-campaign");

        performance.TotalValidationAttempts.Should().Be(0);
        performance.ConversionRate.Should().BeNull();
    }

    [Fact]
    public async Task GetCampaignPerformance_AttemptsOutsideWindow_AreExcluded()
    {
        using var connection = PromoSqliteTestSupport.CreatePrivateConnection();
        var campaignCode = PromoSqliteTestSupport.SeedPromoCode(connection, promo =>
        {
            promo.Code = "WINDOWED";
            promo.CampaignId = "windowed-campaign";
        });

        using (var context = PromoSqliteTestSupport.CreateContext(connection))
        {
            var inside = new PromoCodeValidationAttempt
            {
                Id = Guid.NewGuid(),
                Code = "WINDOWED",
                PromoCodeId = campaignCode.Id,
                Outcome = PromoCodeValidationOutcome.Valid,
                AttemptedAt = SystemClock.UtcNow.AddHours(-2)
            };
            var outside = new PromoCodeValidationAttempt
            {
                Id = Guid.NewGuid(),
                Code = "WINDOWED",
                PromoCodeId = campaignCode.Id,
                Outcome = PromoCodeValidationOutcome.Valid,
                AttemptedAt = SystemClock.UtcNow.AddDays(-10)
            };
            context.Set<PromoCodeValidationAttempt>().AddRange(inside, outside);
            context.SaveChanges();
        }

        var repository = new PromoCodeRepository(
            PromoSqliteTestSupport.CreateContext(connection, ensureCreated: false));

        var from = SystemClock.UtcNow.AddDays(-1);
        var performance = await repository.GetCampaignPerformanceAsync("windowed-campaign", from: from);

        performance.TotalValidationAttempts.Should().Be(1);
    }

    [Fact]
    public async Task GetCampaignPerformance_UnknownCodeAttempts_DoNotInflateCampaignNumbers()
    {
        using var connection = PromoSqliteTestSupport.CreatePrivateConnection();
        PromoSqliteTestSupport.SeedPromoCode(connection, promo =>
        {
            promo.Code = "KNOWN";
            promo.CampaignId = "known-campaign";
        });

        using (var context = PromoSqliteTestSupport.CreateContext(connection))
        {
            context.Set<PromoCodeValidationAttempt>().Add(new PromoCodeValidationAttempt
            {
                Id = Guid.NewGuid(),
                Code = "UNKNOWN",
                PromoCodeId = null, // not-found attempts carry no promo code id
                Outcome = PromoCodeValidationOutcome.NotFound,
                AttemptedAt = SystemClock.UtcNow.AddMinutes(-5)
            });
            context.SaveChanges();
        }

        var repository = new PromoCodeRepository(
            PromoSqliteTestSupport.CreateContext(connection, ensureCreated: false));

        var performance = await repository.GetCampaignPerformanceAsync("known-campaign");

        performance.TotalValidationAttempts.Should().Be(0);
        performance.ConversionRate.Should().BeNull();
    }

    [Fact]
    public async Task RecordValidationAttempt_PersistsNormalizedCodeAndOutcome()
    {
        using var connection = PromoSqliteTestSupport.CreatePrivateConnection();
        var context = PromoSqliteTestSupport.CreateContext(connection);
        var repository = new PromoCodeRepository(context);

        await repository.RecordValidationAttemptAsync(
            "  mixedcase  ",
            promoCodeId: null,
            userId: Guid.NewGuid(),
            outcome: PromoCodeValidationOutcome.NotFound,
            errorMessage: "Promo code not found");

        var attempt = context.Set<PromoCodeValidationAttempt>().Single();
        attempt.Code.Should().Be("MIXEDCASE");
        attempt.Outcome.Should().Be(PromoCodeValidationOutcome.NotFound);
        attempt.ErrorMessage.Should().Be("Promo code not found");
        attempt.PromoCodeId.Should().BeNull();
    }
}
