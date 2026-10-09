using FluentValidation;

namespace GameGuild.Assets.Queries;

/// <summary>
/// Query to get assets by parent resource.
/// </summary>
public sealed record GetAssetsByParentQuery(
    string ParentResourceType,
    Guid ParentResourceId,
    Guid? UserId,
    Guid? TenantId) : IRequest<IReadOnlyList<AssetDto>>;

public sealed class GetAssetsByParentValidator : AbstractValidator<GetAssetsByParentQuery>
{
    public GetAssetsByParentValidator()
    {
        RuleFor(x => x.ParentResourceType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ParentResourceId).NotEmpty();
    }
}

public sealed class GetAssetsByParentHandler : IRequestHandler<GetAssetsByParentQuery, IReadOnlyList<AssetDto>>
{
    private readonly IAssetReferenceRepository _referenceRepository;
    private readonly IAssetAccessService _accessService;

    public GetAssetsByParentHandler(
        IAssetReferenceRepository referenceRepository,
        IAssetAccessService accessService)
    {
        _referenceRepository = referenceRepository;
        _accessService = accessService;
    }

    public async Task<IReadOnlyList<AssetDto>> Handle(
        GetAssetsByParentQuery request,
        CancellationToken ct = default)
    {
        var references = await _referenceRepository.GetByParentAsync(
            request.ParentResourceType,
            request.ParentResourceId,
            ct).ConfigureAwait(false);

        var result = new List<AssetDto>();

        foreach (var reference in references)
        {
            // Check access for each asset
            var validation = await _accessService.ValidateAccessAsync(
                reference.Id,
                request.UserId,
                request.TenantId,
                ct).ConfigureAwait(false);

            if (!validation.IsValid)
            {
                continue;
            }

            AssetContentDto? contentDto = null;
            if (reference.Content != null)
            {
                contentDto = new AssetContentDto(
                    reference.Content.Id,
                    reference.Content.ContentHash,
                    reference.Content.MimeType,
                    reference.Content.SizeBytes,
                    reference.Content.Width,
                    reference.Content.Height,
                    reference.Content.VirusScanStatus,
                    reference.Content.ModerationStatus);
            }

            result.Add(new AssetDto(
                reference.Id,
                reference.AssetContentId,
                reference.CreatedByUserId,
                reference.DisplayName,
                reference.AccessPolicy,
                reference.ParentResourceType,
                reference.ParentResourceId,
                reference.AccessCount,
                reference.LastAccessedAt,
                reference.CreatedAt,
                reference.UpdatedAt,
                contentDto));
        }

        return result;
    }
}
