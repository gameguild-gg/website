using FluentValidation;
using GameGuild.CQRS;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Command to run the pricing engine for a product (issue #395): base/sale price,
/// the highest-priority applicable pricing rule (volume tiers, customer segment),
/// and promo codes. Dispatched as a command (like <see cref="ValidatePromoCodeCommand"/>)
/// so it flows through the CQRS pipeline; it performs no mutation.
/// </summary>
/// <param name="ProductId">Product to price</param>
/// <param name="PricingId">Optional specific pricing option to use</param>
/// <param name="Quantity">Quantity being priced (drives volume tiers)</param>
/// <param name="CustomerSegment">Optional customer segment for segment-scoped rules</param>
/// <param name="PromoCodes">Optional promo codes to apply after rule resolution</param>
/// <param name="UserId">Optional user ID for per-user promo validation</param>
public sealed record CalculatePricingCommand(
    Guid ProductId,
    Guid? PricingId = null,
    int Quantity = 1,
    string? CustomerSegment = null,
    List<string>? PromoCodes = null,
    Guid? UserId = null
) : ICommand<PricingCalculationResult>;

/// <summary>
/// Handler for running the pricing engine for a product.
/// </summary>
public sealed class CalculatePricingCommandHandler(IPricingEngineService pricingEngine)
    : ICommandHandler<CalculatePricingCommand, PricingCalculationResult>
{
    public async Task<PricingCalculationResult> Handle(CalculatePricingCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await pricingEngine.CalculatePriceByIdAsync(
            request.ProductId,
            request.PricingId,
            request.PromoCodes,
            request.UserId,
            request.Quantity,
            request.CustomerSegment,
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Validator for <see cref="CalculatePricingCommand"/>.
/// </summary>
public sealed class CalculatePricingCommandValidator : AbstractValidator<CalculatePricingCommand>
{
    /// <summary>
    /// Initializes the validator.
    /// </summary>
    public CalculatePricingCommandValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty()
            .WithMessage("Product ID is required.");

        RuleFor(x => x.Quantity)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Quantity must be at least 1.");

        RuleFor(x => x.CustomerSegment)
            .MaximumLength(100)
            .WithMessage("Customer segment cannot exceed 100 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.CustomerSegment));
    }
}
