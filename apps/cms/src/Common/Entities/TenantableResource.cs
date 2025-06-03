namespace cms.Common.Entities;

/// <summary>
/// Interface for resources that can belong to a specific tenant
/// </summary>
public interface ITenantable
{
    /// <summary>
    /// Navigation property to the tenant
    /// Entity Framework will automatically create the TenantId foreign key
    /// </summary>
    Modules.Tenant.Models.Tenant? Tenant { get; set; }

    /// <summary>
    /// Indicates whether this resource is accessible across all tenants (when Tenant is null)
    /// or only within a specific tenant
    /// </summary>
    bool IsGlobal { get; }
}
