using FluentValidation;

namespace GameGuild.Assets.Queries;

/// <summary>
/// Query to get reports for an asset (admin only).
/// </summary>
public sealed record GetAssetReportsQuery(
    Guid AssetReferenceId) : IRequest<IReadOnlyList<ReportDto>>;

public sealed class GetAssetReportsValidator : AbstractValidator<GetAssetReportsQuery>
{
    public GetAssetReportsValidator()
    {
        RuleFor(x => x.AssetReferenceId).NotEmpty();
    }
}

public sealed class GetAssetReportsHandler : IRequestHandler<GetAssetReportsQuery, IReadOnlyList<ReportDto>>
{
    private readonly IAssetReportRepository _reportRepository;

    public GetAssetReportsHandler(IAssetReportRepository reportRepository)
    {
        _reportRepository = reportRepository;
    }

    public async Task<IReadOnlyList<ReportDto>> Handle(
        GetAssetReportsQuery request,
        CancellationToken ct = default)
    {
        var reports = await _reportRepository.GetByAssetReferenceAsync(request.AssetReferenceId, ct).ConfigureAwait(false);

        var result = reports.Select(report => new ReportDto(
            report.Id,
            report.AssetReferenceId,
            report.ReportedByUserId,
            report.Reason,
            report.Details,
            report.Status,
            report.Decision,
            report.ReviewedByUserId,
            report.ReviewNotes,
            report.CreatedAt,
            report.ReviewedAt,
            null,
            ExtractLabels(report),
            ExtractModelScores(report),
            ExtractModel(report))).ToList();

        return result;
    }

    private static string[]? ExtractLabels(AssetReport report)
    {
        try
        {
            if (report.Reference?.Content != null && report.Reference.Content.ModerationLabels != null)
            {
                return System.Text.Json.JsonSerializer.Deserialize<string[]>(report.Reference.Content.ModerationLabels);
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, double>? ExtractModelScores(AssetReport report)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(report.ReviewNotes))
            {
                var scoresPart = report.ReviewNotes.Split(';').FirstOrDefault(p => p.StartsWith("scores="));
                if (scoresPart != null)
                {
                    var scoresStr = scoresPart["scores=".Length..];
                    var scoresDict = new Dictionary<string, double>();
                    foreach (var pair in scoresStr.Split(','))
                    {
                        var parts = pair.Split(':');
                        if (parts.Length == 2 && double.TryParse(parts[1], out var score))
                        {
                            scoresDict[parts[0]] = score;
                        }
                    }
                    return scoresDict;
                }
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractModel(AssetReport report)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(report.ReviewNotes))
            {
                var modelPart = report.ReviewNotes.Split(';').FirstOrDefault(p => p.StartsWith("model="));
                return modelPart?["model=".Length..];
            }
            return null;
        }
        catch
        {
            return null;
        }
    }
}
