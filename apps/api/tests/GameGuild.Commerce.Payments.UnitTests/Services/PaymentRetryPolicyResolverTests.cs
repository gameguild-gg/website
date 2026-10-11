using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.Services;

/// <summary>
///     Failure-code retry policy classification and schedule computation (issue #415):
///     hard declines are non-retriable, soft declines follow failure-code ladders, and
///     unclassified codes fall back to the configurable exponential backoff with jitter.
/// </summary>
public class PaymentRetryPolicyResolverTests
{
    private readonly PaymentRetryPolicyResolver _resolver = CreateResolver(new PaymentRetryOptions());

    private static PaymentRetryPolicyResolver CreateResolver(PaymentRetryOptions options) =>
        new(Options.Create(options));

    [Theory]
    [InlineData("expired_card")]
    [InlineData("card_expired")]
    [InlineData("incorrect_cvc")]
    [InlineData("invalid_cvc")]
    [InlineData("cvc_check_failed")]
    [InlineData("incorrect_number")]
    [InlineData("invalid_card_number")]
    [InlineData("card_not_supported")]
    [InlineData("fraudulent")]
    [InlineData("fraud_detected")]
    [InlineData("card_declined_fraud")]
    [InlineData("stolen_card")]
    public void Resolve_HardDeclines_AreNonRetriable(string failureCode)
    {
        var policy = _resolver.Resolve(failureCode);

        policy.IsRetriable.Should().BeFalse();
        policy.ScheduleKind.Should().Be(PaymentRetryScheduleKind.NonRetriable);
        policy.RetryDelays.Should().BeEmpty();
        policy.Classification.Should().Be("hard_decline");
    }

    [Theory]
    [InlineData("Expired Card")]
    [InlineData("  EXPIRED_CARD  ")]
    [InlineData("Incorrect CVC")]
    public void Resolve_NormalizesFailureCodes_BeforeClassifying(string failureCode)
    {
        var policy = _resolver.Resolve(failureCode);

        policy.IsRetriable.Should().BeFalse();
    }

    [Fact]
    public void Resolve_InsufficientFunds_FollowsTheThreeSevenFourteenDayLadder()
    {
        var policy = _resolver.Resolve("insufficient_funds");

        policy.IsRetriable.Should().BeTrue();
        policy.ScheduleKind.Should().Be(PaymentRetryScheduleKind.FailureCodeLadder);
        policy.RetryDelays.Should().BeEquivalentTo(
        [
            TimeSpan.FromDays(3),
            TimeSpan.FromDays(7),
            TimeSpan.FromDays(14)
        ]);
    }

    [Fact]
    public void Resolve_DoNotHonor_FollowsTheOneSevenDayLadder()
    {
        var policy = _resolver.Resolve("do_not_honor");

        policy.IsRetriable.Should().BeTrue();
        policy.RetryDelays.Should().BeEquivalentTo(
        [
            TimeSpan.FromDays(1),
            TimeSpan.FromDays(7)
        ]);
    }

