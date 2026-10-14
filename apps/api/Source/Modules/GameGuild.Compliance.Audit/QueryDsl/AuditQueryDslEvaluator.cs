using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GameGuild.Compliance.Audit;

/// <summary>
///     In-memory evaluator for the audit query DSL. Applies a parsed filter tree to
///     <see cref="UnifiedSecurityAuditEntry"/> rows. Pure logic, no I/O: unit-testable in isolation.
/// </summary>
public static class AuditQueryDslEvaluator
{
    /// <summary>Evaluate a filter tree against one unified audit entry.</summary>
    public static bool Matches(AuditQueryNode node, UnifiedSecurityAuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(entry);

        var result = node switch
        {
            AuditQueryGroup group => MatchesGroup(group, entry),
            AuditQueryCondition condition => MatchesCondition(condition, entry),
            _ => throw new InvalidOperationException($"Unknown audit query node type '{node.GetType().Name}'.")
        };

        return node.Negate ? !result : result;
    }

    private static bool MatchesGroup(AuditQueryGroup group, UnifiedSecurityAuditEntry entry) =>
        group.Logic == AuditQueryLogicalOperator.And
            ? group.Children.All(child => Matches(child, entry))
            : group.Children.Any(child => Matches(child, entry));

    private static bool MatchesCondition(AuditQueryCondition condition, UnifiedSecurityAuditEntry entry)
    {
        var matched = condition.Field switch
        {
            AuditQueryField.Actor => MatchActor(condition, entry),
            AuditQueryField.Action => MatchText(condition, entry.ActionType),
            AuditQueryField.Timestamp => MatchTimestamp(condition, entry.Timestamp),
            AuditQueryField.Tenant => MatchGuid(condition, entry.TenantId),
            AuditQueryField.Resource => MatchText(condition, entry.ResourceType),
            AuditQueryField.ResourceId => MatchText(condition, entry.ResourceId),
            AuditQueryField.Severity => MatchRiskLevel(condition, entry.RiskLevel),
            AuditQueryField.Source => MatchSource(condition, entry.SourceType),
            AuditQueryField.Success => MatchBoolean(condition, entry.Success),
            AuditQueryField.IpAddress => MatchText(condition, entry.IpAddress),
            AuditQueryField.Email => MatchText(condition, entry.UserEmail),
            AuditQueryField.Description => MatchText(condition, entry.Description),
            AuditQueryField.Metadata => MatchText(condition, entry.Metadata),
            AuditQueryField.UserAgent => MatchText(condition, entry.UserAgent),
            AuditQueryField.Id => MatchGuid(condition, entry.Id),
            _ => false
        };

        return matched;
    }

    private static bool MatchActor(AuditQueryCondition condition, UnifiedSecurityAuditEntry entry)
    {
        // 'actor' is the union of the user id and the captured email; a value matches either side.
        if (condition.Operator == AuditQueryComparison.In)
        {
            return condition.Value.EnumerateArray().Any(item => ActorEquals(item, entry));
        }

        return ActorMatches(condition.Operator, condition.Value, entry);
    }

