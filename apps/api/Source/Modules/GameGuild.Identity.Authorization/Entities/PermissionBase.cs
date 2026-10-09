using System.ComponentModel.DataAnnotations.Schema;
using GameGuild.CQRS.Models;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Unified abstract root for every permission-grant entity in the platform (issues #352/#357).
///     Centralizes the shared grant contract — subject (<see cref="UserId"/>), grant metadata
///     (<see cref="GrantedAt"/>), activation (<see cref="IsActive"/>), expiration
///     (<see cref="ExpiresAt"/>) — together with the shared validation, expiration, audit and
///     tenant-isolation behaviors that all permission entities inherit.
/// </summary>
/// <remarks>
///     <para>
///         <b>EF mapping strategy.</b> PermissionBase is deliberately <i>not</i> a mapped
///         hierarchy root: the three persisted permission families keep their existing tables
///         (<c>TenantPermissions</c>, <c>contenttypepermission</c>, <c>ResourceUserPermission</c>).
///         Because every concrete permission entity maps the common properties inherited from
///         this class into its own table, each permission table carries the identical common
///         column block (Id, UserId, TenantId, IsActive, ExpiresAt, GrantedAt, CreatedAt,
///         UpdatedAt, DeletedAt, Version) — the schema-level expression of the shared
///         inheritance contract — while no destructive table merge or data migration is needed.
///     </para>
///     <para>
///         <b>Divergent payload storage.</b> <see cref="TenantPermission"/> and
///         <see cref="ResourceUserPermission"/> store permission strings, while the
///         <c>WithPermissions</c> family stores comma-separated <see cref="PermissionType"/>
///         numbers. The <see cref="GetGrantedPermissionTypes"/> mapping layer bridges both
///         representations without schema changes, failing closed on any value that does not
///         map to a defined <see cref="PermissionType"/>.
///     </para>
///     <para>
///         <b>Fail-closed.</b> Undefined permission values are never guessed, expired or
///         inactive grants never convey permissions, and tenant-scope mismatches are rejected.
///     </para>
/// </remarks>
public abstract class PermissionBase : EntityBase<Guid>
{
    /// <summary>
    ///     Protected constructor for entity framework and derived entities.
    ///     Generates a new identifier, mirroring the non-generic <c>EntityBase</c> behavior
    ///     previously inherited by <see cref="TenantPermission"/>.
    /// </summary>
    protected PermissionBase()
    {
        EnsurePermissionIdGenerated();
    }

    /// <summary>
    ///     User ID of the grant subject (null for tenant/global default grants).
    ///     <see cref="ResourceUserPermission"/> hides this with a required non-nullable equivalent.
    /// </summary>
    public virtual Guid? UserId { get; set; }

    /// <summary>
    ///     When the grant expires (null = never expires). Expiration is inclusive:
    ///     a grant whose <see cref="ExpiresAt"/> equals the current instant is expired.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    ///     Whether the grant is currently active. <see cref="ResourceUserPermission"/> hides this
    ///     with a computed equivalent derived from its revocation state.
    /// </summary>
    public virtual bool IsActive { get; set; } = true;

    /// <summary>
    ///     When the permission was granted.
    /// </summary>
    public DateTime GrantedAt { get; set; } = SystemClock.UtcNow;

    /// <summary>
    ///     Unified audit view over divergent grantor columns (GrantedBy / GrantedByUserId):
    ///     who created (granted) this permission.
    /// </summary>
    [NotMapped]
    public abstract Guid? CreatedBy { get; }

    /// <summary>
    ///     Unified audit view over divergent tenant columns: the tenant scope this permission
    ///     belongs to, regardless of the column shape used by the concrete entity.
    /// </summary>
    [NotMapped]
    public abstract Guid? PermissionTenantId { get; }

    /// <summary>
    ///     Mapping layer over the entity's divergent permission payload storage.
    ///     Returns only values that map to a defined <see cref="PermissionType"/> (fail closed).
    /// </summary>
    public abstract IReadOnlyList<PermissionType> GetGrantedPermissionTypes();

    /// <summary>
    ///     Check if the permission grant has expired (inclusive boundary, fail closed).
    /// </summary>
    public virtual bool IsExpired() { return ExpiresAt.HasValue && ExpiresAt.Value <= SystemClock.UtcNow; }

    /// <summary>
    ///     Check if the grant is currently effective (active and not expired).
    /// </summary>
    public virtual bool IsEffective() { return IsActive && !IsExpired(); }

    /// <summary>
    ///     Check whether this grant currently conveys the given permission (fail closed:
    ///     inactive, expired, undefined or unmapped values never grant anything).
    /// </summary>
    /// <param name="permission">Permission to check.</param>
    /// <returns>True when the grant is effective and its payload includes the permission.</returns>
    public virtual bool Grants(PermissionType permission) { return IsEffective() && GetGrantedPermissionTypes().Contains(permission); }

    /// <summary>
    ///     Expire the grant: deactivate it, stamp the expiration instant and touch the audit timestamp.
    /// </summary>
    public virtual void Expire()
    {
        IsActive = false;
        ExpiresAt = SystemClock.UtcNow;
        Touch();
    }

    /// <summary>
    ///     Extend (or clear) the expiration date.
    /// </summary>
    /// <param name="newExpirationDate">New expiration instant (null for permanent grants).</param>
    public void ExtendExpiration(DateTime? newExpirationDate)
    {
        ExpiresAt = newExpirationDate;
        Touch();
    }

    /// <summary>
    ///     Common validation logic shared by every permission entity:
    ///     expiration must not precede the grant instant, the subject must not be an
    ///     empty identifier, and soft-deleted grants must not remain active.
    /// </summary>
    public virtual bool IsValid()
    {
        if (ExpiresAt is { } expiresAt && expiresAt < GrantedAt)
        {
            return false;
        }

        if (UserId is { } subject && subject == Guid.Empty)
        {
            return false;
        }

        return !(IsDeleted && IsActive);
    }

    /// <summary>
    ///     Tenant isolation check: does this permission belong to the requested tenant scope?
    /// </summary>
    /// <param name="tenantId">Tenant scope to compare against (null = global scope).</param>
    public bool IsInTenantScope(Guid? tenantId) { return PermissionTenantId == tenantId; }

    /// <summary>
    ///     Fail-closed tenant guard: throws when the permission does not belong to the
    ///     requested tenant scope.
    /// </summary>
    /// <param name="tenantId">Tenant scope the caller is operating in.</param>
    public void EnsureTenantScope(Guid? tenantId)
    {
        if (!IsInTenantScope(tenantId))
        {
            throw new UnauthorizedAccessException(
                $"Permission {Id} belongs to tenant scope {PermissionTenantId} and cannot be used in tenant scope {tenantId}.");
        }
    }

    /// <summary>
    ///     Fail-closed parser for a single raw permission value. Accepts the numeric form
    ///     (e.g. "26") and the enum-name form (e.g. "Delete", case-insensitive); any value
    ///     that does not map to a defined <see cref="PermissionType"/> is rejected.
    /// </summary>
    /// <param name="raw">Raw stored value.</param>
    /// <param name="permissionType">Parsed permission when successful.</param>
    /// <returns>True when the value maps to a defined permission type.</returns>
    protected static bool TryParsePermissionType(string? raw, out PermissionType permissionType)
    {
        permissionType = default;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var trimmed = raw.Trim();

        if (int.TryParse(trimmed, out var numeric))
        {
            // Fail closed on undefined numeric values instead of casting them.
            if (Enum.IsDefined(typeof(PermissionType), numeric))
            {
                permissionType = (PermissionType)numeric;
                return true;
            }

            return false;
        }

        return Enum.TryParse(trimmed, ignoreCase: true, out permissionType) && Enum.IsDefined(permissionType);
    }

    /// <summary>
    ///     Fail-closed mapping over a collection of raw stored permission values
    ///     (string arrays such as <c>["Read", "Delete"]</c>). Values that do not map to a
    ///     defined <see cref="PermissionType"/> are dropped, never guessed.
    /// </summary>
    /// <param name="rawValues">Raw stored values.</param>
    /// <returns>Only the values that map to defined permission types.</returns>
    protected static IEnumerable<PermissionType> ParsePermissionTypes(IEnumerable<string?> rawValues)
    {
        foreach (var raw in rawValues)
        {
            if (TryParsePermissionType(raw, out var permissionType))
            {
                yield return permissionType;
            }
        }
    }

    private void EnsurePermissionIdGenerated()
    {
        if (Id == Guid.Empty)
        {
            Id = Guid.NewGuid();
        }
    }
}
