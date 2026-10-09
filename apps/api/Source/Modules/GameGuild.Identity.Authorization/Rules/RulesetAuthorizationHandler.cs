
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Single authorization handler that evaluates all rules in a policy ruleset.
///     This replaces the need for multiple individual handlers per policy.
/// </summary>
public sealed class RulesetAuthorizationHandler : AuthorizationHandler<RulesetRequirement>
{
    private readonly ILogger<RulesetAuthorizationHandler> _logger;
    private readonly IRuleEvaluatorRegistry _ruleRegistry;
    private readonly IScopedRuleEvaluatorFactory _scopedEvaluatorFactory;
    private readonly IRulesetProvider _rulesetProvider;

    public RulesetAuthorizationHandler(
        IRulesetProvider rulesetProvider,
        IRuleEvaluatorRegistry ruleRegistry,
        IScopedRuleEvaluatorFactory scopedEvaluatorFactory,
        ILogger<RulesetAuthorizationHandler> logger)
    {
        _rulesetProvider = rulesetProvider;
        _ruleRegistry = ruleRegistry;
        _scopedEvaluatorFactory = scopedEvaluatorFactory;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RulesetRequirement requirement)
    {
        var policyName = requirement.PolicyName;

        _logger.LogDebug("Evaluating policy ruleset: {PolicyName}", policyName);

        // Use pre-loaded ruleset if available (avoids double DB load)
        var ruleset = requirement.Ruleset
            ?? await _rulesetProvider.GetRulesetAsync(policyName, CancellationToken.None).ConfigureAwait(false);

        if (ruleset is null)
        {
            _logger.LogWarning("No ruleset found for policy: {PolicyName}", policyName);
            context.Fail(new AuthorizationFailureReason(this, $"Policy '{policyName}' not found"));
            return;
        }

        if (!ruleset.IsActive)
        {
            _logger.LogWarning("Ruleset for policy {PolicyName} is inactive", policyName);
            context.Fail(new AuthorizationFailureReason(this, $"Policy '{policyName}' is disabled"));
            return;
        }

        // Check authentication requirement
        if (ruleset.RequireAuthentication && !(context.User.Identity?.IsAuthenticated ?? false))
        {
            _logger.LogDebug("Policy {PolicyName} requires authentication but user is not authenticated",
                policyName);
            context.Fail(new AuthorizationFailureReason(this, "Authentication required"));
            return;
        }

        // Evaluate each rule in order
        var enabledRules = ruleset.Rules.Where(r => r.Enabled).ToList();

        if (enabledRules.Count == 0)
        {
            context.Fail(new AuthorizationFailureReason(this, $"Policy '{policyName}' has no enabled rules"));
            return;
        }

        foreach (var rule in enabledRules)
        {
            var result = await EvaluateRuleAsync(context, rule, policyName).ConfigureAwait(false);
            if (!result.IsSuccess || result.IsSkipped)
            {
                context.Fail(new AuthorizationFailureReason(
                    this, result.FailureReason ?? $"Rule '{rule.Type}' failed"));
                return;
            }
        }

        // All rules passed
        _logger.LogDebug("All rules passed for policy {PolicyName}", policyName);
        context.Succeed(requirement);
    }

    private async Task<RuleEvaluationResult> EvaluateRuleAsync(
        AuthorizationHandlerContext context,
        RuleDefinition rule,
        string policyName)
    {
        var validation = rule.Validate();
        if (!validation.IsValid)
        {
            var reason = $"Invalid rule configuration: {string.Join("; ", validation.Errors)}";
            _logger.LogError("{Reason} in policy {PolicyName}", reason, policyName);
            return RuleEvaluationResult.Fail(reason);
        }

        if (string.Equals(rule.Type, RuleTypes.AnyOf, StringComparison.OrdinalIgnoreCase))
        {
            var failureReasons = new List<string>();
            foreach (var childRule in rule.Rules!.Where(child => child.Enabled))
            {
                var childResult = await EvaluateRuleAsync(context, childRule, policyName).ConfigureAwait(false);
                if (childResult.IsSuccess && !childResult.IsSkipped)
                {
                    return RuleEvaluationResult.Success();
                }

                if (!string.IsNullOrWhiteSpace(childResult.FailureReason))
                {
                    failureReasons.Add(childResult.FailureReason);
                }
            }

            return RuleEvaluationResult.Fail(
                failureReasons.Count == 0
                    ? "No AnyOf child rule passed"
                    : $"No AnyOf child rule passed: {string.Join("; ", failureReasons)}");
        }

        var evaluator = ResolveEvaluator(rule.Type);
        if (evaluator is null)
        {
            _logger.LogError(
                "No evaluator found for rule type: {RuleType} in policy {PolicyName}",
                rule.Type, policyName);
            return RuleEvaluationResult.Fail($"Unknown rule type: {rule.Type}");
        }

        try
        {
            var result = await evaluator.EvaluateAsync(
                context,
                RuleParameters.FromDictionary(rule.Params)).ConfigureAwait(false);

            if (result.IsSkipped)
            {
                return RuleEvaluationResult.Fail(result.FailureReason ?? $"Rule '{rule.Type}' was skipped");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error evaluating rule {RuleType} for policy {PolicyName}",
                rule.Type, policyName);
            return RuleEvaluationResult.Fail($"Error evaluating rule '{rule.Type}'");
        }
    }

    private IRuleEvaluator? ResolveEvaluator(string ruleType)
    {
        // Try to get from registry first (stateless singleton evaluators)
        var evaluator = _ruleRegistry.GetEvaluator(ruleType);
        if (evaluator is not null)
        {
            return evaluator;
        }

        // Try to resolve from scoped factory (scoped evaluators with per-request dependencies)
        return _scopedEvaluatorFactory.GetEvaluator(ruleType);
    }
}
