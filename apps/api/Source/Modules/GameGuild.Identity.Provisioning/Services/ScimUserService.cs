using GameGuild.Identity.Authentication;
using GameGuild.Identity.Provisioning.Scim;
using GameGuild.Identity.Provisioning.Scim.Filtering;
using GameGuild.Identity.Provisioning.Scim.Patch;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Identity.Provisioning;

/// <summary>
///     Outcome of an idempotent SCIM create: RFC 7644 §3.3 returns 200 with the
/// existing resource when a repeated POST carries the same externalId, and 201
/// with the new resource otherwise.
/// </summary>
public sealed record ScimUserCreateResult(ScimUserResource Resource, bool Created);

/// <summary>
///     Core SCIM User provisioning logic. Every method is tenant-scoped by the
/// provisioning token actor; cross-tenant reads are structurally impossible because
/// the tenant never comes from the request.
/// </summary>
public sealed class ScimUserService(
    IApplicationDbContext dbContext,
    IUserRepository userRepository,
    IScimUserMappingRepository mappingRepository,
    IRefreshTokenRepository refreshTokenRepository,
    ISessionManagementService sessionManagementService,
    IVersionedUserTokenRevocationService tokenRevocationService,
    ILogger<ScimUserService> logger,
    IScimProvisioningAuditSink? auditSink = null)
{
    private const string MemberRole = "Member";

    public async Task<ScimUserResource> GetAsync(ScimProvisioningActor actor, Guid userId, CancellationToken cancellationToken)
    {
        var view = await mappingRepository.GetViewAsync(actor.TenantId, userId, cancellationToken).ConfigureAwait(false)
                   ?? throw ScimException.NotFound($"User {userId} was not found.");

        return ScimUserMapper.ToResource(view);
    }

    public async Task<ScimListResponse<ScimUserResource>> ListAsync(
        ScimProvisioningActor actor,
        string? filter,
        ScimPageRequest page,
        CancellationToken cancellationToken)
    {
        IQueryable<ScimUserView> query = mappingRepository.QueryTenantUsers(actor.TenantId);

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var parsed = ScimFilterParser.Parse(filter);
            var predicate = ScimFilterEvaluator.BuildPredicate<ScimUserView>(parsed, ScimFilterableAttributes.User);
            query = query.Where(predicate);
        }

        var totalResults = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        query = query.OrderBy(view => view.UserId);

        var views = page.Count == 0
            ? []
            : await query
                .Skip(page.StartIndex - 1)
                .Take(page.Count)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

        var resources = views.Select(ScimUserMapper.ToResource).ToList();

        return new ScimListResponse<ScimUserResource>
        {
            TotalResults = totalResults,
            StartIndex = page.StartIndex,
            ItemsPerPage = page.Count,
            Resources = resources
        };
    }

    public async Task<ScimUserCreateResult> CreateAsync(
        ScimProvisioningActor actor,
        ScimUserRequest request,
        CancellationToken cancellationToken)
    {
        var externalId = NormalizeExternalId(request.ExternalId);

        if (externalId is not null)
        {
            var existing = await mappingRepository
                .GetByExternalIdAsync(actor.TenantId, externalId, cancellationToken)
                .ConfigureAwait(false);
            if (existing is not null)
            {
                var existingView = await mappingRepository
                    .GetViewAsync(actor.TenantId, existing.UserId, cancellationToken).ConfigureAwait(false);
                if (existingView is not null)
                {
                    // RFC 7644 idempotent create: repeat POST on the same externalId
                    // returns the existing resource with 200.
                    return new ScimUserCreateResult(ScimUserMapper.ToResource(existingView), Created: false);
                }
            }
        }

        var email = request.ResolvePrimaryEmail();
        if (string.IsNullOrWhiteSpace(email))
        {
            throw ScimException.InvalidValue("A User requires a primary e-mail (emails[0].value).");
        }

        email = email.ToLowerInvariant();
        var displayName = request.ResolveDisplayName();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw ScimException.InvalidValue("A User requires a userName, displayName or name attribute.");
        }

        var (user, adopted) = await FindOrCreateUserAsync(request, email, displayName, cancellationToken).ConfigureAwait(false);
        await EnsureTenantMembershipAsync(actor.TenantId, user.Id, cancellationToken).ConfigureAwait(false);

        var mapping = externalId is null ? null : ScimUserMapping.Create(actor.TenantId, externalId, user.Id);
        if (mapping is not null)
        {
            mappingRepository.Add(mapping);
        }

        await mappingRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var view = await mappingRepository.GetViewAsync(actor.TenantId, user.Id, cancellationToken).ConfigureAwait(false)
                   ?? new ScimUserView
                   {
                       UserId = user.Id,
                       ExternalId = mapping?.ExternalId,
                       UserName = user.Username,
                       Email = user.Email,
                       DisplayName = user.Name,
                       PhoneNumber = user.PhoneNumber,
                       Active = user.IsActive && !user.IsSuspended,
                       CreatedAt = user.CreatedAt,
                       UpdatedAt = user.UpdatedAt
                   };

        await ScimProvisioningAudit.RecordAsync(
            auditSink,
            new ScimProvisioningAuditEvent(
                ScimProvisioningAuditActions.UserCreated,
                user.Id,
                TargetRoleId: null,
                actor.TenantId,
                actor.Subject,
                ExternalId: externalId,
                Detail: adopted ? "Existing platform user linked to the provisioning tenant" : "User created",
                Metadata: new Dictionary<string, object?> { ["adopted"] = adopted }),
            logger,
            cancellationToken).ConfigureAwait(false);

        return new ScimUserCreateResult(ScimUserMapper.ToResource(view), Created: true);
    }

    public async Task<ScimUserResource> ReplaceAsync(
        ScimProvisioningActor actor,
        Guid userId,
        ScimUserRequest request,
        CancellationToken cancellationToken)
    {
        var mapping = await mappingRepository.GetByUserIdAsync(actor.TenantId, userId, cancellationToken).ConfigureAwait(false)
                      ?? throw ScimException.NotFound($"User {userId} was not found.");

        var user = await userRepository.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false)
                   ?? throw ScimException.NotFound($"User {userId} was not found.");

        var state = CaptureUserState(user, mapping);
        ApplyRequest(state, request);
        await MaterializeAsync(actor, user, mapping, state, cancellationToken).ConfigureAwait(false);

        await ScimProvisioningAudit.RecordAsync(
            auditSink,
            new ScimProvisioningAuditEvent(
                ScimProvisioningAuditActions.UserReplaced,
                user.Id,
                TargetRoleId: null,
                actor.TenantId,
                actor.Subject,
                ExternalId: state.ExternalId ?? mapping.ExternalId),
            logger,
            cancellationToken).ConfigureAwait(false);

        return await GetAsync(actor, userId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ScimUserResource> PatchAsync(
        ScimProvisioningActor actor,
        Guid userId,
        ScimPatchRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Operations is not { Count: > 0 })
        {
            throw ScimException.BadPayload("A patch request requires at least one operation.");
        }

        var mapping = await mappingRepository.GetByUserIdAsync(actor.TenantId, userId, cancellationToken).ConfigureAwait(false)
                      ?? throw ScimException.NotFound($"User {userId} was not found.");

        var user = await userRepository.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false)
                   ?? throw ScimException.NotFound($"User {userId} was not found.");

        var state = CaptureUserState(user, mapping);
        ScimUserPatchApplier.Apply(state, request.Operations);
        await MaterializeAsync(actor, user, mapping, state, cancellationToken).ConfigureAwait(false);

        await ScimProvisioningAudit.RecordAsync(
            auditSink,
            new ScimProvisioningAuditEvent(
                ScimProvisioningAuditActions.UserPatched,
                user.Id,
                TargetRoleId: null,
                actor.TenantId,
                actor.Subject,
                ExternalId: state.ExternalId ?? mapping.ExternalId,
                Metadata: new Dictionary<string, object?>
                {
                    ["operations"] = request.Operations.Select(operation => operation.NormalizeOp()).ToArray()
                }),
            logger,
            cancellationToken).ConfigureAwait(false);

        return await GetAsync(actor, userId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     RFC 7644 §3.6 DELETE: soft-deletes the user (deprovision) and revokes every
    /// session and token so access stops immediately.
    /// </summary>
    public async Task DeleteAsync(ScimProvisioningActor actor, Guid userId, CancellationToken cancellationToken)
    {
        var mapping = await mappingRepository.GetByUserIdAsync(actor.TenantId, userId, cancellationToken).ConfigureAwait(false)
                      ?? throw ScimException.NotFound($"User {userId} was not found.");

        var user = await userRepository.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false)
                   ?? throw ScimException.NotFound($"User {userId} was not found.");

        user.MarkDeleted();
        await userRepository.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
        await userRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Deprovision revocation: refresh tokens, sessions and legacy access tokens die now.
        await refreshTokenRepository.RevokeAllForUserAsync(userId, null, cancellationToken).ConfigureAwait(false);
        await sessionManagementService.TerminateAllUserSessionsAsync(
            userId,
            SessionTerminationReason.AdministrativeTermination,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        user.IncrementTokenVersion();
        await userRepository.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
        await userRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await tokenRevocationService.RevokeAllUserTokensAsync(
            userId,
            user.TokenVersion,
            "SCIM deprovision",
            cancellationToken).ConfigureAwait(false);

        // Tombstone the mapping so the externalId cannot be silently re-linked by a new user.
        mapping.SoftDelete();
        await mappingRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await ScimProvisioningAudit.RecordAsync(
            auditSink,
            new ScimProvisioningAuditEvent(
                ScimProvisioningAuditActions.UserDeprovisioned,
                user.Id,
                TargetRoleId: null,
                actor.TenantId,
                actor.Subject,
                ExternalId: mapping.ExternalId,
                Detail: "User soft-deleted; sessions and tokens revoked"),
            logger,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<(User User, bool Adopted)> FindOrCreateUserAsync(
        ScimUserRequest request,
        string email,
        string displayName,
        CancellationToken cancellationToken)
    {
        var existing = await userRepository.GetByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        if (existing is not null && !existing.IsDeleted)
        {
            return (existing, Adopted: true);
        }

        if (existing is { IsDeleted: true })
        {
            throw ScimException.Uniqueness(
                $"A deactivated account already exists for '{email}'; it must be restored by an administrator before provisioning.");
        }

        var user = User.CreateOAuthUser(email, displayName, emailVerified: false);

        if (!string.IsNullOrWhiteSpace(request.UserName)
            && !await userRepository.ExistsByUsernameAsync(request.UserName.Trim(), cancellationToken).ConfigureAwait(false))
        {
            try
            {
                user.Username = UsernameSlug.FromExplicit(request.UserName.Trim());
            }
            catch (ArgumentException)
            {
                // Keep the generated username when the requested handle cannot be canonicalized.
            }
        }

        user.PhoneNumber = request.ResolvePhoneNumber();
        if (request.Active == false)
        {
            user.Deactivate();
        }

        await userRepository.AddAsync(user, cancellationToken).ConfigureAwait(false);
        await userRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return (user, Adopted: false);
    }

    private async Task EnsureTenantMembershipAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var memberships = dbContext.Set<TenantMember>();
        var membership = await memberships
            .FirstOrDefaultAsync(member => member.TenantId == tenantId && member.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        if (membership is null)
        {
            memberships.Add(new TenantMember
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = userId,
                Role = MemberRole,
                IsActive = true,
                JoinedAt = SystemClock.UtcNow
            });
        }
        else if (!membership.IsActive)
        {
            membership.Activate();
        }
    }

    private static ScimUserMutableState CaptureUserState(User user, ScimUserMapping mapping)
    {
        var givenName = FirstToken(user.Name);
        return new ScimUserMutableState
        {
            UserName = user.Username,
            DisplayName = user.Name,
            GivenName = givenName,
            FamilyName = givenName is null ? user.Name : user.Name[givenName.Length..].Trim(),
            PrimaryEmail = user.Email,
            PhoneNumber = user.PhoneNumber,
            Active = user.IsActive && !user.IsSuspended,
            ExternalId = mapping.ExternalId
        };
    }

    private static void ApplyRequest(ScimUserMutableState state, ScimUserRequest request)
    {
        if (request.UserName is not null)
        {
            state.UserName = request.UserName.Trim();
        }

        if (request.DisplayName is not null)
        {
            state.DisplayName = request.DisplayName.Trim();
            state.DisplayNameExplicit = true;
        }

        if (request.ExternalId is not null)
        {
            state.ExternalId = NormalizeExternalId(request.ExternalId);
        }

        if (request.Name is not null)
        {
            state.GivenName = request.Name.GivenName;
            state.FamilyName = request.Name.FamilyName;
            if (request.Name.Formatted is not null)
            {
                state.DisplayName = request.Name.Formatted.Trim();
                state.DisplayNameExplicit = true;
            }
        }

        var email = request.ResolvePrimaryEmail();
        if (email is not null)
        {
            state.PrimaryEmail = email.ToLowerInvariant();
        }

        var phone = request.ResolvePhoneNumber();
        if (phone is not null)
        {
            state.PhoneNumber = phone;
        }

        if (request.Active.HasValue)
        {
            state.Active = request.Active.Value;
        }
    }

    private async Task MaterializeAsync(
        ScimProvisioningActor actor,
        User user,
        ScimUserMapping mapping,
        ScimUserMutableState state,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(state.DisplayName))
        {
            throw ScimException.InvalidValue("displayName must not be empty after the update.");
        }

        if (string.IsNullOrWhiteSpace(state.PrimaryEmail))
        {
            throw ScimException.Mutability("The primary e-mail cannot be removed.");
        }

        var normalizedEmail = state.PrimaryEmail.ToLowerInvariant();
        if (!normalizedEmail.Equals(user.Email, StringComparison.OrdinalIgnoreCase))
        {
            var owner = await userRepository.GetByEmailAsync(normalizedEmail, cancellationToken).ConfigureAwait(false);
            if (owner is not null && owner.Id != user.Id)
            {
                throw ScimException.Uniqueness($"The e-mail '{normalizedEmail}' is already in use.");
            }
        }

        if (state.UserName is not null
            && !string.IsNullOrWhiteSpace(state.UserName)
            && !state.UserName.Equals(user.Username, StringComparison.OrdinalIgnoreCase))
        {
            var taken = await userRepository
                .ExistsByUsernameAsync(state.UserName, cancellationToken).ConfigureAwait(false);
            if (taken && !await IsSameUserAsync(state.UserName, user.Id, cancellationToken).ConfigureAwait(false))
            {
                throw ScimException.Uniqueness($"The userName '{state.UserName}' is already in use.");
            }

            user.Username = UsernameSlug.FromExplicit(state.UserName);
        }

        user.Name = ComposeName(state);
        user.Email = normalizedEmail;
        user.PhoneNumber = state.PhoneNumber;

        var active = state.Active ?? true;
        if (active && !user.IsActive)
        {
            user.Activate();
        }
        else if (!active && user.IsActive)
        {
            user.Deactivate();
        }

        if (state.ExternalId is not null
            && !state.ExternalId.Equals(mapping.ExternalId, StringComparison.Ordinal))
        {
            var conflictingMapping = await mappingRepository
                .GetByExternalIdAsync(actor.TenantId, state.ExternalId, cancellationToken).ConfigureAwait(false);
            if (conflictingMapping is not null && conflictingMapping.UserId != user.Id)
            {
                throw ScimException.Uniqueness(
                    $"The externalId '{state.ExternalId}' is already mapped to another user in this tenant.");
            }

            mapping.ExternalId = state.ExternalId;
            mapping.Touch();
        }

        await userRepository.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
        await mappingRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> IsSameUserAsync(string username, Guid userId, CancellationToken cancellationToken)
    {
        var candidate = await userRepository.GetByUsernameAsync(username, cancellationToken).ConfigureAwait(false);
        return candidate is not null && candidate.Id == userId;
    }

    private static string ComposeName(ScimUserMutableState state)
    {
        // An explicit displayName (or name.formatted) from the current request wins;
        // name parts only compose the stored display value when it was not provided.
        if (!string.IsNullOrWhiteSpace(state.DisplayName) && state.DisplayNameExplicit)
        {
            return state.DisplayName;
        }

        var hasParts = !string.IsNullOrWhiteSpace(state.GivenName) || !string.IsNullOrWhiteSpace(state.FamilyName);
        return hasParts
            ? $"{state.GivenName} {state.FamilyName}".Trim()
            : state.DisplayName!;
    }

    private static string? NormalizeExternalId(string? externalId)
        => string.IsNullOrWhiteSpace(externalId) ? null : externalId.Trim();

    private static string? FirstToken(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        var separator = displayName.IndexOf(' ');
        return separator < 0 ? displayName : displayName[..separator];
    }
}
