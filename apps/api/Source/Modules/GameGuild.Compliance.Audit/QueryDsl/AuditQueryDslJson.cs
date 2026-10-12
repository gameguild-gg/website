using System.Text.Json;

namespace GameGuild.Compliance.Audit;

/// <summary>
///     Serializes a parsed audit query DSL tree back to its canonical JSON form. Saved views
///     store this canonical form so that identical logical queries always persist identically,
///     which makes revision diffs meaningful and execute-by-id deterministic.
/// </summary>
public static class AuditQueryDslJson
{
    private static readonly Dictionary<AuditQueryComparison, string> OperatorNames = new()
    {
        [AuditQueryComparison.Eq] = "eq",
        [AuditQueryComparison.Ne] = "ne",
        [AuditQueryComparison.Gt] = "gt",
        [AuditQueryComparison.Gte] = "gte",
        [AuditQueryComparison.Lt] = "lt",
        [AuditQueryComparison.Lte] = "lte",
        [AuditQueryComparison.In] = "in",
        [AuditQueryComparison.Contains] = "contains",
        [AuditQueryComparison.StartsWith] = "startswith",
        [AuditQueryComparison.Wildcard] = "matches"
    };

    private static readonly Dictionary<AuditQueryField, string> CanonicalFieldNames = new()
    {
        [AuditQueryField.Actor] = "actor",
        [AuditQueryField.Action] = "action",
        [AuditQueryField.Timestamp] = "timestamp",
        [AuditQueryField.Tenant] = "tenant",
        [AuditQueryField.Resource] = "resource",
        [AuditQueryField.ResourceId] = "resourceid",
        [AuditQueryField.Severity] = "severity",
        [AuditQueryField.Source] = "source",
        [AuditQueryField.Success] = "success",
        [AuditQueryField.IpAddress] = "ip",
        [AuditQueryField.Email] = "email",
        [AuditQueryField.Description] = "description",
        [AuditQueryField.Metadata] = "metadata",
        [AuditQueryField.UserAgent] = "useragent",
        [AuditQueryField.Id] = "id"
    };

    /// <summary>Serialize a parsed query to canonical JSON.</summary>
    public static string Serialize(AuditQueryDslQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("filter");
            WriteNode(writer, query.Filter);
            writer.WriteString("sortBy", query.SortBy);
            writer.WriteString("sortDirection", query.SortDirection);
            if (query.Take.HasValue)
            {
                writer.WriteNumber("take", query.Take.Value);
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteNode(Utf8JsonWriter writer, AuditQueryNode node)
    {
        switch (node)
        {
            case AuditQueryGroup group:
                writer.WriteStartObject();
                writer.WritePropertyName(group.Logic == AuditQueryLogicalOperator.And ? "and" : "or");
                writer.WriteStartArray();
                foreach (var child in group.Children)
                {
                    WriteNode(writer, child);
                }

                writer.WriteEndArray();
                if (group.Negate)
                {
                    writer.WriteBoolean("negate", true);
                }

                writer.WriteEndObject();
                break;

            case AuditQueryCondition condition:
                writer.WriteStartObject();
                writer.WriteString("field", CanonicalFieldNames[condition.Field]);
                writer.WriteString("op", OperatorNames[condition.Operator]);
                writer.WritePropertyName("value");
                condition.Value.WriteTo(writer);
                if (condition.Negate)
                {
                    writer.WriteBoolean("negate", true);
                }

                writer.WriteEndObject();
                break;

            default:
                throw new InvalidOperationException($"Unknown audit query node type '{node.GetType().Name}'.");
        }
    }
}
