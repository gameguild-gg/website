using System.ComponentModel.DataAnnotations.Schema;
using GameGuild.CQRS.Models;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Durable record of a permission template being instantiated into a tenant
///     (issue #369/#370). One row is written per (template, tenant, user) target at
///     instantiation time, carrying the template version that was applied. Template
///     migrations (issue #370) discover their affected tenants/users from these rows
///     and bump <see cref="TemplateVersionNumber"/> as each target is brought to the
///     migrated version. The tenant scope uses the inherited nullable TenantId
/// property from EntityBase (null never occurs for instantiations). Indexes are
/// declared in <c>PermissionTemplateInstantiationConfiguration</c>.
/// </summary>
[Table("PermissionTemplateInstantiations")]
public class PermissionTemplateInstantiation : EntityBase
{
    /// <summary>
    ///     The instantiated template.
    /// </summary>
    public Guid TemplateId { get; set; }

    /// <summary>
    ///     The user the template was applied to (the instantiation unit is per-user so
    ///     migrations can batch by tenant while tracking per-user progress).
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    ///     The template version number that was applied to this target.
    /// </summary>
    public int TemplateVersionNumber { get; set; }

    /// <summary>
    ///     Which actor instantiated the template.
    /// </summary>
    public Guid InstantiatedByUserId { get; set; }

    /// <summary>
    ///     When the instantiation happened.
    /// </summary>
    public DateTime InstantiatedAt { get; set; } = SystemClock.UtcNow;

    /// <summary>
    ///     Whether this instantiation is still live. Deactivating a target removes it
    ///     from future migrations; the row is kept as provenance (soft delete parity).
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    ///     The migration that most recently moved this target's version, if any.
    /// </summary>
    public Guid? LastMigrationId { get; set; }
}
