using FluentValidation;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Validator for InitiateOrchestratedAuthenticationCommand following CQRS and DRY principles
/// </summary>
public sealed class InitiateOrchestratedAuthenticationCommandValidator : AbstractValidator<InitiateOrchestratedAuthenticationCommand>
{
    public InitiateOrchestratedAuthenticationCommandValidator()
    {
        RuleFor(x => x.Identifier)
            .NotEmpty()
            .WithMessage("Identifier is required");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password is required");

        RuleFor(x => x.TenantId).Must(tenantId => !tenantId.HasValue || tenantId.Value != Guid.Empty).WithMessage("Tenant ID must be a valid GUID when provided");
    }
}

/// <summary>
///     Validator for ProcessOrchestratedAuthenticationStepCommand following CQRS and DRY principles
/// </summary>
public sealed class ProcessOrchestratedAuthenticationStepCommandValidator : AbstractValidator<ProcessOrchestratedAuthenticationStepCommand>
{
    public ProcessOrchestratedAuthenticationStepCommandValidator()
    {
        RuleFor(x => x.FlowId)
            .NotEmpty()
            .WithMessage("Flow ID is required");

        RuleFor(x => x.Step)
            .NotEqual(AuthenticationStep.PrimaryCredential)
            .WithMessage("The primary credential step is satisfied by flow initiation and cannot be processed.");

        RuleFor(x => x)
            .Must(command => !string.IsNullOrWhiteSpace(command.TotpCode)
                             || !string.IsNullOrWhiteSpace(command.BackupCode)
                             || !string.IsNullOrWhiteSpace(command.WebAuthnAssertion))
            .WithMessage("Exactly one verification proof (TOTP code, backup code or WebAuthn assertion) is required.");
    }
}

/// <summary>
///     Validator for CompleteOrchestratedAuthenticationCommand following CQRS and DRY principles
/// </summary>
public sealed class CompleteOrchestratedAuthenticationCommandValidator : AbstractValidator<CompleteOrchestratedAuthenticationCommand>
{
    public CompleteOrchestratedAuthenticationCommandValidator()
    {
        RuleFor(x => x.FlowId)
            .NotEmpty()
            .WithMessage("Flow ID is required");
    }
}

/// <summary>
///     Validator for AbandonOrchestratedAuthenticationCommand following CQRS and DRY principles
/// </summary>
public sealed class AbandonOrchestratedAuthenticationCommandValidator : AbstractValidator<AbandonOrchestratedAuthenticationCommand>
{
    public AbandonOrchestratedAuthenticationCommandValidator()
    {
        RuleFor(x => x.FlowId)
            .NotEmpty()
            .WithMessage("Flow ID is required");
    }
}
