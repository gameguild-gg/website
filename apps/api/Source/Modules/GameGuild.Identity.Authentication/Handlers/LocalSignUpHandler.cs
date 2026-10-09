using GameGuild.CQRS;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handler for local sign-up command
/// </summary>
public sealed class LocalSignUpHandler(
    IAuthService authService,
    IUserRepository userRepository,
    ISender sender,
    ILogger<LocalSignUpHandler> logger) : ICommandHandler<LocalSignUpCommand, SignInResponse>
{
    private const string VerificationSentMessage = "Sign-up successful. A verification email has been sent to your email address.";
    private const string VerificationPendingMessage = "Sign-up successful. The verification email could not be queued; please request it again from your account settings.";

    public async Task<SignInResponse> Handle(LocalSignUpCommand command, CancellationToken cancellationToken)
    {
        var signUpRequest = new LocalSignUpRequest
        {
            Email = command.Email, Password = command.Password, Username = command.Username, TenantId = command.TenantId, FirstName = command.FirstName, LastName = command.LastName, PhoneNumber = command.PhoneNumber
        };

        var domainResult = await authService.LocalSignUpAsync(signUpRequest, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("User successfully signed up via local authentication");

        // Map from Domain response to Application DTO
        var response = await domainResult.ToDto(userRepository, cancellationToken).ConfigureAwait(false);

        // A failed sign-up must never trigger a verification email.
        if (!response.Success)
        {
            return response;
        }

        try
        {
            await sender.Send(new SendEmailVerificationCommand
            {
                Email = response.Email, UserId = response.UserId, UserName = command.Username
            }, cancellationToken).ConfigureAwait(false);

            response.Message = VerificationSentMessage;
            logger.LogInformation("Sign-up verification email dispatched for user {UserId}", response.UserId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The account is already committed; an unavailable notification queue must not fail the sign-up.
            // The user can request a new verification email through the resend endpoint.
            logger.LogWarning(ex, "Sign-up verification email could not be queued for user {UserId}; signup remains successful", response.UserId);
            response.Message = VerificationPendingMessage;
        }

        return response;
    }
}
