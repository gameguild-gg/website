using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.Configuration;

public class PaymentRetryOptionsTests
{
    [Fact]
    public void Defaults_ReproduceTheLegacyHardcodedSchedule()
    {
        var options = new PaymentRetryOptions();

        options.MaxRetries.Should().Be(3);
        options.BackoffBaseMinutes.Should().Be(1.0);
        options.BackoffMultiplier.Should().Be(5.0);
    }

    [Theory]
    [InlineData(0, 1.0)]   // legacy Math.Pow(5, 0) = 1 minute
    [InlineData(1, 5.0)]   // legacy Math.Pow(5, 1) = 5 minutes
    [InlineData(2, 25.0)]  // legacy Math.Pow(5, 2) = 25 minutes
    [InlineData(3, 125.0)] // legacy Math.Pow(5, 3) = 125 minutes
    public void ComputeBackoffDelayMinutes_WithDefaults_MatchesLegacyExponentialBackoff(
        int retryCount,
        double expectedMinutes)
    {
        var options = new PaymentRetryOptions();

        options.ComputeBackoffDelayMinutes(retryCount).Should().Be(expectedMinutes);
    }

    [Fact]
    public void ComputeBackoffDelayMinutes_WithCustomSchedule_AppliesBaseAndMultiplier()
    {
        var options = new PaymentRetryOptions
        {
            MaxRetries = 5,
            BackoffBaseMinutes = 10.0,
            BackoffMultiplier = 2.0
        };

        options.ComputeBackoffDelayMinutes(0).Should().Be(10.0);
        options.ComputeBackoffDelayMinutes(1).Should().Be(20.0);
        options.ComputeBackoffDelayMinutes(2).Should().Be(40.0);
    }

    [Fact]
    public void ComputeBackoffDelayMinutes_NegativeRetryCount_Throws()
    {
        var act = () => new PaymentRetryOptions().ComputeBackoffDelayMinutes(-1);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("retryCount");
    }

    [Fact]
    public void SectionName_IsPaymentsRetry()
    {
        PaymentRetryOptions.SectionName.Should().Be("Payments:Retry");
    }

    [Fact]
    public void Validator_AcceptsDefaults()
    {
        var validator = new PaymentRetryOptionsValidator();
        var result = validator.Validate(null, new PaymentRetryOptions());

        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData(-1, 1.0, 5.0, "MaxRetries")]
    [InlineData(21, 1.0, 5.0, "MaxRetries")]
    [InlineData(3, -0.1, 5.0, "BackoffBaseMinutes")]
    [InlineData(3, 1440.1, 5.0, "BackoffBaseMinutes")]
    [InlineData(3, 1.0, 0.9, "BackoffMultiplier")]
    [InlineData(3, 1.0, 60.1, "BackoffMultiplier")]
    public void Validator_RejectsOutOfRangeValues(
        int maxRetries,
        double backoffBaseMinutes,
        double backoffMultiplier,
        string expectedFailureFragment)
    {
        var validator = new PaymentRetryOptionsValidator();
        var result = validator.Validate(null, new PaymentRetryOptions
        {
            MaxRetries = maxRetries,
            BackoffBaseMinutes = backoffBaseMinutes,
            BackoffMultiplier = backoffMultiplier
        });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(expectedFailureFragment);
    }
}
