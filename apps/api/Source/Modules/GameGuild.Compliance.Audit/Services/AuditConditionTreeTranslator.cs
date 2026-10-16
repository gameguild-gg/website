using System.Globalization;
using System.Linq.Expressions;

namespace GameGuild.Compliance.Audit;

/// <summary>
/// Translates a validated audit log condition tree into an entity framework predicate.
/// </summary>
public static class AuditConditionTreeTranslator
{
    /// <summary>
    /// Builds a predicate matching the given condition tree. The tree is expected to have
    /// passed <see cref="AuditConditionTreeValidator.Validate"/>; invalid trees throw.
    /// </summary>
    /// <param name="condition">The root node of the condition tree.</param>
    /// <returns>A predicate over <see cref="AuditLog"/> entries.</returns>
    public static Expression<Func<AuditLog, bool>> BuildPredicate(AuditConditionNode condition)
    {
        var log = Expression.Parameter(typeof(AuditLog), "log");
        var body = BuildNode(condition, log);
        return Expression.Lambda<Func<AuditLog, bool>>(body, log);
    }

    private static Expression BuildNode(AuditConditionNode node, ParameterExpression log)
    {
        return node.Kind switch
        {
            AuditConditionNodeKind.Group => BuildGroup(node, log),
            AuditConditionNodeKind.Field => BuildField(node, log),
            _ => throw new InvalidOperationException($"Unknown condition node kind '{node.Kind}'."),
        };
    }

    private static Expression BuildGroup(AuditConditionNode node, ParameterExpression log)
    {
        if (node.Children is not { Count: > 0 })
        {
            throw new InvalidOperationException("Group nodes must contain at least one child node.");
        }

        switch (node.Operator)
        {
            case AuditLogicalOperator.Not:
                if (node.Children.Count != 1)
                {
                    throw new InvalidOperationException("Not groups must contain exactly one child node.");
                }

                return Expression.Not(BuildNode(node.Children[0], log));

            case AuditLogicalOperator.And:
                return node.Children
                    .Select(child => BuildNode(child, log))
                    .Aggregate((left, right) => Expression.MakeBinary(ExpressionType.AndAlso, left, right));

            case AuditLogicalOperator.Or:
                return node.Children
                    .Select(child => BuildNode(child, log))
                    .Aggregate((left, right) => Expression.MakeBinary(ExpressionType.OrElse, left, right));

            default:
                throw new InvalidOperationException($"Unknown logical operator '{node.Operator}'.");
        }
    }

    private static Expression BuildField(AuditConditionNode node, ParameterExpression log)
    {
        return node.Field switch
        {
            AuditFilterField.UserId => BuildUserIdBody(node, log),
            AuditFilterField.ActionType => BuildStringBody(node, nameof(AuditLog.ActionType), log),
            AuditFilterField.ResourceType => BuildStringBody(node, nameof(AuditLog.ResourceType), log),
            AuditFilterField.ResourceId => BuildStringBody(node, nameof(AuditLog.ResourceId), log),
            AuditFilterField.IpAddress => BuildStringBody(node, nameof(AuditLog.IpAddress), log),
            AuditFilterField.Category => BuildEnumBody<AuditCategory>(node, nameof(AuditLog.Category), log),
            AuditFilterField.RiskLevel => BuildRiskLevelBody(node, log),
            AuditFilterField.Success => BuildSuccessBody(node, log),
            AuditFilterField.CreatedAt => BuildCreatedAtBody(node, log),
            AuditFilterField.SearchText => BuildSearchTextBody(node, log),
            _ => throw new InvalidOperationException($"Unknown filter field '{node.Field}'."),
        };
    }

    private static Expression BuildStringBody(AuditConditionNode node, string propertyName, ParameterExpression log)
    {
        var property = Expression.Property(log, propertyName);

        return node.Comparison switch
        {
            AuditFilterComparison.Equals => Expression.Equal(property, Expression.Constant(node.Value, typeof(string))),
            AuditFilterComparison.NotEquals => Expression.NotEqual(property, Expression.Constant(node.Value, typeof(string))),
            AuditFilterComparison.In => BuildSetContains(property, node.Values.ToArray()),
            _ => throw new InvalidOperationException($"Field '{node.Field}' does not support the '{node.Comparison}' comparison."),
        };
    }

    private static Expression BuildUserIdBody(AuditConditionNode node, ParameterExpression log)
    {
        var property = Expression.Property(log, nameof(AuditLog.UserId));

        return node.Comparison switch
        {
            AuditFilterComparison.Equals => Expression.Equal(property, Expression.Constant((Guid?)ParseGuid(node.Value!), typeof(Guid?))),
            AuditFilterComparison.NotEquals => Expression.NotEqual(property, Expression.Constant((Guid?)ParseGuid(node.Value!), typeof(Guid?))),
            AuditFilterComparison.In => BuildSetContains(property, node.Values.Select(value => (Guid?)ParseGuid(value)).ToArray()),
            _ => throw new InvalidOperationException($"Field '{node.Field}' does not support the '{node.Comparison}' comparison."),
        };
    }

