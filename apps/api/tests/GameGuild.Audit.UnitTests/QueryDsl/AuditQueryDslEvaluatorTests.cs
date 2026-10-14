using FluentAssertions;
using GameGuild.Compliance.Audit;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.QueryDsl;

public sealed class AuditQueryDslEvaluatorTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantA = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid TenantB = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static UnifiedSecurityAuditEntry Entry(
        string action = "Login",
        Guid? userId = null,
        string? email = null,
        Guid? tenantId = null,
        string resource = "User",
        AuditRiskLevel severity = AuditRiskLevel.Low,
        SecurityAuditSourceType source = SecurityAuditSourceType.Authentication,
        bool success = true,
        DateTime? timestamp = null,
        string? ip = null,
        string? description = null) => new()
    {
        Id = Guid.NewGuid(),
        SourceType = source,
        SourceEntity = source.ToString(),
        ActionType = action,
        UserId = userId ?? UserId,
        UserEmail = email,
        TenantId = tenantId,
        ResourceType = resource,
        IpAddress = ip,
        Success = success,
        Description = description,
        RiskLevel = severity,
        Timestamp = timestamp ?? new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc)
    };

    private readonly AuditQueryDslParser _parser = new();

    private AuditQueryNode ParseFilter(string json)
    {
        var parsed = _parser.ParseJson(json);
        parsed.IsSuccess.Should().BeTrue(parsed.Error.Description);
        return parsed.Value.Filter;
    }

    [Fact]
    public void Actor_MatchesByUserIdOrEmail()
    {
        var filter = ParseFilter("""{"filter":{"field":"actor","op":"eq","value":"11111111-1111-1111-1111-111111111111"}}""");
        AuditQueryDslEvaluator.Matches(filter, Entry(email: "alice@example.com")).Should().BeTrue();

        var emailFilter = ParseFilter("""{"filter":{"field":"actor","op":"eq","value":"alice@example.com"}}""");
        AuditQueryDslEvaluator.Matches(emailFilter, Entry(email: "Alice@Example.com")).Should().BeTrue();
        AuditQueryDslEvaluator.Matches(emailFilter, Entry(email: "bob@example.com")).Should().BeFalse();
    }

    [Fact]
    public void NestedAndOrNot_CombinesCorrectly()
    {
        var filter = ParseFilter(
            """
            {"filter":{"and":[
                {"field":"source","op":"eq","value":"Authentication"},
                {"or":[
                    {"field":"severity","op":"eq","value":"High"},
                    {"field":"success","op":"eq","value":false,"negate":true}
                ]}
            ]}}
            """);

        // Authentication + High -> matches
        AuditQueryDslEvaluator.Matches(filter, Entry(source: SecurityAuditSourceType.Authentication, severity: AuditRiskLevel.High)).Should().BeTrue();
        // Authentication + success (NOT false) -> matches
        AuditQueryDslEvaluator.Matches(filter, Entry(source: SecurityAuditSourceType.Authentication, success: true)).Should().BeTrue();
        // Authentication + failed + low -> no
        AuditQueryDslEvaluator.Matches(filter, Entry(source: SecurityAuditSourceType.Authentication, success: false, severity: AuditRiskLevel.Low)).Should().BeFalse();
        // Wrong source -> no
        AuditQueryDslEvaluator.Matches(filter, Entry(source: SecurityAuditSourceType.General, severity: AuditRiskLevel.High)).Should().BeFalse();
    }

    [Theory]
    [InlineData("gt", "2026-06-15T12:00:00Z", false)]
    [InlineData("gte", "2026-06-15T12:00:00Z", true)]
    [InlineData("lt", "2026-06-15T12:00:01Z", true)]
    [InlineData("lte", "2026-06-15T12:00:00Z", true)]
    [InlineData("eq", "2026-06-15T12:00:00Z", true)]
    public void Timestamp_OrderedComparisons(string op, string value, bool expected)
    {
        var filter = ParseFilter($"{{\"filter\":{{\"field\":\"timestamp\",\"op\":\"{op}\",\"value\":\"{value}\"}}}}");
        AuditQueryDslEvaluator.Matches(filter, Entry()).Should().Be(expected);
    }

    [Fact]
    public void Tenant_Equality_SeparatesTenants()
    {
        var filter = ParseFilter($"{{\"filter\":{{\"field\":\"tenant\",\"op\":\"eq\",\"value\":\"{TenantA}\"}}}}");

        AuditQueryDslEvaluator.Matches(filter, Entry(tenantId: TenantA)).Should().BeTrue();
        AuditQueryDslEvaluator.Matches(filter, Entry(tenantId: TenantB)).Should().BeFalse();
        AuditQueryDslEvaluator.Matches(filter, Entry(tenantId: null)).Should().BeFalse();
    }

    [Fact]
    public void InOperator_MatchesAnyListedSeverity()
    {
        var filter = ParseFilter("""{"filter":{"field":"severity","op":"in","value":["High","Critical"]}}""");

        AuditQueryDslEvaluator.Matches(filter, Entry(severity: AuditRiskLevel.High)).Should().BeTrue();
        AuditQueryDslEvaluator.Matches(filter, Entry(severity: AuditRiskLevel.Critical)).Should().BeTrue();
        AuditQueryDslEvaluator.Matches(filter, Entry(severity: AuditRiskLevel.Low)).Should().BeFalse();
    }

    [Fact]
    public void Contains_IsCaseInsensitiveSubstring()
    {
        var filter = ParseFilter("""{"filter":{"field":"action","op":"contains","value":"GRANT"}}""");

        AuditQueryDslEvaluator.Matches(filter, Entry(action: "PermissionGranted")).Should().BeTrue();
        AuditQueryDslEvaluator.Matches(filter, Entry(action: "Login")).Should().BeFalse();
    }

    [Fact]
    public void Wildcard_AnchoredStarPattern()
    {
        var filter = ParseFilter("""{"filter":{"field":"ip","op":"matches","value":"10.0.*"}}""");

        AuditQueryDslEvaluator.Matches(filter, Entry(ip: "10.0.1.5")).Should().BeTrue();
        AuditQueryDslEvaluator.Matches(filter, Entry(ip: "192.168.0.1")).Should().BeFalse();
        // Anchored: '10.0.*' must match the whole value.
        AuditQueryDslEvaluator.Matches(filter, Entry(ip: "110.0.1.5")).Should().BeFalse();
    }

    [Fact]
    public void Description_WithMissingField_NeMatches()
    {
        var filter = ParseFilter("""{"filter":{"field":"description","op":"eq","value":"boom"}}""");
        AuditQueryDslEvaluator.Matches(filter, Entry(description: null)).Should().BeFalse();

        var neFilter = ParseFilter("""{"filter":{"field":"description","op":"ne","value":"boom"}}""");
        AuditQueryDslEvaluator.Matches(neFilter, Entry(description: null)).Should().BeTrue();
    }
}
