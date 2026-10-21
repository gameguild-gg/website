using FluentValidation;

namespace GameGuild.Identity.Authentication;

public sealed class RequestAccountRecoveryCommandValidator : AbstractValidator<RequestAccountRecoveryCommand>
{
    public RequestAccountRecoveryCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Email must be a valid email address")
            .MaximumLength(254).WithMessage("Email is too long"); // RFC 5321 limit
    }
}

public sealed class CancelAccountRecoveryCommandValidator : AbstractValidator<CancelAccountRecoveryCommand>
{
    public CancelAccountRecoveryCommandValidator()
    {
        RuleFor(x => x.OwnerUserId)
            .NotEmpty().WithMessage("Owner is required");
    }
}

public sealed class ApproveAccountRecoveryCommandValidator : AbstractValidator<ApproveAccountRecoveryCommand>
{
    public ApproveAccountRecoveryCommandValidator()
    {
        RuleFor(x => x.ApprovalCode)
            .NotEmpty().WithMessage("Approval code is required")
            .MaximumLength(256).WithMessage("Approval code is too long");
    }
}

public sealed class BeginPasskeyRecoveryApprovalCommandValidator : AbstractValidator<BeginPasskeyRecoveryApprovalCommand>
{
    public BeginPasskeyRecoveryApprovalCommandValidator()
    {
        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email must be a valid email address")
            .MaximumLength(254).WithMessage("Email is too long")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public sealed class ApproveAccountRecoveryWithPasskeyCommandValidator : AbstractValidator<ApproveAccountRecoveryWithPasskeyCommand>
{
    public ApproveAccountRecoveryWithPasskeyCommandValidator()
    {
        RuleFor(x => x.AssertionResponse)
            .NotEmpty().WithMessage("Assertion response is required")
            .MaximumLength(65536).WithMessage("Assertion response is too long");
    }
}

public sealed class CompleteAccountRecoveryResetCommandValidator : AbstractValidator<CompleteAccountRecoveryResetCommand>
{
    public CompleteAccountRecoveryResetCommandValidator()
    {
        RuleFor(x => x.ResetToken)
            .NotEmpty().WithMessage("Reset token is required")
            .MaximumLength(256).WithMessage("Reset token is too long");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required")
            .MaximumLength(128).WithMessage("New password is too long");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("Password confirmation is required");

        RuleFor(x => x)
            .Must(x => x.NewPassword == x.ConfirmPassword)
            .WithMessage("Password confirmation does not match")
            .OverridePropertyName(nameof(CompleteAccountRecoveryResetCommand.ConfirmPassword));
    }
}
