using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Evaluates conditional access policies.
/// </summary>
public interface IConditionalPolicyEvaluator
{
    /// <summary>
    ///     Evaluates all applicable conditional policies for a request.
    /// </summary>
    Task<ConditionalPolicyResult> EvaluateAsync(
        ConditionalPolicyContext context,
        CancellationToken ct = default);
}

/// <summary>
///     Context for conditional policy evaluation.
/// </summary>
public record ConditionalPolicyContext(
    Guid UserId,
    Guid? TenantId,
    string ResourceType,
    Guid? ResourceId,
    string Action,
    IReadOnlyList<string> UserRoles,
    string? IpAddress = null,
    string? UserAgent = null,
    string? DeviceFingerprint = null,
    string? GeoCountry = null,
    string? GeoRegion = null,
    DateTime? AuthenticationTime = null,
    bool? IsMfaVerified = null,
    int? RiskScore = null,
    IReadOnlyDictionary<string, string>? CustomAttributes = null);

/// <summary>
///     Result of conditional policy evaluation.
/// </summary>
public sealed record ConditionalPolicyResult(
    bool IsAllowed,
    Guid? DeniedByPolicyId = null,
    string? DeniedByPolicyName = null,
    string? DenialReason = null,
    IReadOnlyList<PolicyEvaluationDetail>? Details = null);

/// <summary>
///     Detail of individual policy evaluation.
/// </summary>
public record PolicyEvaluationDetail(
    Guid PolicyId,
    string PolicyName,
    PolicyAction Effect,
    bool ConditionsMet);

