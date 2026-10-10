using GameGuild.CQRS;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

public sealed class RequestEmailCodeCommandHandler(
    IUserRepository userRepository,
    IEmailCodeService emailCodeService,
    IPublisher publisher,
    ILogger<RequestEmailCodeCommandHandler> logger) : ICommandHandler<RequestEmailCodeCommand, EmailCodeRequestResult>
{
    public async Task<EmailCodeRequestResult> Handle(RequestEmailCodeCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await userRepository.GetByEmailAsync(normalizedEmail, cancellationToken).ConfigureAwait(false);

        if (user is not null)
        {
            var code = await emailCodeService.GenerateEmailCodeAsync(user.Id, user.Email).ConfigureAwait(false);

            if (code is not null)
            {
                try
                {
                    await publisher.Publish(
                        new EmailCodeRequestedNotification
                        {
                            Email = user.Email,
                            Code = code,
                            UserName = user.Username ?? user.Name,
                            TenantId = request.TenantId,
                            IpAddress = request.IpAddress,
                            UserAgent = request.UserAgent
                        },
                        cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Failed to dispatch email-code notification for user {UserId}", user.Id);
                }

                logger.LogInformation(
                    "Email sign-in code generated for user {UserId} from {IpAddress}",
                    user.Id,
                    request.IpAddress ?? "unknown");
            }
            else
            {
                logger.LogInformation(
                    "Email-code request throttled for user {UserId} from {IpAddress}",
                    user.Id,
                    request.IpAddress ?? "unknown");
            }
        }
        else
        {
            logger.LogInformation("Email-code requested for unknown email from {IpAddress}", request.IpAddress ?? "unknown");
        }

        // Enumeration-safe: identical payload regardless of user existence or throttling.
        return new EmailCodeRequestResult();
    }
}

public sealed class ConsumeEmailCodeCommandHandler(
    IUserRepository userRepository,
    IEmailCodeService emailCodeService,
    IAuthenticatedSessionIssuer sessionIssuer,
    ILogger<ConsumeEmailCodeCommandHandler> logger) : ICommandHandler<ConsumeEmailCodeCommand, SignInResponse>
{
    public async Task<SignInResponse> Handle(ConsumeEmailCodeCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var validation = await emailCodeService.VerifyEmailCodeAsync(request.Email, request.Code).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!validation.Success || validation.UserId is not { } userId)
        {
            throw new AuthenticationRequiredException("Invalid or expired email sign-in code");
        }

        var user = await userRepository.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (user is null || user.IsDeleted || !user.ValidateForAuthentication(user.TokenVersion).IsSuccess)
        {
            throw new AuthenticationRequiredException("Invalid or expired email sign-in code");
        }

        // Same issuance path as magic-link consume: one shared authenticated session issuer.
        var response = await sessionIssuer.IssueAsync(
            user,
            request.TenantId,
            new DeviceInfo
            {
                Fingerprint = request.DeviceFingerprint ?? $"email-code:{Guid.NewGuid():N}",
                IpAddress = request.IpAddress,
                UserAgent = request.UserAgent,
                DeviceName = "Email Code",
                DeviceType = "Web"
            },
            cancellationToken).ConfigureAwait(false);
        response.Message = "Email-code sign-in successful";
        logger.LogInformation("User {UserId} signed in with an email sign-in code", user.Id);
        return response;
    }
}