    private static bool ActorEquals(JsonElement value, UnifiedSecurityAuditEntry entry)
    {
        var raw = ResolveRawText(value);
        if (Guid.TryParse(raw, out var guid))
        {
            return entry.UserId == guid;
        }

        return string.Equals(entry.UserEmail, raw, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ActorMatches(AuditQueryComparison comparison, JsonElement value, UnifiedSecurityAuditEntry entry)
    {
        var raw = ResolveRawText(value);
        if (Guid.TryParse(raw, out var guid))
        {
            return guid == entry.UserId;
        }

        if (entry.UserEmail is null)
        {
            return false;
        }

        return comparison switch
        {
            AuditQueryComparison.Eq => string.Equals(entry.UserEmail, raw, StringComparison.OrdinalIgnoreCase),
            AuditQueryComparison.Ne => !string.Equals(entry.UserEmail, raw, StringComparison.OrdinalIgnoreCase),
            AuditQueryComparison.Contains => entry.UserEmail.Contains(raw, StringComparison.OrdinalIgnoreCase),
            AuditQueryComparison.StartsWith => entry.UserEmail.StartsWith(raw, StringComparison.OrdinalIgnoreCase),
            AuditQueryComparison.Wildcard => WildcardMatches(raw, entry.UserEmail),
            _ => false
        };
    }

    private static bool MatchText(AuditQueryCondition condition, string? fieldValue)
    {
        if (condition.Operator == AuditQueryComparison.In)
        {
            if (fieldValue is null)
            {
                return false;
            }

            return condition.Value.EnumerateArray().Any(item =>
                string.Equals(fieldValue, ResolveRawText(item), StringComparison.OrdinalIgnoreCase));
        }

        var raw = ResolveRawText(condition.Value);
        if (fieldValue is null)
        {
            return condition.Operator == AuditQueryComparison.Ne;
        }

        return condition.Operator switch
        {
            AuditQueryComparison.Eq => string.Equals(fieldValue, raw, StringComparison.OrdinalIgnoreCase),
            AuditQueryComparison.Ne => !string.Equals(fieldValue, raw, StringComparison.OrdinalIgnoreCase),
            AuditQueryComparison.Contains => fieldValue.Contains(raw, StringComparison.OrdinalIgnoreCase),
            AuditQueryComparison.StartsWith => fieldValue.StartsWith(raw, StringComparison.OrdinalIgnoreCase),
            AuditQueryComparison.Wildcard => WildcardMatches(raw, fieldValue),
            _ => false
        };
    }

    private static bool MatchTimestamp(AuditQueryCondition condition, DateTime fieldValue)
    {
        if (condition.Operator == AuditQueryComparison.In)
        {
            return condition.Value.EnumerateArray()
                .Any(item => fieldValue == ParseTimestamp(item));
        }

        var expected = ParseTimestamp(condition.Value);
        return condition.Operator switch
        {
            AuditQueryComparison.Eq => fieldValue == expected,
            AuditQueryComparison.Ne => fieldValue != expected,
            AuditQueryComparison.Gt => fieldValue > expected,
            AuditQueryComparison.Gte => fieldValue >= expected,
            AuditQueryComparison.Lt => fieldValue < expected,
            AuditQueryComparison.Lte => fieldValue <= expected,
            _ => false
        };
    }

    private static bool MatchGuid(AuditQueryCondition condition, Guid? fieldValue)
    {
        if (condition.Operator == AuditQueryComparison.In)
        {
            if (fieldValue is null)
            {
                return false;
            }

            return condition.Value.EnumerateArray().Any(item => fieldValue.Value == ParseGuid(item));
        }

        var expected = ParseGuid(condition.Value);
        if (fieldValue is null)
        {
            // A missing GUID only differs from any concrete requested value.
            return condition.Operator == AuditQueryComparison.Ne;
        }

        return condition.Operator switch
        {
            AuditQueryComparison.Eq => fieldValue.Value == expected,
            AuditQueryComparison.Ne => fieldValue.Value != expected,
            _ => false
        };
    }

    private static bool MatchRiskLevel(AuditQueryCondition condition, AuditRiskLevel fieldValue)
    {
        if (condition.Operator == AuditQueryComparison.In)
        {
            return condition.Value.EnumerateArray()
                .Any(item => fieldValue == ParseRiskLevel(item));
        }

        var expected = ParseRiskLevel(condition.Value);
        return condition.Operator switch
        {
            AuditQueryComparison.Eq => fieldValue == expected,
            AuditQueryComparison.Ne => fieldValue != expected,
            _ => false
        };
    }

    private static bool MatchSource(AuditQueryCondition condition, SecurityAuditSourceType fieldValue)
    {
        if (condition.Operator == AuditQueryComparison.In)
        {
            return condition.Value.EnumerateArray()
                .Any(item => fieldValue == ParseSource(item));
        }

        var expected = ParseSource(condition.Value);
        return condition.Operator switch
        {
            AuditQueryComparison.Eq => fieldValue == expected,
            AuditQueryComparison.Ne => fieldValue != expected,
            _ => false
        };
    }

    private static bool MatchBoolean(AuditQueryCondition condition, bool fieldValue)
    {
        if (condition.Operator == AuditQueryComparison.In)
        {
            return condition.Value.EnumerateArray().Any(item => fieldValue == item.GetBoolean());
        }

        return condition.Operator switch
        {
            AuditQueryComparison.Eq => fieldValue == condition.Value.GetBoolean(),
            AuditQueryComparison.Ne => fieldValue != condition.Value.GetBoolean(),
            _ => false
        };
    }

    private static string ResolveRawText(JsonElement value) =>
        value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();

    private static DateTime ParseTimestamp(JsonElement value) =>
        DateTime.Parse(ResolveRawText(value), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

    private static Guid ParseGuid(JsonElement value) => Guid.Parse(ResolveRawText(value));

    private static AuditRiskLevel ParseRiskLevel(JsonElement value) =>
        Enum.Parse<AuditRiskLevel>(ResolveRawText(value), ignoreCase: true);

    private static SecurityAuditSourceType ParseSource(JsonElement value) =>
        Enum.Parse<SecurityAuditSourceType>(ResolveRawText(value), ignoreCase: true);

    /// <summary>Translate a '*' wildcard pattern into an anchored, case-insensitive regex match.</summary>
    private static bool WildcardMatches(string pattern, string value)
    {
        var escaped = Regex.Escape(pattern).Replace("\\*", ".*", StringComparison.Ordinal);
        return Regex.IsMatch(value, $"^{escaped}$", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
    }
}