    [Theory]
    [InlineData("processing_error")]
    [InlineData("temporarily_unavailable")]
    public void Resolve_TransientProcessingErrors_FollowTheFastLadder(string failureCode)
    {
        var policy = _resolver.Resolve(failureCode);

        policy.IsRetriable.Should().BeTrue();
        policy.RetryDelays.Should().HaveCount(4);
        policy.RetryDelays[0].Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Resolve_GenericCardDecline_IsRetriableOnTheConfiguredBackoff()
    {
        var policy = _resolver.Resolve("card_declined");

        policy.IsRetriable.Should().BeTrue();
        policy.ScheduleKind.Should().Be(PaymentRetryScheduleKind.ConfiguredExponentialBackoff);
        policy.Classification.Should().Be("card_declined");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("stripe_error")]
    [InlineData("something_new_from_the_gateway")]
    public void Resolve_UnknownCodes_FallBackToTheConfiguredBackoff(string? failureCode)
    {
        var policy = _resolver.Resolve(failureCode);

        policy.IsRetriable.Should().BeTrue();
        policy.ScheduleKind.Should().Be(PaymentRetryScheduleKind.ConfiguredExponentialBackoff);
    }

    [Fact]
    public void ComputeSchedule_NonRetriableFailure_LeavesTheQueueImmediately()
    {
        var schedule = _resolver.ComputeSchedule("expired_card", currentRetryCount: 0);

        schedule.IsRetriable.Should().BeFalse();
        schedule.NextRetryAt.Should().BeNull();
    }

    [Fact]
    public void ComputeSchedule_InsufficientFundsLadder_AdvancesThreeSevenFourteenDays()
    {
        var now = new DateTime(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc);
        var zeroJitterResolver = CreateResolver(new PaymentRetryOptions { JitterRatio = 0.0 });

        var first = zeroJitterResolver.ComputeSchedule("insufficient_funds", 0, now);
        var second = zeroJitterResolver.ComputeSchedule("insufficient_funds", 1, now);
        var third = zeroJitterResolver.ComputeSchedule("insufficient_funds", 2, now);
        var exhausted = zeroJitterResolver.ComputeSchedule("insufficient_funds", 3, now);

        first.NextRetryAt.Should().Be(now.AddDays(3));
        second.NextRetryAt.Should().Be(now.AddDays(7));
        third.NextRetryAt.Should().Be(now.AddDays(14));
        exhausted.NextRetryAt.Should().BeNull();
        exhausted.EffectiveMaxRetries.Should().Be(3); // Ladder caps the configured budget of 4.
    }

    [Fact]
    public void ComputeSchedule_LadderCapsEffectiveMaxRetriesAtTheLadderLength()
    {
        var resolver = CreateResolver(new PaymentRetryOptions { MaxRetries = 10, JitterRatio = 0.0 });

        var schedule = resolver.ComputeSchedule("insufficient_funds", 0);

        schedule.EffectiveMaxRetries.Should().Be(3);
    }

    [Fact]
    public void ComputeSchedule_ConfiguredBackoff_AppliesExponentialDelayWithJitter()
    {
        var options = new PaymentRetryOptions
        {
            MaxRetries = 4,
            BackoffBaseMinutes = 30.0,
            BackoffMultiplier = 2.0,
            JitterRatio = 0.2
        };
        var resolver = CreateResolver(options);
        var now = new DateTime(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc);

        var schedule = resolver.ComputeSchedule("card_declined", 1, now);

        schedule.IsRetriable.Should().BeTrue();
        schedule.EffectiveMaxRetries.Should().Be(4);
        // 30 * 2^1 = 60 minutes, jittered up to +20% -> [60, 72] minutes.
        schedule.NextRetryAt.Should().BeOnOrAfter(now.AddMinutes(60.0));
        schedule.NextRetryAt.Should().BeOnOrBefore(now.AddMinutes(72.0));
    }

    [Fact]
    public void ComputeSchedule_ZeroJitter_MatchesThePlainExponentialBackoff()
    {
        var options = new PaymentRetryOptions
        {
            BackoffBaseMinutes = 30.0,
            BackoffMultiplier = 2.0,
            JitterRatio = 0.0
        };
        var resolver = CreateResolver(options);
        var now = new DateTime(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc);

        var schedule = resolver.ComputeSchedule("card_declined", 1, now);

        schedule.NextRetryAt.Should().Be(now.AddMinutes(60.0));
    }

    [Fact]
    public void ComputeSchedule_ConfiguredBackoff_ExhaustedBudgetLeavesTheQueue()
    {
        var schedule = _resolver.ComputeSchedule("card_declined", currentRetryCount: 4);

        schedule.NextRetryAt.Should().BeNull();
        schedule.EffectiveMaxRetries.Should().Be(4);
    }

    [Fact]
    public void ComputeSchedule_NegativeRetryCount_Throws()
    {
        var act = () => _resolver.ComputeSchedule("card_declined", -1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
