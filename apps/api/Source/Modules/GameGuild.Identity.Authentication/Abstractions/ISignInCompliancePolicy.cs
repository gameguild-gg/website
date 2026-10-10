namespace GameGuild.Identity.Authentication;

/// <summary>
///     Context for a compliance evaluation performed during local sign-in, after
///     credential validation and before any token or session is issued.
/// </summary>
/// <param name="UserId">The authenticated user identifier.</param>
/// <param name="TenantId">The resolved tenant for the sign-in, when one is active.</param>
/// <param name="IpAddress">The client IP address observed for the sign-in attempt.</param>
/// <param name="DeviceFingerprint">The client device fingerprint observed for the sign-in attempt.</param>
public sealed record SignInComplianceContext(
    Guid UserId,
    Guid? TenantId,
    string? IpAddress,
    string? DeviceFingerprint);

/// <summary>
///     Evaluates whether an authenticated sign-in may proceed. Implementations map
///     platform-external compliance signals (verification status, restriction holds)
///     to an allow, challenge, or deny decision. The identity module itself only
///     consumes decisions; hosts provide the policy through an adapter so the module
///     stays free of any product-domain vocabulary.
/// </summary>
public interface ISignInCompliancePolicy
{
    /// <summary>
    ///     Evaluates the compliance decision for an authenticated sign-in attempt.
    ///     Implementations should throw when the underlying compliance signals cannot
    ///     be read; the caller decides whether to fail closed or open based on the
    ///     configured gate mode.
    /// </summary>
    ValueTask<SignInComplianceDecision> EvaluateAsync(
        SignInComplianceContext context,
        CancellationToken cancellationToken = default);
}
