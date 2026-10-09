using System.Security.Cryptography;
using System.Text;
using System.ComponentModel.DataAnnotations;
using GameGuild.CQRS.Models;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Represents an Attribute-Based Access Control (ABAC) policy.
///     Defines rules for fine-grained access control based on attributes.
///     This is the consolidated ABAC policy entity used across the system.
/// </summary>
public class AbacPolicy
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public TenantId? TenantId { get; set; }

    /// <summary>
    ///     Name of the ABAC policy
    /// </summary>
    [MaxLength(256)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Description of what this policy controls
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    ///     Policy effect (Allow or Deny)
    /// </summary>
    public AbacPolicyEffect Effect { get; set; } = AbacPolicyEffect.Allow;

    /// <summary>
    ///     Whether this policy is currently enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    ///     Priority for policy evaluation (higher = evaluated first)
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    ///     Target resource type (null means all resource types)
    /// </summary>
    [MaxLength(256)]
    public string? ResourceType { get; set; }

    // Attribute conditions stored as JSON
    /// <summary>
    ///     Subject/User attribute conditions (JSON)
    ///     Example: {"department": "IT", "clearanceLevel": 3}
    /// </summary>
    [MaxLength(4000)]
    public string? SubjectConditions { get; set; }

    /// <summary>
    ///     Resource attribute conditions (JSON)
    ///     Example: {"classification": "Confidential", "owner": "user-456"}
    /// </summary>
    [MaxLength(4000)]
    public string? ResourceConditions { get; set; }

    /// <summary>
    ///     Environmental attribute conditions (JSON)
    ///     Example: {"ipAddress": "192.168.1.0/24", "dayOfWeek": "Monday"}
    /// </summary>
    [MaxLength(4000)]
    public string? EnvironmentConditions { get; set; }

    /// <summary>
    ///     Action/permission constraints (JSON)
    /// </summary>
    [MaxLength(2000)]
    public string? ActionConditions { get; set; }

    /// <summary>
    ///     Target resources this policy applies to (JSON array of resource types)
    /// </summary>
    [MaxLength(2000)]
    public string? TargetResources { get; set; }

    /// <summary>
    ///     Target actions this policy applies to (JSON array of actions)
    /// </summary>
    [MaxLength(2000)]
    public string? TargetActions { get; set; }

    /// <summary>
    ///     Attribute expression in JSON format that defines the conditions
    ///     Example: {"user.department": "IT", "resource.confidentiality": "public"}
    /// </summary>
    [MaxLength(4000)]
    public string? AttributeExpression { get; set; }

    /// <summary>
    ///     Optional condition expression for complex logic
    ///     Example: "user.clearanceLevel >= resource.requiredClearance"
    /// </summary>
    [MaxLength(2000)]
    public string? ConditionExpression { get; set; }

    /// <summary>
    ///     Time-based conditions (JSON serialized)
    ///     Contains: DaysOfWeek, TimeRanges, TimeZone
    /// </summary>
    [MaxLength(2000)]
    public string? TimeConditions { get; set; }

    /// <summary>
    ///     Location-based conditions (JSON serialized)
    ///     Contains: AllowedCountries, AllowedRegions, IpRanges
    /// </summary>
    [MaxLength(2000)]
    public string? LocationConditions { get; set; }

    /// <summary>
    ///     Obligations that must be fulfilled if policy matches
    ///     JSON array of obligation strings
    /// </summary>
    [MaxLength(2000)]
    public string? Obligations { get; set; }

    /// <summary>
    ///     Date and time when this policy becomes active
    /// </summary>
    public DateTime? EffectiveFrom { get; set; }

    /// <summary>
    ///     Date and time when this policy expires
    /// </summary>
    public DateTime? EffectiveUntil { get; set; }

    /// <summary>
    ///     Tags for policy categorization and searching (JSON array)
    /// </summary>
    [MaxLength(1000)]
    public string? Tags { get; set; }

    /// <summary>
    ///     Version number for policy updates
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    ///     User ID who created this policy
    /// </summary>
    public Guid CreatedBy { get; set; }

    /// <summary>
    ///     User ID who last updated this policy
    /// </summary>
    public Guid? UpdatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = SystemClock.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    ///     Check if policy is active (enabled)
    /// </summary>
    public bool IsActive() => IsEnabled;

    /// <summary>
    ///     Check if policy is currently effective (enabled and within date range)
    /// </summary>
    public bool IsEffective()
    {
        if (!IsEnabled)
        {
            return false;
        }

        var now = SystemClock.UtcNow;

        if (EffectiveFrom.HasValue && now < EffectiveFrom.Value)
        {
            return false;
        }

        if (EffectiveUntil.HasValue && now > EffectiveUntil.Value)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Enable the policy
    /// </summary>
    public void Enable()
    {
        IsEnabled = true;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    ///     Disable the policy
    /// </summary>
    public void Disable()
    {
        IsEnabled = false;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    ///     Set priority
    /// </summary>
    public void SetPriority(int priority)
    {
        if (priority < 0)
        {
            throw new ArgumentException("Priority must be non-negative", nameof(priority));
        }

        Priority = priority;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    ///     Update policy status
    /// </summary>
    public void SetActive(bool isActive, Guid updatedBy)
    {
        IsEnabled = isActive;
        UpdatedBy = updatedBy;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    ///     Update policy effective period
    /// </summary>
    public void SetEffectivePeriod(DateTime? effectiveFrom, DateTime? effectiveUntil, Guid updatedBy)
    {
        EffectiveFrom = effectiveFrom;
        EffectiveUntil = effectiveUntil;
        UpdatedBy = updatedBy;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    ///     Update policy expression
    /// </summary>
    public void UpdateExpression(string? attributeExpression, string? conditionExpression, Guid updatedBy)
    {
        AttributeExpression = attributeExpression;
        ConditionExpression = conditionExpression;
        UpdatedBy = updatedBy;
        Version++;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    ///     Update policy metadata
    /// </summary>
    public void UpdateMetadata(string name, string? description, int priority, Guid updatedBy)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description;
        Priority = priority;
        UpdatedBy = updatedBy;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    ///     Check if policy is a deny policy
    /// </summary>
    public bool IsDenyPolicy() => Effect == AbacPolicyEffect.Deny;

    /// <summary>
    ///     Check if policy is an allow policy
    /// </summary>
    public bool IsAllowPolicy() => Effect == AbacPolicyEffect.Allow;
}

/// <summary>
///     Delegated administration scope definition
/// </summary>
public class DelegatedAdminScope
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public TenantId? TenantId { get; set; }

    public Guid AdminUserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DelegatedAdminScopeType ScopeType { get; set; }

    // Scope definitions (JSON arrays)
    public string? AllowedResourceTypes { get; set; }

    public string? AllowedResourceIds { get; set; }

    public string? AllowedUserIds { get; set; }

    public string? AllowedDepartments { get; set; }

    public string? AllowedTeams { get; set; }

    public string? AllowedRoles { get; set; }

    // Permission constraints
    public string? GrantablePermissions { get; set; }

    public string? DeniedPermissions { get; set; }

    public bool CanManageUsers { get; set; }

    public bool CanManagePermissions { get; set; }

    public bool CanManageResources { get; set; }

    public bool CanViewAuditLogs { get; set; }

    public DateTime StartsAt { get; set; } = SystemClock.UtcNow;

    public DateTime? ExpiresAt { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = SystemClock.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    ///     Check if scope is currently valid
    /// </summary>
    public bool IsValid()
    {
        if (!IsActive)
        {
            return false;
        }

        var now = SystemClock.UtcNow;
        if (now < StartsAt)
        {
            return false;
        }

        if (ExpiresAt is null)
        {
            return true;
        }

        if (now >= ExpiresAt.Value)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Check if admin can manage a specific user
    /// </summary>
    public bool CanManageUser(Guid userId)
    {
        if (!IsValid() || !CanManageUsers)
        {
            return false;
        }

        return AllowedUserIds?.Contains(userId.ToString()) ?? false;
    }

    /// <summary>
    ///     Check if admin can manage a specific resource type
    /// </summary>
    public bool CanManageResourceType(string resourceType)
    {
        if (!IsValid() || !CanManageResources)
        {
            return false;
        }

        return AllowedResourceTypes?.Contains(resourceType) ?? false;
    }

    /// <summary>
    ///     Activate the scope
    /// </summary>
    public void Activate()
    {
        IsActive = true;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    ///     Deactivate the scope
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = SystemClock.UtcNow;
    }
}

/// <summary>
///     Represents a versioned snapshot of a permission template
/// </summary>
public class PermissionTemplateVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TemplateId { get; set; }

    public int VersionNumber { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string[] Permissions { get; set; } = Array.Empty<string>();

    public bool IsActive { get; set; }

    public Guid CreatedByUserId { get; set; }

    public string? ChangeNotes { get; set; }

    public TemplateChangeType ChangeType { get; set; }

    public string[]? AddedPermissions { get; set; }

    public string[]? RemovedPermissions { get; set; }

    public string[]? UnchangedPermissions { get; set; }

    public int? PreviousVersion { get; set; }

    public string? Metadata { get; set; } // JSON

    public string? PermissionHash { get; set; }

    public string[]? Tags { get; set; }

    public DateTime CreatedAt { get; set; } = SystemClock.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    ///     Calculate checksum of permission set
    /// </summary>
    public string CalculateHash()
    {
        var permString = string.Join(",", Permissions.OrderBy(p => p));
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(permString);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }
}

/// <summary>
///     Represents a migration plan for template version upgrades
/// </summary>
public class PermissionTemplateMigration
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TemplateId { get; set; }

    public int FromVersion { get; set; }

    public int ToVersion { get; set; }

    public MigrationStatus Status { get; set; } = MigrationStatus.Planned;

    public MigrationStrategy Strategy { get; set; }

    public DateTime? ScheduledFor { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public Guid InitiatedByUserId { get; set; }

    public Guid[]? AffectedTenantIds { get; set; }

    public Guid[]? AffectedUserIds { get; set; }

    public int SuccessCount { get; set; }

    public int FailureCount { get; set; }

    public int SkippedCount { get; set; }

    public int TotalCount { get; set; }

    public string? Errors { get; set; } // JSON

    public string? Log { get; set; } // JSON

    public string? RollbackPlan { get; set; } // JSON

    public string? DryRunResult { get; set; } // JSON

    public bool IsDryRun { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = SystemClock.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    ///     Calculate progress percentage
    /// </summary>
    public double GetProgressPercentage() =>
        TotalCount > 0
            ? (SuccessCount + FailureCount + SkippedCount) / (double)TotalCount * 100
            : 0;

    /// <summary>
    ///     Check if migration is complete
    /// </summary>
    public bool IsComplete() =>
        Status == MigrationStatus.Completed ||
        Status == MigrationStatus.Failed ||
        Status == MigrationStatus.RolledBack;
}

/// <summary>
///     Represents a policy bundle in the central registry
/// </summary>
public class PolicyBundle
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public TenantId? TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Version { get; set; } = "1.0.0";

    public PolicyBundleType BundleType { get; set; }

    public string ContentHash { get; set; } = string.Empty;

    public string? DigitalSignature { get; set; }

    public string? SignedBy { get; set; }

    public DateTime? SignedAt { get; set; }

    public PolicyBundleStatus Status { get; set; } = PolicyBundleStatus.Draft;

    public string PolicyData { get; set; } = string.Empty; // JSON

    public string? Metadata { get; set; } // JSON

    public bool IsGlobal { get; set; }

    public DateTime? EffectiveFrom { get; set; }

    public DateTime? EffectiveUntil { get; set; }

    public Guid? PreviousVersionId { get; set; }

    public Guid CreatedBy { get; set; }

    public Guid? ApprovedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public int DeploymentCount { get; set; }

    public DateTime? LastDeployedAt { get; set; }

    public DateTime CreatedAt { get; set; } = SystemClock.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    ///     Check if bundle is approved
    /// </summary>
    public bool IsApproved() =>
        Status == PolicyBundleStatus.Approved || Status == PolicyBundleStatus.Active;

    /// <summary>
    ///     Check if bundle is active
    /// </summary>
    public bool IsActive() => Status == PolicyBundleStatus.Active;
}

/// <summary>
///     Tracks deployment of policy bundles
/// </summary>
public class PolicyBundleDeployment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BundleId { get; set; }

    public TenantId? TenantId { get; set; }

    public string Environment { get; set; } = string.Empty;

    public PolicyDeploymentStatus Status { get; set; } = PolicyDeploymentStatus.Pending;

    public DateTime DeployedAt { get; set; } = SystemClock.UtcNow;

    public Guid DeployedBy { get; set; }

    public DateTime? ActivatedAt { get; set; }

    public DateTime? RolledBackAt { get; set; }

    public Guid? RolledBackBy { get; set; }

    public string? RollbackReason { get; set; }

    public bool VerificationPassed { get; set; }

    public string? VerificationDetails { get; set; }

    public string? DeploymentNotes { get; set; }

    public PolicyBundle? Bundle { get; set; }

    /// <summary>
    ///     Check if deployment is active
    /// </summary>
    public bool IsActive() => Status == PolicyDeploymentStatus.Active;

    /// <summary>
    ///     Activate the deployment
    /// </summary>
    public void Activate()
    {
        Status = PolicyDeploymentStatus.Active;
        ActivatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    ///     Rollback the deployment
    /// </summary>
    public void Rollback(Guid userId, string reason)
    {
        Status = PolicyDeploymentStatus.RolledBack;
        RolledBackAt = SystemClock.UtcNow;
        RolledBackBy = userId;
        RollbackReason = reason;
    }
}

/// <summary>
///     Audit log for policy registry operations
/// </summary>
public class PolicyRegistryAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? BundleId { get; set; }

    public PolicyRegistryAction Action { get; set; }

    public Guid PerformedBy { get; set; }

    public DateTime PerformedAt { get; set; } = SystemClock.UtcNow;

    public string? Details { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public bool Success { get; set; } = true;

    public string? ErrorMessage { get; set; }
}
