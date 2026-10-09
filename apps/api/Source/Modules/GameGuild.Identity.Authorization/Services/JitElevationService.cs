using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Service for managing Just-in-Time (JIT) permission elevations
/// </summary>
public class JitElevationService(
    IJitElevationRequestRepository repository,
    IPermissionAuditService auditService,
    ITenantSecurityVersionStore securityVersionStore,
    ILogger<JitElevationService> logger
) : IJitElevationService
{
    private readonly IPermissionAuditService _auditService =
        auditService ?? throw new ArgumentNullException(nameof(auditService));

    private readonly ILogger<JitElevationService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly IJitElevationRequestRepository _repository =
        repository ?? throw new ArgumentNullException(nameof(repository));

    private readonly ITenantSecurityVersionStore _securityVersionStore =
        securityVersionStore ?? throw new ArgumentNullException(nameof(securityVersionStore));

    public async Task<JitElevationRequest> RequestElevationAsync(
        Guid requesterId,
        Guid? tenantId,
        string permission,
        string justification,
        int durationMinutes,
        Guid? resourceId = null,
        string? resourceType = null,
        DateTime? startsAt = null,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation(
            "User {RequesterId} requesting JIT elevation for {Permission} (Duration: {Duration}min)",
            requesterId,
            permission,
            durationMinutes
        );

        var request = new JitElevationRequest
        {
            RequesterId = requesterId,
            TenantId = tenantId,
            Permission = permission,
            ResourceId = resourceId,
            ResourceType = resourceType,
            Justification = justification,
            DurationMinutes = durationMinutes,
            StartsAt = startsAt ?? SystemClock.UtcNow,
            ExpiresAt = (startsAt ?? SystemClock.UtcNow).AddMinutes(durationMinutes),
            Status = ElevationRequestStatus.Pending
        };

        var result = await _repository.CreateAsync(request, cancellationToken).ConfigureAwait(false);

        await _auditService.LogPermissionChangeAsync(
            PermissionOperationType.ElevateJIT,
            requesterId,
            requesterId,
            tenantId,
            permission,
            resourceId,
            resourceType,
            null,
            $"JIT Elevation Requested: {durationMinutes}min",
            justification,
            true,
            null,
            null,
            null,
            cancellationToken
        );

        return result;
    }

    public async Task<JitElevationRequest> ApproveRequestAsync(
        Guid requestId,
        Guid reviewerId,
        string? comments = null,
        CancellationToken cancellationToken = default
    )
    {
        var request = await _repository.GetByIdAsync(requestId, cancellationToken).ConfigureAwait(false);

        if (request == null)
            throw new InvalidOperationException($"Elevation request {requestId} not found");

        // Self-approval is rejected by the entity (reviewer must differ from requester).
        request.Approve(reviewerId, comments);
        await _repository.UpdateAsync(request, cancellationToken).ConfigureAwait(false);

        // Approval may activate the grant (permission mutation): bump the tenant
        // security version so cached permission views invalidate, and audit the review.
        await InvalidateTenantCacheAsync(request.TenantId?.Value, cancellationToken).ConfigureAwait(false);

        await _auditService.LogPermissionChangeAsync(
            PermissionOperationType.Review,
            request.RequesterId,
            reviewerId,
            request.TenantId?.Value,
            request.Permission,
            request.ResourceId,
            request.ResourceType,
            ElevationRequestStatus.Pending.ToString(),
            request.Status.ToString(),
            comments ?? "JIT elevation approved",
            true,
            null,
            null,
            null,
            cancellationToken
        );

        _logger.LogInformation(
            "Reviewer {ReviewerId} approved elevation request {RequestId}",
            reviewerId,
            requestId
        );

        return request;
    }

    public async Task<JitElevationRequest> DenyRequestAsync(
        Guid requestId,
        Guid reviewerId,
        string comments,
        CancellationToken cancellationToken = default
    )
    {
        var request = await _repository.GetByIdAsync(requestId, cancellationToken).ConfigureAwait(false);

        if (request == null)
            throw new InvalidOperationException($"Elevation request {requestId} not found");

        request.Deny(reviewerId, comments);
        await _repository.UpdateAsync(request, cancellationToken).ConfigureAwait(false);

        await _auditService.LogPermissionChangeAsync(
            PermissionOperationType.Deny,
            request.RequesterId,
            reviewerId,
            request.TenantId?.Value,
            request.Permission,
            request.ResourceId,
            request.ResourceType,
            ElevationRequestStatus.Pending.ToString(),
            request.Status.ToString(),
            comments,
            true,
            null,
            null,
            null,
            cancellationToken
        );

        _logger.LogInformation(
            "Reviewer {ReviewerId} denied elevation request {RequestId}",
            reviewerId,
            requestId
        );

        return request;
    }

    public async Task<bool> RevokeElevationAsync(
        Guid requestId,
        Guid revokedBy,
        string reason,
        CancellationToken cancellationToken = default
    )
    {
        var request = await _repository.GetByIdAsync(requestId, cancellationToken).ConfigureAwait(false);

        if (request == null) return false;

        var previousStatus = request.Status.ToString();
        request.Revoke(revokedBy, reason);
        await _repository.UpdateAsync(request, cancellationToken).ConfigureAwait(false);

        // Revocation removes an in-force grant (permission mutation): bump the tenant
        // security version so cached permission views invalidate, and audit the revoke.
        await InvalidateTenantCacheAsync(request.TenantId?.Value, cancellationToken).ConfigureAwait(false);

        await _auditService.LogPermissionChangeAsync(
            PermissionOperationType.Revoke,
            request.RequesterId,
            revokedBy,
            request.TenantId?.Value,
            request.Permission,
            request.ResourceId,
            request.ResourceType,
            previousStatus,
            ElevationRequestStatus.Revoked.ToString(),
            reason,
            true,
            null,
            null,
            null,
            cancellationToken
        );

        _logger.LogInformation(
            "Elevation {RequestId} revoked by {RevokedBy}",
            requestId,
            revokedBy
        );

        return true;
    }

    public async Task<JitElevationRequest?> GetRequestByIdAsync(
        Guid requestId,
        CancellationToken cancellationToken = default
    ) => await _repository.GetByIdAsync(requestId, cancellationToken);

    public async Task<List<JitElevationRequest>> GetPendingRequestsAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default
    ) => await _repository.GetPendingRequestsAsync(tenantId, cancellationToken);

    public async Task<List<JitElevationRequest>> GetUserRequestsAsync(
        Guid userId,
        Guid? tenantId,
        CancellationToken cancellationToken = default
    ) => await _repository.GetByRequesterAsync(userId, tenantId, cancellationToken);

    public async Task<List<JitElevationRequest>> GetActiveElevationsAsync(
        Guid userId,
        Guid? tenantId,
        CancellationToken cancellationToken = default
    ) => await _repository.GetActiveByUserAsync(userId, tenantId, cancellationToken);

    public async Task<bool> HasActiveElevationAsync(
        Guid userId,
        string permission,
        Guid? tenantId,
        Guid? resourceId = null,
        CancellationToken cancellationToken = default
    )
    {
        var activeElevations = await _repository.GetActiveByUserAsync(userId, tenantId, cancellationToken).ConfigureAwait(false);

        return activeElevations.Any(e =>
            e.Permission == permission &&
            e.ResourceId == resourceId &&
            e.IsGrantInForce()
        );
    }

    public async Task<int> CleanupExpiredElevationsAsync(CancellationToken cancellationToken = default)
    {
        var expiredRequests = await _repository.GetExpiredElevationsAsync(cancellationToken).ConfigureAwait(false);

        foreach (var request in expiredRequests)
        {
            request.MarkExpired();
            await _repository.UpdateAsync(request, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation("Marked {Count} elevations as expired", expiredRequests.Count);

        return expiredRequests.Count;
    }

    /// <summary>
    ///     Increments the tenant security version so cached permission views that may
    ///     reflect the previous JIT elevation state are invalidated. Failures are logged
    ///     and non-fatal (the mutation itself has already been persisted).
    /// </summary>
    private async Task InvalidateTenantCacheAsync(Guid? tenantId, CancellationToken cancellationToken)
    {
        var tenantKey = tenantId?.ToString() ?? Guid.Empty.ToString();

        try
        {
            var newVersion = await _securityVersionStore
                .IncrementVersionAsync(tenantKey, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogDebug(
                "Incremented security version for tenant {TenantId} to {Version}",
                tenantKey,
                newVersion
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to increment security version for tenant {TenantId}. Cache may be stale.",
                tenantKey);
        }
    }
}
