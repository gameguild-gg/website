using System.Diagnostics;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

// ============================================================================
// ABAC / CONDITIONAL POLICY EVALUATION COMMAND HANDLERS (issue #374)
//
// These commands were previously Unavailable use-case contracts with throwing
// fallbacks. They are now backed by real handlers that delegate to the Authorization
// module's evaluators (IAbacPolicyEvaluator / IConditionalPolicyEvaluator) — the
// service-level evaluation engines that were already production code.
//
// All handlers are read-only (no business mutation): they evaluate policies against
// caller-supplied hypothetical attributes without writing anything. Guards: the actor
// must be authenticated, and tenant-scoped policy evaluation is pinned to the acting
// tenant unless the actor is a system administrator (fail closed).
// ============================================================================

/// <summary>
///     Concrete <see cref="AbacEvaluationResult"/> envelope for the evaluate endpoint.
/// </summary>
public sealed class EvaluateAbacPoliciesResult : AbacEvaluationResult
{
}

/// <summary>
///     Concrete <see cref="ConditionalPolicyResult"/> envelope for the evaluate endpoint.
/// </summary>
public sealed class EvaluateConditionalPoliciesResult : ConditionalPolicyResult
{
}

/// <summary>
///     Concrete bulk ABAC evaluation envelope.
/// </summary>
public sealed class BulkEvaluateAbacPoliciesResult : BulkAbacEvaluationResult
{
}

/// <summary>
///     Concrete bulk conditional-policy evaluation envelope.
/// </summary>
public sealed class BulkEvaluateConditionalPoliciesResult : BulkConditionalPolicyResult
{
}

/// <summary>
///     Concrete conditional-policy test result envelope.
/// </summary>
public sealed class TestConditionalPolicyRuleResult : ConditionalPolicyTestResult
{
}

/// <summary>
///     Concrete conditional-policy simulation result envelope.
/// </summary>
public sealed class SimulateConditionalPolicyResult : ConditionalPolicySimulationResult
{
}

/// <summary>
///     Handler for <see cref="EvaluateAbacPoliciesCommand"/> (issue #374): evaluates the
///     active ABAC policies against caller-supplied hypothetical attributes through the
///     production <see cref="IAbacPolicyEvaluator"/>. Read-only.
/// </summary>
public sealed class EvaluateAbacPoliciesCommandHandler(
    IAbacPolicyEvaluator abacPolicyEvaluator,
    IActorContextAccessor actorContextAccessor,
    ILogger<EvaluateAbacPoliciesCommandHandler> logger)
    : ICommandHandler<EvaluateAbacPoliciesCommand, AbacEvaluationResult>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<AbacEvaluationResult> Handle(EvaluateAbacPoliciesCommand request, CancellationToken cancellationToken)
    {
        EnsureAuthorized(request.TenantId, "evaluate ABAC policies");

        var context = AbacEvaluationContextMapper.ToRequestContext(request.Context);
        var evaluation = await abacPolicyEvaluator.EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        var result = AbacEvaluationResultMapper.ToApiResult(evaluation);

        logger.LogInformation(
            "ABAC policy evaluation completed with decision {Decision}",
            result.Decision);

        return result;
    }

    private void EnsureAuthorized(Guid? requestedTenantId, string operation)
    {
        if (!Actor.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("User is not authenticated");
        }

        if (Actor.IsSystemAdmin)
        {
            return;
        }

        // Fail closed: a non-system actor may only evaluate policies in their own tenant.
        var tenantId = requestedTenantId ?? Actor.TenantId;
        if (tenantId is null || Actor.TenantId != tenantId)
        {
            throw new UnauthorizedAccessException(
                $"Evaluating {operation} in another tenant requires system administration privileges");
        }
    }
}

