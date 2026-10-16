using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using GameGuild.Compliance.Audit;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameGuild.Compliance.Audit.UnitTests;

#region AuditConditionTreeValidator Tests

public class AuditConditionTreeValidatorTests
{
    [Fact]
    public void Validate_ShouldReturnEmpty_WhenConditionIsNull()
    {
        AuditConditionTreeValidator.Validate(null).Should().BeEmpty();
    }

    [Fact]
    public void Validate_ShouldAcceptValidNestedTree()
    {
        var condition = AuditConditionTrees.GroupNode(
            AuditLogicalOperator.And,
            AuditConditionTrees.GroupNode(
                AuditLogicalOperator.Or,
                AuditConditionTrees.FieldNode(AuditFilterField.ActionType, AuditFilterComparison.Equals, "ViewAuditLogs"),
                AuditConditionTrees.FieldNode(AuditFilterField.ActionType, AuditFilterComparison.Equals, "DeleteAuditLog")),
            AuditConditionTrees.GroupNode(
                AuditLogicalOperator.Not,
                AuditConditionTrees.FieldNode(AuditFilterField.Category, AuditFilterComparison.Equals, "Security")));

        AuditConditionTreeValidator.Validate(condition).Should().BeEmpty();
    }

    [Fact]
    public void Validate_ShouldReportUnknownOrMissingKind()
    {
        foreach (var kind in new AuditConditionNodeKind?[] { (AuditConditionNodeKind)99, null })
        {
            var condition = new AuditConditionNode { Kind = kind };

            var result = AuditConditionTreeValidator.Validate(condition).Should().ContainSingle().Which;

            result.ErrorMessage.Should().Be("Kind must be 'Group' or 'Field'.");
            result.MemberNames.Should().Equal("Condition.Kind");
        }
    }

    [Fact]
    public void Validate_ShouldReportUnknownOrMissingOperator()
    {
        foreach (var logicalOperator in new AuditLogicalOperator?[] { (AuditLogicalOperator)99, null })
        {
            var condition = new AuditConditionNode
            {
                Kind = AuditConditionNodeKind.Group,
                Operator = logicalOperator,
                Children = [AuditConditionTrees.FieldNode(AuditFilterField.ActionType, AuditFilterComparison.Equals, "ViewAuditLogs")]
            };

            var result = AuditConditionTreeValidator.Validate(condition).Should().ContainSingle().Which;

            result.ErrorMessage.Should().Be("Operator must be 'And', 'Or', or 'Not' for Group nodes.");
            result.MemberNames.Should().Equal("Condition.Operator");
        }
    }

    [Fact]
    public void Validate_ShouldRejectGroupWithValue()
    {
        var condition = new AuditConditionNode
        {
            Kind = AuditConditionNodeKind.Group,
            Operator = AuditLogicalOperator.And,
            Value = "ViewAuditLogs",
            Children = [AuditConditionTrees.FieldNode(AuditFilterField.ActionType, AuditFilterComparison.Equals, "ViewAuditLogs")]
        };

        var result = AuditConditionTreeValidator.Validate(condition).Should().ContainSingle().Which;

        result.ErrorMessage.Should().Be("Group nodes must not specify Value or Values.");
        result.MemberNames.Should().Equal("Condition");
    }

    [Fact]
    public void Validate_ShouldRejectGroupWithValues()
    {
        var condition = new AuditConditionNode
        {
            Kind = AuditConditionNodeKind.Group,
            Operator = AuditLogicalOperator.And,
            Values = ["ViewAuditLogs"],
            Children = [AuditConditionTrees.FieldNode(AuditFilterField.ActionType, AuditFilterComparison.Equals, "ViewAuditLogs")]
        };

        var result = AuditConditionTreeValidator.Validate(condition).Should().ContainSingle().Which;

        result.ErrorMessage.Should().Be("Group nodes must not specify Value or Values.");
        result.MemberNames.Should().Equal("Condition");
    }

    [Fact]
    public void Validate_ShouldRejectEmptyGroup()
    {
        var condition = new AuditConditionNode
        {
            Kind = AuditConditionNodeKind.Group,
            Operator = AuditLogicalOperator.And,
            Children = []
        };

        var result = AuditConditionTreeValidator.Validate(condition).Should().ContainSingle().Which;

        result.ErrorMessage.Should().Be("Group nodes must contain at least one child node.");
        result.MemberNames.Should().Equal("Condition");
    }

