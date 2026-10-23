using GameGuild.CQRS;
using GameGuild.Email;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Designates a trusted contact for the acting owner: mints a single-use invite
///     token (stored as a digest) and emails the invite. Fails closed on the contact
///     cap, duplicate emails, and self-delegation.
/// </summary>
public sealed class AddTrustedContactCommandHandler(
    ITrustedContactStore contactStore,
    IUserRepository userRepository,
    IEmailSender emailSender,
    IOptions<AccountRecoveryOptions> options,
    IConfiguration configuration,
    IAuthenticationAuditEventSink auditSink,
    ILogger<AddTrustedContactCommandHandler> logger) : ICommandHandler<AddTrustedContactCommand, TrustedContactActionResult>
{
    private readonly AccountRecoveryOptions options = options.Value;

    public async Task<TrustedContactActionResult> Handle(AddTrustedContactCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // Housekeeping first so expired invites stop occupying the owner+email slot.
        await contactStore.RevokeExpiredInvitesBeforeAsync(SystemClock.UtcNow, cancellationToken).ConfigureAwait(false);

        var owner = await userRepository.GetByIdAsync(request.OwnerUserId, cancellationToken).ConfigureAwait(false);
        if (owner is null)
        {
            return new TrustedContactActionResult { Success = false, Message = "Account not found." };
        }

        if (string.Equals(owner.Email, normalizedEmail, StringComparison.Ordinal))
        {
            return new TrustedContactActionResult { Success = false, Message = "You cannot designate your own account email as a trusted contact." };
        }

        var activeCount = await contactStore.CountActiveByOwnerAsync(owner.Id, cancellationToken).ConfigureAwait(false);
        if (activeCount >= Math.Max(1, options.MaxTrustedContacts))
        {
            return new TrustedContactActionResult
            {
                Success = false,
                Message = $"At most {Math.Max(1, options.MaxTrustedContacts)} trusted contacts can be active at a time."
            };
        }

        var existing = await contactStore.FindActiveByOwnerAndEmailAsync(owner.Id, normalizedEmail, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return new TrustedContactActionResult
            {
                Success = false,
                Message = "That email is already an active trusted contact."
            };
        }

        var now = SystemClock.UtcNow;
        var inviteToken = AccountRecoveryTokens.Generate(AccountRecoveryTokens.InviteTokenBytes);
        var contact = new TrustedContact
        {
            Id = Guid.NewGuid(),
            OwnerUserId = owner.Id,
            TenantId = owner.TenantId ?? Guid.Empty,
            Email = normalizedEmail,
            Relation = string.IsNullOrWhiteSpace(request.Relation) ? null : request.Relation.Trim(),
            Status = TrustedContactStatus.Invited,
            InviteTokenHash = AccountRecoveryTokens.Hash(inviteToken),
            InviteTokenExpiresAt = now.Add(options.InviteLifetime),
            InvitedAt = now
        };

        await contactStore.AddAsync(contact, cancellationToken).ConfigureAwait(false);
        await SendInviteEmailAsync(contact.Email, owner.Email, inviteToken, contact.InviteTokenExpiresAt!.Value, cancellationToken).ConfigureAwait(false);
        await AuditAsync("AccountRecovery.ContactInvited", owner.Id, owner.TenantId, success: true, request, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Trusted contact invite issued for owner {OwnerUserId} to {ContactEmail}", owner.Id, normalizedEmail);

        return new TrustedContactActionResult
        {
            Success = true,
            Message = "Invitation sent. The contact must confirm via the emailed link before receiving recovery codes.",
            Contact = ToDto(contact)
        };
    }

    private Task SendInviteEmailAsync(string contactEmail, string ownerEmail, string inviteToken, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        var link = $"{ResolveWebBaseUrl()}/auth/trusted-contacts/confirm?token={inviteToken}";
        var plain = $"You were designated as a trusted recovery contact for a GameGuild account ({ownerEmail}).\n\n" +
            $"If you accept, open this link before {expiresAt:u} (UTC) to confirm: {link}\n\n" +
            "If you were not expecting this, ignore this email; confirming is voluntary and you can ask the account owner to remove you at any time.";
        var html = $"<p>You were designated as a trusted recovery contact for a GameGuild account ({ownerEmail}).</p>" +
            $"<p>If you accept, open this link before {expiresAt:u} (UTC) to confirm: <a href=\"{link}\">confirm trusted contact</a></p>" +
            "<p>If you were not expecting this, ignore this email; confirming is voluntary and you can ask the account owner to remove you at any time.</p>";

        return emailSender.SendAsync(new EmailMessage(contactEmail, "Trusted recovery contact invitation", plain, html), cancellationToken);
    }

    private static TrustedContactDto ToDto(TrustedContact contact) => new()
    {
        Id = contact.Id,
        Email = contact.Email,
        Relation = contact.Relation,
        Status = contact.Status,
        InvitedAt = contact.InvitedAt,
        ConfirmedAt = contact.ConfirmedAt
    };

    private string ResolveWebBaseUrl()
    {
        var baseUrl = configuration["App:BaseUrl"];
        return string.IsNullOrWhiteSpace(baseUrl) ? "http://localhost:3000" : baseUrl.TrimEnd('/');
    }

    private Task AuditAsync(string actionType, Guid userId, Guid? tenantId, bool success, AddTrustedContactCommand request, CancellationToken cancellationToken) =>
        auditSink.RecordAsync(new AuthenticationAuditEvent(
            actionType,
            userId,
            success,
            "AccountRecovery",
            request.IpAddress,
            request.UserAgent,
            TenantId: tenantId), cancellationToken);
}

/// <summary>
///     Revokes one of the acting owner's contacts. Terminal: the contact immediately
///     stops counting toward recovery thresholds.
/// </summary>
public sealed class RemoveTrustedContactCommandHandler(
    ITrustedContactStore contactStore,
    IAuthenticationAuditEventSink auditSink,
    ILogger<RemoveTrustedContactCommandHandler> logger) : ICommandHandler<RemoveTrustedContactCommand, TrustedContactActionResult>
{
    public async Task<TrustedContactActionResult> Handle(RemoveTrustedContactCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var contact = await contactStore.GetByIdAsync(request.ContactId, cancellationToken).ConfigureAwait(false);

        // Ownership check (tenant isolation): a contact of another owner is indistinguishable from a missing one.
        if (contact is null || contact.OwnerUserId != request.OwnerUserId || contact.Status == TrustedContactStatus.Revoked)
        {
            return new TrustedContactActionResult { Success = false, Message = "Trusted contact not found." };
        }

        var now = SystemClock.UtcNow;
        contact.Status = TrustedContactStatus.Revoked;
        contact.RevokedAt = now;
        // Fail closed for any in-flight invite: dropping the digest makes the token unredeemable.
        contact.InviteTokenHash = null;
        contact.InviteTokenExpiresAt = null;
        await contactStore.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await auditSink.RecordAsync(new AuthenticationAuditEvent(
            "AccountRecovery.ContactRemoved",
            request.OwnerUserId,
            true,
            "AccountRecovery",
            request.IpAddress,
            request.UserAgent,
            TenantId: contact.TenantId), cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Trusted contact {ContactId} removed by owner {OwnerUserId}", contact.Id, request.OwnerUserId);

        return new TrustedContactActionResult { Success = true, Message = "Trusted contact removed." };
    }
}

/// <summary>
///     Redeems a trusted-contact invite token (anonymous, email-verification style).
///     Fail-closed on unknown, expired, already-consumed, or revoked invites; denials
///     are generic so token state is never observable.
/// </summary>
public sealed class ConfirmTrustedContactCommandHandler(
    ITrustedContactStore contactStore,
    IAuthenticationAuditEventSink auditSink,
    ILogger<ConfirmTrustedContactCommandHandler> logger) : ICommandHandler<ConfirmTrustedContactCommand, TrustedContactConfirmResult>
{
    private static readonly TrustedContactConfirmResult Denial = new()
    {
        Success = false,
        Message = "Invalid or expired invitation."
    };

    public async Task<TrustedContactConfirmResult> Handle(ConfirmTrustedContactCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await contactStore.RevokeExpiredInvitesBeforeAsync(SystemClock.UtcNow, cancellationToken).ConfigureAwait(false);

        var tokenHash = AccountRecoveryTokens.Hash(request.InviteToken);
        var contact = await contactStore.FindInvitedByInviteTokenHashAsync(tokenHash, cancellationToken).ConfigureAwait(false);
        if (contact is null || !AccountRecoveryTokens.FixedTimeHexEquals(contact.InviteTokenHash!, tokenHash))
        {
            logger.LogInformation("Trusted contact confirmation denied: no live invitation for the presented token");
            return Denial;
        }

        var now = SystemClock.UtcNow;
        if (contact.InviteTokenExpiresAt is not { } expiresAt || expiresAt < now)
        {
            logger.LogInformation("Trusted contact confirmation denied: invitation expired for contact {ContactId}", contact.Id);
            return Denial;
        }

        // Single-use gate: the status transition consumes the invite; the digest is dropped
        // so the token can never be replayed.
        contact.Status = TrustedContactStatus.Confirmed;
        contact.ConfirmedAt = now;
        contact.InviteTokenHash = null;
        contact.InviteTokenExpiresAt = null;
        await contactStore.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await auditSink.RecordAsync(new AuthenticationAuditEvent(
            "AccountRecovery.ContactConfirmed",
            contact.OwnerUserId,
            true,
            "AccountRecovery",
            request.IpAddress,
            request.UserAgent,
            TenantId: contact.TenantId), cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Trusted contact {ContactId} confirmed for owner {OwnerUserId}", contact.Id, contact.OwnerUserId);

        return new TrustedContactConfirmResult
        {
            Success = true,
            Message = "You are now a confirmed trusted recovery contact. If a recovery is requested, you will receive a recovery code by email."
        };
    }
}
