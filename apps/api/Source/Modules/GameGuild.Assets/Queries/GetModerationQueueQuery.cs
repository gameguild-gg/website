using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace GameGuild.Assets.Queries;

/// <summary>
/// Query to get pending moderation queue (admin only).
/// </summary>
public sealed record GetModerationQueueQuery(
    int Limit = 100) : IRequest<IReadOnlyList<ReportDto>>;

public sealed record ReportDto(
    Guid Id,
    Guid AssetReferenceId,
    Guid ReportedByUserId,
    ReportReason Reason,
    string? Description,
    ReportStatus Status,
    ReviewDecision? Decision,
    Guid? ReviewedByUserId,
    string? ReviewNotes,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    AssetDto? Asset,
    string[]? Labels,
    Dictionary<string, double>? ModelScores,
    string? Model);

public sealed class GetModerationQueueValidator : AbstractValidator<GetModerationQueueQuery>
{
    public GetModerationQueueValidator()
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, 500);
    }
}

public sealed class GetModerationQueueHandler : IRequestHandler<GetModerationQueueQuery, IReadOnlyList<ReportDto>>
{
    private readonly IAssetModerationService _moderationService;

    public GetModerationQueueHandler(IAssetModerationService moderationService)
    {
        _moderationService = moderationService;
    }

    public async Task<IReadOnlyList<ReportDto>> Handle(
        GetModerationQueueQuery request,
        CancellationToken ct = default)
    {
        var reports = await _moderationService.GetPendingReportsAsync(request.Limit, ct).ConfigureAwait(false);

        var result = reports.Select(report =>
        {
            AssetDto? assetDto = null;
            if (report.Reference != null)
            {
                AssetContentDto? contentDto = null;
                if (report.Reference.Content != null)
                {
                    contentDto = new AssetContentDto(
                        report.Reference.Content.Id,
                        report.Reference.Content.ContentHash,
                        report.Reference.Content.MimeType,
                        report.Reference.Content.SizeBytes,
                        report.Reference.Content.Width,
                        report.Reference.Content.Height,
                        report.Reference.Content.VirusScanStatus,
                        report.Reference.Content.ModerationStatus);
                }

                assetDto = new AssetDto(
                    report.Reference.Id,
                    report.Reference.AssetContentId,
                    report.Reference.CreatedByUserId,
                    report.Reference.DisplayName,
                    report.Reference.AccessPolicy,
                    report.Reference.ParentResourceType,
                    report.Reference.ParentResourceId,
                    report.Reference.AccessCount,
                    report.Reference.LastAccessedAt,
                    report.Reference.CreatedAt,
                    report.Reference.UpdatedAt,
                    contentDto);
            }

            return new ReportDto(
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
                assetDto,
                ExtractLabels(report),
                ExtractModelScores(report),
                ExtractModel(report));
        }).ToList();

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
