using GameGuild.Configuration.ApplicationLayer;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authorization.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Guest session lifecycle (issue #213). Issues short-lived anonymous sessions in their
///     own identity namespace, validates presented guest credentials against the persisted
///     snapshot, and migrates a guest session into an authenticated session exactly once
///     (session continuity at sign-in/sign-up). Every issuance, rejection and upgrade is
///     forwarded to the security-event pipeline via <see cref="IAuthenticationAuditEventSink"/>
///     (category Authentication → classified and persisted by the SecurityEventLogger).
/// </summary>
public sealed class GuestSessionService(
    IGuestSessionStore guestSessionStore,
    IGuestTokenService guestTokenService,
    IOptions<GuestSessionOptions> options,
    IAuthenticationAuditEventSink? auditEventSink = null,
    ILogger<GuestSessionService>? logger = null) : IGuestSessionService
{
    private readonly GuestSessionOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<GuestSessionResult> IssueAsync(GuestSessionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_options.IsEnabled)
        {
            await RecordAuditAsync(
                "Authentication.GuestSessionIssuanceRejected",
                success: false,
                sessionId: null,
                userId: null,
                metadata: new { Reason = nameof(GuestSessionFailureReason.GuestAccessDisabled) },
                errorMessage: "Guest access is disabled by configuration.",
                cancellationToken).ConfigureAwait(false);
            return Denied(GuestSessionFailureReason.GuestAccessDisabled);
        }

        var now = SystemClock.UtcNow;
        var guestSessionId = Guid.NewGuid();
        var expiresAt = now.AddMinutes(_options.AccessTokenLifetimeMinutes);

        await guestSessionStore.SaveAsync(
            new GuestSessionSnapshot
            {
                GuestSessionId = guestSessionId,
                IssuedAt = now,
                ExpiresAt = expiresAt,
                IpAddress = request.IpAddress,
                UserAgent = request.UserAgent,
                DeviceFingerprint = request.DeviceFingerprint
            },
            cancellationToken).ConfigureAwait(false);

        var token = guestTokenService.Issue(guestSessionId, expiresAt);

        await RecordAuditAsync(
            "Authentication.GuestSessionIssued",
            success: true,
            sessionId: guestSessionId,
            userId: null,
            metadata: new { ExpiresAt = expiresAt, DeviceFingerprintPresent = !string.IsNullOrWhiteSpace(request.DeviceFingerprint) },
            errorMessage: null,
            cancellationToken).ConfigureAwait(false);

        logger?.LogInformation("Guest session {GuestSessionId} issued; expires {ExpiresAt}", guestSessionId, expiresAt);

        return new GuestSessionResult
        {
            Success = true,
            GuestSessionId = guestSessionId,
            AccessToken = token,
            ExpiresAt = expiresAt,
            Permissions = GuestPermission.Allowlist
        };
    }

    /// <inheritdoc />
    public async Task<GuestSessionSnapshot?> ValidateAsync(string? guestToken, CancellationToken cancellationToken = default)
    {
        var principal = guestTokenService.Validate(guestToken);
        if (principal is null || !principal.TryGetGuestId(out var guestSessionId))
        {
            return null;
        }

        return await guestSessionStore.FindAsync(guestSessionId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<GuestSessionUpgradeResult> UpgradeAsync(
        string? guestToken,
        Guid userId,
        Guid authenticatedSessionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (userId == Guid.Empty || authenticatedSessionId == Guid.Empty)
        {
            return DeniedUpgrade(GuestSessionFailureReason.AuthenticatedSessionRequired);
        }

        var principal = guestTokenService.Validate(guestToken);
        if (principal is null || !principal.TryGetGuestId(out var guestSessionId))
        {
            await RecordUpgradeRejectionAsync(guestSessionId: null, userId, nameof(GuestSessionFailureReason.InvalidGuestToken), cancellationToken).ConfigureAwait(false);
            return DeniedUpgrade(GuestSessionFailureReason.InvalidGuestToken);
        }

        var snapshot = await guestSessionStore.FindAsync(guestSessionId, cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            await RecordUpgradeRejectionAsync(guestSessionId, userId, nameof(GuestSessionFailureReason.GuestSessionUnavailable), cancellationToken).ConfigureAwait(false);
            return DeniedUpgrade(GuestSessionFailureReason.GuestSessionUnavailable);
        }

        var upgradedAt = SystemClock.UtcNow;
        if (!await guestSessionStore.TryMarkUpgradedAsync(guestSessionId, userId, authenticatedSessionId, upgradedAt, cancellationToken).ConfigureAwait(false))
        {
            await RecordUpgradeRejectionAsync(guestSessionId, userId, nameof(GuestSessionFailureReason.GuestSessionUnavailable), cancellationToken).ConfigureAwait(false);
            return DeniedUpgrade(GuestSessionFailureReason.GuestSessionUnavailable);
        }

        await RecordAuditAsync(
            "Authentication.GuestSessionUpgraded",
            success: true,
            sessionId: authenticatedSessionId,
            userId: userId,
            metadata: new { GuestSessionId = guestSessionId, UpgradedAt = upgradedAt },
            errorMessage: null,
            cancellationToken).ConfigureAwait(false);

        logger?.LogInformation(
            "Guest session {GuestSessionId} upgraded to authenticated session {AuthenticatedSessionId} for user {UserId}",
            guestSessionId, authenticatedSessionId, userId);

        return new GuestSessionUpgradeResult { Success = true, GuestSessionId = guestSessionId };
    }

    private async Task RecordUpgradeRejectionAsync(Guid? guestSessionId, Guid userId, string reason, CancellationToken cancellationToken)
    {
        await RecordAuditAsync(
            "Authentication.GuestSessionUpgradeRejected",
            success: false,
            sessionId: null,
            userId: userId,
            metadata: new { GuestSessionId = guestSessionId, Reason = reason },
            errorMessage: reason,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task RecordAuditAsync(
        string actionType,
        bool success,
        Guid? sessionId,
        Guid? userId,
        object metadata,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        if (auditEventSink is null)
        {
            return;
        }

        try
        {
            await auditEventSink.RecordAsync(
                new AuthenticationAuditEvent(
                    actionType,
                    userId,
                    success,
                    "Guest",
                    SessionId: sessionId,
                    Metadata: metadata,
                    ErrorMessage: errorMessage),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // Audit forwarding must not break the flow it observes; the sink itself is the
            // durable pipeline and already retries/spools its own failures.
            logger?.LogError(exception, "Error forwarding guest session event {ActionType}", actionType);
        }
    }

    private static GuestSessionResult Denied(GuestSessionFailureReason reason) => new()
    {
        Success = false,
        FailureReason = reason,
        Permissions = []
    };

    private static GuestSessionUpgradeResult DeniedUpgrade(GuestSessionFailureReason reason) => new()
    {
        Success = false,
        FailureReason = reason
    };
}
