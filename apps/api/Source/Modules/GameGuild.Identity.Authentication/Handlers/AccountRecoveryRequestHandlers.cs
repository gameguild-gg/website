using GameGuild.CQRS;
using GameGuild.Email;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Opens a delegated account recovery for an email address (issue #209). Only an
///     account with recovery factors (confirmed trusted contacts and/or an enrolled
///     passkey) gets a request; the response never reveals account existence or state.
///     Recovery codes are single-use and stored as digests.
/// </summary>
public sealed class RequestAccountRecoveryCommandHandler(
    IUserRepository userRepository,
    ITrustedContactStore contactStore,
    IAccountRecoveryStore recoveryStore,
    IWebAuthnCredentialRepository webAuthnCredentials,
    IEmailSender emailSender,
    IMemoryCache memoryCache,
    IOptions<AccountRecoveryOptions> options,
    IConfiguration configuration,
    IAuthenticationAuditEventSink auditSink,
    ILogger<RequestAccountRecoveryCommandHandler> logger) : ICommandHandler<RequestAccountRecoveryCommand, AccountRecoveryRequestResult>
{
    private const string RateLimitKeyPrefix = "accountrecovery:ratelimit:";
    private readonly AccountRecoveryOptions options = options.Value;

    public async Task<AccountRecoveryRequestResult> Handle(RequestAccountRecoveryCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        // Cache key carries the full email digest so raw addresses never enter keys and
        // case variants share one bucket (mirrors EmailVerificationService).
        var rateLimitKey = RateLimitKeyPrefix + AccountRecoveryTokens.Hash(normalizedEmail);

        if (memoryCache.TryGetValue(rateLimitKey, out DateTime _))
        {
            logger.LogInformation("Account recovery request throttled for an email from rate limiting");
            return new AccountRecoveryRequestResult();
        }

        AccountRecoveryRequestResult ApplyRateLimit(AccountRecoveryRequestResult result)
        {
            // The host cache sets SizeLimit; every entry must carry a size (mirrors EmailVerificationService).
            memoryCache.Set(rateLimitKey, SystemClock.UtcNow, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = options.EmailRateLimit
            }.SetSize(1));
            return result;
        }

        await recoveryStore.ExpireElapsedAsync(SystemClock.UtcNow, cancellationToken).ConfigureAwait(false);

        var user = await userRepository.GetByEmailAsync(normalizedEmail, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            logger.LogInformation("Account recovery requested for unknown email");
            return ApplyRateLimit(new AccountRecoveryRequestResult());
        }

        var confirmedContacts = (await contactStore.GetActiveByOwnerAsync(user.Id, cancellationToken).ConfigureAwait(false))
            .Where(contact => contact.Status == TrustedContactStatus.Confirmed)
            .ToList();
        var hasPasskey = await webAuthnCredentials.HasActiveCredentialsAsync(user.Id, cancellationToken).ConfigureAwait(false);
        var availableFactors = confirmedContacts.Count + (hasPasskey ? 1 : 0);
        if (availableFactors == 0)
        {
            logger.LogInformation("Account recovery skipped for user {UserId}: no confirmed contacts and no enrolled passkey", user.Id);
            await AuditAsync("AccountRecovery.RequestRejected", user.Id, user.TenantId, success: false, request, cancellationToken).ConfigureAwait(false);
            return ApplyRateLimit(new AccountRecoveryRequestResult());
        }

        var liveRequest = await recoveryStore.FindLiveByOwnerAsync(user.Id, cancellationToken).ConfigureAwait(false);
        if (liveRequest is not null)
        {
            logger.LogInformation("Account recovery skipped for user {UserId}: a live request exists", user.Id);
            return ApplyRateLimit(new AccountRecoveryRequestResult());
        }

        var now = SystemClock.UtcNow;
        var recovery = new AccountRecoveryRequest
        {
            Id = Guid.NewGuid(),
            OwnerUserId = user.Id,
            TenantId = user.TenantId ?? Guid.Empty,
            Status = AccountRecoveryStatus.PendingApprovals,
            RequestedAt = now,
            CooldownEndsAt = now.Add(options.Cooldown),
            ApprovalWindowEndsAt = now.Add(options.ApprovalWindow),
            RequiredApprovals = options.EffectiveRequiredApprovals(availableFactors)
        };

        var approvals = new List<RecoveryApproval>(confirmedContacts.Count);
        foreach (var contact in confirmedContacts)
        {
            var code = AccountRecoveryTokens.Generate(AccountRecoveryTokens.ApprovalCodeBytes);
            approvals.Add(new RecoveryApproval
            {
                Id = Guid.NewGuid(),
                RecoveryRequestId = recovery.Id,
                OwnerUserId = user.Id,
                TenantId = recovery.TenantId,
                Factor = RecoveryApprovalFactor.TrustedContact,
                TrustedContactId = contact.Id,
                ApprovalCodeHash = AccountRecoveryTokens.Hash(code),
                IssuedAt = now
            });
            await SendContactCodeEmailAsync(contact.Email, user.Email, code, recovery, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await recoveryStore.AddWithApprovalsAsync(recovery, approvals, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex.Message.Contains("duplicate key") || ex.Message.Contains("unique constraint"))
        {
            logger.LogWarning("Account recovery race detected for user {UserId}. Treating as generic success.", user.Id);
            return ApplyRateLimit(new AccountRecoveryRequestResult());
        }

        await SendOwnerNoticeEmailAsync(user.Email, recovery, cancellationToken).ConfigureAwait(false);
        await AuditAsync("AccountRecovery.RequestOpened", user.Id, user.TenantId, success: true, request, cancellationToken).ConfigureAwait(false);
        logger.LogInformation(
            "Account recovery opened for user {UserId}: {Contacts} contacts, passkey {HasPasskey}, threshold {Threshold}",
            user.Id,
            confirmedContacts.Count,
            hasPasskey,
            recovery.RequiredApprovals);

        // Enumeration-safe: identical payload regardless of account existence, factor state, or a live request.
        return ApplyRateLimit(new AccountRecoveryRequestResult());
    }

    private Task SendContactCodeEmailAsync(string contactEmail, string ownerEmail, string code, AccountRecoveryRequest recovery, CancellationToken cancellationToken)
    {
        var link = $"{ResolveWebBaseUrl()}/auth/account-recovery/approve";
        var plain = $"A recovery was requested for the GameGuild account {ownerEmail}, which lists you as a trusted recovery contact.\n\n" +
            $"Approvals are accepted from {recovery.CooldownEndsAt:u} (UTC) until {recovery.ApprovalWindowEndsAt:u} (UTC).\n" +
            $"Your single-use recovery code: {code}\n\n" +
            $"Submit it at {link} if you verified with the account owner that this request is legitimate.\n" +
            "If you were not expecting this, ignore this email; codes expire with the recovery request.";
        var html = $"<p>A recovery was requested for the GameGuild account {ownerEmail}, which lists you as a trusted recovery contact.</p>" +
            $"<p>Approvals are accepted from {recovery.CooldownEndsAt:u} (UTC) until {recovery.ApprovalWindowEndsAt:u} (UTC).</p>" +
            $"<p>Your single-use recovery code: <code>{code}</code></p>" +
            $"<p>Submit it at <a href=\"{link}\">{link}</a> if you verified with the account owner that this request is legitimate.</p>" +
            "<p>If you were not expecting this, ignore this email; codes expire with the recovery request.</p>";

        return emailSender.SendAsync(new EmailMessage(contactEmail, "Account recovery approval code", plain, html), cancellationToken);
    }

    private Task SendOwnerNoticeEmailAsync(string ownerEmail, AccountRecoveryRequest recovery, CancellationToken cancellationToken)
    {
        var link = $"{ResolveWebBaseUrl()}/auth/account-recovery/cancel";
        var plain = "A delegated account recovery was requested for your account.\n\n" +
            $"Your trusted contacts were emailed single-use approval codes. Approvals are accepted from {recovery.CooldownEndsAt:u} (UTC) until {recovery.ApprovalWindowEndsAt:u} (UTC).\n\n" +
            $"If this was not you, cancel the recovery at {link} while you still have access, or contact support immediately.";
        var html = "<p>A delegated account recovery was requested for your account.</p>" +
            $"<p>Your trusted contacts were emailed single-use approval codes. Approvals are accepted from {recovery.CooldownEndsAt:u} (UTC) until {recovery.ApprovalWindowEndsAt:u} (UTC).</p>" +
            $"<p>If this was not you, <a href=\"{link}\">cancel the recovery</a> while you still have access, or contact support immediately.</p>";

        return emailSender.SendAsync(new EmailMessage(ownerEmail, "Account recovery requested", plain, html), cancellationToken);
    }

    private string ResolveWebBaseUrl()
    {
        var baseUrl = configuration["App:BaseUrl"];
        return string.IsNullOrWhiteSpace(baseUrl) ? "http://localhost:3000" : baseUrl.TrimEnd('/');
    }

    private Task AuditAsync(string actionType, Guid userId, Guid? tenantId, bool success, RequestAccountRecoveryCommand request, CancellationToken cancellationToken) =>
        auditSink.RecordAsync(new AuthenticationAuditEvent(
            actionType,
            userId,
            success,
            "AccountRecovery",
            request.IpAddress,
            request.UserAgent,
            TenantId: tenantId,
            AssessedRiskLevel: RiskLevel.High), cancellationToken);
}

/// <summary>
///     Cancels the acting owner's live recovery request. Cancelling a ResetUnlocked
///     request also kills the minted reset token (lookups require ResetUnlocked).
/// </summary>
public sealed class CancelAccountRecoveryCommandHandler(
    IAccountRecoveryStore recoveryStore,
    IAuthenticationAuditEventSink auditSink,
    ILogger<CancelAccountRecoveryCommandHandler> logger) : ICommandHandler<CancelAccountRecoveryCommand, AccountRecoveryCancelResult>
{
    public async Task<AccountRecoveryCancelResult> Handle(CancelAccountRecoveryCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var recovery = await recoveryStore.FindLiveByOwnerAsync(request.OwnerUserId, cancellationToken).ConfigureAwait(false);
        if (recovery is null)
        {
            return new AccountRecoveryCancelResult { Success = false, Message = "No active recovery request." };
        }

        var now = SystemClock.UtcNow;
        recovery.Status = AccountRecoveryStatus.Cancelled;
        recovery.CancelledAt = now;
        // Fail closed for the in-flight reset token: dropping the digest makes it unusable.
        if (recovery.ResetTokenHash is not null)
        {
            recovery.ResetTokenHash = null;
            recovery.ResetTokenExpiresAt = null;
        }

        await recoveryStore.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await auditSink.RecordAsync(new AuthenticationAuditEvent(
            "AccountRecovery.RequestCancelled",
            request.OwnerUserId,
            true,
            "AccountRecovery",
            request.IpAddress,
            request.UserAgent,
            TenantId: recovery.TenantId,
            AssessedRiskLevel: RiskLevel.Medium), cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Account recovery {RecoveryId} cancelled by owner {OwnerUserId}", recovery.Id, request.OwnerUserId);

        return new AccountRecoveryCancelResult { Success = true, Message = "Recovery request cancelled." };
    }
}
