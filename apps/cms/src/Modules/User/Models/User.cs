using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using GameGuild.Common.Entities;
using GameGuild.Modules.Tenant.Models;

namespace GameGuild.Modules.User.Models;

[Table("Users")]
[Index(nameof(Email), IsUnique = true)]
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
    /// Navigation property to tenant permissions and memberships
    /// </summary>
    public virtual ICollection<TenantPermission> TenantPermissions
    {
        get;
        set;
    } = new List<TenantPermission>();

    /// <summary>
    /// Navigation property to global content type permissions (Layer 2a of permission system)
    /// </summary>
    public virtual ICollection<ContentTypePermission> ContentTypePermissions
    {
        get;
        set;
    } = new List<ContentTypePermission>();

    /// <summary>
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
