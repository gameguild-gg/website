using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;

namespace GameGuild.Finance.Economy.UnitTests.Ledger;

public sealed class RootTraceRangeTests
{
    [Fact]
    public void Take_SplitsRangeWithoutLosingRootIdentityOrEpoch()
    {
        var root = SourceStampId.New();
        var range = new RootTraceRange(root, 10, 8, 4);

        var split = range.Take(3);

        split.Selected.Should().Be(new RootTraceRange(root, 10, 3, 4));
        split.Remaining.Should().Be(new RootTraceRange(root, 13, 5, 4));
        split.Selected.EndExclusive.Should().Be(13);
    }

    [Fact]
    public void Take_EntireRangeLeavesNoRemainder()
    {
        var range = new RootTraceRange(SourceStampId.New(), 0, 5, 0);

        var split = range.Take(5);

        split.Selected.Should().Be(range);
        split.Remaining.Should().BeNull();
    }

    [Theory]
    [InlineData(-1, 1, 0)]
    [InlineData(0, 0, 0)]
    [InlineData(0, 1, -1)]
    public void Constructor_RejectsInvalidBounds(long start, long length, long epoch)
    {
        FluentActions.Invoking(() => new RootTraceRange(SourceStampId.New(), start, length, epoch))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Take_RejectsNonPositiveOrExcessiveAmount(long units)
    {
        var range = new RootTraceRange(SourceStampId.New(), 0, 5, 0);

        FluentActions.Invoking(() => range.Take(units)).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_RejectsOverflowingEnd()
    {
        FluentActions.Invoking(() => new RootTraceRange(SourceStampId.New(), long.MaxValue, 1, 0))
            .Should().Throw<OverflowException>();
    }
}
