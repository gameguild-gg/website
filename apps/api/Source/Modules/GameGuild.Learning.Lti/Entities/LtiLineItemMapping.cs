namespace GameGuild.Learning.Lti;

/// <summary>
/// Links a gameguild assessment to a platform AGS line item for score passback.
/// </summary>
public class LtiLineItemMapping : EntityBase
{
    public Guid AssessmentId { get; private set; }
    public Guid DeploymentId { get; private set; }
    public string LineItemId { get; private set; } = string.Empty;
    public string LineItemUrl { get; private set; } = string.Empty;

    /// <summary>Line item maximum score — the scoreMaximum sent to the platform.</summary>
    public int MaxScore { get; private set; }

    private LtiLineItemMapping() { } // EF Core

    public static LtiLineItemMapping Create(
        Guid assessmentId,
        Guid deploymentId,
        string lineItemId,
        string lineItemUrl,
        int maxScore)
    {
        if (string.IsNullOrWhiteSpace(lineItemId)) throw new ArgumentException("LineItemId is required.", nameof(lineItemId));
        if (string.IsNullOrWhiteSpace(lineItemUrl)) throw new ArgumentException("LineItemUrl is required.", nameof(lineItemUrl));
        if (maxScore <= 0) throw new ArgumentException("MaxScore must be positive.", nameof(maxScore));

        return new LtiLineItemMapping
        {
            Id = Guid.NewGuid(),
            AssessmentId = assessmentId,
            DeploymentId = deploymentId,
            LineItemId = lineItemId.Trim(),
            LineItemUrl = lineItemUrl.Trim(),
            MaxScore = maxScore
        };
    }
}
