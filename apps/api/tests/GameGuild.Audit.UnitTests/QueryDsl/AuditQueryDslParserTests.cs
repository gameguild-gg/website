using FluentAssertions;
using GameGuild.Compliance.Audit;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.QueryDsl;

public sealed class AuditQueryDslParserTests
{
    private readonly AuditQueryDslParser _parser = new();

    // ── JSON form: valid inputs ──────────────────────────────────────────

    [Fact]
    public void ParseJson_SingleCondition_Succeeds()
    {
        var result = _parser.ParseJson("""{"filter":{"field":"actor","op":"eq","value":"alice"}}""");

        result.IsSuccess.Should().BeTrue();
        var condition = result.Value.Filter.Should().BeOfType<AuditQueryCondition>().Subject;
        condition.Field.Should().Be(AuditQueryField.Actor);
        condition.Operator.Should().Be(AuditQueryComparison.Eq);
        condition.Value.GetString().Should().Be("alice");
    }

    [Fact]
    public void ParseJson_NestedAndOrGroups_Succeeds()
    {
        var result = _parser.ParseJson(
            """
            {
              "filter": {
                "and": [
                  { "field": "source", "op": "eq", "value": "Authentication" },
                  { "or": [
                      { "field": "severity", "op": "eq", "value": "High" },
                      { "field": "severity", "op": "eq", "value": "Critical" }
                  ] }
                ]
              },
              "sortBy": "timestamp",
              "sortDirection": "asc",
              "take": 25
            }
            """);

        result.IsSuccess.Should().BeTrue();
        var root = result.Value.Filter.Should().BeOfType<AuditQueryGroup>().Subject;
        root.Logic.Should().Be(AuditQueryLogicalOperator.And);
        root.Children.Should().HaveCount(2);
        var nested = root.Children[1].Should().BeOfType<AuditQueryGroup>().Subject;
        nested.Logic.Should().Be(AuditQueryLogicalOperator.Or);
        nested.Children.Should().HaveCount(2);
        result.Value.SortDirection.Should().Be("asc");
        result.Value.Take.Should().Be(25);
    }

    [Theory]
    [InlineData("eq", AuditQueryComparison.Eq)]
    [InlineData("ne", AuditQueryComparison.Ne)]
    [InlineData("gt", AuditQueryComparison.Gt)]
    [InlineData("gte", AuditQueryComparison.Gte)]
    [InlineData("lt", AuditQueryComparison.Lt)]
    [InlineData("lte", AuditQueryComparison.Lte)]
    [InlineData("contains", AuditQueryComparison.Contains)]
    [InlineData("startswith", AuditQueryComparison.StartsWith)]
    [InlineData("matches", AuditQueryComparison.Wildcard)]
    public void ParseJson_OperatorNames_Resolve(string operatorName, AuditQueryComparison expected)
    {
        var result = _parser.ParseJson("{\"filter\":{\"field\":\"action\",\"op\":\"" + operatorName + "\",\"value\":\"login\"}}");

        result.IsSuccess.Should().BeTrue();
        ((AuditQueryCondition)result.Value.Filter).Operator.Should().Be(expected);
    }

    [Fact]
    public void ParseJson_InOperator_WithArray_Succeeds()
    {
        var result = _parser.ParseJson("""{"filter":{"field":"severity","op":"in","value":["Low","High"]}}""");

        result.IsSuccess.Should().BeTrue();
        var condition = (AuditQueryCondition)result.Value.Filter;
        condition.Operator.Should().Be(AuditQueryComparison.In);
        condition.Value.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
    }

    [Fact]
    public void ParseJson_Negate_Succeeds()
    {
        var result = _parser.ParseJson("""{"filter":{"field":"success","op":"eq","value":true,"negate":true}}""");

        result.IsSuccess.Should().BeTrue();
        result.Value.Filter.Negate.Should().BeTrue();
    }

