using FluentValidation;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Validator for SamlCallbackCommand following CQRS and DRY principles.
/// </summary>
public sealed class SamlCallbackCommandValidator : AbstractValidator<SamlCallbackCommand>
{
    public SamlCallbackCommandValidator()
    {
        RuleFor(x => x.Slug)
            .NotEmpty()
            .WithMessage("Provider slug is required")
            .MaximumLength(64)
            .WithMessage("Provider slug is too long")
            .Must(GameGuild.Configuration.PresentationLayer.Authentication.ExternalProviderOptions.IsValidFederationSlug)
            .WithMessage("Provider slug must be lowercase letters, digits, and hyphens");

        RuleFor(x => x.SamlResponse)
            .NotEmpty()
            .WithMessage("SAML response is required")
            .MaximumLength(1_000_000)
            .WithMessage("SAML response is too long");

        RuleFor(x => x.RelayState)
            .MaximumLength(512)
            .WithMessage("Relay state is too long");

        RuleFor(x => x.TenantId).Must(tenantId => !tenantId.HasValue || tenantId.Value != Guid.Empty).WithMessage("Tenant ID must be a valid GUID when provided");
    }
}
