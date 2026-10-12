using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Unified implementation of the policy gate service.
///     Evaluates all deny-focused gates (Conditional, ABAC, Environment).
///     Uses DENY-WINS precedence.
/// </summary>
public class PolicyGateService(
    IConditionalPolicyEvaluator conditionalEvaluator,
    IAbacPolicyEvaluator abacEvaluator,
    ILogger<PolicyGateService> logger
) : IPolicyGateService
{
    /// <summary>
    ///     Hard-coded gates that cannot be bypassed.
    /// </summary>
    private static readonly ImmutableArray<Func<PolicyGateContext, GateEvaluationDetail?>> StaticGates =
    [
        // Block all requests from localhost in production (example)
        context =>
        {
            var sw = Stopwatch.StartNew();
            if (context.IpAddress == "127.0.0.1" && 
                context.Attributes?.TryGetValue("environment", out var env) == true && 
                env?.ToString() == "production")
            {
                return new GateEvaluationDetail(
                    PolicyGateType.Static,
                    false,
                    "LocalhostProductionBlock",
                    "Localhost access blocked in production",
                    sw.Elapsed);
            }
            return null;
        },
        
        // Block requests with no user agent (bots, scripts)
        context =>
        {
            var sw = Stopwatch.StartNew();
            if (string.IsNullOrEmpty(context.UserAgent) && 
                context.Attributes?.TryGetValue("require-user-agent", out var required) == true && 
                required?.ToString() == "true")
            {
                return new GateEvaluationDetail(
                    PolicyGateType.Static,
                    false,
                    "UserAgentRequired",
                    "User-Agent header is required",
                    sw.Elapsed);
            }
            return null;
        }
    ];

    public async Task<PolicyGateResult> EvaluateGatesAsync(
        PolicyGateContext context,
        CancellationToken ct = default)
    {
        var details = new List<GateEvaluationDetail>();

        // 1. Evaluate static hard-coded gates first (highest priority)
        foreach (var gate in StaticGates)
        {
            var result = gate(context);
            if (result != null)
            {
                details.Add(result);
                if (!result.Passed)
                {
                    logger.LogWarning(
                        "Static gate {PolicyId} denied access for actor {ActorId}: {Reason}",
                        result.PolicyId, context.ActorId, result.Reason);

                    return PolicyGateResult.Denied(
                        PolicyGateType.Static,
                        result.Reason ?? "Static gate denied access",
                        result.PolicyId,
                        details);
                }
            }
        }

        // 2. Evaluate conditional policies (time windows, environment conditions)
        var conditionalSw = Stopwatch.StartNew();
        var conditionalContext = new ConditionalPolicyContext(
            UserId: context.ActorId,
            TenantId: context.TenantId,
            ResourceType: context.ResourceType,
            ResourceId: context.ResourceId,
            Action: context.Action,
            UserRoles: GetRolesFromAttributes(context.Attributes),
            IpAddress: context.IpAddress,
            UserAgent: context.UserAgent,
            DeviceFingerprint: context.DeviceFingerprint,
            GeoCountry: context.GeoLocation?.Split('/').FirstOrDefault(),
            GeoRegion: context.GeoLocation?.Contains('/') == true ? context.GeoLocation.Split('/').LastOrDefault() : null,
            AuthenticationTime: GetDateTimeFromAttributes(context.Attributes, "auth-time"),
            IsMfaVerified: GetBoolFromAttributes(context.Attributes, "mfa-verified"),
            RiskScore: GetIntFromAttributes(context.Attributes, "risk-score"),
            CustomAttributes: GetStringAttributesFromDict(context.Attributes));

        var conditionalResult = await conditionalEvaluator.EvaluateAsync(conditionalContext, ct).ConfigureAwait(false);
        conditionalSw.Stop();

        details.Add(new GateEvaluationDetail(
            PolicyGateType.Conditional,
            conditionalResult.IsAllowed,
            conditionalResult.DeniedByPolicyId?.ToString(),
            conditionalResult.DenialReason,
            conditionalSw.Elapsed));

        if (!conditionalResult.IsAllowed)
        {
            logger.LogWarning(
                "Conditional policy gate denied access for actor {ActorId}: {Reason}",
                context.ActorId, conditionalResult.DenialReason);

            return PolicyGateResult.Denied(
                PolicyGateType.Conditional,
                conditionalResult.DenialReason ?? "Conditional policy denied access",
                conditionalResult.DeniedByPolicyId?.ToString(),
                details);
        }

        // 3. Evaluate ABAC policies
        var abacSw = Stopwatch.StartNew();
        var abacContext = new AbacRequestContextBuilder()
            .WithSubject(
                context.ActorId,
                context.TenantId,
                GetRolesFromAttributes(context.Attributes))
            .WithResource(context.ResourceType, context.ResourceId)
            .WithAction(context.Action)
            .WithEnvironment(context.IpAddress, context.UserAgent, context.GeoLocation?.Split('/').FirstOrDefault())
            .Build();

        var abacResult = await abacEvaluator.EvaluateAsync(abacContext, ct).ConfigureAwait(false);
        abacSw.Stop();

        details.Add(new GateEvaluationDetail(
            PolicyGateType.Abac,
            abacResult.Decision != AbacDecision.Deny,
            abacResult.DecidingPolicyId?.ToString(),
            abacResult.DenialReason,
            abacSw.Elapsed));

        if (abacResult.Decision == AbacDecision.Deny)
        {
            logger.LogWarning(
                "ABAC gate denied access for actor {ActorId}: {Reason}",
                context.ActorId, abacResult.DenialReason);

            return PolicyGateResult.Denied(
                PolicyGateType.Abac,
                abacResult.DenialReason ?? "ABAC policy denied access",
                abacResult.DecidingPolicyId?.ToString(),
                details);
        }

        // All gates passed
        logger.LogDebug("All policy gates passed for actor {ActorId}", context.ActorId);

        return PolicyGateResult.Allowed(details);
    }

    public async Task<PolicyGateResult> EvaluateGateAsync(
        PolicyGateType gateType,
        PolicyGateContext context,
        CancellationToken ct = default)
    {
        return gateType switch
        {
            PolicyGateType.Static => EvaluateStaticGates(context),
            PolicyGateType.Conditional => await EvaluateConditionalGateAsync(context, ct),
            PolicyGateType.Abac => await EvaluateAbacGateAsync(context, ct),
            PolicyGateType.Environment => EvaluateEnvironmentGate(context),
            _ => PolicyGateResult.Allowed()
        };
    }

    private PolicyGateResult EvaluateStaticGates(PolicyGateContext context)
    {
        var details = new List<GateEvaluationDetail>();
        
        foreach (var gate in StaticGates)
        {
            var result = gate(context);
            if (result != null)
            {
                details.Add(result);
                if (!result.Passed)
                {
                    return PolicyGateResult.Denied(
                        PolicyGateType.Static,
                        result.Reason ?? "Static gate denied access",
                        result.PolicyId,
                        details);
                }
            }
        }

        return PolicyGateResult.Allowed(details);
    }

    private async Task<PolicyGateResult> EvaluateConditionalGateAsync(
        PolicyGateContext context,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var conditionalContext = new ConditionalPolicyContext(
            UserId: context.ActorId,
            TenantId: context.TenantId,
            ResourceType: context.ResourceType,
            ResourceId: context.ResourceId,
            Action: context.Action,
            UserRoles: []);

        var result = await conditionalEvaluator.EvaluateAsync(conditionalContext, ct).ConfigureAwait(false);
        sw.Stop();

        var detail = new GateEvaluationDetail(
            PolicyGateType.Conditional,
            result.IsAllowed,
            result.DeniedByPolicyId?.ToString(),
            result.DenialReason,
            sw.Elapsed);

        if (result.IsAllowed)
        {
            return PolicyGateResult.Allowed([detail]);
        }

        return PolicyGateResult.Denied(
            PolicyGateType.Conditional,
            result.DenialReason ?? "Conditional policy denied access",
            result.DeniedByPolicyId?.ToString(),
            [detail]);
    }

    private async Task<PolicyGateResult> EvaluateAbacGateAsync(
        PolicyGateContext context,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var abacContext = new AbacRequestContextBuilder()
            .WithSubject(context.ActorId, context.TenantId, [])
            .WithResource(context.ResourceType, context.ResourceId)
            .WithAction(context.Action)
            .Build();

        var result = await abacEvaluator.EvaluateAsync(abacContext, ct).ConfigureAwait(false);
        sw.Stop();

        var detail = new GateEvaluationDetail(
            PolicyGateType.Abac,
            result.Decision != AbacDecision.Deny,
            result.DecidingPolicyId?.ToString(),
            result.DenialReason,
            sw.Elapsed);

        if (result.Decision != AbacDecision.Deny)
        {
            return PolicyGateResult.Allowed([detail]);
        }

        return PolicyGateResult.Denied(
            PolicyGateType.Abac,
            result.DenialReason ?? "ABAC policy denied access",
            result.DecidingPolicyId?.ToString(),
            [detail]);
    }

    private PolicyGateResult EvaluateEnvironmentGate(PolicyGateContext context)
    {
        var sw = Stopwatch.StartNew();
        var details = new List<GateEvaluationDetail>();

        // Check for suspicious patterns
        if (!string.IsNullOrEmpty(context.UserAgent) && 
            (context.UserAgent.Contains("curl", StringComparison.OrdinalIgnoreCase) ||
             context.UserAgent.Contains("wget", StringComparison.OrdinalIgnoreCase)))
        {
            // Log but don't deny - could be legitimate API usage
            details.Add(new GateEvaluationDetail(
                PolicyGateType.Environment,
                true,
                "CommandLineToolDetected",
                "Command-line tool detected but allowed",
                sw.Elapsed));
        }

        return PolicyGateResult.Allowed(details);
    }

    #region Helper Methods

    private static IReadOnlyList<string> GetRolesFromAttributes(IReadOnlyDictionary<string, object>? attributes)
    {
        if (attributes == null)
        {
            return [];
        }

        if (attributes.TryGetValue("roles", out var roles))
        {
            if (roles is IReadOnlyList<string> roleList)
            {
                return roleList;
            }

            if (roles is IEnumerable<string> roleEnum)
            {
                return roleEnum.ToList();
            }
        }
        return [];
    }

    private static DateTime? GetDateTimeFromAttributes(IReadOnlyDictionary<string, object>? attributes, string key)
    {
        if (attributes?.TryGetValue(key, out var value) == true && value is DateTime dt)
        {
            return dt;
        }

        return null;
    }

    private static bool? GetBoolFromAttributes(IReadOnlyDictionary<string, object>? attributes, string key)
    {
        if (attributes?.TryGetValue(key, out var value) == true && value is bool b)
        {
            return b;
        }

        return null;
    }

    private static int? GetIntFromAttributes(IReadOnlyDictionary<string, object>? attributes, string key)
    {
        if (attributes?.TryGetValue(key, out var value) == true && value is int i)
        {
            return i;
        }

        return null;
    }

    private static Dictionary<string, string> GetStringAttributesFromDict(IReadOnlyDictionary<string, object>? attributes)
    {
        if (attributes == null)
        {
            return [];
        }

        return attributes
            .Where(kv => kv.Value is string)
            .ToDictionary(kv => kv.Key, kv => (string)kv.Value);
    }

    #endregion
}
