namespace GameGuild.Identity.Authentication;

/// <summary>
///     The default sign-in compliance policy: every authenticated sign-in is allowed.
///     Registered by the identity module so hosts that do not provide a compliance
///     adapter keep working unchanged. The safe rollout default keeps the gate itself
///     disabled through <see cref="SignInComplianceGateOptions" />.
/// </summary>
public sealed class AllowAllSignInCompliancePolicy : ISignInCompliancePolicy
{
    /// <summary>The stateless shared instance.</summary>
    public static AllowAllSignInCompliancePolicy Instance { get; } = new();

    private AllowAllSignInCompliancePolicy()
    {
    }

    public ValueTask<SignInComplianceDecision> EvaluateAsync(
        SignInComplianceContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ValueTask.FromResult(SignInComplianceDecision.Allow);
    }
}
