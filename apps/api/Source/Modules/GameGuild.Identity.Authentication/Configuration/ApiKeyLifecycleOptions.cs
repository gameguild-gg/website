namespace GameGuild.Identity.Authentication;

/// <summary>
///     Configuration options for the API-key lifecycle (rotation overlap windows).
/// </summary>
public sealed class ApiKeyLifecycleOptions
{
    public const string SectionName = "ApiKeys";

    /// <summary>
    ///     Default overlap window during which a rotated (old) key keeps working
    ///     while its replacement is distributed. Zero revokes immediately.
    /// </summary>
    public int RotationGracePeriodMinutes { get; set; } = 1440; // 24 hours

    /// <summary>
    ///     Upper bound for any rotation overlap window (30 days).
    /// </summary>
    public int MaxRotationGracePeriodMinutes { get; set; } = 43200;

    /// <summary>
    ///     Validates the configured values.
    /// </summary>
    public (bool IsValid, string[] Errors) Validate()
    {
        List<string> errors = [];

        if (RotationGracePeriodMinutes < 0)
        {
            errors.Add("RotationGracePeriodMinutes must be zero or positive");
        }

        if (MaxRotationGracePeriodMinutes is < 1 or > 43200)
        {
            errors.Add("MaxRotationGracePeriodMinutes must be between 1 and 43200 (30 days)");
        }

        if (RotationGracePeriodMinutes > MaxRotationGracePeriodMinutes)
        {
            errors.Add("RotationGracePeriodMinutes cannot exceed MaxRotationGracePeriodMinutes");
        }

        return (errors.Count == 0, [.. errors]);
    }
}
