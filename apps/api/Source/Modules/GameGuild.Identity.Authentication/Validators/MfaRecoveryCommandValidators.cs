using FluentValidation;

namespace GameGuild.Identity.Authentication;

public sealed class RequestMfaRecoveryCommandValidator : AbstractValidator<RequestMfaRecoveryCommand>
{
    public RequestMfaRecoveryCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Email must be a valid email address")
            .MaximumLength(254).WithMessage("Email is too long"); // RFC 5321 limit
    }
}

public sealed class ContinueMfaRecoveryCommandValidator : AbstractValidator<ContinueMfaRecoveryCommand>
{
    public ContinueMfaRecoveryCommandValidator()
    {
        RuleFor(x => x.RequestToken)
            .NotEmpty().WithMessage("Request token is required")
            .MaximumLength(256).WithMessage("Request token is too long");
    }
}

public sealed class ConfirmMfaRecoveryCommandValidator : AbstractValidator<ConfirmMfaRecoveryCommand>
{
    public ConfirmMfaRecoveryCommandValidator()
    {
        RuleFor(x => x.ConfirmationToken)
            .NotEmpty().WithMessage("Confirmation token is required")
            .MaximumLength(256).WithMessage("Confirmation token is too long");
    }
}
