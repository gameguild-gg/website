using FluentAssertions;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests.Services;

/// <summary>
///     Tests for the ABAC attribute/condition expression evaluator (issue #293): JSON
///     operator expressions, boolean condition expressions, category resolution and the
///     fail-closed behavior on malformed payloads and missing attributes.
/// </summary>
public class AbacExpressionEvaluatorTests
{
    private static AbacRequestContext Context(
        Dictionary<string, object>? subject = null,
        Dictionary<string, object>? resource = null,
        Dictionary<string, object>? environment = null,
        Dictionary<string, object>? action = null)
        => new(
            subject ?? [],
            resource ?? [],
            action ?? [],
            environment ?? []);

    [Fact]
    public void AttributeExpression_Equality_MatchesSubjectAttribute()
    {
        var context = Context(subject: new() { ["subject.department"] = "IT" });

        var result = AbacExpressionEvaluator.EvaluateAttributeExpression(
            @"{""user.department"": ""IT""}", context);

        result.Should().BeTrue();
    }

    [Fact]
    public void AttributeExpression_Equality_FailsOnDifferentValue()
    {
        var context = Context(subject: new() { ["subject.department"] = "Sales" });

        var result = AbacExpressionEvaluator.EvaluateAttributeExpression(
            @"{""user.department"": ""IT""}", context);

        result.Should().BeFalse();
    }