/// <summary>
///     Handler for <see cref="BulkEvaluateAbacPoliciesCommand"/> (issue #374): evaluates
///     ABAC policies for a batch of contexts, aggregating per-context failures. Read-only.
/// </summary>
public sealed class BulkEvaluateAbacPoliciesCommandHandler(
    IAbacPolicyEvaluator abacPolicyEvaluator,
    IActorContextAccessor actorContextAccessor,
    ILogger<BulkEvaluateAbacPoliciesCommandHandler> logger)
    : ICommandHandler<BulkEvaluateAbacPoliciesCommand, BulkAbacEvaluationResult>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<BulkAbacEvaluationResult> Handle(BulkEvaluateAbacPoliciesCommand request, CancellationToken cancellationToken)
    {
        if (!Actor.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("User is not authenticated");
        }

        if (!Actor.IsSystemAdmin)
        {
            var tenantId = request.TenantId ?? Actor.TenantId;
            if (tenantId is null || Actor.TenantId != tenantId)
            {
                throw new UnauthorizedAccessException(
                    "Evaluating ABAC policies in another tenant requires system administration privileges");
            }
        }

        var result = new BulkEvaluateAbacPoliciesResult();
        var stopwatch = Stopwatch.StartNew();

        for (var index = 0; index < request.Contexts.Count; index++)
        {
            try
            {
                var context = AbacEvaluationContextMapper.ToRequestContext(request.Contexts[index]);
                _ = await abacPolicyEvaluator.EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
                result.SuccessfulEvaluations++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                result.FailedEvaluations++;
                result.Failures.Add(new BulkAbacEvaluationFailureEntry
                {
                    ContextIndex = index,
                    Error = exception.GetType().Name,
                    Details = exception.Message
                });
            }
        }

        stopwatch.Stop();
        result.TotalEvaluations = request.Contexts.Count;
        result.AverageEvaluationTime = request.Contexts.Count > 0
            ? stopwatch.Elapsed.TotalMilliseconds / request.Contexts.Count
            : 0;
        result.ProcessedAt = SystemClock.UtcNow;

        logger.LogInformation(
            "Bulk ABAC evaluation completed: {Success}/{Total} succeeded",
            result.SuccessfulEvaluations, result.TotalEvaluations);

        return result;
    }

    private sealed class BulkAbacEvaluationFailureEntry : AbacEvaluationFailure
    {
    }
}

/// <summary>
///     Handler for <see cref="EvaluateConditionalPoliciesCommand"/> (issue #374):
///     evaluates the active conditional policies for a hypothetical request context
///     through the production <see cref="IConditionalPolicyEvaluator"/>. Read-only.
/// </summary>
public sealed class EvaluateConditionalPoliciesCommandHandler(
    IConditionalPolicyEvaluator conditionalPolicyEvaluator,
    IActorContextAccessor actorContextAccessor,
    ILogger<EvaluateConditionalPoliciesCommandHandler> logger)
    : ICommandHandler<EvaluateConditionalPoliciesCommand, ConditionalPolicyResult>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<ConditionalPolicyResult> Handle(EvaluateConditionalPoliciesCommand request, CancellationToken cancellationToken)
    {
        if (!Actor.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("User is not authenticated");
        }

        if (!Actor.IsSystemAdmin && (request.TenantId == Guid.Empty || Actor.TenantId != request.TenantId))
        {
            throw new UnauthorizedAccessException(
                "Evaluating conditional policies in another tenant requires system administration privileges");
        }

        var context = ConditionalPolicyContextMapper.ToContext(
            request.UserId,
            request.TenantId,
            request.PermissionType,
            request.ResourceType,
            request.Context);

        var evaluation = await conditionalPolicyEvaluator.EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        var result = ConditionalPolicyResultMapper.ToApiResult(evaluation);

        logger.LogInformation(
            "Conditional policy evaluation completed: allowed={IsAllowed}",
            result.IsAllowed);

        return result;
    }
}

