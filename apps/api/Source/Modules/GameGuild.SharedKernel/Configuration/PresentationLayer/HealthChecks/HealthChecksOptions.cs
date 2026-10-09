namespace GameGuild.Configuration.PresentationLayer.HealthChecks;

public sealed class HealthChecksOptions : BaseOptions
{
    public string[ ] Endpoints { get; set; } = ["/health"];

    public bool EnableLiveness { get; set; } = true;

    public bool EnableReadiness { get; set; } = true;

    public string HealthCheckPath { get; set; } = "/health";

    public int TimeoutSeconds { get; set; } = 30;

    public override void Validate()
    {
        base.Validate();

        if (Endpoints == null || Endpoints.Length == 0)
        {
            throw new InvalidOperationException("At least one health check endpoint must be configured.");
        }
    }

    public static HealthChecksOptions CreateDefault() { return new HealthChecksOptions(); }
}
