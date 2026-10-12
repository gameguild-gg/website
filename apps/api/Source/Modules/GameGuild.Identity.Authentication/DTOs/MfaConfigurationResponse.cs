namespace GameGuild.Identity.Authentication;

/// <summary>
///     MFA configuration response
/// </summary>
public class MfaConfigurationResponse
{
    public bool IsEnabled { get; set; }

    public string[ ] EnabledMethods { get; set; } = [];

    public DateTime? EnabledAt { get; set; }

    public int BackupCodesRemaining { get; set; }

    /// <summary>Original issued count; null for legacy sets whose issuance metadata is unavailable.</summary>
    public int? BackupCodesIssued { get; set; }
}
