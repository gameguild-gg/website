namespace GameGuild.Identity.Authentication;

/// <summary>
///     Internal denial created only after replay containment succeeds. It carries no
///     identity or tokens and allows the command transaction to retain its mutations.
/// </summary>
internal sealed class RefreshTokenContainmentDenial : SignInResponse, ICommitOnFailureOutcome
{
    public RefreshTokenContainmentDenial()
    {
        Success = false;
        Message = "Invalid refresh token";
    }
}
