using System.Text.Json;

namespace GameGuild.Compliance.Audit;

/// <summary>
///     Root of a parsed audit query DSL expression. A query is a typed filter tree over the
///     unified audit entry model (authentication attempts, permission audit logs, and general
///     audit logs), plus optional presentation hints (sorting / page size).
/// </summary>
public sealed class AuditQueryDslQuery
{
    /// <summary>
    ///     The filter tree that every returned row must satisfy. Never null once parsed.
    /// </summary>
    public required AuditQueryNode Filter { get; init; }

    /// <summary>
    ///     Sort field; only <c>timestamp</c> is supported today. Unknown values fail validation.
    /// </summary>
    public string SortBy { get; init; } = AuditQuerySortFields.Timestamp;

    /// <summary>
    ///     Sort direction: <c>asc</c> or <c>desc</c> (default, newest first).
    /// </summary>
    public string SortDirection { get; init; } = "desc";

    /// <summary>
    ///     Suggested page size stored with saved views; executors clamp it to the configured maximum.
    /// </summary>
    public int? Take { get; init; }
}

/// <summary>Supported sort fields for the audit query DSL.</summary>
public static class AuditQuerySortFields
{
    public const string Timestamp = "timestamp";

    /// <summary>All accepted sort field names (lowercase).</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Timestamp
    };
}

/// <summary>Base class for nodes of the audit query DSL filter tree.</summary>
public abstract class AuditQueryNode
{
    /// <summary>When true, the node's match result is inverted (logical NOT scoped to this node).</summary>
    public bool Negate { get; init; }
}

/// <summary>
///     Logical group node: AND or OR over child nodes. AND binds tighter than OR at the same
///     nesting level in the text syntax; explicit parentheses map 1:1 to nested groups.
/// </summary>
public sealed class AuditQueryGroup : AuditQueryNode
{
    public required AuditQueryLogicalOperator Logic { get; init; }

    /// <summary>Child nodes; must contain at least one element once parsed.</summary>
    public required IReadOnlyList<AuditQueryNode> Children { get; init; }
}

/// <summary>Leaf condition: a field, a comparison operator, and the comparison value(s).</summary>
public sealed class AuditQueryCondition : AuditQueryNode
{
    public required AuditQueryField Field { get; init; }

    public required AuditQueryComparison Operator { get; init; }

    /// <summary>Raw comparison value as provided by the caller (string, number, boolean, or array for `in`).</summary>
    public required JsonElement Value { get; init; }
}

/// <summary>Logical connectives supported by the DSL.</summary>
public enum AuditQueryLogicalOperator
{
    And = 0,
    Or = 1
}

/// <summary>
///     Fields queryable through the audit query DSL. Every field maps onto the unified audit
///     entry projection of the underlying audit stores.
/// </summary>
public enum AuditQueryField
{
    /// <summary>Acting user: matches UserId (GUID) or UserEmail (case-insensitive).</summary>
    Actor = 0,

    /// <summary>Action type, e.g. Login, LoginFailed, Grant, ViewSecurityAuditLogs.</summary>
    Action = 1,

    /// <summary>Event timestamp (UTC).</summary>
    Timestamp = 2,

    /// <summary>Tenant identifier (GUID).</summary>
    Tenant = 3,

    /// <summary>Resource type affected, e.g. User, Asset, Permission.</summary>
    Resource = 4,

    /// <summary>Resource identifier affected (GUID or opaque string).</summary>
    ResourceId = 5,

    /// <summary>Risk/severity level: Low, Medium, High, Critical.</summary>
    Severity = 6,

    /// <summary>Audit store source: Authentication, Permission, General.</summary>
    Source = 7,

    /// <summary>Outcome of the audited operation.</summary>
    Success = 8,

    /// <summary>Client IP address.</summary>
    IpAddress = 9,

    /// <summary>User email when captured by the store (authentication attempts).</summary>
    Email = 10,

    /// <summary>Free-text description of the event.</summary>
    Description = 11,

    /// <summary>Additional metadata JSON payload.</summary>
    Metadata = 12,

    /// <summary>Client user-agent string.</summary>
    UserAgent = 13,

    /// <summary>Entry identifier (GUID).</summary>
    Id = 14
}

/// <summary>Comparison operators supported by the audit query DSL.</summary>
public enum AuditQueryComparison
{
    Eq = 0,
    Ne = 1,
    Gt = 2,
    Gte = 3,
    Lt = 4,
    Lte = 5,
    In = 6,
    Contains = 7,
    StartsWith = 8,
    Wildcard = 9
}

/// <summary>Value kinds the DSL accepts, used to fail closed on type mismatches at parse time.</summary>
public enum AuditQueryFieldValueKind
{
    Text = 0,
    Guid = 1,
    DateTime = 2,
    RiskLevel = 3,
    SourceType = 4,
    Boolean = 5
}