    [Fact]
    public void Validate_ShouldRejectNotGroupWithMultipleChildren()
    {
        var condition = AuditConditionTrees.GroupNode(
            AuditLogicalOperator.Not,
            AuditConditionTrees.FieldNode(AuditFilterField.Category, AuditFilterComparison.Equals, "Security"),
            AuditConditionTrees.FieldNode(AuditFilterField.RiskLevel, AuditFilterComparison.Equals, "High"));

        var result = AuditConditionTreeValidator.Validate(condition).Should().ContainSingle().Which;

        result.ErrorMessage.Should().Be("Not groups must contain exactly one child node.");
        result.MemberNames.Should().Equal("Condition");
    }

    [Fact]
    public void Validate_ShouldRejectFieldNodeWithChildren()
    {
        var condition = new AuditConditionNode
        {
            Kind = AuditConditionNodeKind.Field,
            Field = AuditFilterField.ActionType,
            Comparison = AuditFilterComparison.Equals,
            Value = "ViewAuditLogs",
            Children = [AuditConditionTrees.FieldNode(AuditFilterField.Category, AuditFilterComparison.Equals, "Security")]
        };

        var result = AuditConditionTreeValidator.Validate(condition).Should().ContainSingle().Which;

        result.ErrorMessage.Should().Be("Field nodes must not contain child nodes.");
        result.MemberNames.Should().Equal("Condition");
    }

    [Fact]
    public void Validate_ShouldReportUnknownOrMissingField()
    {
        foreach (var field in new AuditFilterField?[] { (AuditFilterField)99, null })
        {
            var condition = new AuditConditionNode
            {
                Kind = AuditConditionNodeKind.Field,
                Field = field,
                Comparison = AuditFilterComparison.Equals,
                Value = "ViewAuditLogs"
            };

            var result = AuditConditionTreeValidator.Validate(condition).Should().ContainSingle().Which;

            result.ErrorMessage.Should().Be("Field must be a valid audit filter field.");
            result.MemberNames.Should().Equal("Condition.Field");
        }
    }

    [Fact]
    public void Validate_ShouldReportUnknownComparison()
    {
        var condition = new AuditConditionNode
        {
            Kind = AuditConditionNodeKind.Field,
            Field = AuditFilterField.ActionType,
            Comparison = (AuditFilterComparison)99,
            Value = "ViewAuditLogs"
        };

        var result = AuditConditionTreeValidator.Validate(condition).Should().ContainSingle().Which;

        result.ErrorMessage.Should().Be("Comparison must be 'Equals', 'NotEquals', 'In', 'GreaterThanOrEqual', or 'LessThanOrEqual'.");
        result.MemberNames.Should().Equal("Condition.Comparison");
    }

    [Fact]
    public void Validate_ShouldRestrictSearchTextToEqualsComparison()
    {
        var condition = AuditConditionTrees.FieldNode(AuditFilterField.SearchText, AuditFilterComparison.NotEquals, "payment");

        var result = AuditConditionTreeValidator.Validate(condition).Should().ContainSingle().Which;

        result.ErrorMessage.Should().Be("SearchText only supports the 'Equals' comparison.");
        result.MemberNames.Should().Equal("Condition.Comparison");
    }

    [Fact]
    public void Validate_ShouldRejectOrderingComparisonOnNonOrderableField()
    {
        var condition = AuditConditionTrees.FieldNode(AuditFilterField.ActionType, AuditFilterComparison.GreaterThanOrEqual, "ViewAuditLogs");

        var result = AuditConditionTreeValidator.Validate(condition).Should().ContainSingle().Which;

        result.ErrorMessage.Should().Be("Field 'ActionType' does not support ordering comparisons.");
        result.MemberNames.Should().Equal("Condition.Comparison");
    }

    [Fact]
    public void Validate_ShouldAcceptOrderingComparisonOnCreatedAt()
    {
        var condition = AuditConditionTrees.FieldNode(AuditFilterField.CreatedAt, AuditFilterComparison.GreaterThanOrEqual, "2026-01-01T00:00:00Z");

        AuditConditionTreeValidator.Validate(condition).Should().BeEmpty();
    }

    [Fact]
    public void Validate_ShouldAcceptOrderingComparisonOnRiskLevel()
    {
        var condition = AuditConditionTrees.FieldNode(AuditFilterField.RiskLevel, AuditFilterComparison.LessThanOrEqual, "High");

        AuditConditionTreeValidator.Validate(condition).Should().BeEmpty();
    }

