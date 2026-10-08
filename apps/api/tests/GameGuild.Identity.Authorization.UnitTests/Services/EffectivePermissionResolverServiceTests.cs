using FluentAssertions;
using GameGuild.CQRS.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests.Services;

/// <summary>
///     Focused tests for the effective-permission resolution contract (issue #330):
///     absent permissions, explicit grants/denies, cross-layer conflicts, inheritance,
///     tenant/resource context isolation, inactive/expired/revoked grants, invalid
///     contexts, wildcard protection and determinism.
///     Contract: apps/api/docs/effective-permission-resolution.md
/// </summary>
public class EffectivePermissionResolverServiceTests
{
    private static readonly Guid SystemAccountId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly Mock<ITenantPermissionRepository> _repository = new();
    private readonly Mock<IRbacPermissionResolver> _rbacResolver = new();
    private readonly Mock<IAuthorizationRolePermissionProvider> _roleProvider = new();
    private readonly Mock<IResourcePermissionService> _resourceService = new();
    private readonly Mock<IJitElevationRequestRepository> _jitRepository = new();

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    private EffectivePermissionResolverService CreateSut(Guid? systemAccountId = null, bool withJitElevations = false)
    {
        var options = Options.Create(new GameGuild.Configuration.PresentationLayer.Authorization.AuthorizationOptions
        {
            SystemAccountId = systemAccountId ?? SystemAccountId
        });

        return new EffectivePermissionResolverService(
            _repository.Object,
            _rbacResolver.Object,
            [_roleProvider.Object],
            _resourceService.Object,
            options,
            NullLogger<EffectivePermissionResolverService>.Instance,
            withJitElevations ? _jitRepository.Object : null);
    }

    private void SetupNoData()
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

    private void SetupTenantRow(Guid? userId, Guid? tenantId, string[] permissions, string[] denyPermissions, bool isActive = true, DateTime? expiresAt = null)
    {
        _repository
            .Setup(r => r.GetByUserAndTenantAsync(userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantPermission
            {
                UserId = userId,
                TenantId = tenantId,
                Permissions = permissions,
                DenyPermissions = denyPermissions,
                IsActive = isActive,
                ExpiresAt = expiresAt
            });
    }

    // ── Absent permissions: deny-by-default (#327) ─────────────────────────

    [Fact]
    public async Task ResolveAsync_NoGrantsAnywhere_ReturnsEmpty_DenyByDefault()
    {
        SetupNoData();
        var result = await CreateSut().ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId));

