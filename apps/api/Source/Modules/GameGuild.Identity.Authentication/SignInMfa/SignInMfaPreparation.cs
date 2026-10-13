using GameGuild.Identity.Users;

namespace GameGuild.Identity.Authentication;

/// <summary>Server-owned decision before ordinary credential issuance; never a completed MFA proof.</summary>
public sealed class SignInMfaPreparation
{
    private SignInMfaPreparation(SignInResponse? outcome, User? subject = null, MfaRequirementDecision? decision = null)
    {
        Outcome = outcome;
        Subject = subject;
        Decision = decision;
    }

    public SignInResponse? Outcome { get; }

    public bool MayIssueOrdinaryCredentials => Outcome is null && Subject is not null && Decision is not null;

    internal User? Subject { get; }

    internal MfaRequirementDecision? Decision { get; }

    internal static SignInMfaPreparation PermitWithoutMfa(User subject, MfaRequirementDecision decision)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(decision);
        if (decision.SubjectId != subject.Id || decision.TenantId == Guid.Empty || decision.RequiresMfa)
        {
            throw new ArgumentException("Ordinary credential preparation requires a matching optional MFA policy.", nameof(decision));
        }
        return new SignInMfaPreparation(null, subject, decision);
    }

    internal static SignInMfaPreparation WithOutcome(SignInResponse outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        if (outcome.Success || !string.IsNullOrEmpty(outcome.AccessToken) || !string.IsNullOrEmpty(outcome.RefreshToken) || outcome.SessionId != Guid.Empty)
        {
            throw new ArgumentException("A preparation outcome cannot contain ordinary credentials.", nameof(outcome));
        }
        return new SignInMfaPreparation(outcome);
    }
}
