using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests.Services;

/// <summary>
///     Tests for the ABAC overlay on effective-permission resolution (issue #293): a
///     matching deny policy with target actions subtracts those actions, an untargeted
///     deny subtracts everything, allow-effect policies never mint grants, and evaluator
///     failures contribute nothing.
/// </summary>
public class AbacPolicyEvaluationExtensionTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    private static PermissionEvaluationExtensionContext Context(
        Guid userId,
        Guid tenantId,
        string? resourceType = null,
        string? resourceId = null,
        string[]? currentAllows = null) => new()
    {
        UserId = userId,
        TenantId = tenantId,
        ResourceType = resourceType,
        ResourceId = resourceId,
        CurrentAllows = new HashSet<string>(currentAllows ?? [], StringComparer.OrdinalIgnoreCase),
        CurrentDenies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    };

    private static AbacEvaluationResult DenyResult(
        Guid policyId,
        string policyName,
        string[]? targetActions) => new(
            AbacDecision.Deny,
            policyId,
            policyName,
            "Denied by test policy",
            [new AbacPolicyEvaluationDetail(policyId, policyName, ConditionsMatched: true, AbacDecision.Deny, targetActions)]);

    [Fact]
    public async Task MatchingDeny_WithTargetActions_SubtractsOnlyThoseActions()
    {
        var policyId = Guid.NewGuid();
        var evaluator = new Mock<IAbacPolicyEvaluator>();
        evaluator
            .Setup(e => e.EvaluateAsync(It.IsAny<AbacRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DenyResult(policyId, "deny-delete", ["Delete"]));

        var sut = new AbacPolicyEvaluationExtension(evaluator.Object, NullLogger<AbacPolicyEvaluationExtension>.Instance);

        var contribution = await sut.EvaluateAsync(
            Context(_userId, _tenantId, currentAllows: ["Read", "Write", "Delete"]),
            CancellationToken.None);

        contribution.AdditionalAllows.Should().BeEmpty();
        contribution.AdditionalDenies.Should().BeEquivalentTo(["Delete"]);
    }

    [Fact]
    public async Task MatchingDeny_WithWildcardTarget_SubtractsEverything()
    {
        var policyId = Guid.NewGuid();
        var evaluator = new Mock<IAbacPolicyEvaluator>();
        evaluator
            .Setup(e => e.EvaluateAsync(It.IsAny<AbacRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DenyResult(policyId, "deny-all", ["*"]));

        var sut = new AbacPolicyEvaluationExtension(evaluator.Object, NullLogger<AbacPolicyEvaluationExtension>.Instance);

        var contribution = await sut.EvaluateAsync(
            Context(_userId, _tenantId, currentAllows: ["Read", "Write"]),
            CancellationToken.None);

        contribution.AdditionalDenies.Should().BeEquivalentTo(["Read", "Write"]);
    }

    [Fact]
    public async Task MatchingDeny_WithoutTargets_SubtractsEverythingAllowed()
    {
        var policyId = Guid.NewGuid();
        var evaluator = new Mock<IAbacPolicyEvaluator>();
        evaluator
            .Setup(e => e.EvaluateAsync(It.IsAny<AbacRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DenyResult(policyId, "context-deny", null));

        var sut = new AbacPolicyEvaluationExtension(evaluator.Object, NullLogger<AbacPolicyEvaluationExtension>.Instance);

        var contribution = await sut.EvaluateAsync(
            Context(_userId, _tenantId, currentAllows: ["Read"]),
            CancellationToken.None);

        contribution.AdditionalDenies.Should().BeEquivalentTo(["Read"]);
    }

    [Theory]
    [InlineData(AbacDecision.Permit)]
    [InlineData(AbacDecision.NotApplicable)]
    [InlineData(AbacDecision.Indeterminate)]
    public async Task NonDenyOutcome_ContributesNothing(AbacDecision decision)
    {
        var evaluator = new Mock<IAbacPolicyEvaluator>();
        evaluator
            .Setup(e => e.EvaluateAsync(It.IsAny<AbacRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AbacEvaluationResult(decision));

        var sut = new AbacPolicyEvaluationExtension(evaluator.Object, NullLogger<AbacPolicyEvaluationExtension>.Instance);

        var contribution = await sut.EvaluateAsync(
            Context(_userId, _tenantId, currentAllows: ["Read"]),
            CancellationToken.None);

        contribution.AdditionalAllows.Should().BeEmpty();
        contribution.AdditionalDenies.Should().BeEmpty();
    }

    [Fact]
    public async Task EvaluatorFailure_ContributesNothing()
    {
        var evaluator = new Mock<IAbacPolicyEvaluator>();
        evaluator
            .Setup(e => e.EvaluateAsync(It.IsAny<AbacRequestContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("store down"));

        var sut = new AbacPolicyEvaluationExtension(evaluator.Object, NullLogger<AbacPolicyEvaluationExtension>.Instance);

        var contribution = await sut.EvaluateAsync(
            Context(_userId, _tenantId, currentAllows: ["Read"]),
            CancellationToken.None);

        contribution.AdditionalAllows.Should().BeEmpty();
        contribution.AdditionalDenies.Should().BeEmpty();
    }

    [Fact]
    public async Task Extension_BuildsSubjectAndResourceAttributes_FromResolutionContext()
    {
        AbacRequestContext? captured = null;
        var resourceId = Guid.NewGuid();
        var policyId = Guid.NewGuid();
        var evaluator = new Mock<IAbacPolicyEvaluator>();
        evaluator
            .Setup(e => e.EvaluateAsync(It.IsAny<AbacRequestContext>(), It.IsAny<CancellationToken>()))
            .Callback<AbacRequestContext, CancellationToken>((ctx, _) => captured = ctx)
            .ReturnsAsync(DenyResult(policyId, "deny", ["Read"]));

        var sut = new AbacPolicyEvaluationExtension(evaluator.Object, NullLogger<AbacPolicyEvaluationExtension>.Instance);
        _ = await sut.EvaluateAsync(
            Context(_userId, _tenantId, "Document", resourceId.ToString(), ["Read"]),
            CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.SubjectAttributes.Should().ContainKey("subject.user-id").WhoseValue.Should().Be(_userId);
        captured.SubjectAttributes.Should().ContainKey("subject.tenant-id").WhoseValue.Should().Be(_tenantId);
        captured.ResourceAttributes.Should().ContainKey("resource.type").WhoseValue.Should().Be("Document");
        captured.ResourceAttributes.Should().ContainKey("resource.id").WhoseValue.Should().Be(resourceId);
        captured.EnvironmentAttributes.Should().ContainKey("environment.current-time");
    }
}

/// <summary>
///     Tests that the ABAC policy evaluator honors AttributeExpression/ConditionExpression
/// payloads on the policy entity (issue #293) — the expression fields are no longer dead.
/// </summary>
public class AbacPolicyEvaluatorExpressionTests
{
    private readonly Mock<IAbacPolicyRepository> _repository = new();

    private AbacPolicyEvaluator CreateSut()
        => new(_repository.Object, NullLogger<AbacPolicyEvaluator>.Instance);

    private void SetupPolicy(Action<AbacPolicy> configure)
    {
        var policy = new AbacPolicy
        {
            Name = "expression-policy",
            Effect = AbacPolicyEffect.Deny,
            Priority = 10
        };
        configure(policy);
        _repository
            .Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([policy]);
    }

    private static Task<AbacEvaluationResult> Evaluate(AbacPolicyEvaluator sut, AbacRequestContext context)
        => sut.EvaluateAsync(context);

    [Fact]
    public async Task Policy_WithMatchingAttributeExpression_Denies()
    {
        SetupPolicy(p => p.AttributeExpression = @"{""user.department"": ""IT""}");
        var context = new AbacRequestContext(
            new Dictionary<string, object> { ["subject.department"] = "IT" },
            new Dictionary<string, object>(), new Dictionary<string, object>(), new Dictionary<string, object>());

        var result = await Evaluate(CreateSut(), context);

        result.Decision.Should().Be(AbacDecision.Deny);
        result.DecidingPolicyName.Should().Be("expression-policy");
    }

    [Fact]
    public async Task Policy_WithNonMatchingAttributeExpression_DoesNotDeny()
    {
        SetupPolicy(p => p.AttributeExpression = @"{""user.department"": ""Sales""}");
        var context = new AbacRequestContext(
            new Dictionary<string, object> { ["subject.department"] = "IT" },
            new Dictionary<string, object>(), new Dictionary<string, object>(), new Dictionary<string, object>());

        var result = await Evaluate(CreateSut(), context);

        result.Decision.Should().NotBe(AbacDecision.Deny);
    }

    [Fact]
    public async Task Policy_WithMatchingConditionExpression_Denies()
    {
        SetupPolicy(p => p.ConditionExpression = "user.clearanceLevel < resource.requiredClearance");
        var context = new AbacRequestContext(
            new Dictionary<string, object> { ["subject.clearanceLevel"] = 2 },
            new Dictionary<string, object> { ["resource.requiredClearance"] = 4 },
            new Dictionary<string, object>(), new Dictionary<string, object>());

        var result = await Evaluate(CreateSut(), context);

        result.Decision.Should().Be(AbacDecision.Deny);
    }

    [Fact]
    public async Task Policy_WithFailingConditionExpression_DoesNotDeny()
    {
        // 5 < 4 is false, so the deny policy does not match and must not deny.
        SetupPolicy(p => p.ConditionExpression = "user.clearanceLevel < resource.requiredClearance");
        var context = new AbacRequestContext(
            new Dictionary<string, object> { ["subject.clearanceLevel"] = 5 },
            new Dictionary<string, object> { ["resource.requiredClearance"] = 4 },
            new Dictionary<string, object>(), new Dictionary<string, object>());

        var result = await Evaluate(CreateSut(), context);

        result.Decision.Should().NotBe(AbacDecision.Deny);
    }
}
