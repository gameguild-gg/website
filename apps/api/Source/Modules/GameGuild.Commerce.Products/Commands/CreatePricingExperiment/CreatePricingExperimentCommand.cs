using FluentValidation;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Command to create a pricing A/B experiment with its arms (issue #395). Experiments
/// are revenue-generation actions and require the monetization permission, mirroring
/// pricing-rule mutations.
/// </summary>
/// <param name="ProductId">Product the experiment applies to (null = global)</param>
/// <param name="Name">Experiment name</param>
/// <param name="Description">Optional hypothesis description</param>
/// <param name="StartDate">Optional enrollment start (UTC)</param>
/// <param name="EndDate">Optional enrollment end (UTC)</param>
/// <param name="TrafficPercentage">Percentage of bucket-key traffic enrolled (0–100)</param>
/// <param name="Variants">Experiment arms (exactly one must be the control)</param>
[AuthorizeRequest(MonetizationPermission.Keys.Monetize)]
public sealed record CreatePricingExperimentCommand(
    Guid? ProductId,
    string Name,
    string? Description = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    int TrafficPercentage = 100,
    IReadOnlyList<PricingExperimentVariantRequest>? Variants = null
) : ICommand<PricingExperimentDto>;

/// <summary>
/// Handler for creating a pricing experiment.
/// </summary>
public sealed class CreatePricingExperimentCommandHandler(IPricingExperimentRepository repository)
    : ICommandHandler<CreatePricingExperimentCommand, PricingExperimentDto>
{
    public async Task<PricingExperimentDto> Handle(CreatePricingExperimentCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var experiment = new PricingExperiment
        {
            Id = Guid.NewGuid(),
            ProductId = request.ProductId,
            Name = request.Name.Trim(),
            Description = request.Description,
            Status = PricingExperimentStatus.Draft,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            TrafficPercentage = request.TrafficPercentage
        };

        foreach (var variant in request.Variants ?? [])
        {
            experiment.Variants.Add(new PricingExperimentVariant
            {
                Id = Guid.NewGuid(),
                ExperimentId = experiment.Id,
                Name = variant.Name.Trim(),
                IsControl = variant.IsControl,
                AllocationPercentage = variant.AllocationPercentage,
                DiscountPercentage = variant.DiscountPercentage,
                FixedPrice = variant.FixedPrice
            });
        }

        await repository.AddAsync(experiment, cancellationToken).ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return experiment.ToDto();
    }
}

/// <summary>
/// Validator for <see cref="CreatePricingExperimentCommand"/>.
/// </summary>
public sealed class CreatePricingExperimentCommandValidator : AbstractValidator<CreatePricingExperimentCommand>
{
    /// <summary>
    /// Initializes the validator.
    /// </summary>
    public CreatePricingExperimentCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Experiment name is required.")
            .MaximumLength(200)
            .WithMessage("Experiment name cannot exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .WithMessage("Description cannot exceed 1000 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));

        RuleFor(x => x.TrafficPercentage)
            .InclusiveBetween(0, 100)
            .WithMessage("Traffic percentage must be between 0 and 100.");

        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate)
            .WithMessage("End date must be after start date.")
            .When(x => x.StartDate.HasValue && x.EndDate.HasValue);

        RuleFor(x => x.Variants)
            .Must(variants => variants is { Count: >= 2 })
            .WithMessage("An experiment needs at least two variants (a control and one treatment).")
            .When(x => x.Variants is not null);

        RuleFor(x => x.Variants)
            .Must(variants => variants?.Count(v => v.IsControl) == 1)
            .WithMessage("An experiment needs exactly one control variant.")
            .When(x => x.Variants is { Count: > 0 });

        RuleForEach(x => x.Variants).ChildRules(variant =>
        {
            variant.RuleFor(v => v.Name)
                .NotEmpty()
                .WithMessage("Variant name is required.")
                .MaximumLength(100)
                .WithMessage("Variant name cannot exceed 100 characters.");

            variant.RuleFor(v => v.AllocationPercentage)
                .InclusiveBetween(1, 100)
                .WithMessage("Variant allocation percentage must be between 1 and 100.");

            variant.RuleFor(v => v.DiscountPercentage)
                .InclusiveBetween(0.01m, 100m)
                .WithMessage("Variant discount percentage must be between 0.01 and 100.")
                .When(v => v.DiscountPercentage.HasValue);

            variant.RuleFor(v => v.FixedPrice)
                .GreaterThan(0)
                .WithMessage("Variant fixed price must be greater than 0.")
                .When(v => v.FixedPrice.HasValue);

            variant.RuleFor(v => v)
                .Must(v => v.IsControl || v.DiscountPercentage.HasValue || v.FixedPrice.HasValue)
                .WithMessage("A treatment variant needs a discount percentage or a fixed price.");

            variant.RuleFor(v => v)
                .Must(v => !(v.DiscountPercentage.HasValue && v.FixedPrice.HasValue))
                .WithMessage("A variant cannot set both a discount percentage and a fixed price.");
        });
    }
}