    [Fact]
    public void Validate_ShouldRejectInComparisonWithoutValues()
    {
        var condition = new AuditConditionNode
        {
            Kind = AuditConditionNodeKind.Field,
            Field = AuditFilterField.ActionType,
            Comparison = AuditFilterComparison.In,
            Values = []
        };

        var result = AuditConditionTreeValidator.Validate(condition).Should().ContainSingle().Which;

        result.ErrorMessage.Should().Be("In comparisons require at least one value in Values.");
        result.MemberNames.Should().Equal("Condition.Values");
    }

    [Fact]
    public void Validate_ShouldRejectMissingValue()
    {
        foreach (var value in new string?[] { null, "   " })
        {
            var condition = AuditConditionTrees.FieldNode(AuditFilterField.ActionType, AuditFilterComparison.Equals, value);

            var result = AuditConditionTreeValidator.Validate(condition).Should().ContainSingle().Which;

            result.ErrorMessage.Should().Be("Field 'ActionType' requires a non-empty Value.");
            result.MemberNames.Should().Equal("Condition.Value");
        }
    }

    [Fact]
    public void Validate_ShouldRejectValueOver200Characters()
    {
        var condition = AuditConditionTrees.FieldNode(AuditFilterField.ActionType, AuditFilterComparison.Equals, new string('a', 201));

        var result = AuditConditionTreeValidator.Validate(condition).Should().ContainSingle().Which;

        result.ErrorMessage.Should().Be("Value must not exceed 200 characters.");
        result.MemberNames.Should().Equal("Condition.Value");
    }

    [Fact]
    public void Validate_ShouldRejectInValueOver200Characters()
    {
        var condition = AuditConditionTrees.FieldNodeIn(AuditFilterField.ActionType, new string('a', 201));

        var result = AuditConditionTreeValidator.Validate(condition).Should().ContainSingle().Which;

        result.ErrorMessage.Should().Be("Values must not exceed 200 characters.");
        result.MemberNames.Should().Equal("Condition.Values[0]");
    }

    [Fact]
    public void Validate_ShouldRejectBlankInValue()
    {
        var condition = AuditConditionTrees.FieldNodeIn(AuditFilterField.ActionType, "ViewAuditLogs", "   ");

        var result = AuditConditionTreeValidator.Validate(condition).Should().ContainSingle().Which;

        result.ErrorMessage.Should().Be("Values must not be empty.");
        result.MemberNames.Should().Equal("Condition.Values[1]");
    }

    [Fact]
    public void Validate_ShouldRejectInvalidGuidValue()
    {
        var condition = AuditConditionTrees.FieldNode(AuditFilterField.UserId, AuditFilterComparison.Equals, "xyz");

        var result = AuditConditionTreeValidator.Validate(condition).Should().ContainSingle().Which;

        result.ErrorMessage.Should().Be("Field 'UserId' requires a valid GUID.");
        result.MemberNames.Should().Equal("Condition.Value");
    }

    [Fact]
    public void Validate_ShouldRejectInvalidCategoryValue()
    {
        var condition = AuditConditionTrees.FieldNode(AuditFilterField.Category, AuditFilterComparison.Equals, "nope");

        var result = AuditConditionTreeValidator.Validate(condition).Should().ContainSingle().Which;

        result.ErrorMessage.Should().Be("Field 'Category' requires a valid category name.");
        result.MemberNames.Should().Equal("Condition.Value");
    }

    [Fact]
    public void Validate_ShouldRejectInvalidRiskLevelValue()
    {
        var condition = AuditConditionTrees.FieldNode(AuditFilterField.RiskLevel, AuditFilterComparison.Equals, "Extreme");

        var result = AuditConditionTreeValidator.Validate(condition).Should().ContainSingle().Which;

        result.ErrorMessage.Should().Be("Field 'RiskLevel' requires a valid risk level name.");
        result.MemberNames.Should().Equal("Condition.Value");
    }

    [Fact]
    public void Validate_ShouldRejectInvalidBooleanValue()
    {
        var condition = AuditConditionTrees.FieldNode(AuditFilterField.Success, AuditFilterComparison.Equals, "maybe");

        var result = AuditConditionTreeValidator.Validate(condition).Should().ContainSingle().Which;

        result.ErrorMessage.Should().Be("Field 'Success' requires a valid Boolean value.");
        result.MemberNames.Should().Equal("Condition.Value");
    }

    [Fact]
    public void Validate_ShouldRejectInvalidDateValue()
    {
        var condition = AuditConditionTrees.FieldNode(AuditFilterField.CreatedAt, AuditFilterComparison.Equals, "not-a-date");

        var result = AuditConditionTreeValidator.Validate(condition).Should().ContainSingle().Which;

        result.ErrorMessage.Should().Be("Field 'CreatedAt' requires a valid date.");
        result.MemberNames.Should().Equal("Condition.Value");
    }