/// <summary>
///     Handler for <see cref="BulkEvaluateConditionalPoliciesCommand"/> (issue #374):
///     batch-evaluates conditional policies, aggregating per-request failures. Read-only.
/// </summary>
public sealed class BulkEvaluateConditionalPoliciesCommandHandler(
    IConditionalPolicyEvaluator conditionalPolicyEvaluator,
    IActorContextAccessor actorContextAccessor,
    ILogger<BulkEvaluateConditionalPoliciesCommandHandler> logger)
    : ICommandHandler<BulkEvaluateConditionalPoliciesCommand, BulkConditionalPolicyResult>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<BulkConditionalPolicyResult> Handle(BulkEvaluateConditionalPoliciesCommand request, CancellationToken cancellationToken)
    {
        if (!Actor.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("User is not authenticated");
        }

        var result = new BulkEvaluateConditionalPoliciesResult();
        var stopwatch = Stopwatch.StartNew();

        for (var index = 0; index < request.Requests.Count; index++)
        {
            var evaluationRequest = request.Requests[index];
            try
            {
                if (!Actor.IsSystemAdmin && (evaluationRequest.TenantId == Guid.Empty || Actor.TenantId != evaluationRequest.TenantId))
                {
                    throw new UnauthorizedAccessException(
                        "Evaluating conditional policies in another tenant requires system administration privileges");
                }

                var context = ConditionalPolicyContextMapper.ToContext(
                    evaluationRequest.UserId,
                    evaluationRequest.TenantId,
                    evaluationRequest.PermissionType,
                    evaluationRequest.ResourceType,
                    evaluationRequest.Context);
                _ = await conditionalPolicyEvaluator.EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
                result.SuccessfulEvaluations++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                result.FailedEvaluations++;
                result.Failures.Add(new BulkConditionalPolicyFailureEntry
                {
                    ContextIndex = index,
                    Error = exception.GetType().Name,
                    Details = exception.Message
                });
            }
        }

        stopwatch.Stop();
        result.TotalEvaluations = request.Requests.Count;
        result.AverageEvaluationTime = request.Requests.Count > 0
            ? stopwatch.Elapsed.TotalMilliseconds / request.Requests.Count
            : 0;
        result.ProcessedAt = SystemClock.UtcNow;

        logger.LogInformation(
            "Bulk conditional policy evaluation completed: {Success}/{Total} succeeded",
            result.SuccessfulEvaluations, result.TotalEvaluations);

        return result;
    }

    private sealed class BulkConditionalPolicyFailureEntry : ConditionalPolicyEvaluationFailure
    {
    }
}

/// <summary>
///     Handler for <see cref="TestConditionalPolicyRuleCommand"/> (issue #374): tests a
///     hypothetical conditional rule (condition payloads + test context) without saving
///     anything. The rule is evaluated by the production condition evaluator's in-memory
///     overload (<see cref="IConditionalPolicyEvaluator.EvaluatePolicyAsync"/>) — no
///     repository row is created.
/// </summary>
public sealed class TestConditionalPolicyRuleCommandHandler(
    IConditionalPolicyEvaluator conditionalPolicyEvaluator,
    IActorContextAccessor actorContextAccessor,
    ILogger<TestConditionalPolicyRuleCommandHandler> logger)
    : ICommandHandler<TestConditionalPolicyRuleCommand, ConditionalPolicyTestResult>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<ConditionalPolicyTestResult> Handle(TestConditionalPolicyRuleCommand request, CancellationToken cancellationToken)
    {
        if (!Actor.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("User is not authenticated");
        }

        var stopwatch = Stopwatch.StartNew();

        // In-memory synthetic policy: never persisted, never applied.
        var policy = ConditionalPolicyFictions.BuildTestRule(request);

        var validationErrors = PolicyEvaluationValidation.ValidateConditionPayloads(policy);

        var result = new TestConditionalPolicyRuleResult
        {
            IsValid = validationErrors.Count == 0,
            RuleResult = null,
            ValidationErrors = validationErrors,
            RuleContext = request.TestContext
        };

        if (validationErrors.Count > 0)
        {
            stopwatch.Stop();
            result.EvaluationTime = stopwatch.Elapsed.TotalMilliseconds;
            return result;
        }

        // Exercise the conditions through the production evaluator: a hypothetical
        // always-applicable action against the supplied test context.
        var evaluation = await conditionalPolicyEvaluator
            .EvaluatePolicyAsync(policy, ConditionalPolicyContextMapper.ToTestContext(request.TestContext), cancellationToken)
            .ConfigureAwait(false);
        // The synthetic rule is deny-shaped: allowed == false means the rule matched.
        result.RuleResult = !evaluation.IsAllowed;

        stopwatch.Stop();
        result.EvaluationTime = stopwatch.Elapsed.TotalMilliseconds;

        logger.LogInformation(
            "Conditional policy rule test completed: valid={IsValid}, ruleResult={RuleResult}",
            result.IsValid, result.RuleResult);

        return result;
    }
}