    private static Expression BuildEnumBody<TEnum>(AuditConditionNode node, string propertyName, ParameterExpression log)
        where TEnum : struct, Enum
    {
        var property = Expression.Property(log, propertyName);

        return node.Comparison switch
        {
            AuditFilterComparison.Equals => Expression.Equal(property, Expression.Constant(ParseEnum<TEnum>(node.Value!))),
            AuditFilterComparison.NotEquals => Expression.NotEqual(property, Expression.Constant(ParseEnum<TEnum>(node.Value!))),
            AuditFilterComparison.In => BuildSetContains(property, node.Values.Select(value => ParseEnum<TEnum>(value)).ToArray()),
            _ => throw new InvalidOperationException($"Field '{node.Field}' does not support the '{node.Comparison}' comparison."),
        };
    }

    private static Expression BuildRiskLevelBody(AuditConditionNode node, ParameterExpression log)
    {
        if (node.Comparison is AuditFilterComparison.GreaterThanOrEqual or AuditFilterComparison.LessThanOrEqual)
        {
            var property = Expression.Convert(Expression.Property(log, nameof(AuditLog.RiskLevel)), typeof(int));
            var constant = Expression.Constant((int)ParseEnum<AuditRiskLevel>(node.Value!));

            return node.Comparison == AuditFilterComparison.GreaterThanOrEqual
                ? Expression.GreaterThanOrEqual(property, constant)
                : Expression.LessThanOrEqual(property, constant);
        }

        return BuildEnumBody<AuditRiskLevel>(node, nameof(AuditLog.RiskLevel), log);
    }

    private static Expression BuildSuccessBody(AuditConditionNode node, ParameterExpression log)
    {
        var property = Expression.Property(log, nameof(AuditLog.Success));

        return node.Comparison switch
        {
            AuditFilterComparison.Equals => Expression.Equal(property, Expression.Constant(ParseBoolean(node.Value!))),
            AuditFilterComparison.NotEquals => Expression.NotEqual(property, Expression.Constant(ParseBoolean(node.Value!))),
            AuditFilterComparison.In => BuildSetContains(property, node.Values.Select(ParseBoolean).ToArray()),
            _ => throw new InvalidOperationException($"Field '{node.Field}' does not support the '{node.Comparison}' comparison."),
        };
    }

    private static Expression BuildCreatedAtBody(AuditConditionNode node, ParameterExpression log)
    {
        var property = Expression.Property(log, nameof(AuditLog.CreatedAt));

        return node.Comparison switch
        {
            AuditFilterComparison.Equals => Expression.Equal(property, Expression.Constant(ParseDateTime(node.Value!))),
            AuditFilterComparison.NotEquals => Expression.NotEqual(property, Expression.Constant(ParseDateTime(node.Value!))),
            AuditFilterComparison.In => BuildSetContains(property, node.Values.Select(ParseDateTime).ToArray()),
            AuditFilterComparison.GreaterThanOrEqual => Expression.GreaterThanOrEqual(property, Expression.Constant(ParseDateTime(node.Value!))),
            AuditFilterComparison.LessThanOrEqual => Expression.LessThanOrEqual(property, Expression.Constant(ParseDateTime(node.Value!))),
            _ => throw new InvalidOperationException($"Field '{node.Field}' does not support the '{node.Comparison}' comparison."),
        };
    }

    private static Expression BuildSearchTextBody(AuditConditionNode node, ParameterExpression log)
    {
        var predicate = AuditLogSearchFilter.BuildPredicate(node.Value)
            ?? throw new InvalidOperationException("SearchText requires a non-empty Value.");

        return new ParameterReplacer(predicate.Parameters[0], log).Visit(predicate.Body);
    }

    private static Expression BuildSetContains(MemberExpression property, Array values)
    {
        return Expression.Call(
            typeof(Enumerable),
            nameof(Enumerable.Contains),
            [values.GetType().GetElementType()!],
            Expression.Constant(values),
            property);
    }

    private static Guid ParseGuid(string value) =>
        Guid.TryParse(value, out var result) ? result : throw new InvalidOperationException($"'{value}' is not a valid GUID.");

    private static TEnum ParseEnum<TEnum>(string value)
        where TEnum : struct, Enum
    {
        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var result) && Enum.IsDefined(result)
            ? result
            : throw new InvalidOperationException($"'{value}' is not a valid {typeof(TEnum).Name} value.");
    }

    private static bool ParseBoolean(string value) =>
        bool.TryParse(value, out var result) ? result : throw new InvalidOperationException($"'{value}' is not a valid Boolean value.");

    private static DateTime ParseDateTime(string value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result)
            ? result
            : throw new InvalidOperationException($"'{value}' is not a valid date.");

    private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == from ? to : base.VisitParameter(node);
        }
    }
}