    [Fact]
    public void Validate_ShouldAcceptCategoryNameCaseInsensitively()
    {
        var condition = AuditConditionTrees.FieldNode(AuditFilterField.Category, AuditFilterComparison.Equals, "security");

        AuditConditionTreeValidator.Validate(condition).Should().BeEmpty();
    }

    [Fact]
    public void Validate_ShouldRejectInvalidInValueFormat()
    {
        var condition = AuditConditionTrees.FieldNodeIn(
            AuditFilterField.UserId,
            "89f2ec26-0d5a-4f7b-9d3c-6b7a2f3e1a44",
            "xyz");

        var result = AuditConditionTreeValidator.Validate(condition).Should().ContainSingle().Which;

        result.ErrorMessage.Should().Be("Field 'UserId' requires a valid GUID.");
        result.MemberNames.Should().Equal("Condition.Values[1]");
    }

    [Fact]
    public void Validate_ShouldReportNestedErrorsWithIndexPaths()
    {
        var condition = AuditConditionTrees.GroupNode(
            AuditLogicalOperator.And,
            AuditConditionTrees.GroupNode(
                AuditLogicalOperator.Or,
                AuditConditionTrees.FieldNode(AuditFilterField.ActionType, AuditFilterComparison.Equals, null)));

        var result = AuditConditionTreeValidator.Validate(condition).Should().ContainSingle().Which;

        result.ErrorMessage.Should().Be("Field 'ActionType' requires a non-empty Value.");
        result.MemberNames.Should().Equal("Condition.Children[0].Children[0].Value");
    }

    [Fact]
    public void Validate_ShouldRejectTreeNestedTooDeep()
    {
        var condition = AuditConditionTrees.Nest(
            5,
            AuditConditionTrees.FieldNode(AuditFilterField.ActionType, AuditFilterComparison.Equals, "ViewAuditLogs"));

        var result = AuditConditionTreeValidator.Validate(condition).Should().ContainSingle().Which;

        result.ErrorMessage.Should().Be("Condition trees must not be nested deeper than 5 levels.");
        result.MemberNames.Should().Equal("Condition.Children[0].Children[0].Children[0].Children[0].Children[0]");
    }

    [Fact]
    public void Validate_ShouldAcceptTreeAtMaximumDepth()
    {
        var condition = AuditConditionTrees.Nest(
            4,
            AuditConditionTrees.FieldNode(AuditFilterField.ActionType, AuditFilterComparison.Equals, "ViewAuditLogs"));

        AuditConditionTreeValidator.Validate(condition).Should().BeEmpty();
    }

    [Fact]
    public void Validate_ShouldRejectTreesWithMoreThan50Nodes()
    {
        var condition = new AuditConditionNode
        {
            Kind = AuditConditionNodeKind.Group,
            Operator = AuditLogicalOperator.And,
            Children =
            [
                .. Enumerable.Range(0, 50).Select(_ => AuditConditionTrees.FieldNode(
                    AuditFilterField.ActionType,
                    AuditFilterComparison.Equals,
                    "ViewAuditLogs"))
            ]
        };

        var result = AuditConditionTreeValidator.Validate(condition).Should().ContainSingle().Which;

        result.ErrorMessage.Should().Be("Condition trees must contain at most 50 nodes.");
        result.MemberNames.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ShouldAcceptTreeWith50Nodes()
    {
        var condition = new AuditConditionNode
        {
            Kind = AuditConditionNodeKind.Group,
            Operator = AuditLogicalOperator.And,
            Children =
            [
                .. Enumerable.Range(0, 49).Select(_ => AuditConditionTrees.FieldNode(
                    AuditFilterField.ActionType,
                    AuditFilterComparison.Equals,
                    "ViewAuditLogs"))
            ]
        };

        AuditConditionTreeValidator.Validate(condition).Should().BeEmpty();
    }

    [Fact]
    public void Validate_ShouldAccumulateValueErrorsAcrossChildren()
    {
        var condition = AuditConditionTrees.GroupNode(
            AuditLogicalOperator.And,
            AuditConditionTrees.FieldNode(AuditFilterField.ActionType, AuditFilterComparison.Equals, null),
            AuditConditionTrees.FieldNode(AuditFilterField.Success, AuditFilterComparison.Equals, "maybe"));

        var results = AuditConditionTreeValidator.Validate(condition);

        results.Should().HaveCount(2);
        results.Should().Contain(r => r.ErrorMessage == "Field 'ActionType' requires a non-empty Value.");
        results.Should().Contain(r => r.ErrorMessage == "Field 'Success' requires a valid Boolean value.");
    }
}

#endregion

#region AuditConditionTreeTranslator Tests

public class AuditConditionTreeTranslatorTests
{
    private static readonly MethodInfo IlikeMethod = typeof(NpgsqlDbFunctionsExtensions).GetMethod(
        nameof(NpgsqlDbFunctionsExtensions.ILike),
        [typeof(DbFunctions), typeof(string), typeof(string)])!;

