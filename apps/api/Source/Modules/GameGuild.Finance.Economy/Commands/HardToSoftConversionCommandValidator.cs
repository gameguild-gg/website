using FluentValidation;

namespace GameGuild.Finance.Economy.Commands;

public sealed class ConvertMyHardToSoftCommandValidator : AbstractValidator<ConvertMyHardToSoftCommand>
{
    public ConvertMyHardToSoftCommandValidator()
    {
        RuleFor(command => command.Request).NotNull();
        When(command => command.Request is not null, () =>
        {
            RuleFor(command => command.Request.PrincipalHardCoinUnits).GreaterThan(0);
            RuleFor(command => command.Request.IdempotencyKey).NotEmpty().MaximumLength(128);
        });
    }
}
