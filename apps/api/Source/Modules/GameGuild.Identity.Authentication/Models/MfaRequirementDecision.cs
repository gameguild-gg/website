namespace GameGuild.Identity.Authentication;

/// <summary>
/// Server-owned policy evidence. It is not a credential, challenge or proof of completed MFA.
/// The completion flow must re-evaluate it alongside account, tenant and token-version state.
/// </summary>
public sealed record MfaRequirementDecision(
    Guid SubjectId,
    Guid TenantId,
    bool RequiresMfa,
    IReadOnlyList<string> Roles,
    string PolicyFingerprint);
