namespace GameGuild.Identity.Authentication;

/// <summary>Enrollment-specific TOTP watermark. Contains neither the secret nor the supplied code.</summary>
public sealed class TotpReplayState
{
    public Guid ConfigurationId { get; set; }
    public string SecretFingerprint { get; set; } = string.Empty;
    public long LastAcceptedStep { get; set; }
    public DateTimeOffset LastAcceptedAt { get; set; }
}
