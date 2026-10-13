using GameGuild.Identity.Authentication;
using GameGuild.Identity.Provisioning.Scim;
using GameGuild.Identity.Provisioning.Scim.Filtering;
using GameGuild.Identity.Provisioning.Scim.Patch;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Identity.Provisioning;

/// <summary>Outcome of an idempotent SCIM group create.</summary>
public sealed record ScimGroupCreateResult(ScimGroupResource Resource, bool Created);

/// <summary>
///     Core SCIM Group provisioning logic. A group maps to a tenant role plus a
///     <see cref="ScimGroupMapping"/>; members map to role assignments. Membership
///     changes are permission mutations: they are audited (permission audit log through
///     the change tracker) and the tenant security version is bumped so cached
///     permission decisions are invalidated immediately.
/// </summary>
public sealed class ScimGroupService(
    IApplicationDbContext dbContext,
    IRoleRepository roleRepository,
    IScimGroupMappingRepository mappingRepository,
    IUserRepository userRepository,
    ITenantSecurityVersionStore? securityVersionStore,
    ILogger<ScimGroupService> logger,
    IScimProvisioningAuditSink? auditSink = null)
{
    private const string ScimRoleDescription = "Role provisioned through SCIM";

    public async Task<ScimGroupResource> GetAsync(ScimProvisioningActor actor, Guid roleId, CancellationToken cancellationToken)
    {
        var view = await mappingRepository.GetViewAsync(actor.TenantId, roleId, cancellationToken).ConfigureAwait(false)
                   ?? throw ScimException.NotFound($"Group {roleId} was not found.");

        return ScimGroupMapper.ToResource(view, await LoadMembersAsync(roleId, cancellationToken).ConfigureAwait(false));
    }

    public async Task<ScimListResponse<ScimGroupResource>> ListAsync(
        ScimProvisioningActor actor,
        string? filter,
        ScimPageRequest page,
        CancellationToken cancellationToken)
    {
        IQueryable<ScimGroupView> query = mappingRepository.QueryTenantGroups(actor.TenantId);

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var parsed = ScimFilterParser.Parse(filter);
            var predicate = ScimFilterEvaluator.BuildPredicate<ScimGroupView>(parsed, ScimFilterableAttributes.Group);
            query = query.Where(predicate);
        }

        var totalResults = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        query = query.OrderBy(view => view.RoleId);

        var views = page.Count == 0
            ? []
            : await query
                .Skip(page.StartIndex - 1)
                .Take(page.Count)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

        var groups = new List<ScimGroupResource>(views.Count);
        foreach (var view in views)
        {
            var members = await LoadMembersAsync(view.RoleId, cancellationToken).ConfigureAwait(false);
            groups.Add(ScimGroupMapper.ToResource(view, members));
        }

        return new ScimListResponse<ScimGroupResource>
        {
            TotalResults = totalResults,
            StartIndex = page.StartIndex,
            ItemsPerPage = page.Count,
            Resources = groups
        };
    }

    public async Task<ScimGroupCreateResult> CreateAsync(
        ScimProvisioningActor actor,
        ScimGroupRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            throw ScimException.InvalidValue("A Group requires a displayName.");
        }

        var externalId = string.IsNullOrWhiteSpace(request.ExternalId) ? null : request.ExternalId.Trim();

        if (externalId is not null)
        {
            var existing = await mappingRepository
                .GetByExternalIdAsync(actor.TenantId, externalId, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                var group = await GetAsync(actor, existing.RoleId, cancellationToken).ConfigureAwait(false);
                return new ScimGroupCreateResult(group, Created: false);
            }
        }

        var role = new Role(request.DisplayName.Trim(), ScimRoleDescription, actor.TenantId);
        var createdRole = await roleRepository.AddAsync(role, cancellationToken).ConfigureAwait(false);

        var mapping = externalId is null ? null : ScimGroupMapping.Create(actor.TenantId, externalId, createdRole.Id);
        if (mapping is not null)
        {
            mappingRepository.Add(mapping);
            await mappingRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        if (request.Members is { Count: > 0 })
        {
            var memberIds = ScimGroupPatchApplier.ReadMemberIds(
                SerializeMembers(request.Members)).ToList();
            await ReconcileMembershipAsync(actor, createdRole.Id, memberIds, cancellationToken).ConfigureAwait(false);
        }

        await ScimProvisioningAudit.RecordAsync(
            auditSink,
            new ScimProvisioningAuditEvent(
                ScimProvisioningAuditActions.GroupCreated,
                TargetUserId: null,
                createdRole.Id,
                actor.TenantId,
                actor.Subject,
                ExternalId: externalId),
            logger,
            cancellationToken).ConfigureAwait(false);

        var resource = await GetAsync(actor, createdRole.Id, cancellationToken).ConfigureAwait(false);
        return new ScimGroupCreateResult(resource, Created: true);
    }

    public async Task<ScimGroupResource> ReplaceAsync(
        ScimProvisioningActor actor,
        Guid roleId,
        ScimGroupRequest request,
        CancellationToken cancellationToken)
    {
        var mapping = await mappingRepository.GetByRoleIdAsync(actor.TenantId, roleId, cancellationToken).ConfigureAwait(false)
                      ?? throw ScimException.NotFound($"Group {roleId} was not found.");

        var role = await roleRepository.GetByIdAsync(roleId, cancellationToken).ConfigureAwait(false)
                   ?? throw ScimException.NotFound($"Group {roleId} was not found.");

        if (!string.IsNullOrWhiteSpace(request.DisplayName)
            && !request.DisplayName.Trim().Equals(role.Name, StringComparison.Ordinal))
        {
            var nameConflict = await roleRepository
                .GetByNameAsync(request.DisplayName.Trim(), actor.TenantId, cancellationToken).ConfigureAwait(false);
            if (nameConflict is not null && nameConflict.Id != role.Id)
            {
                throw ScimException.Uniqueness($"A group named '{request.DisplayName.Trim()}' already exists in this tenant.");
            }

            role.Name = request.DisplayName.Trim();
            await roleRepository.UpdateAsync(role, cancellationToken).ConfigureAwait(false);
        }

        if (request.ExternalId is not null
            && !request.ExternalId.Trim().Equals(mapping.ExternalId, StringComparison.Ordinal))
        {
            var conflict = await mappingRepository
                .GetByExternalIdAsync(actor.TenantId, request.ExternalId.Trim(), cancellationToken).ConfigureAwait(false);
            if (conflict is not null && conflict.RoleId != roleId)
            {
                throw ScimException.Uniqueness(
                    $"The externalId '{request.ExternalId.Trim()}' is already mapped to another group in this tenant.");
            }

            mapping.ExternalId = request.ExternalId.Trim();
            mapping.Touch();
            await mappingRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        if (request.Members is not null)
        {
            var memberIds = ScimGroupPatchApplier.ReadMemberIds(
                SerializeMembers(request.Members)).ToList();
            await ReconcileMembershipAsync(actor, roleId, memberIds, cancellationToken).ConfigureAwait(false);
        }

        await ScimProvisioningAudit.RecordAsync(
            auditSink,
            new ScimProvisioningAuditEvent(
                ScimProvisioningAuditActions.GroupReplaced,
                TargetUserId: null,
                roleId,
                actor.TenantId,
                actor.Subject,
                ExternalId: mapping.ExternalId),
            logger,
            cancellationToken).ConfigureAwait(false);

        return await GetAsync(actor, roleId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ScimGroupResource> PatchAsync(
        ScimProvisioningActor actor,
        Guid roleId,
        ScimPatchRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Operations is not { Count: > 0 })
        {
            throw ScimException.BadPayload("A patch request requires at least one operation.");
        }

        var mapping = await mappingRepository.GetByRoleIdAsync(actor.TenantId, roleId, cancellationToken).ConfigureAwait(false)
                      ?? throw ScimException.NotFound($"Group {roleId} was not found.");

        var role = await roleRepository.GetByIdAsync(roleId, cancellationToken).ConfigureAwait(false)
                   ?? throw ScimException.NotFound($"Group {roleId} was not found.");

        var currentMembers = await dbContext.Set<UserRole>()
            .AsNoTracking()
            .Where(userRole => userRole.RoleId == roleId
                               && userRole.DeletedAt == null
                               && (userRole.ExpiresAt == null || userRole.ExpiresAt > SystemClock.UtcNow))
            .Select(userRole => userRole.UserId.ToString())
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var state = new ScimGroupMutableState
        {
            DisplayName = role.Name,
            ExternalId = mapping.ExternalId
        };
        state.MemberUserIds.AddRange(currentMembers);

        ScimGroupPatchApplier.Apply(state, request.Operations);

        if (!string.IsNullOrWhiteSpace(state.DisplayName)
            && !state.DisplayName.Trim().Equals(role.Name, StringComparison.Ordinal))
        {
            var nameConflict = await roleRepository
                .GetByNameAsync(state.DisplayName.Trim(), actor.TenantId, cancellationToken).ConfigureAwait(false);
            if (nameConflict is not null && nameConflict.Id != role.Id)
            {
                throw ScimException.Uniqueness($"A group named '{state.DisplayName.Trim()}' already exists in this tenant.");
            }

            role.Name = state.DisplayName.Trim();
            await roleRepository.UpdateAsync(role, cancellationToken).ConfigureAwait(false);
        }

        if (state.ExternalId is not null
            && !state.ExternalId.Trim().Equals(mapping.ExternalId, StringComparison.Ordinal))
        {
            var conflict = await mappingRepository
                .GetByExternalIdAsync(actor.TenantId, state.ExternalId.Trim(), cancellationToken).ConfigureAwait(false);
            if (conflict is not null && conflict.RoleId != roleId)
            {
                throw ScimException.Uniqueness(
                    $"The externalId '{state.ExternalId.Trim()}' is already mapped to another group in this tenant.");
            }

            mapping.ExternalId = state.ExternalId.Trim();
            mapping.Touch();
            await mappingRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        await ReconcileMembershipAsync(actor, roleId, state.MemberUserIds, cancellationToken).ConfigureAwait(false);

        await ScimProvisioningAudit.RecordAsync(
            auditSink,
            new ScimProvisioningAuditEvent(
                ScimProvisioningAuditActions.GroupPatched,
                TargetUserId: null,
                roleId,
                actor.TenantId,
                actor.Subject,
                ExternalId: mapping.ExternalId,
                Metadata: new Dictionary<string, object?>
                {
                    ["operations"] = request.Operations.Select(operation => operation.NormalizeOp()).ToArray()
                }),
            logger,
            cancellationToken).ConfigureAwait(false);

        return await GetAsync(actor, roleId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     RFC 7644 §3.6 DELETE for groups: removes every membership and tombstones the
    /// mapping; the backing role is deactivated rather than dropped so audit history
    /// keeps a stable reference.
    /// </summary>
    public async Task DeleteAsync(ScimProvisioningActor actor, Guid roleId, CancellationToken cancellationToken)
    {
        var mapping = await mappingRepository.GetByRoleIdAsync(actor.TenantId, roleId, cancellationToken).ConfigureAwait(false)
                      ?? throw ScimException.NotFound($"Group {roleId} was not found.");

        await ReconcileMembershipAsync(actor, roleId, [], cancellationToken).ConfigureAwait(false);

        var role = await roleRepository.GetByIdAsync(roleId, cancellationToken).ConfigureAwait(false);
        if (role is not null)
        {
            role.IsActive = false;
            await roleRepository.UpdateAsync(role, cancellationToken).ConfigureAwait(false);
        }

        mapping.SoftDelete();
        await mappingRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await BumpTenantSecurityVersionAsync(actor, cancellationToken).ConfigureAwait(false);

        await ScimProvisioningAudit.RecordAsync(
            auditSink,
            new ScimProvisioningAuditEvent(
                ScimProvisioningAuditActions.GroupDeleted,
                TargetUserId: null,
                roleId,
                actor.TenantId,
                actor.Subject,
                ExternalId: mapping.ExternalId,
                Detail: "Memberships removed; backing role deactivated"),
            logger,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<ScimMember>> LoadMembersAsync(Guid roleId, CancellationToken cancellationToken)
    {
        var members = await dbContext.Set<UserRole>()
            .AsNoTracking()
            .Where(userRole => userRole.RoleId == roleId
                               && userRole.DeletedAt == null
                               && (userRole.ExpiresAt == null || userRole.ExpiresAt > SystemClock.UtcNow))
            .Join(dbContext.Set<User>().AsNoTracking(),
                userRole => userRole.UserId,
                user => user.Id,
                (userRole, user) => new { userRole.UserId, Display = user.Username ?? user.Email })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return members
            .Select(member => new ScimMember
            {
                Value = member.UserId.ToString(),
                Display = member.Display
            })
            .ToList();
    }

    /// <summary>
    ///     Reconciles role memberships to the desired member list. Membership is a
    ///     permission mutation: the DbContext change tracker audits every added/removed
    ///     UserRole and the tenant security version is bumped once per changed batch.
    /// </summary>
    private async Task ReconcileMembershipAsync(
        ScimProvisioningActor actor,
        Guid roleId,
        IReadOnlyList<string> desiredMemberIds,
        CancellationToken cancellationToken)
    {
        var desiredUserIds = new HashSet<Guid>();
        foreach (var memberValue in desiredMemberIds)
        {
            if (!Guid.TryParse(memberValue, out var userId))
            {
                throw ScimException.InvalidValue($"The member value '{memberValue}' is not a valid user id.");
            }

            desiredUserIds.Add(userId);
        }

        foreach (var userId in desiredUserIds)
        {
            var user = await userRepository.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
            if (user is null || user.IsDeleted)
            {
                throw ScimException.InvalidValue($"The member value '{memberValue(userId)}' does not reference a provisionable user.");
            }
        }

        var assignments = await dbContext.Set<UserRole>()
            .Where(userRole => userRole.RoleId == roleId && userRole.DeletedAt == null)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var changed = false;

        foreach (var assignment in assignments)
        {
            if (!desiredUserIds.Remove(assignment.UserId))
            {
                dbContext.Set<UserRole>().Remove(assignment);
                changed = true;
            }
        }

        foreach (var userId in desiredUserIds)
        {
            dbContext.Set<UserRole>().Add(new UserRole(userId, roleId));
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await BumpTenantSecurityVersionAsync(actor, cancellationToken).ConfigureAwait(false);

        await ScimProvisioningAudit.RecordAsync(
            auditSink,
            new ScimProvisioningAuditEvent(
                ScimProvisioningAuditActions.MembersChanged,
                TargetUserId: null,
                roleId,
                actor.TenantId,
                actor.Subject,
                Detail: "Group membership reconciled"),
            logger,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task BumpTenantSecurityVersionAsync(ScimProvisioningActor actor, CancellationToken cancellationToken)
    {
        if (securityVersionStore is null)
        {
            return;
        }

        await securityVersionStore
            .IncrementVersionAsync(actor.TenantId.ToString(), cancellationToken)
            .ConfigureAwait(false);
    }

    private static string memberValue(Guid userId) => userId.ToString();

    private static System.Text.Json.Nodes.JsonArray SerializeMembers(IReadOnlyList<ScimMember> members)
    {
        var array = new System.Text.Json.Nodes.JsonArray();
        foreach (var member in members)
        {
            var entry = new System.Text.Json.Nodes.JsonObject { ["value"] = member.Value };
            array.Add(entry);
        }

        return array;
    }
}
