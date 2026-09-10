namespace GameGuild.Learning.Lti;

/// <summary>
/// Maps a platform subject (`sub` claim) to a gameguild user for one deployment.
/// </summary>
public class LtiUserMapping : EntityBase
{
    public Guid DeploymentId { get; private set; }
    public Guid UserId { get; private set; }
    public string Sub { get; private set; } = string.Empty;

    private LtiUserMapping() { } // EF Core

    public static LtiUserMapping Create(Guid deploymentId, Guid userId, string sub)
    {
        if (string.IsNullOrWhiteSpace(sub)) throw new ArgumentException("Sub is required.", nameof(sub));

        return new LtiUserMapping
        {
            Id = Guid.NewGuid(),
            DeploymentId = deploymentId,
            UserId = userId,
            Sub = sub.Trim()
        };
    }
}
