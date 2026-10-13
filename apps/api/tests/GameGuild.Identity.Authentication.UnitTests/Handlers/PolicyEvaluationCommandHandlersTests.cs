using FluentAssertions;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Handlers;

/// <summary>
///     Tests for the ABAC/conditional policy evaluation command handlers (issue #374):
///     the previously-Unavailable evaluate / bulk-evaluate / test-rule / simulate /
///     validate commands now run through the production evaluators, are tenant-guarded
///     and perform no business mutation.
/// </summary>
public class PolicyEvaluationCommandHandlersTests
{
    private readonly Mock<IAbacPolicyEvaluator> _abacEvaluator = new();
    private readonly Mock<IConditionalPolicyEvaluator> _conditionalEvaluator = new();
    private readonly Mock<IActorContextAccessor> _actorAccessor = new();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();

    public PolicyEvaluationCommandHandlersTests()
    {
        _abacEvaluator
            .Setup(e => e.EvaluateAsync(It.IsAny<AbacRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameGuild.Identity.Authorization.AbacEvaluationResult(
                GameGuild.Identity.Authorization.AbacDecision.Permit,
                Details: []));

        _conditionalEvaluator
            .Setup(e => e.EvaluateAsync(It.IsAny<GameGuild.Identity.Authorization.ConditionalPolicyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameGuild.Identity.Authorization.ConditionalPolicyResult(IsAllowed: true));

        // The in-memory overload used by test-rule/simulate: deny only when conditions
        // actually match the supplied context.
        _conditionalEvaluator
            .Setup(e => e.EvaluatePolicyAsync(It.IsAny<ConditionalPolicy>(), It.IsAny<GameGuild.Identity.Authorization.ConditionalPolicyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConditionalPolicy policy, GameGuild.Identity.Authorization.ConditionalPolicyContext context, CancellationToken _) =>
            {
                // Environment conditions: RequireMfa && !IsMfaVerified => matched deny.
                var matched = policy.EnvironmentConditions is { Length: > 0 }
                              && policy.EnvironmentConditions.Contains("RequireMfa")
                              && context.IsMfaVerified != true;
                return matched
                    ? new GameGuild.Identity.Authorization.ConditionalPolicyResult(
                        IsAllowed: false,
                        DeniedByPolicyId: policy.Id,
                        DeniedByPolicyName: policy.Name)
                    : new GameGuild.Identity.Authorization.ConditionalPolicyResult(IsAllowed: true);
            });
    }

    private void ActAs(Guid? userId, Guid? tenantId, params string[] roles)
    {
        _actorAccessor.Setup(a => a.ActorContext).Returns(new ActorContext
        {
            ActorKind = userId is null ? ActorKind.Anonymous : ActorKind.User,
            SubjectId = userId?.ToString(),
            TenantId = tenantId,
            Roles = new HashSet<string>(roles, StringComparer.OrdinalIgnoreCase),
            Permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            IsAuthenticated = userId is not null
        });
    }

    private EvaluateAbacPoliciesCommandHandler AbacSut() => new(
        _abacEvaluator.Object, _actorAccessor.Object, NullLogger<EvaluateAbacPoliciesCommandHandler>.Instance);

    private BulkEvaluateAbacPoliciesCommandHandler BulkAbacSut() => new(
        _abacEvaluator.Object, _actorAccessor.Object, NullLogger<BulkEvaluateAbacPoliciesCommandHandler>.Instance);

    private EvaluateConditionalPoliciesCommandHandler ConditionalSut() => new(
        _conditionalEvaluator.Object, _actorAccessor.Object, NullLogger<EvaluateConditionalPoliciesCommandHandler>.Instance);

    private TestConditionalPolicyRuleCommandHandler TestRuleSut() => new(
        _conditionalEvaluator.Object, _actorAccessor.Object, NullLogger<TestConditionalPolicyRuleCommandHandler>.Instance);

    private SimulateConditionalPolicyCommandHandler SimulateSut() => new(
        _conditionalEvaluator.Object, _actorAccessor.Object, NullLogger<SimulateConditionalPolicyCommandHandler>.Instance);

    [Fact]
    public async Task EvaluateAbac_AnonymousActor_IsRejected()
    {
        ActAs(null, null);

        var act = () => AbacSut().Handle(new EvaluateAbacPoliciesCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _abacEvaluator.Verify(e => e.EvaluateAsync(It.IsAny<AbacRequestContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateAbac_CrossTenantNonSystemAdmin_IsRejected()
    {
        ActAs(_actorId, _tenantId);

        var act = () => AbacSut().Handle(new EvaluateAbacPoliciesCommand { TenantId = Guid.NewGuid() }, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task EvaluateAbac_SameTenantUser_MapsResultThroughProductionEvaluator()
    {
        ActAs(_actorId, _tenantId);

        var result = await AbacSut().Handle(new EvaluateAbacPoliciesCommand
        {
            TenantId = _tenantId,
            Context = new AbacEvaluationContext()
        }, CancellationToken.None);

        result.Decision.Should().Be(AbacDecision.Allow);
        result.IsAllowed.Should().BeTrue();
        _abacEvaluator.Verify(e => e.EvaluateAsync(
            It.IsAny<AbacRequestContext>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BulkEvaluateAbac_AggregatesSuccessAndFailures()
    {
        ActAs(_actorId, _tenantId);
        _abacEvaluator
            .SetupSequence(e => e.EvaluateAsync(It.IsAny<AbacRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameGuild.Identity.Authorization.AbacEvaluationResult(GameGuild.Identity.Authorization.AbacDecision.Permit))
            .Throws(new InvalidOperationException("boom"));

        var result = await BulkAbacSut().Handle(new BulkEvaluateAbacPoliciesCommand
        {
            TenantId = _tenantId,
            Contexts = [new AbacEvaluationContext(), new AbacEvaluationContext()]
        }, CancellationToken.None);

        result.TotalEvaluations.Should().Be(2);
        result.SuccessfulEvaluations.Should().Be(1);
        result.FailedEvaluations.Should().Be(1);
        result.Failures.Should().ContainSingle(f => f.ContextIndex == 1);
    }

    [Fact]
    public async Task EvaluateConditional_CrossTenantNonSystemAdmin_IsRejected()
    {
        ActAs(_actorId, _tenantId);

        var act = () => ConditionalSut().Handle(new EvaluateConditionalPoliciesCommand
        {
            UserId = _actorId,
            TenantId = Guid.NewGuid()
        }, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _conditionalEvaluator.Verify(e => e.EvaluateAsync(
            It.IsAny<GameGuild.Identity.Authorization.ConditionalPolicyContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateConditional_MapsHypotheticalContextThroughProductionEvaluator()
    {
        ActAs(_actorId, _tenantId);

        var result = await ConditionalSut().Handle(new EvaluateConditionalPoliciesCommand
        {
            UserId = _actorId,
            TenantId = _tenantId,
            ResourceType = "Project",
            Context = new Dictionary<string, object> { ["ipAddress"] = "10.0.0.1" }
        }, CancellationToken.None);

        result.IsAllowed.Should().BeTrue();
        _conditionalEvaluator.Verify(e => e.EvaluateAsync(
            It.Is<GameGuild.Identity.Authorization.ConditionalPolicyContext>(c =>
                c.IpAddress == "10.0.0.1"
                && c.ResourceType == "Project"
                && c.TenantId == _tenantId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TestRule_MatchingDenyRule_ReportsRuleResultTrue()
    {
        ActAs(_actorId, _tenantId);

        var result = await TestRuleSut().Handle(new TestConditionalPolicyRuleCommand
        {
            ConditionType = PolicyConditionType.Environment,
            EnvironmentConditions = """{"RequireMfa":true}"""
        }, CancellationToken.None);

        result.IsValid.Should().BeTrue();
        // RuleResult == true means "the rule matched" (deny conditions satisfied).
        result.RuleResult.Should().BeTrue();
    }

    [Fact]
    public async Task TestRule_NotMatchingRule_ReportsRuleResultFalse()
    {
        ActAs(_actorId, _tenantId);

        var result = await TestRuleSut().Handle(new TestConditionalPolicyRuleCommand
        {
            ConditionType = PolicyConditionType.Environment,
            EnvironmentConditions = """{"RequireMfa":false}"""
        }, CancellationToken.None);

        result.IsValid.Should().BeTrue();
        result.RuleResult.Should().BeFalse();
    }

    [Fact]
    public async Task TestRule_MalformedConditionJson_IsReportedInvalid()
    {
        ActAs(_actorId, _tenantId);

        var result = await TestRuleSut().Handle(new TestConditionalPolicyRuleCommand
        {
            ConditionType = PolicyConditionType.Time,
            TimeConditions = "{ not json"
        }, CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.ValidationErrors.Should().Contain(e => e.Contains("timeConditions"));
    }

    [Fact]
    public async Task Simulate_ExplainsEachScenarioStep()
    {
        ActAs(_actorId, _tenantId);

        var result = await SimulateSut().Handle(new SimulateConditionalPolicyCommand
        {
            PolicyId = Guid.NewGuid(),
            ConditionType = PolicyConditionType.Environment,
            EnvironmentConditions = """{"RequireMfa":true}""",
            TestScenarios =
            [
                new Dictionary<string, object> { ["isMfaVerified"] = "false" },
                new Dictionary<string, object> { ["isMfaVerified"] = "true" }
            ]
        }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Steps.Should().HaveCount(2);
        result.Steps[0].Result.Should().BeFalse("the first scenario is denied by the hypothetical rule");
        result.Steps[1].Result.Should().BeTrue("the second scenario is not denied");
        result.FinalDecision.Should().Contain("Deny");
    }

    [Fact]
    public async Task Simulate_AnonymousActor_IsRejected()
    {
        ActAs(null, null);

        var act = () => SimulateSut().Handle(new SimulateConditionalPolicyCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
