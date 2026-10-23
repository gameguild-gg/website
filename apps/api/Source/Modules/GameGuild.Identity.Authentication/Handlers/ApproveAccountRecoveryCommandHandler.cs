using GameGuild.CQRS;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Begins the passkey assertion ceremony for recovery approval: delegates to the
///     WebAuthn authentication service for assertion options scoped to the account's
///     enrolled credentials. Issues no sign-in credentials.
/// </summary>
public sealed class BeginPasskeyRecoveryApprovalCommandHandler(
    IWebAuthnAuthenticationService webAuthnAuthentication) : ICommandHandler<BeginPasskeyRecoveryApprovalCommand, WebAuthnAuthenticationOptionsResult>
{
    public async Task<WebAuthnAuthenticationOptionsResult> Handle(BeginPasskeyRecoveryApprovalCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await webAuthnAuthentication.BeginAuthenticationAsync(
            string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant(),
            null,
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
///     Redeems one trusted contact's single-use recovery code (issue #209). Fails
///     closed on unknown/consumed codes, non-Confirmed contacts, pre-cooldown requests,
///     elapsed windows, and requests that already reached the threshold; denials are
///     generic so code and request state stay unobservable.
/// </summary>
public sealed class ApproveAccountRecoveryCommandHandler(
    IAccountRecoveryStore recoveryStore,
    ITrustedContactStore contactStore,
    IAccountRecoveryThresholdEvaluator thresholdEvaluator,
    IAuthenticationAuditEventSink auditSink,
    ILogger<ApproveAccountRecoveryCommandHandler> logger) : ICommandHandler<ApproveAccountRecoveryCommand, AccountRecoveryApprovalResult>
{
    private static readonly AccountRecoveryApprovalResult Denial = new()
    {
        Success = false,
        Message = "Invalid or expired recovery approval."
    };

    public async Task<AccountRecoveryApprovalResult> Handle(ApproveAccountRecoveryCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await recoveryStore.ExpireElapsedAsync(SystemClock.UtcNow, cancellationToken).ConfigureAwait(false);

        var codeHash = AccountRecoveryTokens.Hash(request.ApprovalCode);
        var approval = await recoveryStore.FindRedeemableByApprovalCodeHashAsync(codeHash, cancellationToken).ConfigureAwait(false);
        if (approval is null || approval.TrustedContactId is not { } contactId)
        {
            logger.LogInformation("Account recovery approval denied: no redeemable code for the presented value");
            await AuditFailureAsync(null, request.IpAddress, request.UserAgent, cancellationToken).ConfigureAwait(false);
            return Denial;
        }

        var recovery = await recoveryStore.FindByIdAsync(approval.RecoveryRequestId, cancellationToken).ConfigureAwait(false);
        if (recovery is null || recovery.Status != AccountRecoveryStatus.PendingApprovals)
        {
            logger.LogInformation("Account recovery approval denied: request {RequestId} is not accepting approvals", approval.RecoveryRequestId);
            await AuditFailureAsync(recovery, request.IpAddress, request.UserAgent, cancellationToken).ConfigureAwait(false);
            return Denial;
        }

        // Fail closed on contacts revoked after the codes were issued.
        var contact = await contactStore.GetByIdAsync(contactId, cancellationToken).ConfigureAwait(false);
        if (contact is null || contact.Status != TrustedContactStatus.Confirmed)
        {
            logger.LogInformation("Account recovery approval denied: contact {ContactId} is no longer confirmed", contactId);
            await AuditFailureAsync(recovery, request.IpAddress, request.UserAgent, cancellationToken).ConfigureAwait(false);
            return Denial;
        }

        var now = SystemClock.UtcNow;
        if (now < recovery.CooldownEndsAt)
        {
            // The caller holds a valid code; revealing the remaining wait is safe and required.
            return new AccountRecoveryApprovalResult
            {
                Success = false,
                Message = "The recovery cooling-off period has not elapsed yet.",
                RetryAfterSeconds = (int)Math.Ceiling((recovery.CooldownEndsAt - now).TotalSeconds)
            };
        }

        if (now > recovery.ApprovalWindowEndsAt)
        {
            logger.LogInformation("Account recovery approval denied: request {RequestId} window elapsed", recovery.Id);
            await AuditFailureAsync(recovery, request.IpAddress, request.UserAgent, cancellationToken).ConfigureAwait(false);
            return Denial;
        }

        // Single-use gate: stamping ApprovedAt consumes the code; a replay finds no redeemable row.
        approval.ApprovedAt = now;
        approval.ApproverIpAddress = request.IpAddress;
        approval.ApproverUserAgent = request.UserAgent;
        await recoveryStore.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await auditSink.RecordAsync(new AuthenticationAuditEvent(
            "AccountRecovery.ApprovalRecorded",
            recovery.OwnerUserId,
            true,
            "AccountRecovery",
            request.IpAddress,
            request.UserAgent,
            TenantId: recovery.TenantId,
            AssessedRiskLevel: RiskLevel.Medium), cancellationToken).ConfigureAwait(false);

        return await thresholdEvaluator.EvaluateAsync(recovery, request.IpAddress, request.UserAgent, cancellationToken).ConfigureAwait(false);
    }

    private Task AuditFailureAsync(AccountRecoveryRequest? recovery, string? ipAddress, string? userAgent, CancellationToken cancellationToken) =>
        auditSink.RecordAsync(new AuthenticationAuditEvent(
            "AccountRecovery.ApprovalDenied",
            recovery?.OwnerUserId,
            false,
            "AccountRecovery",
            ipAddress,
            userAgent,
            TenantId: recovery?.TenantId,
            AssessedRiskLevel: RiskLevel.High), cancellationToken);
}

/// <summary>
///     Completes a passkey assertion for recovery approval (issue #209): an enrolled
///     WebAuthn credential on a surviving device counts as one approval factor. The
///     assertion is verified cryptographically by the WebAuthn authentication service
///     and never issues sign-in credentials; repeated assertions from the same
///     credential are replays and do not stack.
/// </summary>
public sealed class ApproveAccountRecoveryWithPasskeyCommandHandler(
    IWebAuthnAuthenticationService webAuthnAuthentication,
    IAccountRecoveryStore recoveryStore,
    IAccountRecoveryThresholdEvaluator thresholdEvaluator,
    IAuthenticationAuditEventSink auditSink,
    ILogger<ApproveAccountRecoveryWithPasskeyCommandHandler> logger) : ICommandHandler<ApproveAccountRecoveryWithPasskeyCommand, AccountRecoveryApprovalResult>
{
    private static readonly AccountRecoveryApprovalResult Denial = new()
    {
        Success = false,
        Message = "Invalid or expired recovery approval."
    };

    public async Task<AccountRecoveryApprovalResult> Handle(ApproveAccountRecoveryWithPasskeyCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await recoveryStore.ExpireElapsedAsync(SystemClock.UtcNow, cancellationToken).ConfigureAwait(false);

        // Pure verification: the shared ceremony validates the assertion and consumes the
        // challenge, but returns no session or tokens.
        var assertion = await webAuthnAuthentication.CompleteAuthenticationAsync(
            request.AssertionResponse, request.IpAddress, request.UserAgent, cancellationToken).ConfigureAwait(false);
        if (!assertion.Success || assertion.UserId is not { } userId || assertion.CredentialId is not { } credentialId)
        {
            logger.LogInformation("Account recovery passkey approval denied: assertion verification failed");
            await AuditFailureAsync(null, request.IpAddress, request.UserAgent, cancellationToken).ConfigureAwait(false);
            return Denial;
        }

        var recovery = await recoveryStore.FindLiveByOwnerAsync(userId, cancellationToken).ConfigureAwait(false);
        if (recovery is null || recovery.Status != AccountRecoveryStatus.PendingApprovals)
        {
            logger.LogInformation("Account recovery passkey approval denied: no request accepting approvals for user {UserId}", userId);
            await AuditFailureAsync(recovery, request.IpAddress, request.UserAgent, cancellationToken).ConfigureAwait(false);
            return Denial;
        }

        var now = SystemClock.UtcNow;
        if (now < recovery.CooldownEndsAt)
        {
            // The caller proved possession of an enrolled credential; revealing the wait is safe.
            return new AccountRecoveryApprovalResult
            {
                Success = false,
                Message = "The recovery cooling-off period has not elapsed yet.",
                RetryAfterSeconds = (int)Math.Ceiling((recovery.CooldownEndsAt - now).TotalSeconds)
            };
        }

        if (now > recovery.ApprovalWindowEndsAt)
        {
            logger.LogInformation("Account recovery passkey approval denied: request {RequestId} window elapsed", recovery.Id);
            await AuditFailureAsync(recovery, request.IpAddress, request.UserAgent, cancellationToken).ConfigureAwait(false);
            return Denial;
        }

        var approval = new RecoveryApproval
        {
            Id = Guid.NewGuid(),
            RecoveryRequestId = recovery.Id,
            OwnerUserId = recovery.OwnerUserId,
            TenantId = recovery.TenantId,
            Factor = RecoveryApprovalFactor.Passkey,
            WebAuthnCredentialId = credentialId,
            IssuedAt = recovery.RequestedAt,
            ApprovedAt = now,
            ApproverIpAddress = request.IpAddress,
            ApproverUserAgent = request.UserAgent
        };
        if (!await recoveryStore.TryAddPasskeyApprovalAsync(approval, cancellationToken).ConfigureAwait(false))
        {
            // Same credential already approved this request: a replay, not a second approval.
            logger.LogInformation("Account recovery passkey approval denied: credential {CredentialId} replay", credentialId);
            await AuditFailureAsync(recovery, request.IpAddress, request.UserAgent, cancellationToken).ConfigureAwait(false);
            return Denial;
        }

        await auditSink.RecordAsync(new AuthenticationAuditEvent(
            "AccountRecovery.PasskeyApprovalRecorded",
            recovery.OwnerUserId,
            true,
            "AccountRecovery",
            request.IpAddress,
            request.UserAgent,
            TenantId: recovery.TenantId,
            AssessedRiskLevel: RiskLevel.Medium), cancellationToken).ConfigureAwait(false);

        return await thresholdEvaluator.EvaluateAsync(recovery, request.IpAddress, request.UserAgent, cancellationToken).ConfigureAwait(false);
    }

    private Task AuditFailureAsync(AccountRecoveryRequest? recovery, string? ipAddress, string? userAgent, CancellationToken cancellationToken) =>
        auditSink.RecordAsync(new AuthenticationAuditEvent(
            "AccountRecovery.PasskeyApprovalDenied",
            recovery?.OwnerUserId,
            false,
            "AccountRecovery",
            ipAddress,
            userAgent,
            TenantId: recovery?.TenantId,
            AssessedRiskLevel: RiskLevel.High), cancellationToken);
}