        result.Permissions.Should().BeEmpty();
        result.Sources.Should().BeEmpty();
        result.ContextValid.Should().BeTrue();
    }

    [Fact]
    public async Task HasPermissionAsync_AbsentPermission_ReturnsFalse()
    {
        SetupNoData();
        (await CreateSut().HasPermissionAsync(_userId, _tenantId, "anything:read")).Should().BeFalse();
    }

    // ── Explicit grants per layer ──────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_GlobalDefaults_ContributeWithSource()
    {
        SetupNoData();
        SetupTenantRow(null, null, ["global:read"], []);

        var result = await CreateSut().ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId));

        result.Permissions.Should().Contain("global:read");
        result.Sources["global:read"].Should().Be(PermissionSource.GlobalDefault);
    }

    [Fact]
    public async Task ResolveAsync_TenantDefaults_ContributeWithSource()
    {
        SetupNoData();
        SetupTenantRow(null, _tenantId, ["tenant:read"], []);

        var result = await CreateSut().ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId));

        result.Permissions.Should().Contain("tenant:read");
        result.Sources["tenant:read"].Should().Be(PermissionSource.TenantDefault);
    }

    [Fact]
    public async Task ResolveAsync_DirectGrant_ContributesWithSource()
    {
        SetupNoData();
        SetupTenantRow(_userId, _tenantId, ["direct:read"], []);

        var result = await CreateSut().ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId));

        result.Permissions.Should().Contain("direct:read");
        result.Sources["direct:read"].Should().Be(PermissionSource.DirectGrant);
    }

    [Fact]
    public async Task ResolveAsync_RoleProviderPermissions_ContributeWithRoleSource()
    {
        SetupNoData();
        _roleProvider
            .Setup(p => p.GetPermissionsAsync(_userId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<string>)["role:read"]);

        var result = await CreateSut().ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId));

        result.Permissions.Should().Contain("role:read");
        result.Sources["role:read"].Should().Be(PermissionSource.Role);
    }

    // ── Explicit denials and conflicts: DENY-WINS ──────────────────────────

    [Fact]
    public async Task ResolveAsync_TenantDeny_OverridesGlobalDefaultGrant()
    {
        SetupNoData();
        SetupTenantRow(null, null, ["shared:perm"], []);
        SetupTenantRow(null, _tenantId, [], ["shared:perm"]);

        var result = await CreateSut().ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId));

        result.Permissions.Should().NotContain("shared:perm");
    }

    [Fact]
    public async Task ResolveAsync_DirectDeny_OverridesRoleProviderGrant()
    {
        SetupNoData();
        _roleProvider
            .Setup(p => p.GetPermissionsAsync(_userId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<string>)["courses:update"]);
        SetupTenantRow(_userId, _tenantId, [], ["courses:update"]);

        var result = await CreateSut().ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId));

        result.Permissions.Should().NotContain("courses:update");
    }

    [Fact]
    public async Task ResolveAsync_RbacDeny_OverridesProviderGrant()
    {
        SetupNoData();
        _roleProvider
            .Setup(p => p.GetPermissionsAsync(_userId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<string>)["conflicted:perm"]);
        _rbacResolver
            .Setup(r => r.ResolvePermissionsAsync(_userId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RbacResolutionResult(
                new HashSet<string>(),
                new HashSet<string> { "conflicted:perm" },
                []));

        var result = await CreateSut().ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId));

        result.Permissions.Should().NotContain("conflicted:perm");
    }

    [Fact]
    public async Task ResolveAsync_GrantAndDenyInSameRow_DenyWins()
    {
        SetupNoData();
        SetupTenantRow(_userId, _tenantId, ["both:perm"], ["both:perm"]);

        var result = await CreateSut().ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId));

        result.Permissions.Should().NotContain("both:perm");
    }

    [Fact]
    public async Task ResolveAsync_DenyOnly_DoesNotCreatePermission()
    {
        SetupNoData();
        SetupTenantRow(null, _tenantId, [], ["never:granted"]);

        var result = await CreateSut().ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId));

        result.Permissions.Should().NotContain("never:granted");
        result.Permissions.Should().BeEmpty();
    }

    // ── Inheritance ────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_InheritedRoleContribution_AttributesToRoleInheritance()
    {
        SetupNoData();
        var parentRoleId = Guid.NewGuid();
        _rbacResolver
            .Setup(r => r.ResolvePermissionsAsync(_userId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RbacResolutionResult(
                new HashSet<string> { "inherited:read" },
                new HashSet<string>(),
                [
                    new RoleContribution(parentRoleId, "Viewer", ["inherited:read"], IsInherited: true, InheritedFromRoleId: parentRoleId)
                ]));

        var result = await CreateSut().ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId));

        result.Permissions.Should().Contain("inherited:read");
        result.Sources["inherited:read"].Should().Be(PermissionSource.RoleInheritance);
        result.RoleContributions.Should().ContainSingle(c => c.IsInherited);
    }

    [Fact]
    public async Task ResolveAsync_DirectRoleOutranksInheritedForAttribution()
    {
        SetupNoData();
        _rbacResolver
            .Setup(r => r.ResolvePermissionsAsync(_userId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RbacResolutionResult(
                new HashSet<string> { "shared:role:perm" },
                new HashSet<string>(),
                [
                    new RoleContribution(Guid.NewGuid(), "Admin", ["shared:role:perm"], IsInherited: false, InheritedFromRoleId: null),
                    new RoleContribution(Guid.NewGuid(), "Viewer", ["shared:role:perm"], IsInherited: true, InheritedFromRoleId: Guid.NewGuid())
                ]));

        var result = await CreateSut().ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId));

        result.Permissions.Should().Contain("shared:role:perm");
        result.Sources["shared:role:perm"].Should().Be(PermissionSource.Role);
    }

    // ── Just-in-Time elevation grants (#341, TemporaryElevation layer) ─────

    private JitElevationRequest Elevation(
        string permission,
        ElevationRequestStatus status = ElevationRequestStatus.Active,
        Guid? resourceId = null,
        DateTime? startsAt = null,
        int durationMinutes = 30)
    {
        var start = startsAt ?? SystemClock.UtcNow.AddMinutes(-1);
        return new JitElevationRequest
        {
            RequesterId = _userId,
            Permission = permission,
            ResourceId = resourceId,
            StartsAt = start,
            ExpiresAt = start.AddMinutes(durationMinutes),
            Status = status
        };
    }

    [Fact]
    public async Task ResolveAsync_InForceJitElevation_ContributesWithTemporaryElevationSource()
    {
        SetupNoData();
        _jitRepository
            .Setup(r => r.GetActiveByUserAsync(_userId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Elevation("billing:export")]);

        var result = await CreateSut(withJitElevations: true)
            .ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId));

        result.Permissions.Should().Contain("billing:export");
        result.Sources["billing:export"].Should().Be(PermissionSource.TemporaryElevation);
    }

    [Fact]
    public async Task ResolveAsync_ExpiredJitElevation_DoesNotContribute()
    {
        SetupNoData();
        _jitRepository
            .Setup(r => r.GetActiveByUserAsync(_userId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Elevation("billing:export", startsAt: SystemClock.UtcNow.AddHours(-2), durationMinutes: 30)]);

        var result = await CreateSut(withJitElevations: true)
            .ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId));

        result.Permissions.Should().NotContain("billing:export");
    }

    [Fact]
    public async Task ResolveAsync_RevokedJitElevation_DoesNotContribute()
    {
        SetupNoData();
        _jitRepository
            .Setup(r => r.GetActiveByUserAsync(_userId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Elevation("billing:export", status: ElevationRequestStatus.Revoked)]);

        var result = await CreateSut(withJitElevations: true)
            .ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId));

        result.Permissions.Should().NotContain("billing:export");
    }

    [Fact]
    public async Task ResolveAsync_ResourceScopedJitElevation_DoesNotContributeToTenantResolution()
    {
        SetupNoData();
        _jitRepository
            .Setup(r => r.GetActiveByUserAsync(_userId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Elevation("billing:export", resourceId: Guid.NewGuid())]);

        var result = await CreateSut(withJitElevations: true)
            .ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId));

        result.Permissions.Should().NotContain("billing:export");
    }

    [Fact]
    public async Task ResolveAsync_JitElevationWildcard_NotGrantable()
    {
        SetupNoData();
        _jitRepository
            .Setup(r => r.GetActiveByUserAsync(_userId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Elevation("admin:*")]);

        var result = await CreateSut(withJitElevations: true)
            .ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId));

        result.Permissions.Should().NotContain("admin:*");
    }

    [Fact]
    public async Task ResolveAsync_DirectDeny_OverridesJitElevation()
    {
        SetupNoData();
        SetupTenantRow(_userId, _tenantId, [], ["billing:export"]);
        _jitRepository
            .Setup(r => r.GetActiveByUserAsync(_userId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Elevation("billing:export")]);

        var result = await CreateSut(withJitElevations: true)
            .ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId));

        result.Permissions.Should().NotContain("billing:export");
    }

    // ── Grant validity: inactive / expired / revoked ───────────────────────

    [Fact]
    public async Task ResolveAsync_InactiveGrant_Excluded()
    {
        SetupNoData();
        SetupTenantRow(_userId, _tenantId, ["inactive:perm"], [], isActive: false);

        var result = await CreateSut().ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId));

        result.Permissions.Should().NotContain("inactive:perm");
    }

    [Fact]
    public async Task ResolveAsync_ExpiredGrant_Excluded()
    {
        SetupNoData();
        SetupTenantRow(_userId, _tenantId, ["expired:perm"], [], expiresAt: SystemClock.UtcNow.AddHours(-1));

        var result = await CreateSut().ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId));

        result.Permissions.Should().NotContain("expired:perm");
    }

    [Fact]
    public async Task ResolveAsync_InactiveTenantDefaults_Excluded()
    {
        SetupNoData();
        SetupTenantRow(null, _tenantId, ["tenant:read"], [], isActive: false);

        var result = await CreateSut().ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId));

        result.Permissions.Should().NotContain("tenant:read");
    }

    // ── Context isolation: tenants and resources ───────────────────────────

    [Fact]
    public async Task ResolveAsync_GrantsFromOtherTenant_DoNotContribute()
    {
        SetupNoData();
        var otherTenant = Guid.NewGuid();
        SetupTenantRow(null, otherTenant, ["other-tenant:perm"], []);

        var result = await CreateSut().ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId));

        result.Permissions.Should().NotContain("other-tenant:perm");
    }

    [Fact]
    public async Task ResolveAsync_UserGlobalRow_DoesNotContributeToTenantResolution()
    {
        SetupNoData();
        SetupTenantRow(_userId, null, ["user-global:perm"], []);

        var result = await CreateSut().ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId));

        result.Permissions.Should().NotContain("user-global:perm");
    }

    [Fact]
    public async Task ResolveAsync_ResourceGrant_DoesNotLeakIntoTenantWideResult()
    {
        SetupNoData();
        _resourceService
            .Setup(s => s.GetUserResourcesAsync(It.IsAny<TenantId>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ResourceUserPermission>)
            [
                new ResourceUserPermission
                {
                    TenantId = new TenantId(_tenantId),
                    UserId = _userId,
                    ResourceType = "Project",
                    ResourceId = "res-1",
                    Permissions = ["project:read"],
                    GrantedByUserId = Guid.NewGuid()
                }
            ]);

        var result = await CreateSut().ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId));

        result.Permissions.Should().NotContain("project:read");
    }

    [Fact]
    public async Task ResolveAsync_WithContextResource_MatchingGrantContributes()
    {
        SetupNoData();
        _resourceService
            .Setup(s => s.GetUserResourcesAsync(new TenantId(_tenantId), _userId, "Project", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ResourceUserPermission>)
            [
                new ResourceUserPermission
                {
                    TenantId = new TenantId(_tenantId),
                    UserId = _userId,
                    ResourceType = "Project",
                    ResourceId = "res-1",
                    Permissions = ["project:read", "project:write"],
                    GrantedByUserId = Guid.NewGuid()
                }
            ]);

        var result = await CreateSut()
            .ResolveAsync(EffectivePermissionContext.ForResource(_userId, _tenantId, "Project", "res-1"));

        result.Permissions.Should().Contain("project:read").And.Contain("project:write");
        result.Sources["project:read"].Should().Be(PermissionSource.ResourceGrant);
    }

    [Fact]
    public async Task ResolveAsync_WithContextResource_DifferentResourceId_DoesNotContribute()
    {
        SetupNoData();
        _resourceService
            .Setup(s => s.GetUserResourcesAsync(new TenantId(_tenantId), _userId, "Project", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ResourceUserPermission>)
            [
                new ResourceUserPermission
                {
                    TenantId = new TenantId(_tenantId),
                    UserId = _userId,
                    ResourceType = "Project",
                    ResourceId = "res-other",
                    Permissions = ["project:read"],
                    GrantedByUserId = Guid.NewGuid()
                }
            ]);

        var result = await CreateSut()
            .ResolveAsync(EffectivePermissionContext.ForResource(_userId, _tenantId, "Project", "res-1"));

        result.Permissions.Should().NotContain("project:read");
    }

    [Fact]
    public async Task ResolveAsync_WithContextResource_ExpiredGrant_DoesNotContribute()
    {
        SetupNoData();
        _resourceService
            .Setup(s => s.GetUserResourcesAsync(new TenantId(_tenantId), _userId, "Project", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ResourceUserPermission>)
            [
                new ResourceUserPermission
                {
                    TenantId = new TenantId(_tenantId),
                    UserId = _userId,
                    ResourceType = "Project",
                    ResourceId = "res-1",
                    Permissions = ["project:read"],
                    ExpiresAt = SystemClock.UtcNow.AddHours(-1),
                    GrantedByUserId = Guid.NewGuid()
                }
            ]);

        var result = await CreateSut()
            .ResolveAsync(EffectivePermissionContext.ForResource(_userId, _tenantId, "Project", "res-1"));

        result.Permissions.Should().NotContain("project:read");
    }

    // ── Invalid / missing contexts: fail closed ────────────────────────────

    [Fact]
    public async Task ResolveAsync_LegacyOverload_NullTenant_FailsClosed()
    {
        SetupNoData();
        var result = await CreateSut().ResolveAsync(_userId, null);

        result.Permissions.Should().BeEmpty();
        result.ContextValid.Should().BeFalse();
    }

    [Fact]
    public async Task ResolveAsync_EmptyTenant_FailsClosed()
    {
        var result = await CreateSut().ResolveAsync(EffectivePermissionContext.ForTenant(_userId, Guid.Empty));

        result.Permissions.Should().BeEmpty();
        result.ContextValid.Should().BeFalse();
    }

    [Fact]
    public async Task ResolveAsync_EmptyUser_FailsClosed()
    {
        var result = await CreateSut().ResolveAsync(EffectivePermissionContext.ForTenant(Guid.Empty, _tenantId));

        result.Permissions.Should().BeEmpty();
        result.ContextValid.Should().BeFalse();
    }

    [Fact]
    public async Task ResolveAsync_HalfSpecifiedResourceContext_FailsClosed()
    {
        var result = await CreateSut().ResolveAsync(new EffectivePermissionContext
        {
            UserId = _userId,
            TenantId = _tenantId,
            ResourceType = "Project"
        });

        result.Permissions.Should().BeEmpty();
        result.ContextValid.Should().BeFalse();
    }

    [Fact]
    public async Task HasPermissionAsync_NullTenant_FailsClosed()
    {
        SetupNoData();
        _rbacResolver
            .Setup(r => r.ResolvePermissionsAsync(_userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RbacResolutionResult(new HashSet<string> { "legacy:read" }, new HashSet<string>(), []));

        // Even with RBAC data available, a missing tenant context denies.
        (await CreateSut().HasPermissionAsync(_userId, null, "legacy:read")).Should().BeFalse();
    }

    // ── System account and wildcard handling ───────────────────────────────

    [Fact]
    public async Task ResolveAsync_SystemAccountWildcard_SurvivesExplicitDeny()
    {
        SetupNoData();
        SetupTenantRow(null, null, [], ["*"]);

        var result = await CreateSut().ResolveAsync(EffectivePermissionContext.ForTenant(SystemAccountId, _tenantId));

        result.Permissions.Should().Contain("*");
        result.Sources["*"].Should().Be(PermissionSource.Static);
    }

    [Fact]
    public async Task ResolveAsync_UniversalWildcardFromProvider_NotDelegable()
    {
        SetupNoData();
        _roleProvider
            .Setup(p => p.GetPermissionsAsync(_userId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<string>)["admin:*", "plain:read"]);

        var result = await CreateSut().ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId));

        result.Permissions.Should().NotContain("admin:*");
        result.Permissions.Should().Contain("plain:read");
    }

    // ── Determinism ────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_SameInputs_ProduceSameOutputs()
    {
        SetupNoData();
        SetupTenantRow(null, null, ["global:read"], []);
        SetupTenantRow(null, _tenantId, ["tenant:read", "tenant:write"], ["tenant:write"]);
        _roleProvider
            .Setup(p => p.GetPermissionsAsync(_userId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<string>)["role:read"]);

        var sut = CreateSut();
        var first = await sut.ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId));
        var second = await sut.ResolveAsync(EffectivePermissionContext.ForTenant(_userId, _tenantId));

        first.Permissions.Should().Equal(second.Permissions);
        first.Sources.Should().BeEquivalentTo(second.Sources);
        first.Permissions.Should().BeEquivalentTo(new[] { "global:read", "tenant:read", "role:read" });
    }

    // ── Convenience checks ─────────────────────────────────────────────────

    [Fact]
    public async Task HasAllPermissionsAsync_RequiresEveryPermission()
    {
        SetupNoData();
        SetupTenantRow(_userId, _tenantId, ["a", "b"], []);

        var sut = CreateSut();
        (await sut.HasAllPermissionsAsync(_userId, _tenantId, ["a", "b"])).Should().BeTrue();
        (await sut.HasAllPermissionsAsync(_userId, _tenantId, ["a", "missing"])).Should().BeFalse();
    }

    [Fact]
    public async Task HasAnyPermissionAsync_RequiresAtLeastOne()
    {
        SetupNoData();
        SetupTenantRow(_userId, _tenantId, ["a"], []);

        var sut = CreateSut();
        (await sut.HasAnyPermissionAsync(_userId, _tenantId, ["a", "missing"])).Should().BeTrue();
        (await sut.HasAnyPermissionAsync(_userId, _tenantId, ["x", "missing"])).Should().BeFalse();
    }
}
