using System.Linq.Expressions;
using System.Reflection;

namespace GameGuild.Identity.Provisioning.Scim.Filtering;

/// <summary>
///     Attribute a filter may target on a projection view. String comparisons are
///     case-insensitive because the mapped platform attributes (username, email) are
///     case-insensitive by definition; identifiers compare exactly.
/// </summary>
public sealed record ScimFilterAttribute(
    string Name,
    ScimFilterAttributeType Type,
    bool CaseSensitive);

public enum ScimFilterAttributeType
{
    String,
    Boolean,
    Identifier
}

/// <summary>
///     Compiles a parsed <see cref="ScimFilter"/> into an expression predicate over a
///     projection view. The same expression tree works for in-memory sequences and for
///     EF Core <c>Where</c> translation.
/// </summary>
public static class ScimFilterEvaluator
{
    /// <summary>
    ///     Builds a predicate expression for the filter. <paramref name="resolver"/> maps
    ///     a SCIM attribute name to a member of <typeparamref name="TView"/>; unknown
    ///     attributes fail with <c>invalidFilter</c>.
    /// </summary>
    public static Expression<Func<TView, bool>> BuildPredicate<TView>(
        ScimFilter filter,
        IReadOnlyDictionary<string, (PropertyInfo Property, ScimFilterAttribute Attribute)> resolver)
    {
        var parameter = Expression.Parameter(typeof(TView), "view");
        var body = BuildExpression<TView>(filter, parameter, resolver);
        return Expression.Lambda<Func<TView, bool>>(body, parameter);
    }

    private static Expression BuildExpression<TView>(
        ScimFilter filter,
        ParameterExpression parameter,
        IReadOnlyDictionary<string, (PropertyInfo Property, ScimFilterAttribute Attribute)> resolver) => filter switch
    {
        ScimFilter.And and => Expression.AndAlso(
            BuildExpression<TView>(and.Left, parameter, resolver),
            BuildExpression<TView>(and.Right, parameter, resolver)),
        ScimFilter.Or or => Expression.OrElse(
            BuildExpression<TView>(or.Left, parameter, resolver),
            BuildExpression<TView>(or.Right, parameter, resolver)),
        ScimFilter.Not not => Expression.Not(BuildExpression<TView>(not.Inner, parameter, resolver)),
        ScimFilter.Compare compare => BuildComparison<TView>(compare, parameter, resolver),
        _ => throw ScimException.InvalidFilter("Unsupported filter expression.")
    };

    private static Expression BuildComparison<TView>(
        ScimFilter.Compare compare,
        ParameterExpression parameter,
        IReadOnlyDictionary<string, (PropertyInfo Property, ScimFilterAttribute Attribute)> resolver)
    {
        if (!resolver.TryGetValue(compare.Attribute.ToLowerInvariant(), out var mapped))
        {
            throw ScimException.InvalidFilter(
                $"The attribute '{compare.Attribute}' is not filterable on this resource type.");
        }

        var (property, attribute) = mapped;
        var access = Expression.Property(parameter, property);

        return attribute.Type switch
        {
            ScimFilterAttributeType.Boolean => BuildBooleanComparison(compare, attribute, access),
            ScimFilterAttributeType.Identifier => BuildIdentifierComparison(compare, attribute, access),
            _ => BuildStringComparison(compare, attribute, access)
        };
    }

    private static Expression BuildBooleanComparison(
        ScimFilter.Compare compare,
        ScimFilterAttribute attribute,
        MemberExpression access)
    {
        if (compare.Operator == ScimFilterOperator.Pr)
        {
            return Expression.Constant(true);
        }

        if (compare.Operator is not (ScimFilterOperator.Eq or ScimFilterOperator.Ne))
        {
            throw ScimException.InvalidFilter(
                $"The boolean attribute '{attribute.Name}' only supports the 'eq' and 'ne' operators.");
        }

        if (compare.Value is not { } rawValue || !bool.TryParse(rawValue, out var booleanValue))
        {
            throw ScimException.InvalidFilter(
                $"The attribute '{attribute.Name}' requires a boolean value ('true' or 'false').");
        }

        var equals = Expression.Equal(access, Expression.Constant(booleanValue));
        return compare.Operator == ScimFilterOperator.Eq ? equals : Expression.Not(equals);
    }

    private static Expression BuildIdentifierComparison(
        ScimFilter.Compare compare,
        ScimFilterAttribute attribute,
        MemberExpression access)
    {
        if (compare.Operator == ScimFilterOperator.Pr)
        {
            return Expression.Constant(true);
        }

        if (compare.Operator is not (ScimFilterOperator.Eq or ScimFilterOperator.Ne))
        {
            throw ScimException.InvalidFilter(
                $"The identifier attribute '{attribute.Name}' only supports the 'eq' and 'ne' operators.");
        }

        if (compare.Value is not { } idValue || !Guid.TryParse(idValue, out var parsed))
        {
            throw ScimException.InvalidFilter($"The attribute '{attribute.Name}' requires a valid identifier value.");
        }

        var equals = Expression.Equal(access, Expression.Constant(parsed));
        return compare.Operator == ScimFilterOperator.Eq ? equals : Expression.Not(equals);
    }

    private static Expression BuildStringComparison(
        ScimFilter.Compare compare,
        ScimFilterAttribute attribute,
        MemberExpression access)
    {
        var left = (Expression)Expression.Coalesce(access, Expression.Constant(string.Empty));
        var right = (Expression)Expression.Constant(compare.Value ?? string.Empty);

        if (!attribute.CaseSensitive)
        {
            left = Expression.Call(left, StringToLowerMethod);
            right = Expression.Constant((compare.Value ?? string.Empty).ToLowerInvariant());
        }

        if (compare.Operator == ScimFilterOperator.Pr)
        {
            var notEmpty = Expression.NotEqual(left, Expression.Constant(string.Empty));
            return Expression.AndAlso(Expression.NotEqual(access, Expression.Constant(null, typeof(string))), notEmpty);
        }

        if (compare.Value is null)
        {
            throw ScimException.InvalidFilter(
                $"The '{compare.Operator.ToString().ToLowerInvariant()}' comparison is missing its value.");
        }

        return compare.Operator switch
        {
            ScimFilterOperator.Eq => Expression.Equal(left, right),
            ScimFilterOperator.Ne => Expression.NotEqual(left, right),
            ScimFilterOperator.Co => Expression.Call(left, StringContainsMethod, right),
            ScimFilterOperator.Sw => Expression.Call(left, StringStartsWithMethod, right),
            ScimFilterOperator.Ew => Expression.Call(left, StringEndsWithMethod, right),
            _ => throw ScimException.InvalidFilter($"Unsupported comparison operator '{compare.Operator}'.")
        };
    }

    private static readonly MethodInfo StringContainsMethod =
        typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;

    private static readonly MethodInfo StringStartsWithMethod =
        typeof(string).GetMethod(nameof(string.StartsWith), [typeof(string)])!;

    private static readonly MethodInfo StringEndsWithMethod =
        typeof(string).GetMethod(nameof(string.EndsWith), [typeof(string)])!;

    private static readonly MethodInfo StringToLowerMethod =
        typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
}