/// <summary>
///     Handler for <see cref="SimulateConditionalPolicyCommand"/> (issue #374): runs the
///     supplied test scenarios through the production condition evaluator and returns a
///     step-by-step explanation of the decision for each scenario. Nothing is saved and
///     no policy is applied.
/// </summary>
public sealed class SimulateConditionalPolicyCommandHandler(
    IConditionalPolicyEvaluator conditionalPolicyEvaluator,
    IActorContextAccessor actorContextAccessor,
    ILogger<SimulateConditionalPolicyCommandHandler> logger)
    : ICommandHandler<SimulateConditionalPolicyCommand, ConditionalPolicySimulationResult>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<ConditionalPolicySimulationResult> Handle(SimulateConditionalPolicyCommand request, CancellationToken cancellationToken)
    {
        if (!Actor.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("User is not authenticated");
        }

        var stopwatch = Stopwatch.StartNew();

        var policy = ConditionalPolicyFictions.BuildSimulatedPolicy(request);

        var result = new SimulateConditionalPolicyResult();
        var validationErrors = PolicyEvaluationValidation.ValidateConditionPayloads(policy);
        result.Warnings.AddRange(validationErrors);

        if (validationErrors.Count > 0)
        {
            stopwatch.Stop();
            result.Success = false;
            result.FinalDecision = "Indeterminate: invalid condition payloads";
            result.TotalEvaluationTime = stopwatch.Elapsed.TotalMilliseconds;
            return result;
        }

        var denied = false;

        var stepNumber = 1;
        foreach (var scenario in request.TestScenarios)
        {
            var evaluation = await conditionalPolicyEvaluator
                .EvaluatePolicyAsync(policy, ConditionalPolicyContextMapper.ToTestContext(scenario), cancellationToken)
                .ConfigureAwait(false);
            var scenarioDenied = !evaluation.IsAllowed;
            denied = denied || scenarioDenied;

            result.Steps.Add(new SimulationStepEntry
            {
                StepNumber = stepNumber++,
                Action = "evaluate-scenario",
                Result = !scenarioDenied,
                Description = scenarioDenied
                    ? "The hypothetical deny rule matched this scenario (access would be denied)"
                    : "The hypothetical deny rule did not match this scenario (access would not be denied by this rule)",
                Context = scenario
            });
        }

        result.Success = true;
        result.FinalDecision = denied
            ? "Deny: at least one scenario matched the hypothetical deny rule"
            : "Allow: no scenario matched the hypothetical deny rule";
        result.FinalContext = new Dictionary<string, object>
        {
            ["scenarios"] = request.TestScenarios.Count,
            ["conditionType"] = policy.ConditionType.ToString()
        };

        stopwatch.Stop();
        result.TotalEvaluationTime = stopwatch.Elapsed.TotalMilliseconds;

        logger.LogInformation(
            "Conditional policy simulation completed: {StepCount} steps, final decision {FinalDecision}",
            result.Steps.Count, result.FinalDecision);

        return result;
    }
}

/// <summary>
///     Handler for <see cref="ValidateConditionalPolicyCommand"/> (issue #374): structurally
///     validates the condition payloads of a hypothetical policy without saving anything.
/// </summary>
public sealed class ValidateConditionalPolicyCommandHandler(
    IActorContextAccessor actorContextAccessor,
    ILogger<ValidateConditionalPolicyCommandHandler> logger)
    : ICommandHandler<ValidateConditionalPolicyCommand, ConditionalPolicyValidationResult>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public Task<ConditionalPolicyValidationResult> Handle(ValidateConditionalPolicyCommand request, CancellationToken cancellationToken)
    {
        if (!Actor.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("User is not authenticated");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var policy = new ConditionalPolicy
        {
            Name = "validation",
            ConditionType = request.ConditionType,
            TimeConditions = request.TimeConditions,
            EnvironmentConditions = request.EnvironmentConditions,
            LocationConditions = request.LocationConditions,
            DeviceConditions = request.DeviceConditions,
            CustomConditions = request.CustomConditions
        };

        var errors = PolicyEvaluationValidation.ValidateConditionPayloads(policy);

        var result = new ValidateConditionalPolicyResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Suggestions = errors.Count == 0 ? [] : ["Fix the reported condition payloads and retry"]
        };

        logger.LogInformation("Conditional policy validation completed: valid={IsValid}", result.IsValid);

        return Task.FromResult<ConditionalPolicyValidationResult>(result);
    }
}

