namespace GameGuild.Identity.Authorization;

/// <summary>
///     Database-backed implementation of Access Control List service.
///     Uses the IAccessControlListEntryRepository for persistence.
///     Implements deny-first algorithm for ACL evaluation.
/// </summary>
public sealed class DatabaseAccessControlListService(
    IAccessControlListEntryRepository repository, 
    ITenantSecurityVersionRepository versionRepository) : IAccessControlListService
{
    /// <inheritdoc />
    public async Task<AccessLevel> EvaluateAccessAsync(
        AclSubject subject,
        Guid tenantId,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken = default)
    {
        var evaluation = await EvaluateAccessTimeBoundAsync(subject, tenantId, resourceType, resourceId, cancellationToken)
            .ConfigureAwait(false);
        return evaluation.AccessLevel;
    }

    /// <inheritdoc />
    public async Task<TimeBoundAccessEvaluation> EvaluateAccessTimeBoundAsync(
        AclSubject subject,
        Guid tenantId,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken = default)
    {
        // Get all principals for this subject (user, roles, groups, anonymous)
        var principals = subject.GetPrincipals().ToList();

        // Fetch all matching ACL entries
        var entries = await repository.GetByResourceAndPrincipalsAsync(
            tenantId, resourceType, resourceId, principals, cancellationToken).ConfigureAwait(false);

        if (entries.Count == 0)
            return new TimeBoundAccessEvaluation(AccessLevel.None, EarliestEffectiveExpirationUtc: null);

        // Filter to only effective (active and not expired) entries
        var effectiveEntries = entries.Where(e => e.IsEffective).ToList();

        if (effectiveEntries.Count == 0)
            return new TimeBoundAccessEvaluation(AccessLevel.None, EarliestEffectiveExpirationUtc: null);

        // DENY-FIRST ALGORITHM:
        // 1. Find the highest deny level - if any deny matches, that's the ceiling
        // 2. Find the highest allow level
        // 3. Effective access = min(highest allow, inverse of highest deny)

        var denyEntries = effectiveEntries.Where(e => e.IsDenied).ToList();
        var allowEntries = effectiveEntries.Where(e => !e.IsDenied).ToList();

        // The earliest effective expiration bounds how long any cached copy of this decision may be
        // served: time-based expiry advances no security version, so the cached wrapper must clamp
        // its TTL instead. Expired entries were already filtered out above and cannot extend it.
        DateTime? earliestExpiration = effectiveEntries
            .Select(e => e.ExpiresAt)
            .Where(t => t.HasValue)
            .Min();

        // If there are any deny entries, the highest deny level blocks access at that level and above
        if (denyEntries.Count > 0)
        {
            var highestDeny = denyEntries.Max(e => e.AccessLevel);

            // If denied at None level (explicit block), no access at all
            if (highestDeny == AccessLevel.None)
                return new TimeBoundAccessEvaluation(AccessLevel.None, earliestExpiration);

            // Find highest allowed level that's below the deny threshold
            if (allowEntries.Count == 0)
                return new TimeBoundAccessEvaluation(AccessLevel.None, earliestExpiration);

            var highestAllow = allowEntries.Max(e => e.AccessLevel);

            // Return the lower of: highest allow vs one level below highest deny
            var effective = (AccessLevel)Math.Min((int)highestAllow, (int)highestDeny - 1);
            return new TimeBoundAccessEvaluation(effective, earliestExpiration);
        }

        // No denies - return highest allowed level
        var level = allowEntries.Count > 0
            ? allowEntries.Max(e => e.AccessLevel)
            : AccessLevel.None;
        return new TimeBoundAccessEvaluation(level, earliestExpiration);
    }

    /// <inheritdoc />
    public async Task<bool> HasAccessAsync(
        AclSubject subject,
        Guid tenantId,
        string resourceType,
        string resourceId,
        AccessLevel requiredLevel,
        CancellationToken cancellationToken = default)
    {
        var effectiveLevel = await EvaluateAccessAsync(subject, tenantId, resourceType, resourceId, cancellationToken)
            .ConfigureAwait(false);
        return effectiveLevel >= requiredLevel;
    }

    /// <inheritdoc />
    public async Task<AccessLevel> GetAccessLevelAsync(
        Guid userId,
        Guid tenantId,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken = default)
    {
        // Backward compatibility: create subject for just this user
        var subject = AclSubject.ForUser(userId);
        return await EvaluateAccessAsync(subject, tenantId, resourceType, resourceId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task GrantAccessAsync(
        Guid grantorId,
        AclPrincipalType principalType,
        Guid? principalId,
        Guid tenantId,
        string resourceType,
        string resourceId,
        AccessLevel accessLevel,
        CancellationToken cancellationToken = default)
    {
        // Check if entry already exists
        var existing = await repository.GetByPrincipalAndResourceAsync(
            tenantId, principalType, principalId, resourceType, resourceId, cancellationToken).ConfigureAwait(false);

        if (existing != null)
        {
            // Update existing entry
            existing.AccessLevel = accessLevel;
            existing.IsDenied = false;
            existing.GrantedBy = grantorId;
            existing.GrantedAt = SystemClock.UtcNow;
            existing.IsActive = true;
            await repository.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Create new entry
            var entry = new AccessControlListEntry
            {
                TenantId = tenantId,
                PrincipalType = principalType,
                PrincipalId = principalId,
                ResourceType = resourceType,
                ResourceId = resourceId,
                AccessLevel = accessLevel,
                IsDenied = false,
                GrantedBy = grantorId,
                GrantedAt = SystemClock.UtcNow,
                IsActive = true
            };
            await repository.AddAsync(entry, cancellationToken).ConfigureAwait(false);
        }

        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Increment security version to invalidate caches
        await versionRepository.IncrementVersionAsync(
            tenantId, 
            $"ACL grant: {principalType}/{principalId} -> {resourceType}/{resourceId}", 
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task GrantAccessAsync(
        Guid grantorId,
        Guid granteeId,
        Guid tenantId,
        string resourceType,
        string resourceId,
        AccessLevel accessLevel,
        CancellationToken cancellationToken = default)
    {
        // Backward compatibility overload
        return GrantAccessAsync(
            grantorId, AclPrincipalType.User, granteeId, 
            tenantId, resourceType, resourceId, accessLevel, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DenyAccessAsync(
        Guid grantorId,
        AclPrincipalType principalType,
        Guid? principalId,
        Guid tenantId,
        string resourceType,
        string resourceId,
        AccessLevel accessLevel,
        CancellationToken cancellationToken = default)
    {
        // Check if entry already exists
        var existing = await repository.GetByPrincipalAndResourceAsync(
            tenantId, principalType, principalId, resourceType, resourceId, cancellationToken).ConfigureAwait(false);

        if (existing != null)
        {
            // Update existing entry to deny
            existing.AccessLevel = accessLevel;
            existing.IsDenied = true;
            existing.GrantedBy = grantorId;
            existing.GrantedAt = SystemClock.UtcNow;
            existing.IsActive = true;
            await repository.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Create new deny entry
            var entry = new AccessControlListEntry
            {
                TenantId = tenantId,
                PrincipalType = principalType,
                PrincipalId = principalId,
                ResourceType = resourceType,
                ResourceId = resourceId,
                AccessLevel = accessLevel,
                IsDenied = true,
                GrantedBy = grantorId,
                GrantedAt = SystemClock.UtcNow,
                IsActive = true
            };
            await repository.AddAsync(entry, cancellationToken).ConfigureAwait(false);
        }

        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Increment security version to invalidate caches
        await versionRepository.IncrementVersionAsync(
            tenantId, 
            $"ACL deny: {principalType}/{principalId} -> {resourceType}/{resourceId}", 
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RevokeAccessAsync(
        Guid revokerId,
        AclPrincipalType principalType,
        Guid? principalId,
        Guid tenantId,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken = default)
    {
        var entry = await repository.GetByPrincipalAndResourceAsync(
            tenantId, principalType, principalId, resourceType, resourceId, cancellationToken).ConfigureAwait(false);

        if (entry != null)
        {
            await repository.DeleteAsync(entry, cancellationToken).ConfigureAwait(false);
            await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Increment security version to invalidate caches
            await versionRepository.IncrementVersionAsync(
                tenantId, 
                $"ACL revoke: {principalType}/{principalId} -> {resourceType}/{resourceId}", 
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public Task RevokeAccessAsync(
        Guid revokerId,
        Guid userId,
        Guid tenantId,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken = default)
    {
        // Backward compatibility overload
        return RevokeAccessAsync(
            revokerId, AclPrincipalType.User, userId, 
            tenantId, resourceType, resourceId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> HasAccessAsync(
        Guid userId,
        Guid tenantId,
        string resourceType,
        string resourceId,
        AccessLevel requiredLevel,
        CancellationToken cancellationToken = default)
    {
        // Backward compatibility: create subject for just this user
        var subject = AclSubject.ForUser(userId);
        return await HasAccessAsync(subject, tenantId, resourceType, resourceId, requiredLevel, cancellationToken)
            .ConfigureAwait(false);
    }
}
