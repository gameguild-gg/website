using FluentAssertions;
using GameGuild.Identity.Provisioning.Scim.Filtering;
using Xunit;

namespace GameGuild.Identity.Provisioning.UnitTests.Filtering;

/// <summary>
///     RFC 7644 §3.4.2.2 filter grammar subset: parsing, operator coverage, and the
/// invalidFilter contract for unsupported constructs.
/// </summary>
public sealed class ScimFilterParserTests
{
    [Theory]
    [InlineData("userName eq \"bjensen\"", typeof(ScimFilter.Compare))]
    [InlineData("userName ne \"bjensen\"", typeof(ScimFilter.Compare))]
    [InlineData("userName co \"jens\"", typeof(ScimFilter.Compare))]
    [InlineData("userName sw \"bjen\"", typeof(ScimFilter.Compare))]
    [InlineData("userName ew \"sen\"", typeof(ScimFilter.Compare))]
    [InlineData("userName pr", typeof(ScimFilter.Compare))]
    [InlineData("userName eq \"a\" and active eq \"true\"", typeof(ScimFilter.And))]
    [InlineData("userName eq \"a\" or displayName eq \"b\"", typeof(ScimFilter.Or))]
    [InlineData("not (userName eq \"a\")", typeof(ScimFilter.Not))]
    [InlineData("(userName eq \"a\" or userName eq \"b\") and active pr", typeof(ScimFilter.And))]
    public void Parses_SupportedForms(string filter, Type expectedRoot)
    {
        var parsed = ScimFilterParser.Parse(filter);

        parsed.Should().BeOfType(expectedRoot);
    }

    [Fact]
    public void Parses_CaseInsensitiveOperatorsAndAttributes()
    {
        var parsed = ScimFilterParser.Parse("UserName EQ \"bjensen\"") as ScimFilter.Compare;

        parsed.Should().NotBeNull();
        parsed!.Attribute.Should().Be("UserName");
        parsed.Operator.Should().Be(ScimFilterOperator.Eq);
        parsed.Value.Should().Be("bjensen");
    }

    [Theory]
    [InlineData("userName gt \"a\"")]
    [InlineData("userName lt \"a\"")]
    [InlineData("userName ge \"a\"")]
    [InlineData("userName le \"a\"")]
    public void OrderingOperators_AreRejected_WithInvalidFilter(string filter)
    {
        var act = () => ScimFilterParser.Parse(filter);

        act.Should().Throw<ScimException>()
            .Which.ScimType.Should().Be("invalidFilter");
    }

    [Theory]
    [InlineData("emails[type eq \"work\"]")]
    [InlineData("emails[type eq \"work\"].value pr")]
    public void ValueFilterPaths_AreRejected_WithInvalidFilter(string filter)
    {
        var act = () => ScimFilterParser.Parse(filter);

        act.Should().Throw<ScimException>()
            .Which.ScimType.Should().Be("invalidFilter");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("userName")]
    [InlineData("userName eq")]
    [InlineData("userName eq \"unterminated")]
    [InlineData("(userName eq \"a\"")]
    [InlineData("userName eq \"a\" extra")]
    public void MalformedFilters_AreRejected_WithInvalidFilter(string filter)
    {
        var act = () => ScimFilterParser.Parse(filter);

        act.Should().Throw<ScimException>()
            .Which.ScimType.Should().Be("invalidFilter");
    }

    [Fact]
    public void Precedence_AndBindsTighterThanOr()
    {
        // a or b and c parses as a or (b and c)
        var parsed = ScimFilterParser.Parse("userName eq \"a\" or displayName eq \"b\" and active pr");

        var or = parsed.Should().BeOfType<ScimFilter.Or>().Subject;
        or.Left.Should().BeOfType<ScimFilter.Compare>();
        or.Right.Should().BeOfType<ScimFilter.And>();
    }

    [Fact]
    public void Pr_ComparisonCarriesNoValue()
    {
        var parsed = ScimFilterParser.Parse("externalId pr") as ScimFilter.Compare;

        parsed!.Operator.Should().Be(ScimFilterOperator.Pr);
        parsed.Value.Should().BeNull();
    }
}