/// <summary>
///     Handler for <see cref="ValidateConditionCommand"/> (issue #374): validates one
///     condition payload document for a given condition type without saving anything.
/// </summary>
public sealed class ValidateConditionCommandHandler(
    IActorContextAccessor actorContextAccessor,
    ILogger<ValidateConditionCommandHandler> logger)
    : ICommandHandler<ValidateConditionCommand, ConditionValidationResult>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public Task<ConditionValidationResult> Handle(ValidateConditionCommand request, CancellationToken cancellationToken)
    {
        if (!Actor.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("User is not authenticated");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var errors = new List<string>();
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(request.ConditionJson);
            if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
            {
                errors.Add($"A {request.ConditionType} condition must be a JSON object");
            }
        }
        catch (System.Text.Json.JsonException exception)
        {
            errors.Add($"Malformed JSON ({exception.Message})");
        }

        var result = new ValidateConditionResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            SuggestedCorrection = errors.Count == 0
                ? null
                : "Provide a JSON object matching the condition schema for the requested condition type"
        };

        logger.LogInformation("Condition validation completed: valid={IsValid}", result.IsValid);

        return Task.FromResult<ConditionValidationResult>(result);
    }
}

/// <summary>Concrete policy-validation envelope.</summary>
public sealed class ValidateConditionalPolicyResult : ConditionalPolicyValidationResult
{
}

/// <summary>Concrete condition-validation envelope.</summary>
public sealed class ValidateConditionResult : ConditionValidationResult
{
}

/// <summary>
///     Maps the Authorization evaluator's conditional result record onto the API-layer
///     <see cref="ConditionalPolicyResult"/> envelope.
/// </summary>
internal static class ConditionalPolicyResultMapper
{
    public static ConditionalPolicyResult ToApiResult(
        GameGuild.Identity.Authorization.ConditionalPolicyResult evaluation) => new EvaluateConditionalPoliciesResult
    {
        IsAllowed = evaluation.IsAllowed,
        Action = evaluation.DeniedByPolicyId is null ? null : PolicyAction.Deny,
        Reason = evaluation.DenialReason,
        EnforcementMessage = evaluation.IsAllowed ? null : evaluation.DenialReason,
        MatchedPolicies = (evaluation.Details ?? [])
            .Select(detail => (ConditionalPolicyMatch)new ConditionalPolicyMatchEntry
            {
                PolicyId = detail.PolicyId,
                PolicyName = detail.PolicyName,
                Action = detail.Effect,
                ConditionsMet = detail.ConditionsMet,
                Reason = detail.ConditionsMet
                    ? "Policy conditions matched"
                    : "Policy conditions did not match"
            })
            .ToList(),
        AdditionalRequirements = []
    };

    private sealed class ConditionalPolicyMatchEntry : ConditionalPolicyMatch
    {
    }
}

/// <summary>
///     Maps the Authorization evaluator's result record onto the API-layer
///     <see cref="AbacEvaluationResult"/> envelope. An indeterminate engine outcome maps
///     to NotApplicable with the error surfaced in <see cref="AbacEvaluationResult.Reason"/>
///     (the API surface has no indeterminate state; it must never read as Allow).
/// </summary>
internal static class AbacEvaluationResultMapper
{
    public static AbacEvaluationResult ToApiResult(
        GameGuild.Identity.Authorization.AbacEvaluationResult evaluation) => new EvaluateAbacPoliciesResult
    {
        Decision = evaluation.Decision switch
        {
            GameGuild.Identity.Authorization.AbacDecision.Permit => AbacDecision.Allow,
            GameGuild.Identity.Authorization.AbacDecision.Deny => AbacDecision.Deny,
            _ => AbacDecision.NotApplicable
        },
        MatchedPolicies = (evaluation.Details ?? [])
            .Select(detail => (AbacPolicyMatch)new AbacPolicyMatchEntry
            {
                PolicyId = detail.PolicyId,
                PolicyName = detail.PolicyName,
                Decision = detail.Decision switch
                {
                    GameGuild.Identity.Authorization.AbacDecision.Permit => AbacDecision.Allow,
                    GameGuild.Identity.Authorization.AbacDecision.Deny => AbacDecision.Deny,
                    _ => AbacDecision.NotApplicable
                },
                MatchScore = detail.ConditionsMatched ? 1 : 0,
                Reason = detail.ConditionsMatched
                    ? "Policy conditions matched"
                    : "Policy conditions did not match"
            })
            .ToList(),
        Reason = evaluation.DenialReason
            ?? (evaluation.Decision == GameGuild.Identity.Authorization.AbacDecision.Indeterminate
                ? "Evaluation was indeterminate"
                : null),
        Obligations = [],
        EvaluatedAt = SystemClock.UtcNow
    };

