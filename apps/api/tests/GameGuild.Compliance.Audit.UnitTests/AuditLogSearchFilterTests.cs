using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using GameGuild.Compliance.Audit;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameGuild.Compliance.Audit.UnitTests;

#region AuditLogSearchFilter Tests

public class AuditLogSearchFilterTests
{
    private static readonly MethodInfo IlikeMethod = typeof(NpgsqlDbFunctionsExtensions).GetMethod(
        nameof(NpgsqlDbFunctionsExtensions.ILike),
        [typeof(DbFunctions), typeof(string), typeof(string)])!;

    private static readonly MethodInfo ClientIlikeMethod = typeof(AuditLogSearchFilterTests).GetMethod(
        nameof(ClientIlike),
        BindingFlags.Static | BindingFlags.NonPublic)!;

    [Fact]
    public void Normalize_ShouldTrimAndTreatEmptyAsNull()
    {
        AuditLogSearchFilter.Normalize("  payment failed ").Should().Be("payment failed");
        AuditLogSearchFilter.Normalize(null).Should().BeNull();
        AuditLogSearchFilter.Normalize(string.Empty).Should().BeNull();
        AuditLogSearchFilter.Normalize("   ").Should().BeNull();
    }

    [Fact]
    public void Normalize_ShouldCapLengthAtMaxLength()
    {
        var searchText = new string('a', AuditLogSearchFilter.MaxSearchTextLength + 50);

        var normalized = AuditLogSearchFilter.Normalize(searchText);

        normalized.Should().HaveLength(AuditLogSearchFilter.MaxSearchTextLength);
    }

    [Fact]
    public void EscapeLikeWildcards_ShouldEscapeWildcardsAndEscapeCharacter()
    {
        AuditLogSearchFilter.EscapeLikeWildcards("100%_a\\b").Should().Be("100\\%\\_a\\\\b");
        AuditLogSearchFilter.EscapeLikeWildcards("plain").Should().Be("plain");
    }

    [Fact]
    public void BuildPredicate_ShouldReturnNull_WhenSearchTextIsEmpty()
    {
        AuditLogSearchFilter.BuildPredicate(null).Should().BeNull();
        AuditLogSearchFilter.BuildPredicate(string.Empty).Should().BeNull();
        AuditLogSearchFilter.BuildPredicate("   ").Should().BeNull();
    }

    [Fact]
    public void Predicate_ShouldMatchDescriptionCaseInsensitively()
    {
        var predicate = CompilePredicate("FAILED");

        var logs = new[]
        {
            CreateLog(description: "Permission request failed"),
            CreateLog(description: "Login succeeded"),
            CreateLog(metadata: "{\"reason\":\"unrelated\"}")
        };

        logs.Where(predicate).Should().ContainSingle()
            .Which.Description.Should().Be("Permission request failed");
    }

    [Fact]
    public void Predicate_ShouldMatchSerializedContextMetadata()
    {
        var predicate = CompilePredicate("ledger.delete");

        var logs = new[]
        {
            CreateLog(description: "Login succeeded"),
            CreateLog(metadata: "{\"PermissionName\":\"ledger.delete\",\"Reason\":\"policy\"}")
        };

        logs.Where(predicate).Should().ContainSingle()
            .Which.Metadata.Should().Contain("ledger.delete");
    }

    [Fact]
    public void Predicate_ShouldNotMatchLogsWithoutDescriptionOrMetadata()
    {
        var predicate = CompilePredicate("permission");

        var logs = new[]
        {
            CreateLog(description: null, metadata: null),
            CreateLog(description: "Permission granted")
        };

        logs.Where(predicate).Should().ContainSingle()
            .Which.Description.Should().Be("Permission granted");
    }