/// <summary>
///     Implementation of conditional policy evaluator using existing ConditionalPolicy entity.
/// </summary>
public class ConditionalPolicyEvaluator(
    IConditionalPolicyRepository repository,
    ILogger<ConditionalPolicyEvaluator> logger
) : IConditionalPolicyEvaluator
{
    public async Task<ConditionalPolicyResult> EvaluateAsync(
        ConditionalPolicyContext context,
        CancellationToken ct = default)
    {
        var policies = await repository.GetActivePoliciesAsync(context.TenantId, ct).ConfigureAwait(false);
        var details = new List<PolicyEvaluationDetail>();
        
        // Filter and sort policies by priority (descending)
        var applicablePolicies = policies
            .Where(p => p.IsActive())
            .Where(p => IsApplicable(p, context))
            .OrderByDescending(p => p.Priority);

        foreach (var policy in applicablePolicies)
        {
            var conditionsMet = EvaluateConditions(policy, context);

            details.Add(new PolicyEvaluationDetail(
                policy.Id,
                policy.Name,
                policy.Action,
                conditionsMet));

            // If conditions are met, apply the policy action
            if (conditionsMet)
            {
                if (policy.Action == PolicyAction.Deny)
                {
                    logger.LogWarning(
                        "Conditional policy {PolicyName} denied access for user {UserId} on {ResourceType}/{Action}",
                        policy.Name, context.UserId, context.ResourceType, context.Action);

                    return new ConditionalPolicyResult(
                        IsAllowed: false,
                        DeniedByPolicyId: policy.Id,
                        DeniedByPolicyName: policy.Name,
                        DenialReason: $"Access denied by policy: {policy.Name}",
                        Details: details);
                }

                // Allow policy matched - continue evaluating (Deny takes precedence)
                logger.LogDebug(
                    "Conditional policy {PolicyName} allows access for user {UserId}",
                    policy.Name, context.UserId);
            }
        }

        // No deny policy matched
        return new ConditionalPolicyResult(IsAllowed: true, Details: details);
    }

    private static bool IsApplicable(ConditionalPolicy policy, ConditionalPolicyContext context)
    {
        // Check permission type filter
        if (!policy.AppliesTo(context.Action))
        {
            return false;
        }

        // Check resource type filter
        if (!policy.AppliesToResourceType(context.ResourceType))
        {
            return false;
        }

        return true;
    }

    private bool EvaluateConditions(ConditionalPolicy policy, ConditionalPolicyContext context)
    {
        // Evaluate time conditions
        if (!string.IsNullOrEmpty(policy.TimeConditions))
        {
            if (!EvaluateTimeConditions(policy.TimeConditions))
            {
                return false;
            }
        }

        // Evaluate environment conditions
        if (!string.IsNullOrEmpty(policy.EnvironmentConditions))
        {
            if (!EvaluateEnvironmentConditions(policy.EnvironmentConditions, context))
            {
                return false;
            }
        }

        // Evaluate location conditions
        if (!string.IsNullOrEmpty(policy.LocationConditions))
        {
            if (!EvaluateLocationConditions(policy.LocationConditions, context))
            {
                return false;
            }
        }

        // Evaluate device conditions
        if (!string.IsNullOrEmpty(policy.DeviceConditions))
        {
            if (!EvaluateDeviceConditions(policy.DeviceConditions, context))
            {
                return false;
            }
        }

        // Evaluate custom conditions
        if (!string.IsNullOrEmpty(policy.CustomConditions))
        {
            if (!EvaluateCustomConditions(policy.CustomConditions, context))
            {
                return false;
            }
        }

        return true;
    }

    private bool EvaluateTimeConditions(string timeConditionsJson)
    {
        try
        {
            var conditions = JsonSerializer.Deserialize<TimeConditions>(timeConditionsJson);
            if (conditions == null)
            {
                return true;
            }

            var now = SystemClock.UtcNow;

            // Check day of week
            if (conditions.DaysOfWeek?.Length > 0)
            {
                if (!conditions.DaysOfWeek.Contains(now.DayOfWeek))
                {
                    return false;
                }
            }

            // Check time range
            if (!string.IsNullOrEmpty(conditions.StartTime) && !string.IsNullOrEmpty(conditions.EndTime))
            {
                if (TimeOnly.TryParse(conditions.StartTime, out var start) &&
                    TimeOnly.TryParse(conditions.EndTime, out var end))
                {
                    var currentTime = TimeOnly.FromDateTime(now);
                    if (start <= end)
                    {
                        if (currentTime < start || currentTime > end)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        // Overnight window
                        if (currentTime < start && currentTime > end)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse time conditions: {TimeConditionsJson}", timeConditionsJson);
            return true; // If parsing fails, don't block
        }
    }

    private bool EvaluateEnvironmentConditions(string conditionsJson, ConditionalPolicyContext context)
    {
        try
        {
            var conditions = JsonSerializer.Deserialize<EnvironmentConditions>(conditionsJson);
            if (conditions == null)
            {
                return true;
            }

            // Check MFA requirement
            if (conditions.RequireMfa == true && context.IsMfaVerified != true)
            {
                return false;
            }

            // Check risk score
            if (conditions.MaxRiskScore.HasValue && context.RiskScore > conditions.MaxRiskScore.Value)
            {
                return false;
            }

            // Check session age
            if (conditions.MaxSessionAgeMinutes.HasValue && context.AuthenticationTime.HasValue)
            {
                var sessionAge = SystemClock.UtcNow - context.AuthenticationTime.Value;
                if (sessionAge.TotalMinutes > conditions.MaxSessionAgeMinutes.Value)
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse environment conditions: {ConditionsJson}", conditionsJson);
            return true;
        }
    }

    private bool EvaluateLocationConditions(string conditionsJson, ConditionalPolicyContext context)
    {
        try
        {
            var conditions = JsonSerializer.Deserialize<LocationConditions>(conditionsJson);
            if (conditions == null)
            {
                return true;
            }

            // Check allowed countries
            if (conditions.AllowedCountries?.Length > 0)
            {
                if (string.IsNullOrEmpty(context.GeoCountry) ||
                    !conditions.AllowedCountries.Contains(context.GeoCountry, StringComparer.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            // Check blocked countries
            if (conditions.BlockedCountries?.Length > 0)
            {
                if (!string.IsNullOrEmpty(context.GeoCountry) &&
                    conditions.BlockedCountries.Contains(context.GeoCountry, StringComparer.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            // Check IP ranges
            if (conditions.AllowedIpRanges?.Length > 0 && !string.IsNullOrEmpty(context.IpAddress))
            {
                var ipAllowed = false;
                foreach (var range in conditions.AllowedIpRanges)
                {
                    if (IsIpInRange(context.IpAddress, range))
                    {
                        ipAllowed = true;
                        break;
                    }
                }
                if (!ipAllowed)
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse location conditions: {ConditionsJson}", conditionsJson);
            return true;
        }
    }

    private bool IsIpInRange(string ipAddress, string range)
    {
        try
        {
            if (range.Contains('/'))
            {
                // CIDR notation
                var parts = range.Split('/');
                var networkAddress = IPAddress.Parse(parts[0]);
                var prefixLength = int.Parse(parts[1]);
                var ip = IPAddress.Parse(ipAddress);

                var networkBytes = networkAddress.GetAddressBytes();
                var ipBytes = ip.GetAddressBytes();

                if (networkBytes.Length != ipBytes.Length)
                {
                    return false;
                }

                var fullBytes = prefixLength / 8;
                var remainingBits = prefixLength % 8;

                for (var i = 0; i < fullBytes; i++)
                {
                    if (networkBytes[i] != ipBytes[i])
                    {
                        return false;
                    }
                }

                if (remainingBits > 0 && fullBytes < networkBytes.Length)
                {
                    var mask = (byte)(0xFF << (8 - remainingBits));
                    if ((networkBytes[fullBytes] & mask) != (ipBytes[fullBytes] & mask))
                    {
                        return false;
                    }
                }

                return true;
            }

            return ipAddress.Equals(range, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse IP range - IP: {IpAddress}, Range: {Range}", ipAddress, range);
            return false;
        }
    }

    private bool EvaluateDeviceConditions(string conditionsJson, ConditionalPolicyContext context)
    {
        try
        {
            var conditions = JsonSerializer.Deserialize<DeviceConditions>(conditionsJson);
            if (conditions == null)
            {
                return true;
            }

            // Check required device fingerprints
            if (conditions.AllowedFingerprints?.Length > 0)
            {
                if (string.IsNullOrEmpty(context.DeviceFingerprint) ||
                    !conditions.AllowedFingerprints.Contains(context.DeviceFingerprint))
                {
                    return false;
                }
            }

            // Check blocked user agents
            if (conditions.BlockedUserAgents?.Length > 0 && !string.IsNullOrEmpty(context.UserAgent))
            {
                foreach (var pattern in conditions.BlockedUserAgents)
                {
                    if (context.UserAgent.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse device conditions: {ConditionsJson}", conditionsJson);
            return true;
        }
    }

    private bool EvaluateCustomConditions(string conditionsJson, ConditionalPolicyContext context)
    {
        try
        {
            var conditions = JsonSerializer.Deserialize<Dictionary<string, string>>(conditionsJson);
            if (conditions == null || context.CustomAttributes == null)
            {
                return true;
            }

            foreach (var (key, expectedValue) in conditions)
            {
                if (!context.CustomAttributes.TryGetValue(key, out var actualValue))
                {
                    return false;
                }

                if (!actualValue.Equals(expectedValue, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse custom conditions: {ConditionsJson}", conditionsJson);
            return true;
        }
    }

    // Helper classes for JSON deserialization
    private record TimeConditions
    {
        public DayOfWeek[]? DaysOfWeek { get; init; }
        public string? StartTime { get; init; }
        public string? EndTime { get; init; }
        public string? TimeZone { get; init; }
    }

    private record EnvironmentConditions
    {
        public bool? RequireMfa { get; init; }
        public int? MaxRiskScore { get; init; }
        public int? MaxSessionAgeMinutes { get; init; }
    }

    private record LocationConditions
    {
        public string[]? AllowedCountries { get; init; }
        public string[]? BlockedCountries { get; init; }
        public string[]? AllowedIpRanges { get; init; }
    }

    private record DeviceConditions
    {
        public string[]? AllowedFingerprints { get; init; }
        public string[]? BlockedUserAgents { get; init; }
    }
}
