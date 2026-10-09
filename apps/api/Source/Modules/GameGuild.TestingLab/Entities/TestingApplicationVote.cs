using GameGuild.Identity.Users;

namespace GameGuild.TestingLab;

[Table("testing_application_votes")]
public sealed class TestingApplicationVote : EntityBase
{
    public Guid ApplicationId { get; private set; }

    public TestingProjectApplication Application { get; private set; } = null!;

    public Guid ReviewerId { get; private set; }

    public User Reviewer { get; private set; } = null!;

    public TestingApplicationVoteDecision Decision { get; private set; }

    [MaxLength(2000)]
    public string? Comments { get; private set; }

    private TestingApplicationVote()
    {
    }

    public static TestingApplicationVote Cast(
        Guid applicationId,
        Guid reviewerId,
        TestingApplicationVoteDecision decision,
        string? comments,
        Guid? tenantId)
    {
        if (applicationId == Guid.Empty || reviewerId == Guid.Empty)
        {
            throw new ArgumentException("Application and reviewer are required.");
        }

        return new TestingApplicationVote
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            ReviewerId = reviewerId,
            Decision = decision,
            Comments = string.IsNullOrWhiteSpace(comments) ? null : comments.Trim(),
            TenantId = tenantId,
        };
    }
}
