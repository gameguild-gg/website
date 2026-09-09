using FluentValidation;

namespace GameGuild.Finance.Economy.Commands;

public sealed class CreateMyHardCoinTopUpCommandValidator
    : AbstractValidator<CreateMyHardCoinTopUpCommand>
{
    public CreateMyHardCoinTopUpCommandValidator()
    {
        RuleFor(command => command.Request).NotNull();
        When(command => command.Request is not null, () =>
        {
            RuleFor(command => command.Request.HardCoinUnits).GreaterThan(0);
            RuleFor(command => command.Request.IdempotencyKey).NotEmpty().MaximumLength(128);
        });
    }
}
