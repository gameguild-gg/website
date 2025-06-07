using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using cms.Common.Entities;

namespace cms.Modules.Tenant.Models;

/// <summary>
/// Represents a tenant in a multi-tenant system
/// Inherits from BaseEntity to provide UUID IDs, version control, timestamps, and soft delete functionality
/// </summary>
[Table("Tenants")]
[Index(nameof(Name), IsUnique = true)]
public class Tenant : BaseEntity
{
    /// <summary>
    /// Name of the tenant
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// Description of the tenant
    /// </summary>
    [MaxLength(500)]
    public string? Description
    {
        get;
        set;
    }

    /// <summary>
    /// Whether this tenant is currently active
    /// </summary>
    public bool IsActive
    {
        get;
        set;
    } = true;

    /// <summary>
    /// Slug for the tenant (URL-friendly unique identifier)
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Slug
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// Navigation property to users in this tenant
    /// </summary>
    public virtual ICollection<UserTenant> UserTenants
    {
        get;
        set;
    } = new List<UserTenant>();

    /// <summary>
    /// Navigation property to tenant roles
    /// </summary>
    public virtual ICollection<TenantRole> TenantRoles
    {
        get;
        set;
    } = new List<TenantRole>();

    /// <summary>
    /// Default constructor
    /// </summary>
    public Tenant() { }

    /// <summary>
    /// Constructor for partial initialization
    /// </summary>
    /// <param name="partial">Partial tenant data</param>
    public Tenant(object partial) : base(partial) { }

    /// <summary>
    /// Activate the tenant
    /// </summary>
    public void Activate()
    {
        IsActive = true;
        Touch();
    }

    /// <summary>
    /// Deactivate the tenant
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }
}
