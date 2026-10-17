namespace GameGuild.Assets.Moderation;

/// <summary>
/// Pure threshold decision policy mapping a classification plus configuration
/// to a moderation status. Stateless and side-effect free: the worker owns all
/// persistence.
/// </summary>
public static class ModerationDecisionPolicy
{
    /// <summary>
    /// Labels whose scores trigger auto-rejection once they reach the
    /// configured rejection threshold. Labels outside the known taxonomy are
    /// treated as unknown and force human review.
    /// </summary>
    public static readonly IReadOnlySet<string> KnownLabels = new HashSet<string>(StringComparer.Ordinal)
    {
        "adult_content", "violence", "hate_symbols", "drugs", "weapons", "self_harm", "suggestive", "safe"
    };

    private static readonly IReadOnlySet<string> HardRejectLabels = new HashSet<string>(StringComparer.Ordinal)
    {
        "adult_content", "hate_symbols", "self_harm"
    };

    /// <summary>
    /// Decides the moderation outcome. Fail-closed: classifier failure, unknown
    /// label names, and human-review mime types always land in NeedsReview —
    /// never Approved.
    /// </summary>
    public static ModerationDecision Decide(
        AssetModerationClassification classification,
        AutoModerationOptions options,
        string mimeType)
    {
        ArgumentNullException.ThrowIfNull(classification);
        ArgumentNullException.ThrowIfNull(options);

        if (classification.Failed)
        {
            return new ModerationDecision(
                ModerationStatus.NeedsReview,
                "classifier_failure: classifier did not produce a usable result");
        }

        var normalizedMime = mimeType.Trim().ToLowerInvariant();
        if (options.RequireHumanReviewMimeTypes.Contains(normalizedMime, StringComparer.OrdinalIgnoreCase))
        {
            return new ModerationDecision(
                ModerationStatus.NeedsReview,
                $"human review required for mime type '{normalizedMime}'");
        }

        if (classification.Labels.Count == 0)
        {
            return new ModerationDecision(
                ModerationStatus.NeedsReview, "classifier returned no labels");
        }

        var unknown = classification.Labels
            .Where(label => !KnownLabels.Contains(label.Name))
            .Select(label => label.Name)
            .Distinct()
            .ToList();
        if (unknown.Count > 0)
        {
            return new ModerationDecision(
                ModerationStatus.NeedsReview,
                $"unknown label name(s): {string.Join(", ", unknown)}");
        }

        var hardReject = classification.Labels
            .FirstOrDefault(label => HardRejectLabels.Contains(label.Name) &&
                                     label.Score >= options.AutoRejectionThreshold);
        if (hardReject is not null)
        {
            return new ModerationDecision(
                ModerationStatus.Rejected,
                $"auto-rejected: {hardReject.Name} score {hardReject.Score:0.00} >= {options.AutoRejectionThreshold:0.00}");
        }

        if (classification.Labels.All(label => label.Score < options.AutoApprovalThreshold))
        {
            return new ModerationDecision(
                ModerationStatus.Approved,
                $"auto-approved: all scores < {options.AutoApprovalThreshold:0.00}");
        }

        return new ModerationDecision(
            ModerationStatus.NeedsReview,
            "scores in review band: max score >= auto-approval threshold without hard rejection");
    }
}

/// <summary>
/// Output of the decision policy: target status plus a human-readable reason
/// persisted into <see cref="AssetContent.ModerationReviewNotes"/>.
/// </summary>
public sealed record ModerationDecision(ModerationStatus Status, string Reason);
