using GameGuild.CQRS;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handler for Google ID token sign-in command
/// </summary>
public sealed class GoogleIdTokenSignInHandler(IAuthService authService, IUserRepository userRepository, ILogger<GoogleIdTokenSignInHandler> logger, FluentValidation.IValidator<GoogleIdTokenSignInCommand> validator)
    : ICommandHandler<GoogleIdTokenSignInCommand, SignInResponse>
{
    public async Task<SignInResponse> Handle(GoogleIdTokenSignInCommand command, CancellationToken cancellationToken)
    {
        // Validate command
        var validationResult = await validator.ValidateAsync(command, cancellationToken).ConfigureAwait(false);

        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => new ValidationError(e.PropertyName, e.ErrorMessage));

            throw new RequestValidationException(errors);
        }

        var signInRequest = new GoogleIdTokenRequest { IdToken = command.IdToken, TenantId = command.TenantId };

        var domainResult = await authService.GoogleIdTokenSignInAsync(signInRequest, cancellationToken).ConfigureAwait(false);

        if (domainResult == null) { throw new InvalidOperationException("Authentication service returned null result"); }

        logger.LogInformation("Google ID token sign-in successful");

        // Map from Domain response to Application DTO
        return await domainResult.ToDto(userRepository, cancellationToken).ConfigureAwait(false);
    }
}
