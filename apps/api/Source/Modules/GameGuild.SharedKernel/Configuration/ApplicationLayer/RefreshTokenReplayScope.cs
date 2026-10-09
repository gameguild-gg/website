namespace GameGuild.Configuration.ApplicationLayer;

/// <summary>Server-selected containment boundary after refresh credential reuse.</summary>
public enum RefreshTokenReplayScope
{
    /// <summary>Revoke the proven session family; legacy unknown families use account containment.</summary>
    Family,

    /// <summary>Revoke every session and refresh credential owned by the affected account.</summary>
    Account
}
