using FluentValidation;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Validator for SteamCallbackCommand following CQRS and DRY principles
/// </summary>
public sealed class SteamCallbackCommandValidator : AbstractValidator<SteamCallbackCommand>
{
    public SteamCallbackCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .WithMessage("OpenID assertion is required");

        RuleFor(x => x.State)
            .NotEmpty()
            .WithMessage("State parameter is required");

        RuleFor(x => x.RedirectUri)
            .NotEmpty()
            .WithMessage("Redirect URI is required")
            .MaximumLength(2048)
            .WithMessage("Redirect URI is too long");

        RuleFor(x => x.TenantId).Must(tenantId => !tenantId.HasValue || tenantId.Value != Guid.Empty).WithMessage("Tenant ID must be a valid GUID when provided");
    }
}