    [Fact]
    public void AttributeExpression_MissingAttribute_FailsClosed()
    {
        var result = AbacExpressionEvaluator.EvaluateAttributeExpression(
            @"{""user.clearanceLevel"": 3}", Context());

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("$gte", 5, true)]
    [InlineData("$gte", 3, true)]
    [InlineData("$gte", 6, false)]
    [InlineData("$gt", 5, false)]
    [InlineData("$gt", 4, true)]
    [InlineData("$lte", 5, true)]
    [InlineData("$lt", 5, false)]
    [InlineData("$ne", 7, true)]
    public void AttributeExpression_OrderingOperators_CompareNumerically(string op, int threshold, bool expected)
    {
        var context = Context(subject: new() { ["user.clearanceLevel"] = 5 });

        var result = AbacExpressionEvaluator.EvaluateAttributeExpression(
            @"{""user.clearanceLevel"": {""" + op + @""": " + threshold + "}}", context);

        result.Should().Be(expected);
    }

    [Fact]
    public void AttributeExpression_InOperator_MatchesMembership()
    {
        var context = Context(resource: new() { ["resource.tag"] = "Beta" });

        var result = AbacExpressionEvaluator.EvaluateAttributeExpression(
            @"{""resource.tag"": {""$in"": [""Alpha"", ""Beta""]}}", context);

        result.Should().BeTrue();
    }

    [Fact]
    public void AttributeExpression_NinOperator_ExcludesMembership()
    {
        var context = Context(resource: new() { ["resource.tag"] = "Gamma" });

        var result = AbacExpressionEvaluator.EvaluateAttributeExpression(
            @"{""resource.tag"": {""$nin"": [""Alpha"", ""Beta""]}}", context);

        result.Should().BeTrue();
    }

    [Fact]
    public void AttributeExpression_ExistsOperator_ReflectsPresence()
    {
        var context = Context(environment: new() { ["mfa-verified"] = true });

        AbacExpressionEvaluator.EvaluateAttributeExpression(
            @"{""environment.mfa-verified"": {""$exists"": true}}", context)
            .Should().BeTrue();
        AbacExpressionEvaluator.EvaluateAttributeExpression(
            @"{""environment.mfa-verified"": {""$exists"": false}}", context)
            .Should().BeFalse();
    }

    [Fact]
    public void AttributeExpression_MultipleKeys_AllMustHold()
    {
        var context = Context(
            subject: new() { ["subject.department"] = "IT" },
            resource: new() { ["resource.classification"] = "Internal" });

        AbacExpressionEvaluator.EvaluateAttributeExpression(
            @"{""user.department"": ""IT"", ""resource.classification"": ""Internal""}", context)
            .Should().BeTrue();
        AbacExpressionEvaluator.EvaluateAttributeExpression(
            @"{""user.department"": ""IT"", ""resource.classification"": ""Confidential""}", context)
            .Should().BeFalse();
    }

    [Fact]
    public void AttributeExpression_BareKey_ResolvesAcrossCategories()
    {
        var context = Context(resource: new() { ["classification"] = "Internal" });

        AbacExpressionEvaluator.EvaluateAttributeExpression(
            @"{""classification"": ""Internal""}", context)
            .Should().BeTrue();
    }

    [Fact]
    public void AttributeExpression_MalformedJson_FailsClosed_AndReportsError()
    {
        var errors = new List<string>();

        var result = AbacExpressionEvaluator.EvaluateAttributeExpression("{not json", Context(), errors);

        result.Should().BeFalse();
        errors.Should().NotBeEmpty();
    }

    [Fact]
    public void ValidateAttributeExpression_RejectsNonObject()
    {
        var errors = AbacExpressionEvaluator.ValidateAttributeExpression("[1,2,3]");

        errors.Should().Contain(e => e.Contains("must be a JSON object"));
    }

    [Fact]
    public void ValidateAttributeExpression_RejectsUnknownOperator()
    {
        var errors = AbacExpressionEvaluator.ValidateAttributeExpression(
            @"{""user.level"": {""$frobnicate"": 1}}");

        errors.Should().Contain(e => e.Contains("$frobnicate"));
    }

    [Fact]
    public void ValidateAttributeExpression_AcceptsValidPayload()
    {
        var errors = AbacExpressionEvaluator.ValidateAttributeExpression(
            @"{""user.department"": ""IT"", ""user.level"": {""$gte"": 3}, ""resource.tag"": {""$in"": [""a""]}}");

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ConditionExpression_AttributeToAttributeComparison()
    {
        var context = Context(
            subject: new() { ["user.clearanceLevel"] = 5 },
            resource: new() { ["resource.requiredClearance"] = 3 });

        AbacExpressionEvaluator.EvaluateConditionExpression(
            "user.clearanceLevel >= resource.requiredClearance", context)
            .Should().BeTrue();
        AbacExpressionEvaluator.EvaluateConditionExpression(
            "user.clearanceLevel < resource.requiredClearance", context)
            .Should().BeFalse();
    }

    [Fact]
    public void ConditionExpression_AttributeToLiteralComparison()
    {
        var context = Context(environment: new() { ["current-hour"] = 22 });

        AbacExpressionEvaluator.EvaluateConditionExpression(
            "environment.current-hour <= 20", context)
            .Should().BeFalse();
        AbacExpressionEvaluator.EvaluateConditionExpression(
            "environment.current-hour > 20", context)
            .Should().BeTrue();
    }

    [Fact]
    public void ConditionExpression_AndOrPrecedence()
    {
        var context = Context(
            subject: new() { ["department"] = "IT", ["level"] = 2 },
            resource: new() { ["classification"] = "Public" });

        // (dept=IT AND level>=3) OR classification=Public → true via OR branch
        AbacExpressionEvaluator.EvaluateConditionExpression(
            "user.department == \"IT\" AND user.level >= 3 OR resource.classification == \"Public\"", context)
            .Should().BeTrue();

        // dept=IT AND (level>=3 OR classification=Public) → AND left fails, right true → true
        AbacExpressionEvaluator.EvaluateConditionExpression(
            "user.department == \"Sales\" AND user.level >= 3 OR resource.classification == \"Public\"", context)
            .Should().BeTrue();

        // Neither side holds
        AbacExpressionEvaluator.EvaluateConditionExpression(
            "user.department == \"Sales\" AND user.level >= 3 OR resource.classification == \"Secret\"", context)
            .Should().BeFalse();
    }

    [Fact]
    public void ConditionExpression_Parentheses()
    {
        var context = Context(
            subject: new() { ["department"] = "Sales" },
            resource: new() { ["classification"] = "Public" });

        AbacExpressionEvaluator.EvaluateConditionExpression(
            "(user.department == \"Sales\" OR user.department == \"IT\") AND resource.classification == \"Public\"", context)
            .Should().BeTrue();
    }

    [Fact]
    public void ConditionExpression_NotOperator()
    {
        var context = Context(environment: new() { ["is-production"] = false });

        AbacExpressionEvaluator.EvaluateConditionExpression(
            "NOT environment.is-production", context)
            .Should().BeTrue();
    }

    [Fact]
    public void ConditionExpression_StringEquality_CaseInsensitive()
    {
        var context = Context(subject: new() { ["department"] = "Engineering" });

        AbacExpressionEvaluator.EvaluateConditionExpression(
            "user.department == 'engineering'", context)
            .Should().BeTrue();
    }

    [Fact]
    public void ConditionExpression_MissingAttribute_FailsClosed()
    {
        AbacExpressionEvaluator.EvaluateConditionExpression(
            "user.clearanceLevel >= 3", Context())
            .Should().BeFalse();
    }

    [Fact]
    public void ConditionExpression_Malformed_ReportsError()
    {
        var errors = new List<string>();

        var result = AbacExpressionEvaluator.EvaluateConditionExpression(
            "user.level >>>", Context(), errors);

        result.Should().BeFalse();
        errors.Should().NotBeEmpty();
    }

    [Fact]
    public void ValidateConditionExpression_DetectsTrailingGarbage()
    {
        var errors = AbacExpressionEvaluator.ValidateConditionExpression(
            "user.level >= 3 AND");

        errors.Should().NotBeEmpty();
    }

    [Fact]
    public void ValidateConditionExpression_AcceptsValidExpression()
    {
        var errors = AbacExpressionEvaluator.ValidateConditionExpression(
            "user.clearanceLevel >= resource.requiredClearance AND environment.current-hour <= 20");

        errors.Should().BeEmpty();
    }
}
