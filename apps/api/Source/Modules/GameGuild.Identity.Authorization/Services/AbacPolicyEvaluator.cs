using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Evaluates ABAC policies.
/// </summary>
public interface IAbacPolicyEvaluator
{
    /// <summary>
    ///     Evaluates all applicable ABAC policies for a request.
    /// </summary>
    Task<AbacEvaluationResult> EvaluateAsync(
        AbacRequestContext context,
        CancellationToken ct = default);
}

/// <summary>
///     Request context for ABAC evaluation with all attributes.
/// </summary>
public record AbacRequestContext(
    IReadOnlyDictionary<string, object> SubjectAttributes,
    IReadOnlyDictionary<string, object> ResourceAttributes,
    IReadOnlyDictionary<string, object> ActionAttributes,
    IReadOnlyDictionary<string, object> EnvironmentAttributes);

/// <summary>
///     Result of ABAC policy evaluation.
/// </summary>
public sealed record AbacEvaluationResult(
    AbacDecision Decision,
    Guid? DecidingPolicyId = null,
    string? DecidingPolicyName = null,
    string? DenialReason = null,
    IReadOnlyList<AbacPolicyEvaluationDetail>? Details = null);

/// <summary>
///     ABAC decision outcome.
/// </summary>
public enum AbacDecision
{
    /// <summary>Access is permitted.</summary>
    Permit,
    
    /// <summary>Access is denied.</summary>
    Deny,
    
    /// <summary>No applicable policy found.</summary>
    NotApplicable,
    
    /// <summary>Error during evaluation.</summary>
    Indeterminate
}

/// <summary>
///     Detail of individual policy evaluation.
/// </summary>
public record AbacPolicyEvaluationDetail(
    Guid PolicyId,
    string PolicyName,
    bool ConditionsMatched,
    AbacDecision Decision);

/// <summary>
///     Implementation of ABAC policy evaluator using existing AbacPolicy entity.
/// </summary>
public class AbacPolicyEvaluator(
    IAbacPolicyRepository repository,
    ILogger<AbacPolicyEvaluator> logger
) : IAbacPolicyEvaluator
{
    public async Task<AbacEvaluationResult> EvaluateAsync(
        AbacRequestContext context,
        CancellationToken ct = default)
    {
        // Get tenant from context
        Guid? tenantId = null;
        if (context.SubjectAttributes.TryGetValue("subject.tenant-id", out var tid) && tid is Guid t)
        {
            tenantId = t;
        }

        var policies = await repository.GetActivePoliciesAsync(tenantId, ct).ConfigureAwait(false);
        var details = new List<AbacPolicyEvaluationDetail>();

        foreach (var policy in policies.OrderByDescending(p => p.Priority))
        {
            if (!policy.IsEffective())
            {
                continue;
            }

            // Check if policy target matches
            var matches = EvaluatePolicy(policy, context);

            var decision = matches
                ? (policy.Effect == AbacPolicyEffect.Deny ? AbacDecision.Deny : AbacDecision.Permit)
                : AbacDecision.NotApplicable;

            details.Add(new AbacPolicyEvaluationDetail(
                policy.Id, policy.Name, matches, decision));

            if (matches && policy.Effect == AbacPolicyEffect.Deny)
            {
                logger.LogWarning(
                    "ABAC policy {PolicyName} denied access",
                    policy.Name);

                return new AbacEvaluationResult(
                    AbacDecision.Deny,
                    policy.Id,
                    policy.Name,
                    policy.Description ?? $"Access denied by ABAC policy: {policy.Name}",
                    details);
            }
        }

        // Check if any permit was found
        var hasPermit = details.Any(d => d.Decision == AbacDecision.Permit);
        
        return new AbacEvaluationResult(
            hasPermit ? AbacDecision.Permit : AbacDecision.NotApplicable,
            Details: details);
    }

    private bool EvaluatePolicy(AbacPolicy policy, AbacRequestContext context)
    {
        // Check resource type filter
        if (!string.IsNullOrEmpty(policy.ResourceType))
        {
            if (!context.ResourceAttributes.TryGetValue("resource.type", out var resType))
            {
                return false;
            }

            if (resType?.ToString() != policy.ResourceType)
            {
                return false;
            }
        }

        // Evaluate subject conditions
        if (!string.IsNullOrEmpty(policy.SubjectConditions))
        {
            if (!EvaluateJsonConditions(policy.SubjectConditions, context.SubjectAttributes))
            {
                return false;
            }
        }

        // Evaluate resource conditions
        if (!string.IsNullOrEmpty(policy.ResourceConditions))
        {
            if (!EvaluateJsonConditions(policy.ResourceConditions, context.ResourceAttributes))
            {
                return false;
            }
        }

        // Evaluate environment conditions
        if (!string.IsNullOrEmpty(policy.EnvironmentConditions))
        {
            if (!EvaluateJsonConditions(policy.EnvironmentConditions, context.EnvironmentAttributes))
            {
                return false;
            }
        }

        // Evaluate action conditions
        if (!string.IsNullOrEmpty(policy.ActionConditions))
        {
            if (!EvaluateJsonConditions(policy.ActionConditions, context.ActionAttributes))
            {
                return false;
            }
        }

        return true;
    }

    private bool EvaluateJsonConditions(string jsonConditions, IReadOnlyDictionary<string, object> attributes)
    {
        try
        {
            var conditions = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonConditions);
            if (conditions == null)
            {
                return true;
            }

            foreach (var (key, expectedValue) in conditions)
            {
                if (!attributes.TryGetValue(key, out var actualValue))
                {
                    return false;
                }

                if (!CompareValues(actualValue, expectedValue))
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse ABAC JSON conditions: {Conditions}", jsonConditions);
            return false;
        }
    }

    private static bool CompareValues(object actual, JsonElement expected)
    {
        switch (expected.ValueKind)
        {
            case JsonValueKind.String:
            {
                var actualString = actual?.ToString();
                var expectedString = expected.GetString();
                return actualString is not null
                       && expectedString is not null
                       && actualString.Equals(expectedString, StringComparison.OrdinalIgnoreCase);
            }
            case JsonValueKind.Number when expected.TryGetInt32(out var expectedInt):
                return int.TryParse(actual?.ToString(), out var actualInt) && actualInt == expectedInt;
            case JsonValueKind.True:
                return actual is bool trueValue && trueValue;
            case JsonValueKind.False:
                return actual is bool falseValue && !falseValue;
            case JsonValueKind.Array:
            {
                if (actual is not IEnumerable<string> actualValues)
                    {
                        return false;
                    }

                    return expected
                    .EnumerateArray()
                    .Any(e => actualValues.Contains(e.GetString() ?? string.Empty, StringComparer.OrdinalIgnoreCase));
            }
            default:
                return false;
        }
    }
}

