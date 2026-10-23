namespace GameGuild.Identity.Authentication;

/// <summary>Lifecycle of a delegated trusted contact (issue #209).</summary>
public enum TrustedContactStatus
{
    /// <summary>The owner invited the contact; the invite token has not been redeemed yet.</summary>
    Invited = 0,

    /// <summary>The contact redeemed their invite token and can receive recovery codes.</summary>
    Confirmed = 1,

    /// <summary>The owner (or a completed recovery) revoked the contact; terminal.</summary>
    Revoked = 2
}
