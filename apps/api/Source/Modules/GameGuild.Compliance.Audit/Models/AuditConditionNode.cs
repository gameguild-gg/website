namespace GameGuild.Compliance.Audit;

/// <summary>
/// Defines the kind of a condition tree node.
/// </summary>
public enum AuditConditionNodeKind
{
    /// <summary>
    /// A boolean group that combines its child nodes with a logical operator.
    /// </summary>
    Group = 0,

    /// <summary>
    /// A terminal node that applies a comparison to a single audit field.
    /// </summary>
    Field = 1,
}

/// <summary>
/// Defines how a group node combines its children.
/// </summary>
public enum AuditLogicalOperator
{
    /// <summary>
    /// All children must match.
    /// </summary>
    And = 0,

    /// <summary>
    /// At least one child must match.
    /// </summary>
    Or = 1,

    /// <summary>
    /// Exactly one child must not match.
    /// </summary>
    Not = 2,
}

/// <summary>
/// The audit log field targeted by a field node.
/// </summary>
public enum AuditFilterField
{
    /// <summary>
    /// The acting user identifier.
    /// </summary>
    UserId = 0,

    /// <summary>
    /// The action type.
    /// </summary>
    ActionType = 1,

    /// <summary>
    /// The resource type.
    /// </summary>
    ResourceType = 2,

    /// <summary>
    /// The resource identifier.
    /// </summary>
    ResourceId = 3,

    /// <summary>
    /// The audit category.
    /// </summary>
    Category = 4,

    /// <summary>
    /// The risk level.
    /// </summary>
    RiskLevel = 5,

    /// <summary>
    /// Whether the audited operation succeeded.
    /// </summary>
    Success = 6,

    /// <summary>
    /// When the audit entry was created.
    /// </summary>
    CreatedAt = 7,

    /// <summary>
    /// The caller IP address.
    /// </summary>
    IpAddress = 8,

    /// <summary>
    /// Free-text search over description and metadata.
    /// </summary>
    SearchText = 9,
}

/// <summary>
/// The comparison applied by a field node.
/// </summary>
public enum AuditFilterComparison
{
    /// <summary>
    /// The field equals the value.
    /// </summary>
    Equals = 0,

    /// <summary>
    /// The field does not equal the value.
    /// </summary>
    NotEquals = 1,

    /// <summary>
    /// The field is one of the provided values.
    /// </summary>
    In = 2,

    /// <summary>
    /// The field is greater than or equal to the value.
    /// </summary>
    GreaterThanOrEqual = 3,

    /// <summary>
    /// The field is less than or equal to the value.
    /// </summary>
    LessThanOrEqual = 4,
}

/// <summary>
/// A node of a nested audit log filter condition tree. A node is either a group that
/// combines child nodes with a boolean operator or a field node that applies a single
/// comparison to an audit field.
/// </summary>
public sealed class AuditConditionNode
{
    /// <summary>
    /// Gets or sets whether this node is a <see cref="AuditConditionNodeKind.Group"/> or a
    /// <see cref="AuditConditionNodeKind.Field"/> node.
    /// </summary>
    public AuditConditionNodeKind? Kind { get; set; }

    /// <summary>
    /// Gets or sets the boolean operator that combines the children of a group node.
    /// </summary>
    public AuditLogicalOperator? Operator { get; set; }

    /// <summary>
    /// Gets or sets the audit field targeted by a field node.
    /// </summary>
    public AuditFilterField? Field { get; set; }

    /// <summary>
    /// Gets or sets the comparison applied by a field node.
    /// </summary>
    public AuditFilterComparison? Comparison { get; set; }

    /// <summary>
    /// Gets or sets the single comparison value of a field node.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Gets or sets the value list of an <see cref="AuditFilterComparison.In"/> comparison.
    /// </summary>
    public List<string> Values { get; set; } = [];

    /// <summary>
    /// Gets or sets the child nodes of a group node.
    /// </summary>
    public List<AuditConditionNode> Children { get; set; } = [];
}