    // ── JSON form: fail-closed on invalid inputs ────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("[1,2,3]")]
    [InlineData("{}")]
    [InlineData("{\"sortBy\":\"timestamp\"}")]
    public void ParseJson_MalformedOrEmpty_FailsClosed(string json)
    {
        var result = _parser.ParseJson(json);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AuditQueryDsl.Invalid");
        result.Error.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ParseJson_UnknownField_FailsClosedAndListsKnownFields()
    {
        var result = _parser.ParseJson("""{"filter":{"field":"hackerfield","op":"eq","value":"x"}}""");

        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Contain("Unknown field 'hackerfield'");
        result.Error.Description.Should().Contain("actor").And.Contain("severity");
    }

    [Fact]
    public void ParseJson_UnknownOperator_FailsClosed()
    {
        var result = _parser.ParseJson("""{"filter":{"field":"action","op":"explode","value":"x"}}""");

        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Contain("Unknown operator 'explode'");
    }

    [Fact]
    public void ParseJson_OperatorNotAllowedForField_FailsClosed()
    {
        var result = _parser.ParseJson("""{"filter":{"field":"actor","op":"gt","value":"x"}}""");

        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Contain("not supported for field 'actor'");
    }

    [Fact]
    public void ParseJson_TenantValueMustBeGuid_FailsClosed()
    {
        var result = _parser.ParseJson("""{"filter":{"field":"tenant","op":"eq","value":"not-a-guid"}}""");

        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Contain("GUID");
    }

    [Fact]
    public void ParseJson_TimestampValueMustBeIso8601_FailsClosed()
    {
        var result = _parser.ParseJson("""{"filter":{"field":"timestamp","op":"gte","value":"yesterday-ish"}}""");

        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Contain("ISO-8601");
    }

    [Fact]
    public void ParseJson_SeverityValueMustBeKnownLevel_FailsClosed()
    {
        var result = _parser.ParseJson("""{"filter":{"field":"severity","op":"eq","value":"Apocalyptic"}}""");

        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Contain("Low").And.Contain("Critical");
    }

    [Fact]
    public void ParseJson_SuccessValueMustBeBoolean_FailsClosed()
    {
        var result = _parser.ParseJson("""{"filter":{"field":"success","op":"eq","value":"yes"}}""");

        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Contain("boolean");
    }

    [Fact]
    public void ParseJson_EmptyGroup_FailsClosed()
    {
        var result = _parser.ParseJson("""{"filter":{"and":[]}}""");

        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Contain("non-empty array");
    }

    [Fact]
    public void ParseJson_AndAndOrTogether_FailsClosed()
    {
        var result = _parser.ParseJson("""{"filter":{"and":[{"field":"actor","op":"eq","value":"a"}],"or":[{"field":"actor","op":"eq","value":"b"}]}}""");

        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Contain("cannot declare both");
    }

    [Fact]
    public void ParseJson_UnknownTopLevelProperty_FailsClosed()
    {
        var result = _parser.ParseJson("""{"filter":{"field":"actor","op":"eq","value":"a"},"hax":true}""");

        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Contain("Unknown query property 'hax'");
    }

    [Fact]
    public void ParseJson_InWithScalarValue_FailsClosed()
    {
        var result = _parser.ParseJson("""{"filter":{"field":"severity","op":"in","value":"High"}}""");

        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Contain("array");
    }

    [Fact]
    public void ParseJson_UnknownSortField_FailsClosed()
    {
        var result = _parser.ParseJson("""{"filter":{"field":"actor","op":"eq","value":"a"},"sortBy":"email"}""");

        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Contain("Unknown sortBy");
    }

    // ── Text form: valid inputs ─────────────────────────────────────────

    [Fact]
    public void ParseText_ActorAndGrantedAfterDate_MatchesIssueExample()
    {
        var result = _parser.ParseText("actor:alice AND action:granted AFTER 2026-01-01");

        result.IsSuccess.Should().BeTrue();
        var root = result.Value.Filter.Should().BeOfType<AuditQueryGroup>().Subject;
        root.Logic.Should().Be(AuditQueryLogicalOperator.And);
        root.Children.Should().HaveCount(3);
        root.Children[2].Should().BeOfType<AuditQueryCondition>()
            .Which.Field.Should().Be(AuditQueryField.Timestamp);
    }

    [Fact]
    public void ParseText_ParenthesesOverridePrecedence()
    {
        // a OR b AND c  ==  a OR (b AND c); with parens: (a OR b) AND c
        var withoutParens = _parser.ParseText("action:login OR action:logout AND severity:High");
        var withParens = _parser.ParseText("(action:login OR action:logout) AND severity:High");

        withoutParens.IsSuccess.Should().BeTrue();
        withParens.IsSuccess.Should().BeTrue();

        var plain = withoutParens.Value.Filter.Should().BeOfType<AuditQueryGroup>().Subject;
        plain.Logic.Should().Be(AuditQueryLogicalOperator.Or);
        plain.Children[1].Should().BeOfType<AuditQueryGroup>()
            .Which.Logic.Should().Be(AuditQueryLogicalOperator.And);

        var grouped = withParens.Value.Filter.Should().BeOfType<AuditQueryGroup>().Subject;
        grouped.Logic.Should().Be(AuditQueryLogicalOperator.And);
        grouped.Children[0].Should().BeOfType<AuditQueryGroup>()
            .Which.Logic.Should().Be(AuditQueryLogicalOperator.Or);
    }

    [Fact]
    public void ParseText_NotOperator()
    {
        var result = _parser.ParseText("NOT source:Authentication");

        result.IsSuccess.Should().BeTrue();
        result.Value.Filter.Negate.Should().BeTrue();
    }

    [Theory]
    [InlineData("action:~login", AuditQueryComparison.Contains)]
    [InlineData("action:!login", AuditQueryComparison.Ne)]
    [InlineData("action:!=login", AuditQueryComparison.Ne)]
    [InlineData("action:^log", AuditQueryComparison.StartsWith)]
    [InlineData("severity:[Low,High]", AuditQueryComparison.In)]
    public void ParseText_OperatorSugar(string text, AuditQueryComparison expected)
    {
        var result = _parser.ParseText(text);

        result.IsSuccess.Should().BeTrue();
        ((AuditQueryCondition)result.Value.Filter).Operator.Should().Be(expected);
    }

    [Fact]
    public void ParseText_QuotedValueWithSpaces()
    {
        var result = _parser.ParseText("description:~\"failed login attempt\"");

        result.IsSuccess.Should().BeTrue();
        var condition = (AuditQueryCondition)result.Value.Filter;
        condition.Operator.Should().Be(AuditQueryComparison.Contains);
        condition.Value.GetString().Should().Be("failed login attempt");
    }

    [Fact]
    public void ParseText_BeforeDateIsInclusiveUpperBound()
    {
        var result = _parser.ParseText("BEFORE 2026-02-01");

        result.IsSuccess.Should().BeTrue();
        var condition = (AuditQueryCondition)result.Value.Filter;
        condition.Field.Should().Be(AuditQueryField.Timestamp);
        condition.Operator.Should().Be(AuditQueryComparison.Lte);
    }

    [Fact]
    public void ParseText_GuidAndBooleanCoercion()
    {
        var tenant = Guid.NewGuid();
        var result = _parser.ParseText($"tenant:{tenant} AND success:true");

        result.IsSuccess.Should().BeTrue();
        var group = (AuditQueryGroup)result.Value.Filter;
        ((AuditQueryCondition)group.Children[0]).Value.GetString().Should().Be(tenant.ToString());
        ((AuditQueryCondition)group.Children[1]).Value.GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void ParseText_CanonicalRoundTrip()
    {
        var parsed = _parser.ParseText("actor:alice AND (action:~grant OR severity:[High,Critical]) AFTER 2026-01-01");
        parsed.IsSuccess.Should().BeTrue();

        var canonical = AuditQueryDslJson.Serialize(parsed.Value);
        var reparsed = _parser.ParseJson(canonical);

        reparsed.IsSuccess.Should().BeTrue();
        AuditQueryDslJson.Serialize(reparsed.Value).Should().Be(canonical);
    }

    // ── Text form: fail-closed on invalid inputs ────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bogusfield:value")]
    [InlineData("actor:")]
    [InlineData("action:login AND")]
    [InlineData("severity:Meh")]
    [InlineData("success:maybe")]
    [InlineData("source:Somewhere")]
    [InlineData("actor:alice nonsense")]
    public void ParseText_InvalidInputs_FailClosed(string text)
    {
        var result = _parser.ParseText(text);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AuditQueryDsl.Invalid");
        result.Error.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ParseText_UnknownOperatorSugarForField_FailsClosed()
    {
        var result = _parser.ParseText("actor:>alice");

        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Contain("not supported for field 'actor'");
    }

    [Fact]
    public void ParseText_UnmatchedParentheses_FailClosed()
    {
        var openOnly = _parser.ParseText("(action:login AND severity:High");
        openOnly.IsFailure.Should().BeTrue();
        openOnly.Error.Description.Should().Contain("closing parenthesis");

        var closeOnly = _parser.ParseText("action:login)");
        closeOnly.IsFailure.Should().BeTrue();
        closeOnly.Error.Description.Should().Contain("Unexpected token");
    }

    [Fact]
    public void ParseText_DeepNestingBeyondLimit_FailsClosed()
    {
        var query = string.Concat(Enumerable.Repeat("(", 17)) + "action:login" + string.Concat(Enumerable.Repeat(")", 17));

        var result = _parser.ParseText(query);

        result.IsFailure.Should().BeTrue();
    }
}