    private static readonly MethodInfo ClientIlikeMethod = typeof(AuditConditionTreeTranslatorTests).GetMethod(
        nameof(ClientIlike),
        BindingFlags.Static | BindingFlags.NonPublic)!;

    [Fact]
    public void Predicate_ShouldMatchActionTypeEquals()
    {
        var predicate = Compile(AuditConditionTrees.FieldNode(AuditFilterField.ActionType, AuditFilterComparison.Equals, "ViewAuditLogs"));

        var logs = new[]
        {
            CreateLog(),
            CreateLog(actionType: "DeleteAuditLog")
        };

        logs.Where(predicate).Should().ContainSingle()
            .Which.ActionType.Should().Be("ViewAuditLogs");
    }

    [Fact]
    public void Predicate_ShouldMatchActionTypeIn()
    {
        var predicate = Compile(AuditConditionTrees.FieldNodeIn(AuditFilterField.ActionType, "ViewAuditLogs", "ExportAuditLog"));

        var logs = new[]
        {
            CreateLog(),
            CreateLog(actionType: "ExportAuditLog"),
            CreateLog(actionType: "DeleteAuditLog")
        };

        logs.Where(predicate).Should().HaveCount(2);
    }

    [Fact]
    public void Predicate_ShouldExcludeNullUserIdsOnEquals()
    {
        var userId = Guid.Parse("89f2ec26-0d5a-4f7b-9d3c-6b7a2f3e1a44");
        var predicate = Compile(AuditConditionTrees.FieldNode(AuditFilterField.UserId, AuditFilterComparison.Equals, userId.ToString()));

        var logs = new[]
        {
            CreateLog(userId: userId),
            CreateLog(userId: Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7")),
            CreateLog()
        };

        logs.Where(predicate).Should().ContainSingle()
            .Which.UserId.Should().Be(userId);
    }

    [Fact]
    public void Predicate_ShouldMatchUserIdIn()
    {
        var first = Guid.Parse("89f2ec26-0d5a-4f7b-9d3c-6b7a2f3e1a44");
        var second = Guid.Parse("3f1a5c9e-2b47-4d86-9a15-6c8e7f2d0b31");
        var predicate = Compile(AuditConditionTrees.FieldNodeIn(
            AuditFilterField.UserId,
            first.ToString(),
            second.ToString()));

        var logs = new[]
        {
            CreateLog(userId: first),
            CreateLog(userId: second),
            CreateLog(userId: Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7")),
            CreateLog()
        };

        logs.Where(predicate).Should().HaveCount(2);
    }

    [Fact]
    public void Predicate_ShouldMatchCategoryCaseInsensitively()
    {
        var predicate = Compile(AuditConditionTrees.FieldNode(AuditFilterField.Category, AuditFilterComparison.Equals, "security"));

        var logs = new[]
        {
            CreateLog(category: AuditCategory.Security),
            CreateLog(category: AuditCategory.General)
        };

        logs.Where(predicate).Should().ContainSingle()
            .Which.Category.Should().Be(AuditCategory.Security);
    }

    [Fact]
    public void Predicate_ShouldMatchRiskLevelIn()
    {
        var predicate = Compile(AuditConditionTrees.FieldNodeIn(AuditFilterField.RiskLevel, "High", "Critical"));

        var logs = new[]
        {
            CreateLog(riskLevel: AuditRiskLevel.High),
            CreateLog(riskLevel: AuditRiskLevel.Critical),
            CreateLog(riskLevel: AuditRiskLevel.Low)
        };

        logs.Where(predicate).Should().HaveCount(2);
    }

    [Fact]
    public void Predicate_ShouldMatchRiskLevelAtLeast()
    {
        var predicate = Compile(AuditConditionTrees.FieldNode(AuditFilterField.RiskLevel, AuditFilterComparison.GreaterThanOrEqual, "High"));

        var logs = new[]
        {
            CreateLog(riskLevel: AuditRiskLevel.Low),
            CreateLog(riskLevel: AuditRiskLevel.High),
            CreateLog(riskLevel: AuditRiskLevel.Critical)
        };

        logs.Where(predicate).Should().HaveCount(2);
    }

    [Fact]
    public void Predicate_ShouldMatchRiskLevelAtMost()
    {
        var predicate = Compile(AuditConditionTrees.FieldNode(AuditFilterField.RiskLevel, AuditFilterComparison.LessThanOrEqual, "Medium"));

        var logs = new[]
        {
            CreateLog(riskLevel: AuditRiskLevel.Low),
            CreateLog(riskLevel: AuditRiskLevel.Medium),
            CreateLog(riskLevel: AuditRiskLevel.High)
        };

        logs.Where(predicate).Should().HaveCount(2);
    }

    [Fact]
    public void Predicate_ShouldMatchCreatedAtRange()
    {
        var predicate = Compile(AuditConditionTrees.GroupNode(
            AuditLogicalOperator.And,
            AuditConditionTrees.FieldNode(AuditFilterField.CreatedAt, AuditFilterComparison.GreaterThanOrEqual, "2026-01-01T00:00:00Z"),
            AuditConditionTrees.FieldNode(AuditFilterField.CreatedAt, AuditFilterComparison.LessThanOrEqual, "2026-12-31T23:59:59Z")));

        var inRange = CreateLog();
        var logs = new[]
        {
            inRange,
            CreateLog(createdAt: new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc)),
            CreateLog(createdAt: new DateTime(2027, 1, 1, 12, 0, 0, DateTimeKind.Utc))
        };

        logs.Where(predicate).Should().ContainSingle().Which.Should().BeSameAs(inRange);
    }

    [Fact]
    public void Predicate_ShouldMatchSuccessEqualsFalse()
    {
        var predicate = Compile(AuditConditionTrees.FieldNode(AuditFilterField.Success, AuditFilterComparison.Equals, "false"));

        var logs = new[]
        {
            CreateLog(),
            CreateLog(success: false)
        };

        logs.Where(predicate).Should().ContainSingle()
            .Which.Success.Should().BeFalse();
    }

    [Fact]
    public void Predicate_ShouldMatchSuccessNotEqualsTrue()
    {
        var predicate = Compile(AuditConditionTrees.FieldNode(AuditFilterField.Success, AuditFilterComparison.NotEquals, "true"));

        var logs = new[]
        {
            CreateLog(),
            CreateLog(success: false)
        };

        logs.Where(predicate).Should().ContainSingle()
            .Which.Success.Should().BeFalse();
    }

    [Fact]
    public void Predicate_ShouldMatchSuccessIn()
    {
        var predicate = Compile(AuditConditionTrees.FieldNodeIn(AuditFilterField.Success, "false"));

        var logs = new[]
        {
            CreateLog(),
            CreateLog(success: false)
        };

        logs.Where(predicate).Should().ContainSingle()
            .Which.Success.Should().BeFalse();
    }

    [Fact]
    public void Predicate_ShouldMatchIpAddressIn()
    {
        var predicate = Compile(AuditConditionTrees.FieldNodeIn(AuditFilterField.IpAddress, "10.0.0.1", "10.0.0.2"));

        var logs = new[]
        {
            CreateLog(ipAddress: "10.0.0.1"),
            CreateLog(ipAddress: "10.0.0.2"),
            CreateLog(ipAddress: "192.168.0.1"),
            CreateLog()
        };

        logs.Where(predicate).Should().HaveCount(2);
    }

    [Fact]
    public void Predicate_ShouldMatchResourceIdNotEquals()
    {
        var predicate = Compile(AuditConditionTrees.FieldNode(AuditFilterField.ResourceId, AuditFilterComparison.NotEquals, "order-1"));

        var logs = new[]
        {
            CreateLog(resourceId: "order-1"),
            CreateLog(resourceId: "order-2"),
            CreateLog(resourceId: "order-3")
        };

        logs.Where(predicate).Should().HaveCount(2);
    }

    [Fact]
    public void Predicate_ShouldSearchDescriptionAndMetadata()
    {
        var predicate = Compile(AuditConditionTrees.FieldNode(AuditFilterField.SearchText, AuditFilterComparison.Equals, "payment"));

        var logs = new[]
        {
            CreateLog(description: "Payment captured for order-42"),
            CreateLog(metadata: "{\"Reason\":\"payment_retry\"}"),
            CreateLog(description: "Login succeeded")
        };

        logs.Where(predicate).Should().HaveCount(2);
    }

    [Fact]
    public void Predicate_ShouldSearchCaseInsensitively()
    {
        var predicate = Compile(AuditConditionTrees.FieldNode(AuditFilterField.SearchText, AuditFilterComparison.Equals, "PAYMENT"));

        var logs = new[]
        {
            CreateLog(description: "Payment captured for order-42"),
            CreateLog(metadata: "{\"Reason\":\"payment_retry\"}"),
            CreateLog(description: "Login succeeded")
        };

        logs.Where(predicate).Should().HaveCount(2);
    }

    [Fact]
    public void Predicate_ShouldMatchEitherBranchOfOrGroup()
    {
        var predicate = Compile(AuditConditionTrees.GroupNode(
            AuditLogicalOperator.Or,
            AuditConditionTrees.FieldNode(AuditFilterField.ActionType, AuditFilterComparison.Equals, "DeleteAuditLog"),
            AuditConditionTrees.FieldNode(AuditFilterField.Category, AuditFilterComparison.Equals, "Security")));

        var logs = new[]
        {
            CreateLog(actionType: "DeleteAuditLog"),
            CreateLog(category: AuditCategory.Security),
            CreateLog()
        };

        logs.Where(predicate).Should().HaveCount(2);
    }

    [Fact]
    public void Predicate_ShouldNegateWithNotGroup()
    {
        var predicate = Compile(AuditConditionTrees.GroupNode(
            AuditLogicalOperator.Not,
            AuditConditionTrees.FieldNode(AuditFilterField.ActionType, AuditFilterComparison.Equals, "ViewAuditLogs")));

        var logs = new[]
        {
            CreateLog(),
            CreateLog(actionType: "DeleteAuditLog"),
            CreateLog(actionType: "ExportAuditLog")
        };

        logs.Where(predicate).Should().HaveCount(2);
    }

    [Fact]
    public void Predicate_ShouldEvaluateNestedGroups()
    {
        var predicate = Compile(AuditConditionTrees.GroupNode(
            AuditLogicalOperator.And,
            AuditConditionTrees.GroupNode(
                AuditLogicalOperator.Or,
                AuditConditionTrees.FieldNode(AuditFilterField.Category, AuditFilterComparison.Equals, "Security"),
                AuditConditionTrees.FieldNode(AuditFilterField.RiskLevel, AuditFilterComparison.GreaterThanOrEqual, "Critical")),
            AuditConditionTrees.GroupNode(
                AuditLogicalOperator.Not,
                AuditConditionTrees.FieldNode(AuditFilterField.Success, AuditFilterComparison.Equals, "true"))));

        var logs = new[]
        {
            CreateLog(category: AuditCategory.Security, riskLevel: AuditRiskLevel.Low, success: false),
            CreateLog(category: AuditCategory.General, riskLevel: AuditRiskLevel.Critical, success: false),
            CreateLog(category: AuditCategory.General, riskLevel: AuditRiskLevel.Low, success: false),
            CreateLog(category: AuditCategory.Security, riskLevel: AuditRiskLevel.Low, success: true)
        };

        logs.Where(predicate).Should().HaveCount(2);
    }

    [Fact]
    public void BuildPredicate_ShouldThrow_WhenGroupHasNoChildren()
    {
        var condition = new AuditConditionNode { Kind = AuditConditionNodeKind.Group, Operator = AuditLogicalOperator.And };

        Action act = () => AuditConditionTreeTranslator.BuildPredicate(condition);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Group nodes must contain at least one child node.");
    }

    [Fact]
    public void BuildPredicate_ShouldThrow_WhenNotGroupHasMultipleChildren()
    {
        var condition = AuditConditionTrees.GroupNode(
            AuditLogicalOperator.Not,
            AuditConditionTrees.FieldNode(AuditFilterField.Category, AuditFilterComparison.Equals, "Security"),
            AuditConditionTrees.FieldNode(AuditFilterField.RiskLevel, AuditFilterComparison.Equals, "High"));

        Action act = () => AuditConditionTreeTranslator.BuildPredicate(condition);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Not groups must contain exactly one child node.");
    }

    [Fact]
    public void BuildPredicate_ShouldThrow_WhenKindIsUnknown()
    {
        var condition = new AuditConditionNode { Kind = (AuditConditionNodeKind)99 };

        Action act = () => AuditConditionTreeTranslator.BuildPredicate(condition);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Unknown condition node kind '99'.");
    }

    [Fact]
    public void BuildPredicate_ShouldThrow_WhenFieldIsUnknown()
    {
        var condition = new AuditConditionNode
        {
            Kind = AuditConditionNodeKind.Field,
            Field = (AuditFilterField)99,
            Comparison = AuditFilterComparison.Equals,
            Value = "ViewAuditLogs"
        };

        Action act = () => AuditConditionTreeTranslator.BuildPredicate(condition);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Unknown filter field '99'.");
    }

    [Fact]
    public void BuildPredicate_ShouldThrow_WhenOperatorIsUnknown()
    {
        var condition = new AuditConditionNode
        {
            Kind = AuditConditionNodeKind.Group,
            Operator = (AuditLogicalOperator)99,
            Children = [AuditConditionTrees.FieldNode(AuditFilterField.ActionType, AuditFilterComparison.Equals, "ViewAuditLogs")]
        };

        Action act = () => AuditConditionTreeTranslator.BuildPredicate(condition);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Unknown logical operator '99'.");
    }

    [Fact]
    public void BuildPredicate_ShouldThrow_WhenSuccessUsesOrderingComparison()
    {
        var condition = AuditConditionTrees.FieldNode(AuditFilterField.Success, AuditFilterComparison.GreaterThanOrEqual, "true");

        Action act = () => AuditConditionTreeTranslator.BuildPredicate(condition);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Field 'Success' does not support the 'GreaterThanOrEqual' comparison.");
    }

    [Fact]
    public void BuildPredicate_ShouldThrow_WhenUserIdValueIsNotAGuid()
    {
        var condition = AuditConditionTrees.FieldNode(AuditFilterField.UserId, AuditFilterComparison.Equals, "xyz");

        Action act = () => AuditConditionTreeTranslator.BuildPredicate(condition);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("'xyz' is not a valid GUID.");
    }

    [Fact]
    public void BuildPredicate_ShouldThrow_WhenCategoryValueIsInvalid()
    {
        var condition = AuditConditionTrees.FieldNode(AuditFilterField.Category, AuditFilterComparison.Equals, "Nope");

        Action act = () => AuditConditionTreeTranslator.BuildPredicate(condition);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("'Nope' is not a valid AuditCategory value.");
    }

    [Fact]
    public void BuildPredicate_ShouldThrow_WhenSearchTextHasNoValue()
    {
        var condition = AuditConditionTrees.FieldNode(AuditFilterField.SearchText, AuditFilterComparison.Equals, null);

        Action act = () => AuditConditionTreeTranslator.BuildPredicate(condition);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("SearchText requires a non-empty Value.");
    }

    private static AuditLog CreateLog(
        string? actionType = "ViewAuditLogs",
        Guid? userId = null,
        string? resourceId = null,
        AuditCategory category = AuditCategory.General,
        AuditRiskLevel riskLevel = AuditRiskLevel.Low,
        bool success = true,
        DateTime? createdAt = null,
        string? ipAddress = null,
        string? description = null,
        string? metadata = null) =>
        new()
        {
            ActionType = actionType!,
            ResourceType = "System",
            ResourceId = resourceId,
            UserId = userId,
            Category = category,
            RiskLevel = riskLevel,
            Success = success,
            CreatedAt = createdAt ?? new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc),
            IpAddress = ipAddress,
            Description = description,
            Metadata = metadata
        };

