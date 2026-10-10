using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Provisioning;

/// <summary>
///     Links an externally provisioned SCIM identity to a platform user. One row per
///     (TenantId, ExternalId) pair: the unique index enforces RFC 7644 idempotent
///     creation, where a repeated POST with the same externalId returns the existing
///     resource instead of creating a duplicate.
/// </summary>
[Table("scim_user_mappings")]
[Index(nameof(TenantId), nameof(ExternalId), IsUnique = true)]
[Index(nameof(UserId))]
public class ScimUserMapping : EntityBase
{
    /// <summary>
    ///     Tenant scope of the mapping. The tenant is derived from the provisioning
    ///     token, never from the request route.
    /// </summary>
    [Required]
    public new Guid TenantId { get; set; }

    /// <summary>
    ///     externalId supplied by the identity provider for this user.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string ExternalId { get; set; } = string.Empty;

    /// <summary>
    ///     Platform user the external identity maps to.
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    public static ScimUserMapping Create(Guid tenantId, string externalId, Guid userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

        return new ScimUserMapping
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ExternalId = externalId,
            UserId = userId
        };
    }
}