/// <summary>
///     Builder for ABAC request context.
/// </summary>
public class AbacRequestContextBuilder
{
    private readonly Dictionary<string, object> _subjectAttributes = new();
    private readonly Dictionary<string, object> _resourceAttributes = new();
    private readonly Dictionary<string, object> _actionAttributes = new();
    private readonly Dictionary<string, object> _environmentAttributes = new();

    public AbacRequestContextBuilder WithSubject(Guid userId, Guid? tenantId, IEnumerable<string> roles)
    {
        _subjectAttributes["subject.user-id"] = userId;
        if (tenantId.HasValue)
        {
            _subjectAttributes["subject.tenant-id"] = tenantId.Value;
        }

        _subjectAttributes["subject.roles"] = roles.ToList();
        return this;
    }

    public AbacRequestContextBuilder WithSubjectAttribute(string key, object value)
    {
        _subjectAttributes[key] = value;
        return this;
    }

    public AbacRequestContextBuilder WithResource(string type, Guid? id, Guid? ownerId = null)
    {
        _resourceAttributes["resource.type"] = type;
        if (id.HasValue)
        {
            _resourceAttributes["resource.id"] = id.Value;
        }

        if (ownerId.HasValue)
        {
            _resourceAttributes["resource.owner-id"] = ownerId.Value;
        }

        return this;
    }

    public AbacRequestContextBuilder WithResourceAttribute(string key, object value)
    {
        _resourceAttributes[key] = value;
        return this;
    }

    public AbacRequestContextBuilder WithAction(string actionId)
    {
        _actionAttributes["action.id"] = actionId;
        return this;
    }

    public AbacRequestContextBuilder WithActionAttribute(string key, object value)
    {
        _actionAttributes[key] = value;
        return this;
    }

    public AbacRequestContextBuilder WithEnvironment(
        string? ipAddress = null,
        string? userAgent = null,
        string? geoCountry = null)
    {
        _environmentAttributes["environment.current-time"] = SystemClock.UtcNow;
        _environmentAttributes["environment.current-date"] = DateOnly.FromDateTime(SystemClock.UtcNow);
        
        if (!string.IsNullOrEmpty(ipAddress))
        {
            _environmentAttributes["environment.ip-address"] = ipAddress;
        }

        if (!string.IsNullOrEmpty(userAgent))
        {
            _environmentAttributes["environment.user-agent"] = userAgent;
        }

        if (!string.IsNullOrEmpty(geoCountry))
        {
            _environmentAttributes["environment.geo-country"] = geoCountry;
        }

        return this;
    }

    public AbacRequestContextBuilder WithEnvironmentAttribute(string key, object value)
    {
        _environmentAttributes[key] = value;
        return this;
    }

    public AbacRequestContext Build()
        => new(_subjectAttributes, _resourceAttributes, _actionAttributes, _environmentAttributes);
}
