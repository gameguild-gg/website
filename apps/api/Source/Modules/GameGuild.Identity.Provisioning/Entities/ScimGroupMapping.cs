using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Provisioning;

/// <summary>
///     Links an externally provisioned SCIM group to a tenant role. One row per
///     (TenantId, ExternalId) pair so group creation is idempotent on externalId,
///     mirroring <see cref="ScimUserMapping"/>.
/// </summary>
[Table("scim_group_mappings")]
[Index(nameof(TenantId), nameof(ExternalId), IsUnique = true)]
[Index(nameof(RoleId))]
public class ScimGroupMapping : EntityBase
{
    /// <summary>
    ///     Tenant scope of the mapping (derived from the provisioning token).
    /// </summary>
    [Required]
    public new Guid TenantId { get; set; }

    /// <summary>
    ///     externalId supplied by the identity provider for this group.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string ExternalId { get; set; } = string.Empty;

    /// <summary>
    ///     Tenant role backing the SCIM group.
    /// </summary>
    [Required]
    public Guid RoleId { get; set; }

    public static ScimGroupMapping Create(Guid tenantId, string externalId, Guid roleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

        return new ScimGroupMapping
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ExternalId = externalId,
            RoleId = roleId
        };
    }
}
