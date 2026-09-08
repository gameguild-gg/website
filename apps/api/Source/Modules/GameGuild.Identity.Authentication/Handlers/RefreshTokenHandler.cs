using GameGuild.CQRS;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handler for refresh token command
/// </summary>
public sealed class RefreshTokenHandler(IAuthService authService, IUserRepository userRepository, ILogger<RefreshTokenHandler> logger, FluentValidation.IValidator<RefreshTokenCommand> validator)
    : ICommandHandler<RefreshTokenCommand, SignInResponse>
{
    private readonly IAuthService _authService = authService ?? throw new ArgumentNullException(nameof(authService));

    private readonly IUserRepository _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));

    private readonly ILogger<RefreshTokenHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<SignInResponse> Handle(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        // Validate command
        var validationResult = await validator.ValidateAsync(command, cancellationToken).ConfigureAwait(false);

        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => new ValidationError(e.PropertyName, e.ErrorMessage));

            throw new RequestValidationException(errors);
        }

        _logger.LogInformation("Processing refresh token request");

        var refreshRequest = new RefreshTokenRequest
        {
            RefreshToken = command.RefreshToken,
            TenantId = command.TenantId
        };

        try
        {
            var domainResponse = await _authService.RefreshTokenAsync(refreshRequest, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Refresh token processed successfully");

            // Map from Domain response to Application DTO
            return await domainResponse.ToDto(_userRepository, cancellationToken).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("RefreshTokenHandler caught UnauthorizedAccessException: {Message}", ex.Message);

            throw; // Re-throw to propagate to controller
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in RefreshTokenHandler");

            throw;
        }
    }
}
