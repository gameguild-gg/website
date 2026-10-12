using System.ComponentModel.DataAnnotations;

namespace GameGuild.Compliance.Audit;

public enum AuditActionTypeLogicalOperator
{
    Any = 0,
    All = 1,
    None = 2
}

public enum AuditActionTypeSortField
{
    CreatedAt = 0,
    ActionType = 1,
    ResourceType = 2,
    UserId = 3,
    RiskLevel = 4
}

public enum AuditActionTypeSortDirection
{
    Ascending = 0,
    Descending = 1
}

/// <summary>Filters, ordering, and aggregation options for action-type audit searches.</summary>
public sealed class AuditActionTypeSearchRequest : IValidatableObject
{
    /// <summary>Exact action types. Multiple values are combined as a set.</summary>
    public string[] ActionTypes { get; set; } = [];

    /// <summary>Predefined groups: CRUD, Security, and Admin.</summary>
    public string[] ActionGroups { get; set; } = [];

    /// <summary>Hierarchical taxonomy paths such as Security.Authentication.</summary>
    public string[] Categories { get; set; } = [];

    /// <summary>
    /// Combines the selected dimensions (action types, groups, and taxonomy paths).
    /// Any is OR, All is AND, and None excludes records matching any selected dimension.
    /// Values within an individual dimension are always ORed.
    /// </summary>
    public AuditActionTypeLogicalOperator LogicalOperator { get; set; } = AuditActionTypeLogicalOperator.Any;

    public Guid? UserId { get; set; }

    public Guid? TenantId { get; set; }

    public DateTimeOffset? StartDate { get; set; }

    public DateTimeOffset? EndDate { get; set; }

    [Range(0, int.MaxValue)]
    public int Skip { get; set; }

    [Range(1, 1000)]
    public int Take { get; set; } = 100;

    public AuditActionTypeSortField SortBy { get; set; } = AuditActionTypeSortField.CreatedAt;

    public AuditActionTypeSortDirection SortDirection { get; set; } = AuditActionTypeSortDirection.Descending;

    public bool IncludeTrends { get; set; } = true;

    public AuditActivityBucketSize TrendBucketSize { get; set; } = AuditActivityBucketSize.Daily;

    public bool IncludeRelatedActions { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var actionTypes = ActionTypes ?? [];
        var actionGroups = ActionGroups ?? [];
        var categories = Categories ?? [];

        if (actionTypes.Length > 50 || actionGroups.Length > 10 || categories.Length > 20)
        {
            yield return new ValidationResult("The search may include at most 50 action types, 10 groups, and 20 categories.");
        }

        if (!actionTypes.Any(value => !string.IsNullOrWhiteSpace(value)) &&
            !actionGroups.Any(value => !string.IsNullOrWhiteSpace(value)) &&
            !categories.Any(value => !string.IsNullOrWhiteSpace(value)))
        {
            yield return new ValidationResult("At least one action type, action group, or taxonomy category is required.");
        }

        if (actionTypes.Any(value => value is null || value.Length > 100))
        {
            yield return new ValidationResult("Action type values cannot exceed 100 characters.", [nameof(ActionTypes)]);
        }

        var unknownGroup = actionGroups.FirstOrDefault(value => !AuditActionTypeTaxonomy.IsKnownGroup(value));
        if (unknownGroup is not null)
        {
            yield return new ValidationResult($"Unknown action group '{unknownGroup}'. Supported groups are CRUD, Security, and Admin.", [nameof(ActionGroups)]);
        }

        var unknownCategory = categories.FirstOrDefault(value => !AuditActionTypeTaxonomy.IsKnownCategory(value));
        if (unknownCategory is not null)
        {
            yield return new ValidationResult($"Unknown action taxonomy category '{unknownCategory}'.", [nameof(Categories)]);
        }

        if (StartDate.HasValue && EndDate.HasValue && StartDate.Value > EndDate.Value)
        {
            yield return new ValidationResult("StartDate must be less than or equal to EndDate.", [nameof(StartDate), nameof(EndDate)]);
        }

        if (!Enum.IsDefined(LogicalOperator) || !Enum.IsDefined(SortBy) ||
            !Enum.IsDefined(SortDirection) || !Enum.IsDefined(TrendBucketSize))
        {
            yield return new ValidationResult("A logical operator, sort value, or trend bucket is not supported.");
        }
    }
}
