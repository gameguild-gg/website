using FluentValidation;

namespace GameGuild.Identity.Authentication;

public sealed class AdminResetUserMfaCommandValidator : AbstractValidator<AdminResetUserMfaCommand>
{
    public AdminResetUserMfaCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User id is required");
    }
}
