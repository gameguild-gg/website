using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace GameGuild.Compliance.Audit;

/// <summary>
/// Validates the structure and the values of an audit log condition tree.
/// </summary>
public static class AuditConditionTreeValidator
{
    private const int MaxDepth = 5;
    private const int MaxNodes = 50;
    private const int MaxValueLength = 200;

    /// <summary>
    /// Validates the given condition tree and returns every violation found.
    /// </summary>
    /// <param name="condition">The root node of the condition tree, if any.</param>
    /// <param name="memberName">The name reported for the root node in validation results.</param>
    /// <returns>All validation violations found in the tree.</returns>
    public static IReadOnlyList<ValidationResult> Validate(AuditConditionNode? condition, string memberName = "Condition")
    {
        if (condition is null)
        {
            return [];
        }

        var state = new ValidationState();
        ValidateNode(condition, memberName, 1, state);
        return state.Results;
    }

    private static bool ValidateNode(AuditConditionNode node, string path, int depth, ValidationState state)
    {
        if (depth > MaxDepth)
        {
            state.Results.Add(new ValidationResult("Condition trees must not be nested deeper than 5 levels.", [path]));
            return false;
        }

        state.NodeCount++;
        if (state.NodeCount > MaxNodes)
        {
            state.ReportNodeLimit();
            return false;
        }

        if (node.Kind is not AuditConditionNodeKind kind || !Enum.IsDefined(kind))
        {
            state.Results.Add(new ValidationResult("Kind must be 'Group' or 'Field'.", [Combine(path, "Kind")]));
            return false;
        }

        return kind == AuditConditionNodeKind.Group
            ? ValidateGroup(node, path, depth, state)
            : ValidateField(node, path, state);
    }

