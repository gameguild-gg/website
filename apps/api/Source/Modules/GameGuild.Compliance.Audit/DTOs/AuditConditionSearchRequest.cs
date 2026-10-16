using System.ComponentModel.DataAnnotations;

namespace GameGuild.Compliance.Audit;

/// <summary>
/// Request to search audit log entries with a nested condition tree.
/// </summary>
public sealed class AuditConditionSearchRequest : IValidatableObject
{
    /// <summary>
    /// Gets or sets the tenant scope. Non-system callers are always clamped to the
    /// tenant from their authenticated actor context.
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Gets or sets the root node of the condition tree applied to the search.
    /// </summary>
    [Required]
    public AuditConditionNode? Condition { get; set; }

    /// <summary>
    /// Gets or sets the number of entries to skip.
    /// </summary>
    public int Skip { get; set; }

    /// <summary>
    /// Gets or sets the number of entries to take.
    /// </summary>
    public int Take { get; set; } = 100;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        return AuditConditionTreeValidator.Validate(Condition, nameof(Condition));
    }
}
