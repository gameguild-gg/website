using GameGuild.CQRS;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handles the SendEmailVerificationCommand by generating a verification token
///     and sending the verification email via IEmailVerificationService.
///     Known and unknown accounts receive an identical public response so the
///     endpoint cannot be used to enumerate registered email addresses, even when
///     the durable delivery boundary fails.
/// </summary>
public sealed class SendEmailVerificationCommandHandler(
    IEmailVerificationService emailVerificationService,
    IUserRepository userRepository,
    ILogger<SendEmailVerificationCommandHandler> logger,
    IAuthenticationAuditEventSink? auditEventSink = null
) : ICommandHandler<SendEmailVerificationCommand, EmailVerificationResponse>
{
    private const int DeliveryAttempts = 2;

    public async Task<EmailVerificationResponse> Handle(SendEmailVerificationCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = request.UserId.HasValue
            ? await userRepository.GetByIdAsync(request.UserId.Value, cancellationToken).ConfigureAwait(false)
            : await userRepository.GetByEmailAsync(email, cancellationToken).ConfigureAwait(false);

        if (user is null)
        {
            logger.LogInformation("Email verification requested for unknown email {Email}", email);
            return GenericResponse();
        }

        logger.LogInformation("Sending email verification for user {UserId}", user.Id);

        // Recipient and token stay bound to the resolved account; an unknown account never
        // reaches token generation or delivery, so it cannot trigger observable side effects.
        var delivered = false;
        Exception? finalFailure = null;
        for (var attempt = 1; attempt <= DeliveryAttempts && !delivered; attempt++)
        {
            try
            {
                var token = await emailVerificationService.GenerateVerificationTokenAsync(user.Id, user.Email).ConfigureAwait(false);
                await emailVerificationService.SendVerificationEmailAsync(user.Email, token, request.UserName ?? user.Username ?? user.Name).ConfigureAwait(false);
                delivered = true;
            }
            catch (Exception exception)
            {
                finalFailure = exception;
                logger.LogError(exception, "Email verification delivery attempt {Attempt} failed for user {UserId}", attempt, user.Id);
            }
        }

        if (!delivered)
        {
            // The response boundary must not reveal that the account exists (unknown accounts cannot
            // fail delivery, so a propagated failure would be an existence oracle). The loss is still
            // surfaced through a critical log and a security audit event instead of being silent.
            logger.LogCritical(
                finalFailure,
                "Email verification could not be durably queued for user {UserId} after {Attempts} attempts; public response stays enumeration-safe",
                user.Id,
                DeliveryAttempts);

            await RecordDeliveryFailureAuditAsync(user.Id, cancellationToken).ConfigureAwait(false);
        }

        return GenericResponse();
    }

    private static EmailVerificationResponse GenericResponse() =>
        new() { Message = UserEnumerationProtectionService.GenericEmailVerificationMessage };

    private async Task RecordDeliveryFailureAuditAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (auditEventSink is null)
        {
            return;
        }

        try
        {
            await auditEventSink.RecordAsync(
                new AuthenticationAuditEvent(
                    "Authentication.EmailVerificationDeliveryFailed",
                    userId,
                    Success: false,
                    Method: "EmailVerification"),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // Audit delivery failure must never turn into a public error response.
            logger.LogError(exception, "Could not record email verification delivery failure audit for user {UserId}", userId);
        }
    }
}