/// <summary>Static registry describing every queryable field: canonical name, aliases, value kind, and allowed operators.</summary>
public static class AuditQueryFieldRegistry
{
    private static readonly AuditQueryComparison[] AllComparisons =
    [
        AuditQueryComparison.Eq,
        AuditQueryComparison.Ne,
        AuditQueryComparison.Gt,
        AuditQueryComparison.Gte,
        AuditQueryComparison.Lt,
        AuditQueryComparison.Lte,
        AuditQueryComparison.In,
        AuditQueryComparison.Contains,
        AuditQueryComparison.StartsWith,
        AuditQueryComparison.Wildcard
    ];

    private static readonly AuditQueryComparison[] EqualityOnly =
    [
        AuditQueryComparison.Eq,
        AuditQueryComparison.Ne,
        AuditQueryComparison.In
    ];

    private static readonly AuditQueryComparison[] TextComparisons =
    [
        AuditQueryComparison.Eq,
        AuditQueryComparison.Ne,
        AuditQueryComparison.In,
        AuditQueryComparison.Contains,
        AuditQueryComparison.StartsWith,
        AuditQueryComparison.Wildcard
    ];

    private static readonly AuditQueryComparison[] OrderedComparisons =
    [
        AuditQueryComparison.Eq,
        AuditQueryComparison.Ne,
        AuditQueryComparison.Gt,
        AuditQueryComparison.Gte,
        AuditQueryComparison.Lt,
        AuditQueryComparison.Lte,
        AuditQueryComparison.In
    ];

    public sealed record FieldSpec(
        AuditQueryField Field,
        AuditQueryFieldValueKind ValueKind,
        IReadOnlySet<string> Names,
        IReadOnlyList<AuditQueryComparison> AllowedOperators);

    private static readonly Dictionary<AuditQueryField, FieldSpec> Specs = CreateSpecs();

    private static Dictionary<AuditQueryField, FieldSpec> CreateSpecs()
    {
        FieldSpec Spec(
            AuditQueryField field,
            AuditQueryFieldValueKind kind,
            string[] names,
            AuditQueryComparison[] operators) => new(field, kind, new HashSet<string>(names, StringComparer.OrdinalIgnoreCase), operators);

        var specs = new[]
        {
            Spec(AuditQueryField.Actor, AuditQueryFieldValueKind.Text, ["actor", "user", "userid"], EqualityOnly),
            Spec(AuditQueryField.Action, AuditQueryFieldValueKind.Text, ["action", "actiontype"], TextComparisons),
            Spec(AuditQueryField.Timestamp, AuditQueryFieldValueKind.DateTime, ["timestamp", "time", "when", "at"], OrderedComparisons),
            Spec(AuditQueryField.Tenant, AuditQueryFieldValueKind.Guid, ["tenant", "tenantid"], EqualityOnly),
            Spec(AuditQueryField.Resource, AuditQueryFieldValueKind.Text, ["resource", "resourcetype"], TextComparisons),
            Spec(AuditQueryField.ResourceId, AuditQueryFieldValueKind.Text, ["resourceid"], EqualityOnly.Concat([AuditQueryComparison.Contains]).ToArray()),
            Spec(AuditQueryField.Severity, AuditQueryFieldValueKind.RiskLevel, ["severity", "risk", "risklevel"], EqualityOnly),
            Spec(AuditQueryField.Source, AuditQueryFieldValueKind.SourceType, ["source", "sourcetype"], EqualityOnly),
            Spec(AuditQueryField.Success, AuditQueryFieldValueKind.Boolean, ["success", "outcome"], EqualityOnly),
            Spec(AuditQueryField.IpAddress, AuditQueryFieldValueKind.Text, ["ip", "ipaddress"], TextComparisons),
            Spec(AuditQueryField.Email, AuditQueryFieldValueKind.Text, ["email"], TextComparisons),
            Spec(AuditQueryField.Description, AuditQueryFieldValueKind.Text, ["description", "reason"], TextComparisons),
            Spec(AuditQueryField.Metadata, AuditQueryFieldValueKind.Text, ["metadata"], [AuditQueryComparison.Contains, AuditQueryComparison.Eq]),
            Spec(AuditQueryField.UserAgent, AuditQueryFieldValueKind.Text, ["useragent", "agent"], TextComparisons),
            Spec(AuditQueryField.Id, AuditQueryFieldValueKind.Guid, ["id"], EqualityOnly)
        };

        return specs.ToDictionary(spec => spec.Field);
    }

    /// <summary>Resolve a caller-provided field name (canonical or alias) to its spec. Fail-closed: unknown names return null.</summary>
    public static FieldSpec? Resolve(string name) => Specs.Values.FirstOrDefault(spec => spec.Names.Contains(name));

    /// <summary>Get the spec for a known field.</summary>
    public static FieldSpec Get(AuditQueryField field) => Specs[field];

    /// <summary>All accepted field names, for error messages and documentation.</summary>
    public static string DescribeAllFields() => string.Join(", ", Specs.Values.SelectMany(spec => spec.Names).OrderBy(name => name, StringComparer.Ordinal));
}
