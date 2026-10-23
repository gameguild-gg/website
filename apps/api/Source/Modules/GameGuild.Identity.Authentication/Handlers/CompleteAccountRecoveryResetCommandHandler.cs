using GameGuild.CQRS;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Consumes the single-use forced-reset token minted at the approval threshold
///     (issue #209) and performs the forced credential reset: all sessions terminated,
///     all refresh tokens revoked, every passkey revoked (mandatory re-enrollment) and a
///     new password set. The token is consumed BEFORE any destructive step so a crash or
///     replay mid-flow can never leave a live reset token behind.
/// </summary>
public sealed class CompleteAccountRecoveryResetCommandHandler(
    IAccountRecoveryStore recoveryStore,
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IUserSessionRepository userSessionRepository,
    IWebAuthnCredentialRepository webAuthnCredentials,
    ITokenRevocationService tokenRevocation,
    IAuthenticationAuditEventSink auditSink,
    ILogger<CompleteAccountRecoveryResetCommandHandler> logger) : ICommandHandler<CompleteAccountRecoveryResetCommand, AccountRecoveryResetResult>
{
    private static readonly AccountRecoveryResetResult Denial = new()
    {
        Success = false,
        Message = "Invalid or expired recovery reset."
    };

    public async Task<AccountRecoveryResetResult> Handle(CompleteAccountRecoveryResetCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var strength = passwordHasher.ValidatePasswordStrength(request.NewPassword);
        if (!strength.IsValid)
        {
            return new AccountRecoveryResetResult
            {
                Success = false,
                Message = string.Join("; ", strength.ValidationFailures)
            };
        }

        await recoveryStore.ExpireElapsedAsync(SystemClock.UtcNow, cancellationToken).ConfigureAwait(false);

        var tokenHash = AccountRecoveryTokens.Hash(request.ResetToken);
        var recovery = await recoveryStore.FindResetUnlockedByResetTokenHashAsync(tokenHash, cancellationToken).ConfigureAwait(false);
        if (recovery is null || !AccountRecoveryTokens.FixedTimeHexEquals(recovery.ResetTokenHash!, tokenHash))
        {
            logger.LogInformation("Account recovery reset denied: no unlocked request for the presented token");
            await AuditFailureAsync(null, request, cancellationToken).ConfigureAwait(false);
            return Denial;
        }

        var now = SystemClock.UtcNow;
        if (recovery.ResetTokenExpiresAt is not { } expiresAt || expiresAt < now)
        {
            logger.LogInformation("Account recovery reset denied: reset token expired for request {RequestId}", recovery.Id);
            await AuditFailureAsync(recovery, request, cancellationToken).ConfigureAwait(false);
            return Denial;
        }

        var user = await userRepository.GetByIdAsync(recovery.OwnerUserId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            // Consume first (below) keeps this fail-closed: the token dies with this denial.
            await ConsumeAsync(recovery, now, cancellationToken).ConfigureAwait(false);
            await AuditFailureAsync(recovery, request, cancellationToken).ConfigureAwait(false);
            return Denial;
        }

        if (PasswordHistoryPolicy.WasRecentlyUsed(user, request.NewPassword, passwordHasher))
        {
            await ConsumeAsync(recovery, now, cancellationToken).ConfigureAwait(false);
            await AuditFailureAsync(recovery, request, cancellationToken).ConfigureAwait(false);
            return new AccountRecoveryResetResult
            {
                Success = false,
                Message = "You cannot reuse any of your last 5 passwords. Start a new recovery."
            };
        }

        // Single-use gate: the status transition consumes the token; a replay finds no
        // ResetUnlocked row. This happens BEFORE any destructive step on purpose.
        await ConsumeAsync(recovery, now, cancellationToken).ConfigureAwait(false);

        // Invalidate existing access first: sessions and refresh tokens die even if the
        // password step below fails, so a compromised session cannot outlive the reset.
        await userSessionRepository.TerminateAllForUserAsync(
            recovery.OwnerUserId,
            SessionTerminationReason.SecurityViolation.ToString(),
            cancellationToken).ConfigureAwait(false);
        await tokenRevocation.RevokeAllUserTokensAsync(recovery.OwnerUserId, "account_recovery_forced_reset", cancellationToken).ConfigureAwait(false);

        // Mandatory passkey re-enrollment: every enrolled credential is treated as
        // potentially lost or suspect and revoked (revocation is terminal by design).
        var credentials = await webAuthnCredentials.GetByUserIdAsync(recovery.OwnerUserId, cancellationToken).ConfigureAwait(false);
        foreach (var credential in credentials.Where(credential => !credential.IsRevoked))
        {
            await webAuthnCredentials.RevokeAsync(credential.Id, cancellationToken).ConfigureAwait(false);
        }

        var passwordHash = passwordHasher.HashPassword(request.NewPassword);
        var updated = await userRepository.UpdatePasswordHashAsync(
            recovery.OwnerUserId, passwordHash, user.PasswordHash, cancellationToken).ConfigureAwait(false);
        if (!updated)
        {
            logger.LogWarning(
                "Account recovery reset for user {UserId}: password changed concurrently; credentials were already invalidated",
                recovery.OwnerUserId);
            await AuditFailureAsync(recovery, request, cancellationToken).ConfigureAwait(false);
            return new AccountRecoveryResetResult
            {
                Success = false,
                Message = "Your password changed during this reset. Start a new recovery."
            };
        }

        await auditSink.RecordAsync(new AuthenticationAuditEvent(
            "AccountRecovery.ResetCompleted",
            recovery.OwnerUserId,
            true,
            "AccountRecovery",
            request.IpAddress,
            request.UserAgent,
            TenantId: recovery.TenantId,
            AssessedRiskLevel: RiskLevel.Critical), cancellationToken).ConfigureAwait(false);
        logger.LogInformation(
            "Account recovery {RequestId} completed: forced credential reset applied for user {UserId}",
            recovery.Id,
            recovery.OwnerUserId);

        return new AccountRecoveryResetResult
        {
            Success = true,
            Message = "Password set. All sessions were terminated and all passkeys revoked; sign in with your new password and re-enroll passkeys."
        };
    }

    private async Task ConsumeAsync(AccountRecoveryRequest recovery, DateTimeOffset now, CancellationToken cancellationToken)
    {
        recovery.Status = AccountRecoveryStatus.Completed;
        recovery.CompletedAt = now;
        // Fail closed for any in-flight redemption: dropping the digest kills the token.
        recovery.ResetTokenHash = null;
        recovery.ResetTokenExpiresAt = null;
        await recoveryStore.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private Task AuditFailureAsync(AccountRecoveryRequest? recovery, CompleteAccountRecoveryResetCommand request, CancellationToken cancellationToken) =>
        auditSink.RecordAsync(new AuthenticationAuditEvent(
            "AccountRecovery.ResetDenied",
            recovery?.OwnerUserId,
            false,
            "AccountRecovery",
            request.IpAddress,
            request.UserAgent,
            TenantId: recovery?.TenantId,
            AssessedRiskLevel: RiskLevel.High), cancellationToken);
}
