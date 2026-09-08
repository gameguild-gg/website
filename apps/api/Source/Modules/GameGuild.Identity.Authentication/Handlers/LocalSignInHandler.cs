using GameGuild.CQRS;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handler for local sign-in command
/// </summary>
public sealed class LocalSignInHandler(
    IAuthService authService,
    IUserRepository userRepository,
    IHttpContextAccessor httpContextAccessor,
    ILogger<LocalSignInHandler> logger,
    FluentValidation.IValidator<LocalSignInCommand> validator
) : ICommandHandler<LocalSignInCommand, SignInResponse>
{
    public async Task<SignInResponse> Handle(LocalSignInCommand command, CancellationToken cancellationToken)
    {
        // Validate command
        var validationResult = await validator.ValidateAsync(command, cancellationToken).ConfigureAwait(false);

        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => new ValidationError(e.PropertyName, e.ErrorMessage));

            throw new RequestValidationException(errors);
        }

        var httpContext = httpContextAccessor.HttpContext;
        var ipAddress = GetClientIpAddress(httpContext);

        var signInRequest = new LocalSignInRequest { Email = command.Email, Password = command.Password, TenantId = command.TenantId, DeviceFingerprint = command.DeviceFingerprint };

        var domainResult = await authService.LocalSignInAsync(signInRequest, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("User successfully signed in via local authentication from IP {IpAddress}", ipAddress);

        // Map from Domain response to Application DTO
        return await domainResult.ToDto(userRepository, cancellationToken).ConfigureAwait(false);
    }

    private static string? GetClientIpAddress(HttpContext? httpContext)
    {
        if (httpContext == null) return null;

        var forwarded = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();

        if (!string.IsNullOrEmpty(forwarded)) { return forwarded.Split(',')[0].Trim(); }

        return httpContext.Connection.RemoteIpAddress?.ToString();
    }
}
