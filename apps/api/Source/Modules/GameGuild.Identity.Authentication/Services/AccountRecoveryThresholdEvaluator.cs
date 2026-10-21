using GameGuild.CQRS;
using GameGuild.Email;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     K-of-N evaluation for delegated account recovery (issue #209): counts distinct
///     approvals and, when the snapshotted threshold is reached, mints the single-use
///     forced-reset token and emails the owner. Idempotent for the already-unlocked state.
/// </summary>
public interface IAccountRecoveryThresholdEvaluator
{
    Task<AccountRecoveryApprovalResult> EvaluateAsync(
        AccountRecoveryRequest recovery, string? ipAddress, string? userAgent, CancellationToken cancellationToken);
}

public sealed class AccountRecoveryThresholdEvaluator(
    IAccountRecoveryStore recoveryStore,
    IUserRepository userRepository,
    IEmailSender emailSender,
    IOptions<AccountRecoveryOptions> options,
    IConfiguration configuration,
    IAuthenticationAuditEventSink auditSink,
    ILogger<AccountRecoveryThresholdEvaluator> logger) : IAccountRecoveryThresholdEvaluator
{
    private readonly AccountRecoveryOptions options = options.Value;

    public async Task<AccountRecoveryApprovalResult> EvaluateAsync(
        AccountRecoveryRequest recovery, string? ipAddress, string? userAgent, CancellationToken cancellationToken)
    {
        var approvedCount = await recoveryStore.CountDistinctApprovalsAsync(recovery.Id, cancellationToken).ConfigureAwait(false);
        if (recovery.Status == AccountRecoveryStatus.ResetUnlocked || approvedCount < recovery.RequiredApprovals)
        {
            logger.LogInformation(
                "Account recovery {RequestId} approvals: {Approved} of {Required}",
                recovery.Id,
                approvedCount,
                recovery.RequiredApprovals);
            return new AccountRecoveryApprovalResult
            {
                Success = true,
                Message = $"Approval recorded ({approvedCount} of {recovery.RequiredApprovals})."
            };
        }

        var resetToken = AccountRecoveryTokens.Generate(AccountRecoveryTokens.ResetTokenBytes);
        var now = SystemClock.UtcNow;
        recovery.Status = AccountRecoveryStatus.ResetUnlocked;
        recovery.ResetUnlockedAt = now;
        recovery.ResetTokenHash = AccountRecoveryTokens.Hash(resetToken);
        recovery.ResetTokenExpiresAt = now.Add(options.ResetTokenLifetime);
        await recoveryStore.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await SendResetEmailAsync(recovery.OwnerUserId, resetToken, recovery.ResetTokenExpiresAt.Value, cancellationToken).ConfigureAwait(false);
        await auditSink.RecordAsync(new AuthenticationAuditEvent(
            "AccountRecovery.ResetUnlocked",
            recovery.OwnerUserId,
            true,
            "AccountRecovery",
            ipAddress,
            userAgent,
            TenantId: recovery.TenantId,
            AssessedRiskLevel: RiskLevel.Critical), cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Account recovery {RequestId} reached the threshold; forced reset unlocked", recovery.Id);

        return new AccountRecoveryApprovalResult
        {
            Success = true,
            Message = "Approval recorded; the approval threshold was reached and the account owner received a reset link.",
            ResetUnlocked = true
        };
    }

    private async Task SendResetEmailAsync(Guid ownerUserId, string resetToken, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        var owner = await userRepository.GetByIdAsync(ownerUserId, cancellationToken).ConfigureAwait(false);
        if (owner is null)
        {
            // The minted token exists; a missing owner cannot redeem it anyway. Fail closed.
            logger.LogWarning("Account recovery reset email skipped: owner {OwnerUserId} no longer resolvable", ownerUserId);
            return;
        }

        var link = $"{ResolveWebBaseUrl()}/auth/account-recovery/reset?token={resetToken}";
        var plain = "Your account recovery reached the approval threshold.\n\n" +
            $"Open this link before {expiresAt:u} (UTC) to choose a new password and re-enroll your passkeys: {link}\n\n" +
            "The link is single-use. All active sessions and passkeys are revoked when you complete it.";
        var html = "<p>Your account recovery reached the approval threshold.</p>" +
            $"<p><a href=\"{link}\">Complete the forced credential reset</a> before {expiresAt:u} (UTC) to choose a new password and re-enroll your passkeys.</p>" +
            "<p>The link is single-use. All active sessions and passkeys are revoked when you complete it.</p>";

        await emailSender.SendAsync(new EmailMessage(owner.Email, "Complete your account recovery", plain, html), cancellationToken).ConfigureAwait(false);
    }

    private string ResolveWebBaseUrl()
    {
        var baseUrl = configuration["App:BaseUrl"];
        return string.IsNullOrWhiteSpace(baseUrl) ? "http://localhost:3000" : baseUrl.TrimEnd('/');
    }
}
