using GameGuild.Compliance.KYC;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Identity.Authentication;

namespace GameGuild.API.Core.Compliance;

/// <summary>
///     Host-side sign-in compliance policy (issue #267). Composes the shared KYC module
///     with the economy compliance hold store and maps those signals onto the platform's
///     allow/challenge/deny contract, keeping <c>GameGuild.Identity.Authentication</c>
///     free of product-domain vocabulary.
/// </summary>
/// <remarks>
///     <para>Decision mapping (enforce semantics):</para>
///     <list type="bullet">
///         <item>Active compliance hold for the resolved tenant and user, or a verification whose latest outcome is Rejected or Suspended, deny.</item>
///         <item>Pending/InProgress/Expired/missing verification, or an approved verification whose validity window lapsed, challenge.</item>
///         <item>Otherwise, allow.</item>
///     </list>
///     <para>
///         Evaluation errors (unreadable signals) throw; <c>LocalAuthService</c> decides
///         whether to fail closed based on the configured gate mode.
///     </para>
/// </remarks>
public sealed class KycAndHoldSignInCompliancePolicy(
    IKycService kycService,
    IComplianceHoldStore complianceHoldStore,
    TimeProvider timeProvider) : ISignInCompliancePolicy
{
    public async ValueTask<SignInComplianceDecision> EvaluateAsync(
        SignInComplianceContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Tenant-scoped restriction holds take precedence over verification status. Holds
        // are keyed by the opaque economy subject reference, never by the raw user ID.
        if (context.TenantId is Guid tenantId)
        {
            var scope = new ComplianceHoldScope(
                tenantId,
                EconomySubjectReference.ForUser(tenantId, context.UserId),
                Capability: null);
            var holdActive = await complianceHoldStore
                .IsActiveAsync(scope, timeProvider.GetUtcNow(), cancellationToken)
                .ConfigureAwait(false);
            if (holdActive)
            {
                return SignInComplianceDecision.Deny(SignInComplianceReasons.ComplianceHold);
            }
        }

        var latestResult = await kycService
            .GetLatestVerificationAsync(context.UserId, cancellationToken)
            .ConfigureAwait(false);
        if (latestResult.IsFailure)
        {
            throw new InvalidOperationException(
                $"The verification status for user {context.UserId} could not be read ({latestResult.Error.Code}).");
        }

        var verification = latestResult.Value;
        if (verification is null)
        {
            return SignInComplianceDecision.Challenge(SignInComplianceReasons.VerificationMissing);
        }

        return verification.Status switch
        {
            KycVerificationStatus.Rejected => SignInComplianceDecision.Deny(SignInComplianceReasons.VerificationRejected),
            KycVerificationStatus.Suspended => SignInComplianceDecision.Deny(SignInComplianceReasons.VerificationSuspended),
            KycVerificationStatus.Approved => await ConfirmApprovedVerificationAsync(context.UserId, cancellationToken).ConfigureAwait(false),
            KycVerificationStatus.Expired => SignInComplianceDecision.Challenge(SignInComplianceReasons.VerificationExpired),
            // Pending, InProgress and any unknown future status are undecided signals:
            // challenge rather than silently allowing or hard-denying an unrecognised value.
            _ => SignInComplianceDecision.Challenge(SignInComplianceReasons.VerificationPending),
        };
    }

    private async Task<SignInComplianceDecision> ConfirmApprovedVerificationAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        // The repository-level check also applies verification expiry: an approved
        // verification whose validity window lapsed no longer counts as verified.
        var verifiedResult = await kycService
            .IsUserVerifiedAsync(userId, cancellationToken)
            .ConfigureAwait(false);
        if (verifiedResult.IsFailure)
        {
            throw new InvalidOperationException(
                $"The verification state for user {userId} could not be read ({verifiedResult.Error.Code}).");
        }

        return verifiedResult.Value
            ? SignInComplianceDecision.Allow
            : SignInComplianceDecision.Challenge(SignInComplianceReasons.VerificationExpired);
    }
}
