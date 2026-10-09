using GameGuild.CQRS;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Backward-compatible facade for password and email-verification operations.
///     Public API controllers use the same CQRS commands directly.
/// </summary>
public class PasswordService(
    ILogger<PasswordService> logger,
    IUserRepository userRepository,
    ISender sender
) : IPasswordService
{
    public async Task<EmailOperationResponse> SendEmailVerificationAsync(SendEmailVerificationRequest request, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Sending email verification to {Email}", request.Email);

        var user = await userRepository.GetByEmailAsync(request.Email.ToLowerInvariant(), cancellationToken).ConfigureAwait(false);
        if (user == null)
        {
            return GenericVerificationResponse();
        }

        await sender.Send(
            new SendEmailVerificationCommand
            {
                Email = request.Email,
                UserId = user.Id,
                UserName = user.Username
            },
            cancellationToken).ConfigureAwait(false);

        return GenericVerificationResponse();
    }

    // Known and unknown accounts must observe the same message; the command performs the
    // enumeration-safe delivery, so the facade only mirrors its public response.
    private static EmailOperationResponse GenericVerificationResponse() =>
        new() { Success = true, Message = UserEnumerationProtectionService.GenericEmailVerificationMessage };

    public async Task<EmailOperationResponse> VerifyEmailAsync(EmailVerificationRequest verificationRequest, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Verifying email with token");

        var result = await sender.Send(
            new VerifyEmailCommand { Token = verificationRequest.Token },
            cancellationToken).ConfigureAwait(false);

        return new EmailOperationResponse
        {
            Success = result.Success,
            Message = result.Message
        };
    }

    public async Task<EmailOperationResponse> ForgotPasswordAsync(PasswordResetRequest request, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Processing forgot password request for {Email}", request.Email);

        var result = await sender.Send(
            new RequestPasswordResetCommand { Email = request.Email },
            cancellationToken).ConfigureAwait(false);

        return new EmailOperationResponse
        {
            Success = result.Success,
            Message = result.Message
        };
    }

    public async Task<EmailOperationResponse> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Processing password reset");

        var result = await sender.Send(
            new ResetPasswordCommand
            {
                Token = request.Token,
                NewPassword = request.NewPassword,
                ConfirmPassword = request.NewPassword
            },
            cancellationToken).ConfigureAwait(false);

        return new EmailOperationResponse
        {
            Success = result.Success,
            Message = result.Message
        };
    }

    public async Task<EmailOperationResponse> ChangePasswordAsync(ChangePasswordRequest request, Guid userId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Processing password change for user {UserId}", userId);

        var result = await sender.Send(
            new ChangePasswordCommand
            {
                UserId = userId,
                CurrentPassword = request.CurrentPassword,
                NewPassword = request.NewPassword,
                ConfirmPassword = request.NewPassword,
                RevokeOtherSessions = true
            },
            cancellationToken).ConfigureAwait(false);

        return new EmailOperationResponse
        {
            Success = result.Success,
            Message = result.Message
        };
    }
}
