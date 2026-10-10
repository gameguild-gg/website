using System.ComponentModel.DataAnnotations;

namespace GameGuild.Configuration.ApplicationLayer;

/// <summary>
///     Configuration options for User Enumeration Protection
/// </summary>
public sealed class UserEnumerationProtectionOptions : BaseOptions
{
    public const string SectionName = "UserEnumerationProtection";

    /// <summary>
    ///     Whether user enumeration protection is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Minimum processing time in milliseconds for authentication attempts
    /// </summary>
    [Range(50, 2000)]
    public int MinProcessingTimeMs { get; set; } = 200;

    /// <summary>
    ///     Maximum processing time in milliseconds for authentication attempts
    /// </summary>
    [Range(100, 5000)]
    public int MaxProcessingTimeMs { get; set; } = 800;

    /// <summary>
    ///     Target processing time in milliseconds (average)
    /// </summary>
    [Range(100, 3000)]
    public int TargetProcessingTimeMs { get; set; } = 400;

    /// <summary>
    ///     Consistent error message for failed authentication
    /// </summary>
    [Required]
    public string ConsistentErrorMessage { get; set; } = "Invalid credentials. Please check your email and password.";

    /// <summary>
    ///     Whether to add random jitter to response times
    /// </summary>
    public bool EnableRandomJitter { get; set; } = true;

    /// <summary>
    ///     Maximum jitter in milliseconds
    /// </summary>
    [Range(0, 500)]
    public int MaxJitterMs { get; set; } = 100;

    public bool IsValid { get => Validate().IsValid; }

    public new (bool IsValid, string[ ] Errors) Validate()
    {
        var errors = new List<string>();

        if (MinProcessingTimeMs < 50 || MinProcessingTimeMs > 2000)
        {
            errors.Add("MinProcessingTimeMs must be between 50 and 2000");
        }

        if (MaxProcessingTimeMs < 100 || MaxProcessingTimeMs > 5000)
        {
            errors.Add("MaxProcessingTimeMs must be between 100 and 5000");
        }

        if (TargetProcessingTimeMs < 100 || TargetProcessingTimeMs > 3000)
        {
            errors.Add("TargetProcessingTimeMs must be between 100 and 3000");
        }

        if (MinProcessingTimeMs > TargetProcessingTimeMs)
        {
            errors.Add("MinProcessingTimeMs cannot be greater than TargetProcessingTimeMs");
        }

        if (TargetProcessingTimeMs > MaxProcessingTimeMs)
        {
            errors.Add("TargetProcessingTimeMs cannot be greater than MaxProcessingTimeMs");
        }

        if (string.IsNullOrWhiteSpace(ConsistentErrorMessage))
        {
            errors.Add("ConsistentErrorMessage is required");
        }

        if (EnableRandomJitter && (MaxJitterMs < 0 || MaxJitterMs > 500))
        {
            errors.Add("MaxJitterMs must be between 0 and 500");
        }

        return (errors.Count == 0, errors.ToArray());
    }
}
