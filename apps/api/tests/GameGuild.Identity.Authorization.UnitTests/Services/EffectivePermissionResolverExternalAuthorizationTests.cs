using FluentAssertions;
using GameGuild.CQRS.Models;
using GameGuild.Identity.Authorization.UnitTests.TestSupport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests.Services;

/// <summary>
///     Resolver-chain integration tests for the external authorization-decision layer
///     (issue #146): an external deny overrides a local allow, an external allow can
///     never override a local deny nor grant an absent permission, not-applicable and
///     missing decisions pass through, the static system-account wildcard stays
///     non-deniable, the provider receives the full query context and a throwing
///     provider fails closed.
///     Contract: apps/api/docs/effective-permission-resolution.md
/// </summary>
public class EffectivePermissionResolverExternalAuthorizationTests
{
    private static readonly Guid SystemAccountId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly Mock<ITenantPermissionRepository> _repository = new();
    private readonly Mock<IRbacPermissionResolver> _rbacResolver = new();
    private readonly Mock<IAuthorizationRolePermissionProvider> _roleProvider = new();
    private readonly Mock<IResourcePermissionService> _resourceService = new();
    private readonly Mock<IExternalAuthorizationDecisionProvider> _externalProvider = new();

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    public EffectivePermissionResolverExternalAuthorizationTests()
    {
        _repository
            .Setup(r => r.GetByUserAndTenantAsync(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantPermission?)null);
        _rbacResolver
            .Setup(r => r.ResolvePermissionsAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RbacResolutionResult(new HashSet<string>(), new HashSet<string>(), []));
        _roleProvider
            .Setup(p => p.GetPermissionsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<string>)[]);
        _resourceService
            .Setup(s => s.GetUserResourcesAsync(It.IsAny<TenantId>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ResourceUserPermission>)[]);
    }

    private EffectivePermissionResolverService CreateSut(
        Guid? systemAccountId = null,
        ILogger<EffectivePermissionResolverService>? logger = null) =>
        new(
            _repository.Object,
            _rbacResolver.Object,
            [_roleProvider.Object],
            _resourceService.Object,
            Options.Create(new GameGuild.Configuration.PresentationLayer.Authorization.AuthorizationOptions
            {
                SystemAccountId = systemAccountId ?? SystemAccountId
            }),
            logger ?? NullLogger<EffectivePermissionResolverService>.Instance,
            externalDecisionProvider: _externalProvider.Object);

    private void SetupDirectGrant(string[] permissions, string[] denyPermissions) =>
        _repository
            .Setup(r => r.GetByUserAndTenantAsync(_userId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantPermission
            {
                UserId = _userId,
                TenantId = _tenantId,
                Permissions = permissions,
                DenyPermissions = denyPermissions
            });

    private void SetupExternal(Func<ExternalAuthorizationQuery, ExternalAuthorizationDecision?> decide) =>
        _externalProvider
            .Setup(p => p.EvaluateAsync(It.IsAny<ExternalAuthorizationQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalAuthorizationQuery query, CancellationToken _) => decide(query));

    [Theory]
    [InlineData("policy-7", "policy-7")]
    [InlineData("deny\r\nforged", "deny␀␀forged")]
    [InlineData("deny\0\t\u0085\u2028\u2029forged", "deny␀␀␀␀␀forged")]
    public async Task ExternalDeny_ProjectsReasonOnlyInLogs_AndRetainsVeto(string reason, string renderedReason)
    {
        SetupDirectGrant(["reports:read", "other:read"], []);
        var decision = ExternalAuthorizationDecision.Deny(reason);
        SetupExternal(query => query.Permission == "reports:read" ? decision : ExternalAuthorizationDecision.NotApplicable);
        var logger = new SpyLogger<EffectivePermissionResolverService>();

        var result = await CreateSut(logger: logger).ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId));

        result.Permissions.Should().NotContain("reports:read");
        result.Permissions.Should().Contain("other:read");
        decision.Reasons.Should().Equal(reason);
        var entry = logger.Entries.Should().ContainSingle(e =>
            e.Message.StartsWith("External authorization decision provider denied permission", StringComparison.Ordinal)).Which;
        entry.Level.Should().Be(LogLevel.Debug);
        entry.Message.Should().EndWith(": " + renderedReason);
        entry.Message.Should().NotContain(_userId.ToString()).And.NotContain(_tenantId.ToString());
        foreach (var log in logger.Entries)
        {
            log.Message.Should().NotMatchRegex("[\\x00-\\x1F\\x7F-\\x9F\\u2028\\u2029]");
        }
    }

    // ── External deny overrides local allow ─────────────────────────────────

    [Fact]
    public async Task ExternalDeny_RemovesLocallyAllowedPermission()
    {
        SetupDirectGrant(["reports:read", "other:read"], []);
        SetupExternal(query => query.Permission == "reports:read"
            ? ExternalAuthorizationDecision.Deny("policy-7")
            : ExternalAuthorizationDecision.NotApplicable);

        var result = await CreateSut().ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId));

        result.Permissions.Should().NotContain("reports:read", "an external deny overrides a local allow");
        result.Permissions.Should().Contain("other:read", "not-applicable external decisions pass through");
    }

    [Fact]
    public async Task ExternalDeny_HasPermissionAsync_ReturnsFalse()
    {
        SetupDirectGrant(["reports:read"], []);
        SetupExternal(_ => ExternalAuthorizationDecision.Deny("policy-7"));

        (await CreateSut().HasPermissionAsync(_userId, _tenantId, "reports:read")).Should().BeFalse();
    }

    // ── External allow cannot override a local deny, cannot grant ───────────

    [Fact]
    public async Task ExternalAllow_DoesNotOverrideLocalDeny()
    {
        SetupDirectGrant(["reports:read"], ["reports:read"]);
        SetupExternal(_ => ExternalAuthorizationDecision.Allow);

        var result = await CreateSut().ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId));

        result.Permissions.Should().NotContain("reports:read", "local DENY-WINS still applies against external allows");
    }

    [Fact]
    public async Task ExternalAllow_DoesNotGrantAbsentPermission()
    {
        SetupDirectGrant(["granted:read"], []);
        SetupExternal(_ => ExternalAuthorizationDecision.Allow);

        var result = await CreateSut().ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId));

        result.Permissions.Should().Contain("granted:read");
        result.Permissions.Should().NotContain("absent:read", "grants stay exclusively local (absent = deny, #327)");
    }

    // ── Not-applicable / missing decisions pass through ─────────────────────

    [Fact]
    public async Task ExternalNotApplicable_PassesThrough()
    {
        SetupDirectGrant(["reports:read"], []);
        SetupExternal(_ => ExternalAuthorizationDecision.NotApplicable);

        var result = await CreateSut().ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId));

        result.Permissions.Should().Contain("reports:read");
    }

    [Fact]
    public async Task NullDecision_PassesThrough()
    {
        SetupDirectGrant(["reports:read"], []);
        SetupExternal(_ => null);

        var result = await CreateSut().ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId));

        result.Permissions.Should().Contain("reports:read", "a disabled/unavailable observe-mode provider leaves local resolution unchanged");
    }

    // ── Non-deniable static wildcard ────────────────────────────────────────

    [Fact]
    public async Task StaticWildcard_IsNotVetoedByExternalDeny()
    {
        SetupExternal(_ => ExternalAuthorizationDecision.Deny("everything"));

        var result = await CreateSut().ResolveAsync(EffectivePermissionContext.ForTenant(SystemAccountId, _tenantId));

        result.Permissions.Should().Contain("*", "the static system-account wildcard is non-deniable, including by external decisions");
        _externalProvider.Verify(p => p.EvaluateAsync(It.IsAny<ExternalAuthorizationQuery>(), It.IsAny<CancellationToken>()), Times.Never,
            "the static grant is not queried externally at all");
    }

    // ── Fail-closed on a throwing provider ──────────────────────────────────

    [Fact]
    public async Task ThrowingProvider_FailsClosed()
    {
        SetupDirectGrant(["reports:read"], []);
        _externalProvider
            .Setup(p => p.EvaluateAsync(It.IsAny<ExternalAuthorizationQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("provider violated the never-throw contract"));

        var result = await CreateSut().ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId));

        result.Permissions.Should().NotContain("reports:read", "an unexpected provider exception denies the permission under question");
    }

    [Fact]
    public async Task ThrowingProvider_DeniesOnlyThePermissionUnderQuestion()
    {
        SetupDirectGrant(["reports:read", "stable:read"], []);
        _externalProvider
            .Setup(p => p.EvaluateAsync(It.Is<ExternalAuthorizationQuery>(q => q.Permission == "reports:read"), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        _externalProvider
            .Setup(p => p.EvaluateAsync(It.Is<ExternalAuthorizationQuery>(q => q.Permission == "stable:read"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExternalAuthorizationDecision.NotApplicable);

        var result = await CreateSut().ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId));

        result.Permissions.Should().NotContain("reports:read", "the throwing query fails closed");
        result.Permissions.Should().Contain("stable:read", "an unrelated permission is not collateral damage of the throwing query");
    }

    [Theory]
    [InlineData("reports:read", "reports:read")]
    [InlineData("reports\r\nforged:read", "reports␀␀forged:read")]
    [InlineData("reports\0\t\u0085\u2028\u2029forged:read", "reports␀␀␀␀␀forged:read")]
    public async Task ThrowingProvider_ProjectsPermissionOnlyInLogs_AndPreservesQueryAndDenial(
        string permission,
        string renderedPermission)
    {
        SetupDirectGrant([permission, "stable:read"], []);
        var failure = new InvalidOperationException("provider failure");
        _externalProvider
            .Setup(provider => provider.EvaluateAsync(
                It.Is<ExternalAuthorizationQuery>(query => query.Permission == permission),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);
        _externalProvider
            .Setup(provider => provider.EvaluateAsync(
                It.Is<ExternalAuthorizationQuery>(query => query.Permission == "stable:read"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExternalAuthorizationDecision.NotApplicable);
        var logger = new Mock<ILogger<EffectivePermissionResolverService>>();
        logger.Setup(value => value.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        var result = await CreateSut(logger: logger.Object)
            .ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId));

        result.ContextValid.Should().BeTrue();
        result.Permissions.Should().ContainSingle().Which.Should().Be("stable:read");
        result.Sources.Should().NotContainKey(permission);
        result.Sources["stable:read"].Should().Be(PermissionSource.DirectGrant);
        _externalProvider.Verify(provider => provider.EvaluateAsync(
            It.Is<ExternalAuthorizationQuery>(query =>
                query.Permission == permission && query.UserId == _userId && query.TenantId == _tenantId),
            It.IsAny<CancellationToken>()), Times.Once);

        var error = logger.Invocations.Should().ContainSingle(call =>
            call.Method.Name == nameof(ILogger.Log) && (LogLevel)call.Arguments[0] == LogLevel.Error).Which;
        error.Arguments[3].Should().BeSameAs(failure);
        var properties = Assert.IsAssignableFrom<IEnumerable<KeyValuePair<string, object?>>>(error.Arguments[2])
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        properties["Permission"].Should().Be(renderedPermission);
        properties["UserId"].Should().Be(LogRedaction.RedactId(_userId, "uid"));
        properties["TenantId"].Should().Be(LogRedaction.RedactId(_tenantId));
        properties.Values.Should().NotContain(_userId).And.NotContain(_tenantId);
        var formatter = Assert.IsAssignableFrom<Delegate>(error.Arguments[4]);
        var message = Assert.IsType<string>(formatter.DynamicInvoke(error.Arguments[2], error.Arguments[3]));
        message.Should().Be(
            $"External authorization decision provider threw while evaluating permission {renderedPermission} for user {LogRedaction.RedactId(_userId, "uid")} in tenant {LogRedaction.RedactId(_tenantId)} - the permission fails closed.");
    }

    // ── Query shape: the provider sees the full authorization context ───────

    [Fact]
    public async Task Provider_ReceivesResourceScopedQuery()
    {
        SetupDirectGrant(["documents:write"], []);
        SetupExternal(_ => ExternalAuthorizationDecision.NotApplicable);

        await CreateSut().ResolveAsync(EffectivePermissionContext.ForResource(_userId, _tenantId, "document", "42"));

        _externalProvider.Verify(
            p => p.EvaluateAsync(
                It.Is<ExternalAuthorizationQuery>(q =>
                    q.UserId == _userId
                    && q.TenantId == _tenantId
                    && q.Permission == "documents:write"
                    && q.ResourceType == "document"
                    && q.ResourceId == "42"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Provider_IsConsultedOncePerEffectivePermission()
    {
        SetupDirectGrant(["a:read", "b:read", "c:read"], []);
        SetupExternal(_ => ExternalAuthorizationDecision.NotApplicable);

        await CreateSut().ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId));

        _externalProvider.Verify(
            p => p.EvaluateAsync(It.IsAny<ExternalAuthorizationQuery>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    // ── Cancellation propagation ────────────────────────────────────────────

    [Fact]
    public async Task CancelledResolution_PropagatesOperationCanceled()
    {
        SetupDirectGrant(["reports:read"], []);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        _externalProvider
            .Setup(p => p.EvaluateAsync(It.IsAny<ExternalAuthorizationQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalAuthorizationQuery _, CancellationToken token) =>
            {
                token.ThrowIfCancellationRequested();
                return ExternalAuthorizationDecision.NotApplicable;
            });

        var act = () => CreateSut().ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId), cancelled.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
