using FluentValidation;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Validator for OidcCallbackCommand following CQRS and DRY principles.
/// </summary>
public sealed class OidcCallbackCommandValidator : AbstractValidator<OidcCallbackCommand>
{
    public OidcCallbackCommandValidator()
    {
        RuleFor(x => x.Slug)
            .NotEmpty()
            .WithMessage("Provider slug is required")
            .MaximumLength(64)
            .WithMessage("Provider slug is too long")
            .Must(GameGuild.Configuration.PresentationLayer.Authentication.ExternalProviderOptions.IsValidOidcSlug)
            .WithMessage("Provider slug must be lowercase letters, digits, and hyphens");

        RuleFor(x => x.Code)
            .NotEmpty()
            .WithMessage("Authorization code is required");

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
