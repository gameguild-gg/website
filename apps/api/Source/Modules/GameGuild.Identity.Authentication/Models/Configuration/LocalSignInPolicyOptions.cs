namespace GameGuild.Identity.Authentication;

/// <summary>Server-owned local password sign-in policy. Existing hosts remain compatible until enabled.</summary>
public sealed class LocalSignInPolicyOptions
{
    public const string SectionName = "Authentication:LocalSignIn";

    /// <summary>Require verified email for local password sign-in in every tenant.</summary>
    public bool RequireEmailVerification { get; set; }

    /// <summary>Additional tenants requiring verified email; cannot weaken the global requirement.</summary>
    public List<Guid> RequireEmailVerificationForTenants { get; set; } = [];

    public bool IsValid() => RequireEmailVerificationForTenants is not null
        && RequireEmailVerificationForTenants.All(static tenantId => tenantId != Guid.Empty);

    /// <summary>Evaluate only after the active tenant has been resolved on the server.</summary>
    public bool RequiresVerifiedEmail(Guid? resolvedTenantId)
    {
        if (resolvedTenantId is not { } tenantId || tenantId == Guid.Empty)
        {
            throw new ArgumentException("An active tenant is required for local sign-in policy.", nameof(resolvedTenantId));
        }

        if (!IsValid())
        {
            throw new InvalidOperationException("Invalid local sign-in email-verification policy.");
        }

        return RequireEmailVerification || RequireEmailVerificationForTenants.Contains(tenantId);
    }
}