    private sealed class AbacPolicyMatchEntry : AbacPolicyMatch
    {
    }
}

/// <summary>
///     Maps the API-layer <see cref="AbacEvaluationContext"/> onto the evaluator's
///     <see cref="GameGuild.Identity.Authorization.AbacRequestContext"/>, injecting the
///     identity attribute keys the evaluator matches on when the caller did not supply
///     them.
/// </summary>
internal static class AbacEvaluationContextMapper
{
    public static GameGuild.Identity.Authorization.AbacRequestContext ToRequestContext(AbacEvaluationContext evaluationContext)
    {
        var subject = new Dictionary<string, object>(evaluationContext.UserAttributes, StringComparer.OrdinalIgnoreCase);
        var resource = new Dictionary<string, object>(evaluationContext.ResourceAttributes, StringComparer.OrdinalIgnoreCase);
        var environment = new Dictionary<string, object>(evaluationContext.EnvironmentalAttributes, StringComparer.OrdinalIgnoreCase);
        var action = new Dictionary<string, object>(evaluationContext.ActionAttributes, StringComparer.OrdinalIgnoreCase);

        if (evaluationContext.UserId.HasValue)
        {
            subject.TryAdd("subject.user-id", evaluationContext.UserId.Value);
        }

        if (evaluationContext.TenantId.HasValue)
        {
            subject.TryAdd("subject.tenant-id", evaluationContext.TenantId.Value);
        }

        if (!string.IsNullOrEmpty(evaluationContext.ResourceType))
        {
            resource.TryAdd("resource.type", evaluationContext.ResourceType);
        }

        if (evaluationContext.ResourceId.HasValue)
        {
            resource.TryAdd("resource.id", evaluationContext.ResourceId.Value);
        }

        action.TryAdd("action.permission", evaluationContext.RequestedPermission.ToString());

        return new AbacRequestContext(subject, resource, action, environment);
    }
}

/// <summary>
///     Maps a dictionary-based evaluation payload onto the evaluator's
/// <see cref="ConditionalPolicyContext"/>.
/// </summary>
internal static class ConditionalPolicyContextMapper
{
    public static GameGuild.Identity.Authorization.ConditionalPolicyContext ToContext(
        Guid userId,
        Guid tenantId,
        PermissionType? permissionType,
        string? resourceType,
        IReadOnlyDictionary<string, object> context)
    {
        return new GameGuild.Identity.Authorization.ConditionalPolicyContext(
            UserId: userId,
            TenantId: tenantId,
            ResourceType: resourceType ?? "*",
            ResourceId: TryGetGuid(context, "resourceId"),
            Action: permissionType?.ToString() ?? TryGetString(context, "action") ?? "*",
            UserRoles: TryGetRoles(context),
            IpAddress: TryGetString(context, "ipAddress"),
            UserAgent: TryGetString(context, "userAgent"),
            DeviceFingerprint: TryGetString(context, "deviceFingerprint"),
            GeoCountry: TryGetString(context, "geoCountry"),
            GeoRegion: TryGetString(context, "geoRegion"),
            AuthenticationTime: TryGetDateTime(context, "authenticationTime"),
            IsMfaVerified: TryGetBool(context, "isMfaVerified"),
            RiskScore: TryGetInt(context, "riskScore"),
            CustomAttributes: null);
    }

    /// <summary>
    ///     Builds a hypothetical context for what-if rule testing/simulation from a raw
    ///     scenario dictionary. The user/tenant identifiers are synthetic: the condition
    ///     evaluators only read the attribute-style keys.
    /// </summary>
    public static GameGuild.Identity.Authorization.ConditionalPolicyContext ToTestContext(IReadOnlyDictionary<string, object> scenario)
        => ToContext(
            userId: TryGetGuid(scenario, "userId") ?? Guid.Empty,
            tenantId: TryGetGuid(scenario, "tenantId") ?? Guid.Empty,
            permissionType: null,
            resourceType: TryGetString(scenario, "resourceType") ?? "*",
            context: scenario);