    [Fact]
    public void Predicate_ShouldCombineWithRiskLevelFilter()
    {
        var predicate = CompilePredicate("permission");

        var logs = new[]
        {
            CreateLog(description: "Permission denied", riskLevel: AuditRiskLevel.High),
            CreateLog(description: "Permission granted", riskLevel: AuditRiskLevel.Low),
            CreateLog(description: "Login failed", riskLevel: AuditRiskLevel.Critical)
        };

        logs.Where(log => log.RiskLevel >= AuditRiskLevel.High).Where(predicate).Should().ContainSingle()
            .Which.Description.Should().Be("Permission denied");
    }

    [Fact]
    public void Predicate_ShouldTreatPercentSignInSearchTextAsLiteral()
    {
        var predicate = CompilePredicate("100%");

        var logs = new[]
        {
            CreateLog(description: "Progress reached 100%"),
            CreateLog(description: "Progress reached 100x")
        };

        logs.Where(predicate).Should().ContainSingle()
            .Which.Description.Should().Be("Progress reached 100%");
    }

    [Fact]
    public void Predicate_ShouldTreatUnderscoreInSearchTextAsLiteral()
    {
        var predicate = CompilePredicate("tenant_id");

        var logs = new[]
        {
            CreateLog(metadata: "{\"tenant_id\":\"t-1\"}"),
            CreateLog(metadata: "{\"tenantXid\":\"t-2\"}")
        };

        logs.Where(predicate).Should().ContainSingle()
            .Which.Metadata.Should().Contain("tenant_id");
    }

    [Fact]
    public void Predicate_ShouldTreatBackslashInSearchTextAsLiteral()
    {
        var predicate = CompilePredicate("C:\\temp");

        var logs = new[]
        {
            CreateLog(description: "Exported file to C:\\temp\\audit.csv"),
            CreateLog(description: "Exported file to C:itempaudit.csv")
        };

        logs.Where(predicate).Should().ContainSingle()
            .Which.Description.Should().Be("Exported file to C:\\temp\\audit.csv");
    }

    [Fact]
    public void EmptySearchText_ShouldLeaveLogsUnfiltered()
    {
        foreach (var empty in new string?[] { null, string.Empty, "   " })
        {
            AuditLogSearchFilter.BuildPredicate(empty).Should().BeNull($"SearchText '{empty}' must not filter");
        }
    }

    private static AuditLog CreateLog(
        string? description = null,
        string? metadata = null,
        AuditRiskLevel riskLevel = AuditRiskLevel.Low) =>
        new()
        {
            ActionType = "ViewAuditLogs",
            ResourceType = "System",
            Description = description,
            Metadata = metadata,
            RiskLevel = riskLevel
        };

    private static Func<AuditLog, bool> CompilePredicate(string? searchText)
    {
        var predicate = AuditLogSearchFilter.BuildPredicate(searchText);
        predicate.Should().NotBeNull("a non-empty search text must build a predicate");
        var rewritten = new ClientIlikeRewriter().Visit(predicate!)!;
        return ((Expression<Func<AuditLog, bool>>)rewritten).Compile();
    }

    // Reference in-memory implementation of PostgreSQL ILIKE (%, _ wildcards and
    // backslash escapes, case-insensitive) used to evaluate the SQL predicate built
    // by AuditLogSearchFilter against in-memory data in these tests.
    private static bool ClientIlike(string? field, string pattern)
    {
        if (field is null)
        {
            return false;
        }

        var regex = new StringBuilder("^");
        var index = 0;
        while (index < pattern.Length)
        {
            var character = pattern[index++];
            if (character == '\\' && index < pattern.Length)
            {
                regex.Append(Regex.Escape(pattern[index++].ToString()));
            }
            else if (character == '%')
            {
                regex.Append(".*");
            }
            else if (character == '_')
            {
                regex.Append('.');
            }
            else
            {
                regex.Append(Regex.Escape(character.ToString()));
            }
        }

        return Regex.IsMatch(field, regex.Append('$').ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private sealed class ClientIlikeRewriter : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method == IlikeMethod)
            {
                return Expression.Call(ClientIlikeMethod, Visit(node.Arguments[1]), Visit(node.Arguments[2]));
            }

            return base.VisitMethodCall(node);
        }
    }
}

#endregion
