using FluentAssertions;
using GameGuild.CQRS.Models;
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
    }

    private EffectivePermissionResolverService CreateSut(Guid? systemAccountId = null) =>
        new(
            _repository.Object,
            _rbacResolver.Object,
            [_roleProvider.Object],
            _resourceService.Object,
            Options.Create(new GameGuild.Configuration.PresentationLayer.Authorization.AuthorizationOptions
            {
                SystemAccountId = systemAccountId ?? SystemAccountId
            }),
            NullLogger<EffectivePermissionResolverService>.Instance,
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
