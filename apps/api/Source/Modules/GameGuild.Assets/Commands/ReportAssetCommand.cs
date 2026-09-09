using FluentValidation;

namespace GameGuild.Assets.Commands;

/// <summary>
/// Command to report an asset for moderation.
/// </summary>
public sealed record ReportAssetCommand(
    Guid AssetReferenceId,
    Guid ReportedByUserId,
    ReportReason Reason,
    string? Description = null) : ICommand<ReportAssetResponse?>;

public sealed record ReportAssetResponse(
    Guid ReportId,
    ReportStatus Status);

public sealed class ReportAssetValidator : AbstractValidator<ReportAssetCommand>
{
    public ReportAssetValidator()
    {
        RuleFor(x => x.AssetReferenceId).NotEmpty();
        RuleFor(x => x.ReportedByUserId).NotEmpty();
        RuleFor(x => x.Reason).IsInEnum();
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description != null);
    }
}

public sealed class ReportAssetHandler : ICommandHandler<ReportAssetCommand, ReportAssetResponse?>
{
    private readonly IAssetModerationService _moderationService;
    private readonly IAssetReferenceRepository _referenceRepository;

    public ReportAssetHandler(
        IAssetModerationService moderationService,
        IAssetReferenceRepository referenceRepository)
    {
        _moderationService = moderationService;
        _referenceRepository = referenceRepository;
    }

    public async Task<ReportAssetResponse?> Handle(
        ReportAssetCommand request,
        CancellationToken ct = default)
    {
        // Verify asset exists
        var reference = await _referenceRepository.GetByIdAsync(request.AssetReferenceId, ct).ConfigureAwait(false);
        if (reference == null)
        {
            return null;
        }

        // Cannot report your own asset
        if (reference.CreatedByUserId == request.ReportedByUserId)
        {
            return null;
        }

        var report = await _moderationService.CreateReportAsync(
            request.AssetReferenceId,
            request.ReportedByUserId,
            request.Reason,
            request.Description,
            ct).ConfigureAwait(false);

        if (report == null)
        {
            return null;
        }

        return new ReportAssetResponse(report.Id, report.Status);
    }
}
