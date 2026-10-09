using GameGuild.Configuration;

namespace GameGuild.Resources;

/// <summary>
///     Configuration options for the Resources module
/// </summary>
public class ResourcesOptions : ModuleOptions
{
    /// <summary>
    ///     Maximum file size in bytes for resource uploads
    /// </summary>
    public long MaxFileSize { get; set; } = 10 * 1024 * 1024; // 10MB default

    /// <summary>
    ///     Allowed file extensions for uploads
    /// </summary>
    public string[ ] AllowedFileExtensions { get; set; } = [".jpg", ".png", ".gif", ".pdf", ".docx"];

    /// <summary>
    ///     Base path for storing resource files
    /// </summary>
    public string BasePath { get; set; } = "uploads";

    /// <summary>
    ///     Whether to enable content scanning for uploaded files
    /// </summary>
    public bool EnableContentScanning { get; set; } = true;

    /// <summary>
    ///     Cost per unit for different resource types (used in CostAllocationService)
    /// </summary>
    public Dictionary<string, decimal> CostPerUnit { get; set; } = new()
    {
        { "Users", 5.00m },
        { "Projects", 10.00m },
        { "Teams", 5.00m },
        { "Storage", 0.05m },
        { "ApiCalls", 0.001m }
    };

    /// <summary>
    ///     Default cost per unit when resource type is not configured
    /// </summary>
    public decimal DefaultCostPerUnit { get; set; } = 0.01m;

    public override void Validate()
    {
        base.Validate();

        if (MaxFileSize <= 0)
        {
            throw new InvalidOperationException("MaxFileSize must be greater than 0");
        }

        if (string.IsNullOrWhiteSpace(BasePath))
        {
            throw new InvalidOperationException("BasePath cannot be empty");
        }

        if (AllowedFileExtensions.Length == 0)
        {
            throw new InvalidOperationException("At least one file extension must be allowed");
        }

        if (CostPerUnit == null || CostPerUnit.Count == 0)
        {
            throw new InvalidOperationException("CostPerUnit configuration cannot be empty");
        }

        if (DefaultCostPerUnit < 0)
        {
            throw new InvalidOperationException("DefaultCostPerUnit must be non-negative");
        }
    }
}
