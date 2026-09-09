using FluentValidation;

namespace GameGuild.Assets.Commands;

/// <summary>
/// Command to review a moderation report (admin only).
/// </summary>
public sealed record ReviewReportCommand(
    Guid ReportId,
    Guid ReviewerId,
    ReviewDecision Decision,
    string? Notes = null) : ICommand<ReviewReportResponse?>;

public sealed record ReviewReportResponse(
    Guid ReportId,
    ReportStatus Status,
    ReviewDecision Decision);

public sealed class ReviewReportValidator : AbstractValidator<ReviewReportCommand>
{
    public ReviewReportValidator()
    {
        RuleFor(x => x.ReportId).NotEmpty();
        RuleFor(x => x.ReviewerId).NotEmpty();
        RuleFor(x => x.Decision).IsInEnum();
        RuleFor(x => x.Notes).MaximumLength(2000).When(x => x.Notes != null);
    }
}

public sealed class ReviewReportHandler : ICommandHandler<ReviewReportCommand, ReviewReportResponse?>
{
    private readonly IAssetModerationService _moderationService;

    public ReviewReportHandler(IAssetModerationService moderationService)
    {
        _moderationService = moderationService;
    }

    public async Task<ReviewReportResponse?> Handle(
        ReviewReportCommand request,
        CancellationToken ct = default)
    {
        var success = await _moderationService.SubmitReviewAsync(
            request.ReportId,
            request.ReviewerId,
            request.Decision,
            request.Notes,
            ct).ConfigureAwait(false);

        if (!success)
        {
            return null;
        }

        return new ReviewReportResponse(
            request.ReportId,
            ReportStatus.Resolved,
            request.Decision);
    }
}
