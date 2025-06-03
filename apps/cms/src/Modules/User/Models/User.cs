using System.ComponentModel.DataAnnotations;
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
    /// Default constructor
    /// </summary>
    public User() { }

    /// <summary>
    /// Constructor for partial initialization
    /// </summary>
    /// <param name="partial">Partial user data</param>
    public User(object partial) : base(partial) { }
}
