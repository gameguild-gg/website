namespace GameGuild.Identity.Authentication;

/// <summary>Lifecycle states of a lost-MFA-device recovery request.</summary>
public enum MfaRecoveryRequestStatus
{
    Pending = 1,
    AwaitingConfirmation = 2,
    Completed = 3,
    Cancelled = 4,
    Expired = 5
}
