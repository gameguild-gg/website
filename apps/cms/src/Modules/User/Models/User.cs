using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using cms.Common.Entities;

namespace cms.Modules.User.Models;

public class User : BaseEntity
{
    [Required]
    [MaxLength(100)]
    public string Name
    {
        get;
        set;
    } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email
    {
        get;
        set;
    } = string.Empty;

    public bool IsActive
    {
        get;
        set;
    } = true;

    /// <summary>
    /// Total wallet balance including pending/frozen funds
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal Balance { get; set; } = 0;

    /// <summary>
    /// Available balance that can be spent (excludes frozen/pending funds)
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal AvailableBalance { get; set; } = 0;

    /// <summary>
    /// Navigation property to user credentials
    /// </summary>
    public virtual ICollection<Credential> Credentials
    {
        get;
        set;
    } = new List<Credential>();

    /// <summary>
    /// Navigation property to user-tenant relationships
    /// </summary>
    public virtual ICollection<cms.Modules.Tenant.Models.UserTenant> UserTenants
    {
        get;
        set;
    } = new List<cms.Modules.Tenant.Models.UserTenant>();

    /// <summary>
    /// Navigation property to global content type permissions (Layer 2a of permission system)
    /// </summary>
    public virtual ICollection<cms.Common.Entities.ContentTypePermission> ContentTypePermissions
    {
        get;
        set;
    } = new List<cms.Common.Entities.ContentTypePermission>();

    /// <summary>
    /// Navigation property to resource permissions granted to this user (Layer 3 of permission system)
    /// </summary>
    public virtual ICollection<cms.Common.Entities.ResourcePermission> GrantedResourcePermissions
    {
        get;
        set;
    } = new List<cms.Common.Entities.ResourcePermission>();

    /// <summary>
    /// Navigation property to permissions granted by this user
    /// </summary>
    public virtual ICollection<cms.Common.Entities.ResourcePermission> GrantedPermissions
    {
        get;
        set;
    } = new List<cms.Common.Entities.ResourcePermission>();

    /// <summary>
    /// Default constructor
    /// </summary>
    public User() { }

    /// <summary>
    /// Constructor for partial initialization
    /// </summary>
    /// <param name="partial">Partial user data</param>
    public User(object partial) : base(partial) { }
}
