using FluentAssertions;
using GameGuild.Identity.Provisioning.Scim;
using GameGuild.Identity.Provisioning.Scim.Filtering;
using Xunit;

namespace GameGuild.Identity.Provisioning.UnitTests.Filtering;

/// <summary>
///     Predicate compilation over the user and group projections: matching semantics
/// (case-insensitive strings, boolean eq, exact ids) and the invalidFilter contract
/// for unmapped attributes.
/// </summary>
public sealed class ScimFilterEvaluatorTests
{
    private static readonly ScimUserView Sample = new()
    {
        UserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        ExternalId = "ext-42",
        UserName = "bjensen",
        Email = "bjensen@example.com",
        DisplayName = "Barbara Jensen",
        PhoneNumber = "+15551234",
        Active = true,
        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        UpdatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)
    };

    [Theory]
    [InlineData("userName eq \"bjensen\"", true)]
    [InlineData("userName eq \"BJENSEN\"", true)]
    [InlineData("userName eq \"jsmith\"", false)]
    [InlineData("userName sw \"bjen\"", true)]
    [InlineData("userName co \"jens\"", true)]
    [InlineData("userName ew \"sen\"", true)]
    [InlineData("userName pr", true)]
    [InlineData("externalId eq \"ext-42\"", true)]
    [InlineData("externalId eq \"EXT-42\"", true)]
    [InlineData("emails eq \"bjensen@example.com\"", true)]
    [InlineData("emails.value eq \"bjensen@example.com\"", true)]
    [InlineData("displayName eq \"barbara jensen\"", true)]
    [InlineData("name eq \"barbara jensen\"", true)]
    [InlineData("active eq \"true\"", true)]
    [InlineData("active eq \"false\"", false)]
    [InlineData("active pr", true)]
    [InlineData("id eq \"11111111-1111-1111-1111-111111111111\"", true)]
    [InlineData("id eq \"22222222-2222-2222-2222-222222222222\"", false)]
    [InlineData("userName eq \"a\" or userName eq \"bjensen\"", true)]
    [InlineData("userName eq \"a\" and active eq \"true\"", false)]
    [InlineData("userName eq \"a\" and active eq \"true\" or userName eq \"bjensen\"", true)]
    [InlineData("not (userName eq \"bjensen\")", false)]
    public void UserPredicates_MatchAsExpected(string filter, bool expected)
    {
        var predicate = CompileUser(filter);

        predicate(Sample).Should().Be(expected);
    }

    [Theory]
    [InlineData("phoneNumbers pr", true)]
    [InlineData("phoneNumbers.value co \"555\"", true)]
    public void OptionalAttributes_HandlePresence(string filter, bool expected = false)
    {
        var predicate = CompileUser(filter);

        predicate(Sample).Should().Be(expected);
    }

    [Fact]
    public void Presence_IsFalseWhenTheOptionalAttributeIsAbsent()
    {
        var noPhone = Sample with { PhoneNumber = null };

        CompileUser("phoneNumbers pr")(noPhone).Should().BeFalse(
            "pr must not match when the optional attribute is missing");
    }

    [Theory]
    [InlineData("nickName eq \"barb\"")]
    [InlineData("title eq \"CEO\"")]
    [InlineData("meta.created gt \"2026-01-01\"")]
    [InlineData("active co \"tru\"")]
    [InlineData("id eq \"not-a-guid\"")]
    public void UnfilterableAttributeOrValue_FailsWithInvalidFilter(string filter)
    {
        var act = () => CompileUser(filter);

        act.Should().Throw<ScimException>()
            .Which.ScimType.Should().Be("invalidFilter");
    }

    [Fact]
    public void GroupPredicates_MatchDisplayNameAndExternalId()
    {
        var now = DateTime.UtcNow;
        var view = new ScimGroupView
        {
            RoleId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            ExternalId = "grp-1",
            DisplayName = "Engineering",
            Description = "desc",
            Active = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        CompileGroup("displayName sw \"eng\"")(view).Should().BeTrue();
        CompileGroup("displayName sw \"sales\"")(view).Should().BeFalse();
        CompileGroup("externalId eq \"GRP-1\"")(view).Should().BeTrue("group externalId matching is case-insensitive");
    }

    private static Func<ScimUserView, bool> CompileUser(string filter)
        => ScimFilterEvaluator.BuildPredicate<ScimUserView>(ScimFilterParser.Parse(filter), ScimFilterableAttributes.User).Compile();

    private static Func<ScimGroupView, bool> CompileGroup(string filter)
        => ScimFilterEvaluator.BuildPredicate<ScimGroupView>(ScimFilterParser.Parse(filter), ScimFilterableAttributes.Group).Compile();
}
