using FluentValidation;

namespace GameGuild.Finance.Economy.Commands;

public sealed class CreateMyEconomyTransferCommandValidator
    : AbstractValidator<CreateMyEconomyTransferCommand>
{
    public CreateMyEconomyTransferCommandValidator()
    {
        RuleFor(command => command.Request).NotNull();
        When(command => command.Request is not null, () =>
        {
            RuleFor(command => command.Request.RecipientUserId).NotEmpty();
            RuleFor(command => command.Request.TransferType).IsInEnum();
            RuleFor(command => command.Request.Currency).IsInEnum();
            RuleFor(command => command.Request.AmountUnits).GreaterThan(0);
            RuleFor(command => command.Request.IdempotencyKey).NotEmpty().MaximumLength(128);
        });
    }
}
