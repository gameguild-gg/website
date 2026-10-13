using System.Text.RegularExpressions;
using System.ComponentModel.DataAnnotations;
using GameGuild.CQRS;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Logging;

using Microsoft.AspNetCore.Http;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handler for polymorphic sign-in command supporting multiple credential types
/// </summary>
public sealed class PolymorphicSignInHandler(
    IAuthService authService,
    IUserRepository userRepository,
    ILogger<PolymorphicSignInHandler> logger,
    IPasswordSignInAdmissionService admissionService,
    FluentValidation.IValidator<PolymorphicSignInCommand>? validator = null,
    TimeProvider? timeProvider = null,
    IHttpContextAccessor? httpContextAccessor = null
) : ICommandHandler<PolymorphicSignInCommand, SignInResponse>
{
    public async Task<SignInResponse> Handle(PolymorphicSignInCommand command, CancellationToken cancellationToken)
    {
        var timingOrigin = AuthenticationTimingOrigin.GetOrStartForRequest(httpContextAccessor?.HttpContext, timeProvider);
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        // Validate command if validator is available
        if (validator != null)
        {
            var validationResult = await validator.ValidateAsync(command, cancellationToken).ConfigureAwait(false);

            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => new ValidationError(e.PropertyName, e.ErrorMessage));
                throw new RequestValidationException(errors);
            }
        }

        var identifier = command.Credential?.Trim() ?? string.Empty;
        var credentialType = command.CredentialType ?? DetectCredentialType(identifier);

        logger.LogInformation("Processing polymorphic sign-in");

        User? account = null;
        var lookupType = ValidIdentifierType(identifier, credentialType);
        if (lookupType.HasValue)
        {
            var candidates = await userRepository.FindSignInCandidatesAsync(identifier, lookupType.Value, cancellationToken).ConfigureAwait(false);
            if (candidates.Count == 1)
            {
                account = candidates[0];
            }
        }

        // Failed resolution still runs the existing denial, timing, attempt and risk path.
        // It cannot authenticate another account through an email fallback.
        var localSignInRequest = new LocalSignInRequest
        {
            Email = account?.Email ?? identifier,
            Password = command.Password,
            TenantId = command.TenantId,
            DeviceFingerprint = command.DeviceFingerprint,
            RememberMe = command.RememberMe,
            CredentialResolutionFailed = account is null,
            ResolvedUserId = account?.Id,
            TimingOrigin = timingOrigin
        };

        // Resolve username/phone to the same account key used by email sign-in.
        // Missing/ambiguous identifiers still consume the source-IP admission budget.
        await using var admission = await admissionService.AdmitAsync(
            localSignInRequest.Email, httpContextAccessor?.HttpContext, timingOrigin, cancellationToken).ConfigureAwait(false);
        var domainResult = await authService.LocalSignInAsync(localSignInRequest, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Polymorphic password flow returned; success: {Success}", domainResult.Success);

        // Map from Domain response to Application DTO
        return await domainResult.ToDto(userRepository, cancellationToken).ConfigureAwait(false);
    }

    private static CredentialType DetectCredentialType(string credential)
    {
        if (credential.Contains('@', StringComparison.Ordinal))
        {
            return CredentialType.Email;
        }

        if (credential.StartsWith('+'))
        {
            return CredentialType.Phone;
        }

        // Default to username
        return CredentialType.Username;
    }

    private static SignInIdentifierType? ValidIdentifierType(string identifier, CredentialType type)
    {
        if (identifier.Length == 0 || identifier.Any(char.IsControl))
        {
            return null;
        }
        return type switch
        {
            CredentialType.Email when identifier.Length <= 255 && new EmailAddressAttribute().IsValid(identifier) => SignInIdentifierType.Email,
            CredentialType.Username when identifier.Length <= UsernameSlug.MaximumLength && identifier.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-') => SignInIdentifierType.Username,
            CredentialType.Phone when identifier.Length <= 16 && Regex.IsMatch(identifier, @"^\+[1-9][0-9]{1,14}$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)) => SignInIdentifierType.Phone,
            _ => null
        };
    }
}
