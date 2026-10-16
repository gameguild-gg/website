using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Folds active ABAC policies (including their <see cref="AbacPolicy.AttributeExpression"/>
///     and <see cref="AbacPolicy.ConditionExpression"/> payloads, issue #293) into effective
///     permission resolution as a registered <see cref="IPermissionEvaluationExtension"/>.
/// </summary>
/// <remarks>
///     <para>
///         This is the active production consumer of <see cref="IAbacPolicyEvaluator"/> on the
///         canonical resolution path: every effective-permission computation (the resolver
///         behind <c>IPermissionQueryService</c>, the authorization entry points and the
///         hybrid check cache's misses) now consults the tenant's active ABAC policies.
///     </para>
///     <para>
///         <b>Semantics (fail-closed, DENY-WINS):</b> only matching <b>deny</b> policies
///         contribute. A matching deny policy subtracts its declared target actions from the
///         accumulated allow set; a deny policy with no target-action constraint is a
///         context-wide deny and subtracts everything the other layers allowed for this
///         resolution. <b>Allow</b>-effect ABAC policies never mint permissions — ABAC
///         permits are request gates, not grant sources, so an ABAC allow cannot widen the
///         effective set (deny-by-default, #327).
///     </para>
///     <para>
///         Attribute availability at resolution time is narrower than at request-gate time:
///         subject identity (user/tenant), resource type/id and environment time are always
///         supplied; policies conditioned on attributes only present per-request (IP, device,
///         roles) do not match here and remain enforced by the request-time gates. An
///         evaluator failure is logged and contributes nothing <b>permissive</b> — but the
///         extension contract already skips throwing plugins fail-closed.
///     </para>
/// </remarks>
public sealed class AbacPolicyEvaluationExtension(
    IAbacPolicyEvaluator abacPolicyEvaluator,
    ILogger<AbacPolicyEvaluationExtension> logger
) : IPermissionEvaluationExtension
{
    public string Name => "AbacPolicyOverlay";

    /// <summary>Runs after other extensions so ABAC denies veto every contributing layer.</summary>
    public int Order => 500;

    public async Task<PermissionEvaluationExtensionResult> EvaluateAsync(
        PermissionEvaluationExtensionContext context,
        CancellationToken cancellationToken = default)
    {
        var builder = new AbacRequestContextBuilder()
            .WithSubject(context.UserId, context.TenantId, roles: [])
            .WithEnvironment();
        if (!string.IsNullOrWhiteSpace(context.ResourceType))
        {
            builder.WithResource(
                context.ResourceType,
                Guid.TryParse(context.ResourceId, out var resourceId) ? resourceId : null);
        }

        var abacContext = builder.Build();

        AbacEvaluationResult evaluation;
        try
        {
            evaluation = await abacPolicyEvaluator.EvaluateAsync(abacContext, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // The extension contract skips throwing plugins, but never rely on that:
            // log and contribute nothing rather than widening or breaking the decision.
            logger.LogError(
                exception,
                "ABAC policy overlay evaluation failed in tenant {TenantId}; no ABAC contributions are applied.",
                context.TenantId);
            return PermissionEvaluationExtensionResult.None;
        }

        if (evaluation.Decision != AbacDecision.Deny)
        {
            return PermissionEvaluationExtensionResult.None;
        }

        var decidingDetail = evaluation.Details?.FirstOrDefault(
            detail => detail.PolicyId == evaluation.DecidingPolicyId);

        // Targeted deny: subtract the policy's declared actions. Untargeted deny: subtract
        // everything this resolution had allowed (context-wide deny, fail-closed).
        var denies = decidingDetail?.TargetActions is { Count: > 0 } targetActions
            ? targetActions.SelectMany(ExpandAction).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            : context.CurrentAllows.ToArray();

        logger.LogWarning(
            "ABAC deny policy {PolicyName} ({PolicyId}) subtracted {DenyCount} permission(s) from the effective set of user {UserId} in tenant {TenantId}.",
            decidingDetail?.PolicyName ?? evaluation.DecidingPolicyName,
            evaluation.DecidingPolicyId,
            denies.Length,
            context.UserId,
            context.TenantId);

        return new PermissionEvaluationExtensionResult(
            AdditionalAllows: Array.Empty<string>(),
            AdditionalDenies: denies);

        // "*" is the policy catalogue's wildcard for "every action".
        IEnumerable<string> ExpandAction(string action) =>
            action.Trim() == "*" ? context.CurrentAllows : [action.Trim()];
    }
}
