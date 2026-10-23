using System.Security.Cryptography;
using GameGuild.CQRS;
using GameGuild.Resources;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Command to bulk-generate promo codes with a shared prefix and random suffixes.
/// Codes are generated uppercase, guaranteed unique within the batch and against
/// existing codes, and created atomically in a single save.
/// </summary>
/// <param name="Count">Number of codes to generate (1–1000)</param>
/// <param name="Prefix">Shared prefix (uppercase letters, digits, hyphens; 2–40 chars)</param>
/// <param name="Name">Base display name; each code gets a numbered suffix</param>
/// <param name="Description">Optional shared description</param>
/// <param name="Type">Discount type</param>
/// <param name="DiscountPercentage">Percentage discount (percentage type)</param>
/// <param name="DiscountAmount">Fixed amount / set price</param>
/// <param name="MaxDiscountAmount">Cap for percentage discounts</param>
/// <param name="Currency">Currency code</param>
/// <param name="MinimumOrderAmount">Minimum order amount</param>
/// <param name="MaxUses">Maximum total uses per code</param>
/// <param name="MaxUsesPerUser">Maximum uses per user per code</param>
/// <param name="FreeTrialExtensionDays">Trial days (FreeTrialExtension)</param>
/// <param name="DurationMonths">Free months (FreeMonths)</param>
/// <param name="ApplyToRenewal">Whether discounts apply to renewals</param>
/// <param name="ValidFrom">Validity start</param>
/// <param name="ValidUntil">Validity end</param>
/// <param name="StackingPriority">Stacking priority</param>
/// <param name="CampaignId">Campaign identifier stamped on every generated code</param>
/// <param name="ProductId">Product restriction</param>
/// <param name="PlanId">Subscription plan restriction (issue #418)</param>
/// <param name="CreatedBy">Creating user</param>
[RequiresQuota(ResourceUsageType.PromoCodes, 1, Source = "GenerateBulkPromoCodes")]
public sealed record GenerateBulkPromoCodesCommand(
    int Count,
    string Prefix,
    string Name,
    string? Description = null,
    PromoCodeType Type = PromoCodeType.PercentageOff,
    decimal? DiscountPercentage = null,
    decimal? DiscountAmount = null,
    decimal? MaxDiscountAmount = null,
    string Currency = "USD",
    decimal? MinimumOrderAmount = null,
    int? MaxUses = null,
    int? MaxUsesPerUser = null,
    int? FreeTrialExtensionDays = null,
    int? DurationMonths = null,
    bool ApplyToRenewal = false,
    DateTime? ValidFrom = null,
    DateTime? ValidUntil = null,
    int StackingPriority = 0,
    string? CampaignId = null,
    Guid? ProductId = null,
    Guid? CreatedBy = null,
    Guid? PlanId = null
) : ICommand<IReadOnlyList<PromoCodeDto>>;

/// <summary>
/// Handler for <see cref="GenerateBulkPromoCodesCommand"/>.
/// </summary>
public sealed class GenerateBulkPromoCodesCommandHandler(
    IPromoCodeRepository promoCodeRepository,
    IPromoCodeAuditLogger auditLogger)
    : ICommandHandler<GenerateBulkPromoCodesCommand, IReadOnlyList<PromoCodeDto>>
{
    /// <summary>Maximum codes a single command may generate.</summary>
    public const int MaxCount = 1000;

    // Unambiguous uppercase alphabet: no 0/O or 1/I confusion for customer support.
    private const string SuffixAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int SuffixLength = 8;

    /// <inheritdoc />
    public async Task<IReadOnlyList<PromoCodeDto>> Handle(
        GenerateBulkPromoCodesCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Count is < 1 or > MaxCount)
        {
            throw new ArgumentOutOfRangeException(nameof(request), $"Count must be between 1 and {MaxCount}.");
        }

        var prefix = PromoCodeNormalizer.Normalize(request.Prefix)
            ?? throw new ArgumentException("Prefix is required.", nameof(request));

        // Generate candidates that are unique within the batch, then verify against
        // existing codes; collisions (astronomically rare with an 8-char suffix) regenerate.
        var candidateCodes = GenerateUniqueCodes(prefix, request.Count, taken: null);
        var existing = await promoCodeRepository.GetExistingCodesAsync(candidateCodes, cancellationToken).ConfigureAwait(false);
        while (existing.Count > 0)
        {
            candidateCodes = GenerateUniqueCodes(prefix, request.Count, existing);
            existing = await promoCodeRepository.GetExistingCodesAsync(candidateCodes, cancellationToken).ConfigureAwait(false);
        }

        var createdAt = SystemClock.UtcNow;
        var promoCodes = candidateCodes.Select((code, index) => new PromoCode
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = $"{request.Name} #{index + 1}",
            Description = request.Description,
            Type = request.Type,
            DiscountPercentage = request.DiscountPercentage,
            DiscountAmount = request.DiscountAmount,
            MaxDiscountAmount = request.MaxDiscountAmount,
            Currency = request.Currency,
            MinimumOrderAmount = request.MinimumOrderAmount,
            MaxUses = request.MaxUses,
            MaxUsesPerUser = request.MaxUsesPerUser,
            FreeTrialExtensionDays = request.FreeTrialExtensionDays,
            DurationMonths = request.DurationMonths,
            ApplyToRenewal = request.ApplyToRenewal,
            ValidFrom = request.ValidFrom,
            ValidUntil = request.ValidUntil,
            IsActive = true,
            IsExclusive = false,
            StackingPriority = request.StackingPriority,
            CampaignId = request.CampaignId,
            ProductId = request.ProductId,
            PlanId = request.PlanId,
            CreatedBy = request.CreatedBy ?? Guid.Empty
        }).ToList();

        await promoCodeRepository.AddRangeAsync(promoCodes, cancellationToken).ConfigureAwait(false);
        await promoCodeRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        foreach (var promoCode in promoCodes)
        {
            await auditLogger.LogMutationAsync(
                request.CreatedBy,
                "Created",
                promoCode.Id,
                promoCode.Code,
                new { bulk = true, campaignId = request.CampaignId },
                cancellationToken).ConfigureAwait(false);
        }

        return promoCodes.Select(p => p.ToDto(0)).ToList();
    }

    private static List<string> GenerateUniqueCodes(string prefix, int count, HashSet<string>? taken)
    {
        var codes = new List<string>(count);
        var seen = new HashSet<string>(taken ?? [], StringComparer.OrdinalIgnoreCase);

        Span<char> suffix = stackalloc char[SuffixLength];
        while (codes.Count < count)
        {
            RandomNumberGenerator.GetItems(SuffixAlphabet, suffix);
            var code = $"{prefix}-{new string(suffix)}";
            if (seen.Add(code))
            {
                codes.Add(code);
            }
        }

        return codes;
    }
}
