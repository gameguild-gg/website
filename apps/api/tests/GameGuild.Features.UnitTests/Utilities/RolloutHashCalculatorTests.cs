using FluentAssertions;
using Xunit;

namespace GameGuild.Features.UnitTests.Utilities;

public class RolloutHashCalculatorTests
{
    [Fact]
    public void IsInRollout_ShouldReturnTrue_WhenPercentageIs100()
    {
        RolloutHashCalculator.IsInRollout("user-1", 100).Should().BeTrue();
    }

    [Fact]
    public void IsInRollout_ShouldReturnFalse_WhenPercentageIs0()
    {
        RolloutHashCalculator.IsInRollout("user-1", 0).Should().BeFalse();
    }

    [Fact]
    public void IsInRollout_ShouldBeDeterministic()
    {
        var first = RolloutHashCalculator.IsInRollout("user-42", 50);
        var second = RolloutHashCalculator.IsInRollout("user-42", 50);

        first.Should().Be(second);
    }

    [Fact]
    public void IsInRollout_ShouldUseSaltForDifferentBucketing()
    {
        var bucket1 = RolloutHashCalculator.GetBucketValue("user-1", "salt-a");
        var bucket2 = RolloutHashCalculator.GetBucketValue("user-1", "salt-b");

        // Different salt should produce different bucket (extremely unlikely to collide)
        bucket1.Should().NotBe(bucket2);
    }

    [Fact]
    public void IsInRollout_ShouldUseDefaultSalt_WhenSaltIsNull()
    {
        var result1 = RolloutHashCalculator.IsInRollout("user-1", 50, null);
        var result2 = RolloutHashCalculator.IsInRollout("user-1", 50);

        result1.Should().Be(result2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsInRollout_ShouldThrow_WhenIdentifierIsNullOrEmpty(string? identifier)
    {
        var act = () => RolloutHashCalculator.IsInRollout(identifier!, 50);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateIdentifier_ShouldReturnTenantId_WhenAvailable()
    {
        var tenantId = Guid.NewGuid();
        var context = new FeatureContext { TenantId = tenantId, UserId = Guid.NewGuid() };

        var identifier = RolloutHashCalculator.CreateIdentifier(context);

        identifier.Should().Be(tenantId.ToString());
    }

    [Fact]
    public void CreateIdentifier_ShouldReturnUserId_WhenNoTenantId()
    {
        var userId = Guid.NewGuid();
        var context = new FeatureContext { TenantId = null, UserId = userId };

        var identifier = RolloutHashCalculator.CreateIdentifier(context);

        identifier.Should().Be(userId.ToString());
    }

    [Fact]
    public void CreateIdentifier_ShouldReturnIpAddress_WhenNoTenantOrUser()
    {
        var context = new FeatureContext { TenantId = null, UserId = null, IpAddress = "192.168.1.1" };

        var identifier = RolloutHashCalculator.CreateIdentifier(context);

        identifier.Should().Be("192.168.1.1");
    }

    [Fact]
    public void CreateIdentifier_ShouldReturnAnonymous_WhenNoContext()
    {
        var context = new FeatureContext { TenantId = null, UserId = null, IpAddress = null };

        var identifier = RolloutHashCalculator.CreateIdentifier(context);

        identifier.Should().Be(FeatureFlagConstants.AnonymousIdentifier);
    }

    [Fact]
    public void CreateIdentifier_ShouldThrow_WhenContextIsNull()
    {
        var act = () => RolloutHashCalculator.CreateIdentifier(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(50, true)]
    [InlineData(100, true)]
    [InlineData(-1, false)]
    [InlineData(101, false)]
    public void IsValidPercentage_ShouldValidateCorrectly(int percentage, bool expected)
    {
        RolloutHashCalculator.IsValidPercentage(percentage).Should().Be(expected);
    }

    [Fact]
    public void GetBucketValue_ShouldReturnValueBetween0And99()
    {
        var bucket = RolloutHashCalculator.GetBucketValue("test-user");

        bucket.Should().BeInRange(0u, 99u);
    }

    [Fact]
    public void GetBucketValue_ShouldBeDeterministic()
    {
        var first = RolloutHashCalculator.GetBucketValue("test-user");
        var second = RolloutHashCalculator.GetBucketValue("test-user");

        first.Should().Be(second);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetBucketValue_ShouldThrow_WhenIdentifierIsInvalid(string? identifier)
    {
        var act = () => RolloutHashCalculator.GetBucketValue(identifier!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IsInRollout_ShouldDistributeReasonablyAcrossUsers()
    {
        var inRollout = 0;
        const int totalUsers = 1000;
        const int percentage = 50;

        for (var i = 0; i < totalUsers; i++)
        {
            if (RolloutHashCalculator.IsInRollout($"user-{i}", percentage))
            {
                inRollout++;
            }
        }

        // Should be roughly 50% (within 10% tolerance)
        var actualPercentage = (double)inRollout / totalUsers * 100;
        actualPercentage.Should().BeInRange(35, 65);
    }
}
