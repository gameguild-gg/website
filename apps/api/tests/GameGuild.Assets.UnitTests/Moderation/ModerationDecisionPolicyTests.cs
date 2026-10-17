using GameGuild.Assets.Moderation;

namespace GameGuild.Assets.UnitTests.Moderation;

/// <summary>
/// Table tests for the pure threshold decision policy: score bands, classifier
/// failure, unknown label names, and mime-forced human review.
/// </summary>
public sealed class ModerationDecisionPolicyTests
{
    private static readonly AutoModerationOptions Options = new()
    {
        AutoApprovalThreshold = 0.80,
        AutoRejectionThreshold = 0.75
    };

    [Theory]
    [InlineData("adult_content", 0.90, ModerationStatus.Rejected)]
    [InlineData("hate_symbols", 0.75, ModerationStatus.Rejected)]
    [InlineData("self_harm", 1.00, ModerationStatus.Rejected)]
    [InlineData("violence", 0.90, ModerationStatus.NeedsReview)]   // violence not in hard-reject set
    [InlineData("drugs", 0.90, ModerationStatus.NeedsReview)]
    [InlineData("weapons", 0.90, ModerationStatus.NeedsReview)]
    [InlineData("suggestive", 0.90, ModerationStatus.NeedsReview)]
    [InlineData("safe", 0.99, ModerationStatus.NeedsReview)]       // safe >= 0.8 → review band (no special-case)
    public void Decide_HardRejectLabels_AboveThreshold(string label, double score, ModerationStatus expected)
    {
        var classification = new AssetModerationClassification(
            false, [new(label, score)], "qwen3.8-flash-next");

        var decision = ModerationDecisionPolicy.Decide(classification, Options, "image/png");

        decision.Status.Should().Be(expected);
        decision.Reason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Decide_AllScoresBelowApprovalThreshold_Approves()
    {
        var classification = new AssetModerationClassification(
            false,
            [new("adult_content", 0.02), new("violence", 0.10), new("safe", 0.70)],
            "qwen3.8-flash-next");

        var decision = ModerationDecisionPolicy.Decide(classification, Options, "image/png");

        decision.Status.Should().Be(ModerationStatus.Approved);
    }

    [Fact]
    public void Decide_MiddleScores_NeedReview()
    {
        // 0.5: below no auto-reject but >= auto-approval? No — 0.5 < 0.8 so all < 0.8 → Approved.
        // Wait: all scores < 0.80 → Approved. Middle band exists only when SOME score >= 0.8
        // on a non-hard-reject label.
        var classification = new AssetModerationClassification(
            false, [new("violence", 0.85)], "qwen3.8-flash-next");

        var decision = ModerationDecisionPolicy.Decide(classification, Options, "image/png");

        decision.Status.Should().Be(ModerationStatus.NeedsReview);
    }

    [Fact]
    public void Decide_ClassifierFailure_NeverAutoApproves()
    {
        var classification = new AssetModerationClassification(
            true, [new("safe", 0.99)], "qwen3.8-flash-next");

        var decision = ModerationDecisionPolicy.Decide(classification, Options, "image/png");

        decision.Status.Should().Be(ModerationStatus.NeedsReview);
    }

    [Fact]
    public void Decide_UnknownLabelName_NeedsReview()
    {
        var classification = new AssetModerationClassification(
            false, [new("totally_made_up", 0.02)], "qwen3.8-flash-next");

        var decision = ModerationDecisionPolicy.Decide(classification, Options, "image/png");

        decision.Status.Should().Be(ModerationStatus.NeedsReview);
    }

    [Fact]
    public void Decide_MimeRequiresHumanReview_NeedsReview()
    {
        var classification = new AssetModerationClassification(
            false, [new("safe", 0.99)], "qwen3.8-flash-next");

        var decision = ModerationDecisionPolicy.Decide(classification, Options, "video/mp4");

        decision.Status.Should().Be(ModerationStatus.NeedsReview);
        decision.Reason.Should().Contain("human review");
    }

    [Fact]
    public void Decide_Edge_ScoreExactlyAtRejectionThreshold_Rejects()
    {
        var classification = new AssetModerationClassification(
            false, [new("adult_content", 0.75)], "qwen3.8-flash-next");

        var decision = ModerationDecisionPolicy.Decide(classification, Options, "image/png");

        decision.Status.Should().Be(ModerationStatus.Rejected);
    }

    [Fact]
    public void Decide_Edge_ScoreExactlyAtApprovalThreshold_NeedsReview()
    {
        // 0.80 on non-hard-reject label: not all < 0.80 → NeedsReview band.
        var classification = new AssetModerationClassification(
            false, [new("violence", 0.80)], "qwen3.8-flash-next");

        var decision = ModerationDecisionPolicy.Decide(classification, Options, "image/png");

        decision.Status.Should().Be(ModerationStatus.NeedsReview);
    }

    [Fact]
    public void Decide_EmptyLabelList_NeedsReview()
    {
        var classification = new AssetModerationClassification(
            false, [], "qwen3.8-flash-next");

        var decision = ModerationDecisionPolicy.Decide(classification, Options, "image/png");

        decision.Status.Should().Be(ModerationStatus.NeedsReview);
    }
}
