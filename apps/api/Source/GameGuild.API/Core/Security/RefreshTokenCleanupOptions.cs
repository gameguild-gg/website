namespace GameGuild.API.Core.Security;

public sealed class RefreshTokenCleanupOptions
{
    public static string SectionName => "Authentication:RefreshTokenCleanup";
    public bool Enabled { get; set; } = true;
    public int RetentionDays { get; set; } = 30;
    public int BatchSize { get; set; } = 500;
    public int MaxBatchesPerCycle { get; set; } = 10;
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMinutes(2);
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);
    public TimeSpan ExecutionTimeout { get; set; } = TimeSpan.FromMinutes(1);

    public List<string> Validate()
    {
        var errors = new List<string>();
        if (RetentionDays is < 1 or > 3650) { errors.Add("RetentionDays must be between 1 and 3650."); }
        if (BatchSize is < 1 or > 1000) { errors.Add("BatchSize must be between 1 and 1000."); }
        if (MaxBatchesPerCycle is < 1 or > 100) { errors.Add("MaxBatchesPerCycle must be between 1 and 100."); }
        if (InitialDelay < TimeSpan.Zero || InitialDelay > TimeSpan.FromDays(1)) { errors.Add("InitialDelay must be between zero and one day."); }
        if (Interval < TimeSpan.FromSeconds(1) || Interval > TimeSpan.FromDays(1)) { errors.Add("Interval must be between one second and one day."); }
        if (ExecutionTimeout < TimeSpan.FromSeconds(1) || ExecutionTimeout > TimeSpan.FromMinutes(5)) { errors.Add("ExecutionTimeout must be between one second and five minutes."); }
        return errors;
    }
}