    private static bool ValidateGroup(AuditConditionNode node, string path, int depth, ValidationState state)
    {
        if (node.Operator is not AuditLogicalOperator logicalOperator || !Enum.IsDefined(logicalOperator))
        {
            state.Results.Add(new ValidationResult("Operator must be 'And', 'Or', or 'Not' for Group nodes.", [Combine(path, "Operator")]));
            return false;
        }

        if (!string.IsNullOrWhiteSpace(node.Value) || node.Values.Count > 0)
        {
            state.Results.Add(new ValidationResult("Group nodes must not specify Value or Values.", [path]));
            return false;
        }

        if (node.Children is not { Count: > 0 })
        {
            state.Results.Add(new ValidationResult("Group nodes must contain at least one child node.", [path]));
            return false;
        }

        if (logicalOperator == AuditLogicalOperator.Not && node.Children.Count > 1)
        {
            state.Results.Add(new ValidationResult("Not groups must contain exactly one child node.", [path]));
            return false;
        }

        for (var index = 0; index < node.Children.Count; index++)
        {
            if (!ValidateNode(node.Children[index], Combine(path, $"Children[{index}]"), depth + 1, state))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ValidateField(AuditConditionNode node, string path, ValidationState state)
    {
        if (node.Children.Count > 0)
        {
            state.Results.Add(new ValidationResult("Field nodes must not contain child nodes.", [path]));
            return false;
        }

        if (node.Field is not AuditFilterField field || !Enum.IsDefined(field))
        {
            state.Results.Add(new ValidationResult("Field must be a valid audit filter field.", [Combine(path, "Field")]));
            return false;
        }

        if (node.Comparison is not AuditFilterComparison comparison || !Enum.IsDefined(comparison))
        {
            state.Results.Add(new ValidationResult("Comparison must be 'Equals', 'NotEquals', 'In', 'GreaterThanOrEqual', or 'LessThanOrEqual'.", [Combine(path, "Comparison")]));
            return false;
        }

        if (field == AuditFilterField.SearchText && comparison != AuditFilterComparison.Equals)
        {
            state.Results.Add(new ValidationResult("SearchText only supports the 'Equals' comparison.", [Combine(path, "Comparison")]));
            return false;
        }

        if ((comparison == AuditFilterComparison.GreaterThanOrEqual || comparison == AuditFilterComparison.LessThanOrEqual)
            && field is not (AuditFilterField.CreatedAt or AuditFilterField.RiskLevel))
        {
            state.Results.Add(new ValidationResult($"Field '{field}' does not support ordering comparisons.", [Combine(path, "Comparison")]));
            return false;
        }

        if (comparison == AuditFilterComparison.In)
        {
            return ValidateInComparison(node, field, path, state);
        }

        if (string.IsNullOrWhiteSpace(node.Value))
        {
            state.Results.Add(new ValidationResult($"Field '{field}' requires a non-empty Value.", [Combine(path, "Value")]));
            return true;
        }

        if (node.Value.Length > MaxValueLength)
        {
            state.Results.Add(new ValidationResult("Value must not exceed 200 characters.", [Combine(path, "Value")]));
            return true;
        }

        if (!IsValidFormat(field, node.Value))
        {
            state.Results.Add(new ValidationResult(FormatErrorMessage(field), [Combine(path, "Value")]));
            return true;
        }

        return true;
    }

    private static bool ValidateInComparison(AuditConditionNode node, AuditFilterField field, string path, ValidationState state)
    {
        if (node.Values.Count == 0)
        {
            state.Results.Add(new ValidationResult("In comparisons require at least one value in Values.", [Combine(path, "Values")]));
            return false;
        }

        for (var index = 0; index < node.Values.Count; index++)
        {
            var value = node.Values[index];
            var valuePath = Combine(path, $"Values[{index}]");

            if (string.IsNullOrWhiteSpace(value))
            {
                state.Results.Add(new ValidationResult("Values must not be empty.", [valuePath]));
                return false;
            }

            if (value.Length > MaxValueLength)
            {
                state.Results.Add(new ValidationResult("Values must not exceed 200 characters.", [valuePath]));
                return false;
            }

            if (!IsValidFormat(field, value))
            {
                state.Results.Add(new ValidationResult(FormatErrorMessage(field), [valuePath]));
                return false;
            }
        }

        return true;
    }

    private static bool IsValidFormat(AuditFilterField field, string value) => field switch
    {
        AuditFilterField.UserId => Guid.TryParse(value, out _),
        AuditFilterField.Category => Enum.TryParse<AuditCategory>(value, ignoreCase: true, out var category) && Enum.IsDefined(category),
        AuditFilterField.RiskLevel => Enum.TryParse<AuditRiskLevel>(value, ignoreCase: true, out var riskLevel) && Enum.IsDefined(riskLevel),
        AuditFilterField.Success => bool.TryParse(value, out _),
        AuditFilterField.CreatedAt => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _),
        _ => true,
    };

    private static string FormatErrorMessage(AuditFilterField field) => field switch
    {
        AuditFilterField.UserId => $"Field '{field}' requires a valid GUID.",
        AuditFilterField.Category => $"Field '{field}' requires a valid category name.",
        AuditFilterField.RiskLevel => $"Field '{field}' requires a valid risk level name.",
        AuditFilterField.Success => $"Field '{field}' requires a valid Boolean value.",
        AuditFilterField.CreatedAt => $"Field '{field}' requires a valid date.",
        _ => $"Field '{field}' requires a valid value.",
    };

    private static string Combine(string parent, string member) => string.IsNullOrEmpty(parent) ? member : $"{parent}.{member}";

    private sealed class ValidationState
    {
        public List<ValidationResult> Results { get; } = [];

        public int NodeCount { get; set; }

        public bool NodeLimitReported { get; set; }

        public void ReportNodeLimit()
        {
            if (NodeLimitReported)
            {
                return;
            }

            NodeLimitReported = true;
            Results.Add(new ValidationResult("Condition trees must contain at most 50 nodes."));
        }
    }
}
