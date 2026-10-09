namespace GameGuild.Configuration.PresentationLayer.HealthChecks;

/// <summary>
///     Configuration options for application health checks.
/// </summary>
public sealed class HealthCheckOptions : BaseOptions
{
    /// <summary>
    ///     The configuration section name for this options type.
    /// </summary>
    public const string SectionName = "HealthChecks";

    public HealthCheckOptions()
    {
        Tags.Add("database", "infrastructure");
        Tags.Add("api", "readiness");
    }

    /// <summary>Whether to include a database connectivity health check.</summary>
    public bool EnableDatabaseCheck { get; set; } = true;

    /// <summary>Whether to include an API readiness health check.</summary>
    public bool EnableApiHealthCheck { get; set; } = true;

    /// <summary>Maximum time allowed for each health check to complete.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Tags used to categorize and filter health checks (e.g. "database", "readiness").</summary>
    public Dictionary<string, string> Tags { get; } = new();

    public override void Validate()
    {
        base.Validate();

        if (Timeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Health check timeout must be greater than zero");
        }
    }
}