    private static Func<AuditLog, bool> Compile(AuditConditionNode condition)
    {
        var predicate = AuditConditionTreeTranslator.BuildPredicate(condition);
        var rewritten = new ClientIlikeRewriter().Visit(predicate)!;
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
        for (var index = 0; index < pattern.Length; index++)
        {
            var character = pattern[index];
            if (character == '\\' && index + 1 < pattern.Length)
            {
                regex.Append(Regex.Escape(pattern[index + 1].ToString()));
                index++;
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

internal static class AuditConditionTrees
{
    internal static AuditConditionNode FieldNode(AuditFilterField field, AuditFilterComparison comparison, string? value = null) =>
        new() { Kind = AuditConditionNodeKind.Field, Field = field, Comparison = comparison, Value = value };

    internal static AuditConditionNode FieldNodeIn(AuditFilterField field, params string[] values) =>
        new() { Kind = AuditConditionNodeKind.Field, Field = field, Comparison = AuditFilterComparison.In, Values = [.. values] };

    internal static AuditConditionNode GroupNode(AuditLogicalOperator @operator, params AuditConditionNode[] children) =>
        new() { Kind = AuditConditionNodeKind.Group, Operator = @operator, Children = [.. children] };

    internal static AuditConditionNode Nest(int groups, AuditConditionNode leaf)
    {
        var node = leaf;
        for (var index = 0; index < groups; index++)
        {
            node = new AuditConditionNode
            {
                Kind = AuditConditionNodeKind.Group,
                Operator = AuditLogicalOperator.And,
                Children = [node]
            };
        }

        return node;
    }
}
