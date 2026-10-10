using GameGuild.Identity.Authentication;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GameGuild.API.Core.Security;

/// <summary>
/// Closes the risk-detection loop into containment: confirmed-compromise suspicious-login
/// signals (impossible travel on a successful sign-in, brute force that eventually succeeded)
/// revoke every refresh token, terminate every active session, and cut off already-issued
/// access tokens by bumping the user's token version.
/// </summary>
/// <remarks>
///     <para>
///         Runs inside the durable consumer's inbox transaction, so the revocations commit
///         atomically with the inbox receipt. Idempotency per <c>EventId</c> is provided by that
///         inbox (one receipt per <c>EventId</c> + consumer), exactly like the sibling alert
///         consumers; the revocation operations are individually monotonic, so a re-delivery
///         before the receipt commits can only revoke more, never less.
///     </para>
///     <para>
///         <see cref="SecurityAlertKinds.LoginStepUpRequired" /> is a prevention signal (the
///         sign-in was challenged and did not complete), not a confirmed compromise: it is a
///         no-op here and keeps its notify-only behavior.
///     </para>
///     <para>
///         Gated by <c>Authentication:RiskEventRevocation:Enabled</c> (default on). The revocation
///         is audited through the central authentication audit pipeline as a security event.
///     </para>
/// </remarks>
internal sealed class RiskEventSessionRevocationHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    ISessionManagementService sessions,
    IVersionedUserTokenRevocationService tokenRevocation,
    IRefreshTokenLifecycleRecorder lifecycleRecorder,
    IAuthenticationAuditEventSink auditEventSink,
    IConfiguration configuration,
    ILogger<RiskEventSessionRevocationHandler> logger) : IIntegrationEventHandler<SuspiciousLoginDetectedV1>
{
    private const string EnabledPath = "Authentication:RiskEventRevocation:Enabled";
    private const string RevocationAuditAction = "Authentication.RiskEventRevocation";

    /// <summary>Alert kinds that confirm an account compromise rather than a blocked attempt.</summary>
    private static readonly HashSet<string> ConfirmedCompromiseKinds =
    [
        SecurityAlertKinds.BruteForceDetected,
        SecurityAlertKinds.ImpossibleTravel
    ];

    public async Task HandleAsync(SuspiciousLoginDetectedV1 @event, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        if (!configuration.GetValue(EnabledPath, true))
        {
            return;
        }

        if (!ConfirmedCompromiseKinds.Contains(@event.AlertKind))
        {
            // Step-up challenges (and any future prevention-only kind) stay notify-only.
            return;
        }

        var userId = @event.UserId;
        if (userId == Guid.Empty)
        {
            return;
        }

        var user = await users.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null || user.IsDeleted)
        {
            logger.LogWarning(
                "Risk-event revocation skipped for user {UserId} ({AlertKind}): the account no longer exists.",
                userId, @event.AlertKind);
            return;
        }

        // Mirrors RevokeAllUserTokensHandler and the refresh-replay containment path: revoke every
        // refresh token, terminate every session as a security violation, then bump the token
        // version so already-issued access tokens stop validating.
        RefreshTokenLifecycleMetrics.RecordAttempt(RefreshTokenLifecycleOperation.AllRevoked);
        await refreshTokens.RevokeAllForUserAsync(userId, revokedByIp: null, cancellationToken)
            .ConfigureAwait(false);
        await sessions.TerminateAllUserSessionsAsync(
                userId,
                SessionTerminationReason.SecurityViolation,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        user.IncrementTokenVersion();
        await users.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
        await users.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var tenantId = @event.TenantId == DurableIntegrationEventTenants.Platform ? (Guid?)null : @event.TenantId;
        await lifecycleRecorder.RecordMutationAsync(
                new RefreshTokenLifecycleEvent(RefreshTokenLifecycleOperation.AllRevoked, userId, TenantId: tenantId),
                cancellationToken)
            .ConfigureAwait(false);
        // Cover legacy access tokens without version/session claims as well. A store failure must
        // not report containment: it propagates and the inbox retries the whole revocation.
        await tokenRevocation.RevokeAllUserTokensAsync(
                userId, user.TokenVersion, $"Risk event: {@event.AlertKind}", cancellationToken)
            .ConfigureAwait(false);

        logger.LogWarning(
            "Revoked all sessions and tokens for user {UserId} after risk event {AlertKind} ({EventId}).",
            userId, @event.AlertKind, @event.EventId);
        await AuditRevocationAsync(@event, userId, tenantId).ConfigureAwait(false);
    }

    /// <summary>
    ///     Records the containment as a security event through the central authentication audit
    ///     pipeline. The central sink never fails the caller, so auditing cannot undo containment.
    /// </summary>
    private async Task AuditRevocationAsync(SuspiciousLoginDetectedV1 @event, Guid userId, Guid? tenantId)
    {
        try
        {
            await auditEventSink.RecordAsync(new AuthenticationAuditEvent(
                RevocationAuditAction,
                userId,
                Success: true,
                Method: "RiskEvent",
                TenantId: tenantId,
                Metadata: new
                {
                    @event.AlertKind,
                    @event.RiskLevel,
                    @event.RiskScore,
                    @event.EventId,
                    RevokedScope = "AllSessionsAndTokens"
                }), CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // The revocation itself already committed with its inbox receipt; an audit transport
            // failure is logged, never rethrown (the central sink swallows by contract anyway).
            logger.LogError(exception,
                "Could not audit the risk-event revocation for user {UserId} ({AlertKind}).",
                userId, @event.AlertKind);
        }
    }
}
