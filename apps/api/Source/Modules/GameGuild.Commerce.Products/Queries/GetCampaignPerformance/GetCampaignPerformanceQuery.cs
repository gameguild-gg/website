using FluentValidation;
using GameGuild.CQRS;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Query to get aggregated campaign performance for promo codes within an optional window
/// </summary>
/// <param name="CampaignId">Campaign identifier</param>
/// <param name="From">Window start (null = unbounded)</param>
/// <param name="To">Window end (null = unbounded)</param>
public sealed record GetCampaignPerformanceQuery(
    string CampaignId,
    DateTime? From = null,
    DateTime? To = null
) : IQuery<CampaignPerformanceDto>;

/// <summary>
/// Handler for <see cref="GetCampaignPerformanceQuery"/>
/// </summary>
public sealed class GetCampaignPerformanceQueryHandler(IPromoCodeRepository promoCodeRepository)
    : IQueryHandler<GetCampaignPerformanceQuery, CampaignPerformanceDto>
{
    /// <inheritdoc />
    public async Task<CampaignPerformanceDto> Handle(GetCampaignPerformanceQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await promoCodeRepository.GetCampaignPerformanceAsync(
            request.CampaignId.Trim(),
            request.From,
            request.To,
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Validator for <see cref="GetCampaignPerformanceQuery"/>
/// </summary>
public sealed class GetCampaignPerformanceQueryValidator : AbstractValidator<GetCampaignPerformanceQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetCampaignPerformanceQueryValidator"/> class.
    /// </summary>
    public GetCampaignPerformanceQueryValidator()
    {
        RuleFor(x => x.CampaignId)
            .NotEmpty()
            .WithMessage("Campaign identifier is required.")
            .MaximumLength(100)
            .WithMessage("Campaign identifier cannot exceed 100 characters.");

        RuleFor(x => x.To)
            .GreaterThan(x => x.From)
            .WithMessage("Window end must be after window start.")
            .When(x => x.From.HasValue && x.To.HasValue);
    }
}