    private static string? TryGetString(IReadOnlyDictionary<string, object> context, string key)
        => context.TryGetValue(key, out var value) ? value?.ToString() : null;

    private static Guid? TryGetGuid(IReadOnlyDictionary<string, object> context, string key)
        => Guid.TryParse(TryGetString(context, key), out var guid) ? guid : null;

    private static bool? TryGetBool(IReadOnlyDictionary<string, object> context, string key)
        => bool.TryParse(TryGetString(context, key), out var value) ? value : null;

    private static int? TryGetInt(IReadOnlyDictionary<string, object> context, string key)
        => int.TryParse(TryGetString(context, key), out var value) ? value : null;

    private static DateTime? TryGetDateTime(IReadOnlyDictionary<string, object> context, string key)
        => DateTime.TryParse(TryGetString(context, key), out var value) ? value : null;

    private static IReadOnlyList<string> TryGetRoles(IReadOnlyDictionary<string, object> context)
        => context.TryGetValue("roles", out var value) && value is IEnumerable<object> roles
            ? roles.Select(r => r?.ToString() ?? string.Empty).Where(r => r.Length > 0).ToList()
            : [];
}

/// <summary>
///     Structural validation of the JSON condition payloads on a hypothetical policy:
///     each non-empty payload must parse as well-formed JSON with the expected document
///     shape (an object) — fail closed on malformed input instead of silently passing.
/// </summary>
internal static class PolicyEvaluationValidation
{
    public static List<string> ValidateConditionPayloads(ConditionalPolicy policy)
    {
        var errors = new List<string>();
        Validate(policy.TimeConditions, "timeConditions", errors);
        Validate(policy.EnvironmentConditions, "environmentConditions", errors);
        Validate(policy.LocationConditions, "locationConditions", errors);
        Validate(policy.DeviceConditions, "deviceConditions", errors);
        Validate(policy.CustomConditions, "customConditions", errors);
        return errors;
    }

    private static void Validate(string? json, string field, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
            {
                errors.Add($"{field}: expected a JSON object");
            }
        }
        catch (System.Text.Json.JsonException exception)
        {
            errors.Add($"{field}: malformed JSON ({exception.Message})");
        }
    }
}

/// <summary>
///     Builds the in-memory (never persisted) <see cref="ConditionalPolicy"/> instances
///     used by the what-if test-rule and simulate handlers (issue #374). The synthetic
///     policies are deny-shaped: "the rule matched" surfaces as "not allowed".
/// </summary>
internal static class ConditionalPolicyFictions
{
    public static ConditionalPolicy BuildTestRule(TestConditionalPolicyRuleCommand request) => new()
    {
        Name = "test-rule",
        Description = "Hypothetical rule supplied by the test-rule request (never saved)",
        ConditionType = request.ConditionType,
        Action = PolicyAction.Deny,
        IsEnabled = true,
        Priority = 0,
        TimeConditions = request.TimeConditions,
        EnvironmentConditions = request.EnvironmentConditions,
        LocationConditions = request.LocationConditions,
        DeviceConditions = request.DeviceConditions,
        CustomConditions = request.CustomConditions
    };

    public static ConditionalPolicy BuildSimulatedPolicy(SimulateConditionalPolicyCommand request) => new()
    {
        Name = request.PolicyId?.ToString("D") ?? "simulated-policy",
        Description = "Hypothetical policy supplied by the simulate request (never saved)",
        ConditionType = request.ConditionType ?? PolicyConditionType.None,
        Action = PolicyAction.Deny,
        IsEnabled = true,
        Priority = 0,
        TimeConditions = request.TimeConditions,
        EnvironmentConditions = request.EnvironmentConditions,
        LocationConditions = request.LocationConditions,
        DeviceConditions = request.DeviceConditions,
        CustomConditions = request.CustomConditions
    };
}

/// <summary>Concrete simulation-step envelope.</summary>
public sealed class SimulationStepEntry : SimulationStep
{
}
