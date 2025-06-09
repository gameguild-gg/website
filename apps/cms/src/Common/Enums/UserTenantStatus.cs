namespace cms.Common.Enums;

/// <summary>
/// Status of a user's membership in a tenant
/// </summary>
public enum UserTenantStatus
{
    /// <summary>
    /// User is an active member of the tenant
    /// </summary>
    Active = 1,
    
    /// <summary>
    /// User membership is temporarily suspended
    /// </summary>
    Suspended = 2,
    
    /// <summary>
    /// User membership is inactive (left or removed)
    /// </summary>
    Inactive = 3
}
