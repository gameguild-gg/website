using FluentValidation;

namespace GameGuild.Identity.Authentication;

public sealed class AddTrustedContactCommandValidator : AbstractValidator<AddTrustedContactCommand>
{
    public AddTrustedContactCommandValidator()
    {
        RuleFor(x => x.OwnerUserId)
            .NotEmpty().WithMessage("Owner is required");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Email must be a valid email address")
            .MaximumLength(254).WithMessage("Email is too long"); // RFC 5321 limit

        RuleFor(x => x.Relation)
            .MaximumLength(100).WithMessage("Relation is too long");
    }
}

public sealed class RemoveTrustedContactCommandValidator : AbstractValidator<RemoveTrustedContactCommand>
{
    public RemoveTrustedContactCommandValidator()
    {
        RuleFor(x => x.OwnerUserId)
            .NotEmpty().WithMessage("Owner is required");

        RuleFor(x => x.ContactId)
            .NotEmpty().WithMessage("Contact id is required");
    }
}

public sealed class ConfirmTrustedContactCommandValidator : AbstractValidator<ConfirmTrustedContactCommand>
{
    public ConfirmTrustedContactCommandValidator()
    {
        RuleFor(x => x.InviteToken)
            .NotEmpty().WithMessage("Invite token is required")
            .MaximumLength(256).WithMessage("Invite token is too long");
    }
}
