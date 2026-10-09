using FluentAssertions;
using GameGuild.Identity.Authorization.Utilities;

namespace GameGuild.Identity.Authorization.UnitTests.Utilities;

/// <summary>
///     Tests for the platform wildcard/deny key-matching semantics shared by the
///     permission graph and impact analysis (issue #334).
/// </summary>
public class PermissionKeyMatcherTests
{
    [Theory]
    [InlineData("content:read", "content:read", true)]          // exact
    [InlineData("content:read", "CONTENT:READ", true)]          // case-insensitive
    [InlineData("*", "anything:at:all", true)]                  // universal wildcard
    [InlineData("content:*", "content:read", true)]             // resource wildcard
    [InlineData("content:*", "content:read:own", true)]         // resource wildcard is a prefix
    [InlineData("content:*", "assets:read", false)]             // other resource
    [InlineData("content:read", "content:write", false)]        // no cross-key grant
    [InlineData("", "content:read", false)]                     // empty grant
    [InlineData("content:read", "", false)]                     // empty key
    public void Covers_ImplementsPlatformWildcardSemantics(string grant, string key, bool expected)
    {
        PermissionKeyMatcher.Covers(grant, key).Should().Be(expected);
    }

    [Fact]
    public void AnyCovers_ReturnsTrueWhenAnyGrantCovers()
    {
        var grants = new[] { "assets:read", "content:*" };

        PermissionKeyMatcher.AnyCovers(grants, "content:write").Should().BeTrue();
        PermissionKeyMatcher.AnyCovers(grants, "course:manage").Should().BeFalse();
    }

    [Fact]
    public void ContainsExact_MatchesDenyKeysCaseInsensitively()
    {
        var denies = new[] { "content:read" };

        PermissionKeyMatcher.ContainsExact(denies, "CONTENT:READ").Should().BeTrue();
        PermissionKeyMatcher.ContainsExact(denies, "content:write").Should().BeFalse();
        // Denies are exact-key only: a wildcard deny must not cover a concrete key.
        PermissionKeyMatcher.ContainsExact(new[] { "content:*" }, "content:read").Should().BeFalse();
    }
}
