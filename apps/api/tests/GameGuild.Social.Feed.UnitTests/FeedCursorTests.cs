using FluentAssertions;
using Xunit;

namespace GameGuild.Social.Feed.UnitTests;

public sealed class FeedCursorTests
{
    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("eyJ0IjoiYmFkIn0=")]
    public void TryDecode_RejectsMalformedCursor(string value)
    {
        FeedCursor.TryDecode(value, out _).Should().BeFalse();
    }

    [Fact]
    public void Encode_RoundTripsTimestampAndStableId()
    {
        var cursor = new FeedCursor(
            new DateTime(2026, 9, 10, 12, 34, 56, DateTimeKind.Utc).AddTicks(1234),
            Guid.NewGuid());

        FeedCursor.TryDecode(cursor.Encode(), out var decoded).Should().BeTrue();

        decoded.Should().Be(cursor);
    }
}
