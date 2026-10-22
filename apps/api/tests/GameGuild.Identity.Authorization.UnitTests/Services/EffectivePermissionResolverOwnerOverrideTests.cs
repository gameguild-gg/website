using FluentAssertions;
using GameGuild.Configuration.PresentationLayer.Authorization;
using GameGuild.CQRS.Models;
using GameGuild.Identity.Authorization.UnitTests.TestSupport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace GameGuild.Identity.Authorization.UnitTests.Services;

/// <summary>
///     Owner-override tests for the effective-permission resolution engine (issue #372):
///     the allow/deny precedence matrix per <c>Authorization:OwnerOverride</c> mode
///     (fail-closed on unknown modes), temporal precedence (more restrictive expiry
///     wins, issue #363), context isolation, DAC resource-layer parity, the debug-level
///     override-use audit and the wildcard exclusions.
///     Contract: apps/api/docs/effective-permission-resolution.md (Owner override).
/// </summary>
public class EffectivePermissionResolverOwnerOverrideTests
{
    private static readonly Guid SystemAccountId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly Mock<ITenantPermissionRepository> _repository = new();
    private readonly Mock<IRbacPermissionResolver> _rbacResolver = new();
    private readonly Mock<IAuthorizationRolePermissionProvider> _roleProvider = new();
    private readonly Mock<IResourcePermissionService> _resourceService = new();
    private readonly Mock<IResourceOwnershipProvider> _ownershipProvider = new();
    private readonly SpyLogger<EffectivePermissionResolverService> _logger = new();
    private readonly SpyLogger<DacPermissionResolver> _dacLogger = new();

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();
    private const string ResourceType = "Project";
    private const string ResourceId = "p-1";

    private EffectivePermissionResolverService CreateSut(string mode)
    {
        var options = Options.Create(
            new AuthorizationOptions
            {
                SystemAccountId = SystemAccountId,
                OwnerOverride = new OwnerOverrideOptions
                {
                    Mode = mode
                }
            });

        return new EffectivePermissionResolverService(
            _repository.Object,
            _rbacResolver.Object,
            [_roleProvider.Object],
            _resourceService.Object,
            options,
            _logger,
            ownershipProvider: _ownershipProvider.Object);
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
        _resourceService
            .Setup(s => s.GetUserResourcesAsync(It.IsAny<TenantId>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ResourceUserPermission>());
    }

    private void SetupTenantDeny(params string[] denied)
        => _repository
            .Setup(r => r.GetByUserAndTenantAsync(_userId, (Guid?)_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantPermission
            {
                UserId = _userId,
                TenantId = _tenantId,
                Permissions = [],
                DenyPermissions = denied
            });

    private void SetupOwnerRow(ResourceUserPermission row)
    {
        _resourceService
            .Setup(s => s.GetUserResourcesAsync(It.IsAny<TenantId>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ResourceUserPermission> { row });
        _ownershipProvider
            .Setup(p => p.GetOwnerAsync(_tenantId, _userId, ResourceType, ResourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceOwnership(
                row.UserId, _tenantId, ResourceType, ResourceId, row.Permissions, row.ExpiresAt, row.Id));
    }

    private static ResourceUserPermission OwnerRow(
        string[] permissions,
        DateTime? expiresAt = null,
        DateTime? revokedAt = null)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = new TenantId(),
            UserId = Guid.NewGuid(),
            ResourceType = ResourceType,
            ResourceId = ResourceId,
            Permissions = permissions,
            GrantedByUserId = Guid.NewGuid(),
            ExpiresAt = expiresAt,
            RevokedAt = revokedAt
        };

    // ── Precedence matrix: mode × explicit deny ───────────────────────────

    [Fact]
    public async Task ResolveAsync_OffMode_MintsNoOwnerPermissions()
    {
        SetupNoData();
        var owner = OwnerRow(["read"]);
        owner.UserId = _userId;
        SetupOwnerRow(owner);

        var result = await CreateSut("Off").ResolveAsync(
            EffectivePermissionContext.ForResource(_userId, _tenantId, ResourceType, ResourceId));

        // The owner row still contributes through the regular resource-grant layer (plain
        // name), but no override happens: no qualified name, no Owner attribution, no metadata.
        result.Permissions.Should().Contain("read");
        result.Permissions.Should().NotContain($"{ResourceType}.{ResourceId}.read");
        result.Sources["read"].Should().Be(PermissionSource.ResourceGrant);
        result.OwnerOverride.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_AllowUnlessDenied_OwnerMintsTenantAndResourceLayerNames()
    {
        SetupNoData();
        var owner = OwnerRow(["read", "write"]);
        owner.UserId = _userId;
        SetupOwnerRow(owner);

        var result = await CreateSut("AllowUnlessDenied").ResolveAsync(
            EffectivePermissionContext.ForResource(_userId, _tenantId, ResourceType, ResourceId));

        result.Permissions.Should().Contain("read").And.Contain("write");
        result.Permissions.Should().Contain($"{ResourceType}.{ResourceId}.read")
            .And.Contain($"{ResourceType}.{ResourceId}.write");
        result.Sources[$"{ResourceType}.{ResourceId}.read"].Should().Be(PermissionSource.Owner);
        result.OwnerOverride.Should().NotBeNull();
        result.OwnerOverride!.Mode.Should().Be(OwnerOverrideMode.AllowUnlessDenied);
        result.OwnerOverride.OwnerUserId.Should().Be(_userId);
        result.OwnerOverride.EffectiveThroughUtc.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_AllowUnlessDenied_ExplicitDenyWinsOverOwner_FailClosed()
    {
        SetupNoData();
        var owner = OwnerRow(["read"]);
        owner.UserId = _userId;
        SetupOwnerRow(owner);
        SetupTenantDeny("read", $"{ResourceType}.{ResourceId}.read");

        var result = await CreateSut("AllowUnlessDenied").ResolveAsync(
            EffectivePermissionContext.ForResource(_userId, _tenantId, ResourceType, ResourceId));

        result.Permissions.Should().NotContain("read");
        result.Permissions.Should().NotContain($"{ResourceType}.{ResourceId}.read");
        result.Permissions.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveAsync_AlwaysAllow_OwnerMintedGrantsSurviveExplicitDeny()
    {
        SetupNoData();
        var owner = OwnerRow(["read"]);
        owner.UserId = _userId;
        SetupOwnerRow(owner);
        SetupTenantDeny("read", $"{ResourceType}.{ResourceId}.read");

        var result = await CreateSut("AlwaysAllow").ResolveAsync(
            EffectivePermissionContext.ForResource(_userId, _tenantId, ResourceType, ResourceId));

        result.Permissions.Should().Contain("read")
            .And.Contain($"{ResourceType}.{ResourceId}.read");
        result.OwnerOverride!.Mode.Should().Be(OwnerOverrideMode.AlwaysAllow);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\r")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\t")]
    [InlineData("\0")]
    [InlineData("\u001b")]
    [InlineData("\u0085")]
    [InlineData("\u2028")]
    [InlineData("\u2029")]
    public async Task ResolveAsync_AlwaysAllow_SanitizesDenyWarningsWithoutChangingPermissions(string separator)
    {
        SetupNoData();
        var permission = $"read{separator}forged";
        var qualifiedPermission = $"{ResourceType}.{ResourceId}.{permission}";
        var owner = OwnerRow([permission]);
        owner.UserId = _userId;
        SetupOwnerRow(owner);
        SetupTenantDeny(permission, qualifiedPermission);

        var result = await CreateSut("AlwaysAllow").ResolveAsync(
            EffectivePermissionContext.ForResource(_userId, _tenantId, ResourceType, ResourceId));

        result.Permissions.Should().BeEquivalentTo(new[] { permission, qualifiedPermission });
        result.OwnerOverride!.Mode.Should().Be(OwnerOverrideMode.AlwaysAllow);
        var safePermission = $"read{new string('\u2400', separator.Length)}forged";
        var safeQualifiedPermission = $"{ResourceType}.{ResourceId}.{safePermission}";
        var warnings = _logger.Entries
            .Where(entry => entry.Level == LogLevel.Warning)
            .Select(entry => entry.Message)
            .ToArray();
        warnings.Should().BeEquivalentTo(new[]
        {
            $"Attempted to deny owner-minted permission {safePermission} - the AlwaysAllow owner-override mode makes owner grants non-deniable",
            $"Attempted to deny owner-minted permission {safeQualifiedPermission} - the AlwaysAllow owner-override mode makes owner grants non-deniable"
        });
    }

    [Fact]
    public async Task ResolveAsync_StaticDenyWarning_PreservesWildcardGrantAndOrdinaryDiagnostic()
    {
        SetupNoData();
        _repository
            .Setup(repository => repository.GetByUserAndTenantAsync(
                (Guid?)SystemAccountId, (Guid?)_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantPermission
            {
                UserId = SystemAccountId,
                TenantId = _tenantId,
                Permissions = [],
                DenyPermissions = ["*"]
            });

        var result = await CreateSut("AlwaysAllow").ResolveAsync(
            EffectivePermissionContext.ForTenant(SystemAccountId, _tenantId));

        result.Permissions.Should().BeEquivalentTo(new[] { "*" });
        result.Sources["*"].Should().Be(PermissionSource.Static);
        _logger.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Warning
            && entry.Message == $"Attempted to deny static permission * for user {SystemAccountId} - denies cannot override static grants");
    }

    [Fact]
    public async Task ResolveAsync_UnknownMode_FailsClosedToOff_AndWarns()
    {
        SetupNoData();
        var owner = OwnerRow(["read"]);
        owner.UserId = _userId;
        SetupOwnerRow(owner);

        var result = await CreateSut("YoloAllow").ResolveAsync(
            EffectivePermissionContext.ForResource(_userId, _tenantId, ResourceType, ResourceId));

        result.Permissions.Should().NotContain($"{ResourceType}.{ResourceId}.read");
        result.OwnerOverride.Should().BeNull();
        _logger.Entries.Should().Contain(e =>
            e.Level == LogLevel.Warning && e.Message.Contains("fails closed to Off"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("sometimes")]
    [InlineData("AllowUnlessDenied ")]
    public async Task ResolveAsync_UnparseableModes_AllFailClosedToOff(string mode)
    {
        SetupNoData();
        var owner = OwnerRow(["read"]);
        owner.UserId = _userId;
        SetupOwnerRow(owner);

        var result = await CreateSut(mode).ResolveAsync(
            EffectivePermissionContext.ForResource(_userId, _tenantId, ResourceType, ResourceId));

        result.OwnerOverride.Should().BeNull();
        result.Permissions.Should().NotContain($"{ResourceType}.{ResourceId}.read");
    }

    [Fact]
    public async Task ResolveAsync_DefaultOptions_AreAllowUnlessDenied()
    {
        var options = new OwnerOverrideOptions();
        options.Mode.Should().Be("AllowUnlessDenied");
        options.ResolveMode().Should().Be(OwnerOverrideMode.AllowUnlessDenied);
    }

    // ── Ownership state gating ─────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_RevokedOwnership_NoOverride()
    {
        SetupNoData();
        _resourceService
            .Setup(s => s.GetUserResourcesAsync(It.IsAny<TenantId>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ResourceUserPermission>());
        // A revoked owner row is never surfaced by the ownership provider.
        _ownershipProvider
            .Setup(p => p.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResourceOwnership?)null);

        var result = await CreateSut("AllowUnlessDenied").ResolveAsync(
            EffectivePermissionContext.ForResource(_userId, _tenantId, ResourceType, ResourceId));

        result.OwnerOverride.Should().BeNull();
        result.Permissions.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveAsync_TenantContextWithoutResource_NeverConsultsOwnership()
    {
        SetupNoData();

        var result = await CreateSut("AlwaysAllow").ResolveAsync(
            EffectivePermissionContext.ForTenant(_userId, _tenantId));

        result.OwnerOverride.Should().BeNull();
        _ownershipProvider.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ResolveAsync_OwnerPermissions_NeverMintWildcards()
    {
        // The wildcard-bearing permission set is exposed ONLY through the ownership
        // designation: routing it through a resource-grant row as well would make the
        // pre-existing resource-grant layer mint the wildcards, which is not the
        // behavior under test (the owner-override layer must never mint them).
        SetupNoData();
        _ownershipProvider
            .Setup(p => p.GetOwnerAsync(_tenantId, _userId, ResourceType, ResourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceOwnership(
                _userId, _tenantId, ResourceType, ResourceId, ["read", "admin:*", "*"], null, Guid.NewGuid()));

        var result = await CreateSut("AlwaysAllow").ResolveAsync(
            EffectivePermissionContext.ForResource(_userId, _tenantId, ResourceType, ResourceId));

        result.Permissions.Should().NotContain("admin:*").And.NotContain("*");
        result.Permissions.Should().Contain("read");
        result.OwnerOverride!.GrantedPermissions.Should().NotContain("admin:*").And.NotContain("*");
    }

    // ── Temporal precedence (#363): more restrictive expiry wins ───────────

    [Fact]
    public async Task ResolveAsync_ExpiredOwnershipDesignation_AppliesNoOverride_FailClosed()
    {
        SetupNoData();
        var owner = OwnerRow(["read"], expiresAt: SystemClock.UtcNow.AddHours(-1));
        // Expired rows do not contribute through the resource-grant layer either.
        _resourceService
            .Setup(s => s.GetUserResourcesAsync(It.IsAny<TenantId>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ResourceUserPermission> { owner });
        _ownershipProvider
            .Setup(p => p.GetOwnerAsync(_tenantId, _userId, ResourceType, ResourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceOwnership(
                _userId, _tenantId, ResourceType, ResourceId, owner.Permissions, owner.ExpiresAt, owner.Id));

        var result = await CreateSut("AlwaysAllow").ResolveAsync(
            EffectivePermissionContext.ForResource(_userId, _tenantId, ResourceType, ResourceId));

        result.OwnerOverride.Should().BeNull();
        result.Permissions.Should().BeEmpty("an expired time-bound ownership must fail closed");
    }

    [Fact]
    public async Task ResolveAsync_TimeBoundOwnership_ReportsItsOwnExpiryAsEffectiveBound()
    {
        SetupNoData();
        var expiry = SystemClock.UtcNow.AddHours(6);
        var owner = OwnerRow(["read"], expiresAt: expiry);
        owner.UserId = _userId;
        SetupOwnerRow(owner);

        var result = await CreateSut("AllowUnlessDenied").ResolveAsync(
            EffectivePermissionContext.ForResource(_userId, _tenantId, ResourceType, ResourceId));

        result.OwnerOverride!.EffectiveThroughUtc.Should().Be(expiry);
    }

    [Fact]
    public async Task ResolveAsync_ConflictingTimeBoundGrant_earlierExpiryWins()
    {
        SetupNoData();
        var ownerExpiry = SystemClock.UtcNow.AddHours(12);
        var grantExpiry = SystemClock.UtcNow.AddHours(2);
        var owner = OwnerRow(["read", "write"], expiresAt: ownerExpiry);
        owner.UserId = _userId;
        var conflictingGrant = new ResourceUserPermission
        {
            Id = Guid.NewGuid(),
            TenantId = new TenantId(),
            UserId = _userId,
            ResourceType = ResourceType,
            ResourceId = ResourceId,
            Permissions = ["read"],
            GrantedByUserId = Guid.NewGuid(),
            ExpiresAt = grantExpiry
        };
        _resourceService
            .Setup(s => s.GetUserResourcesAsync(It.IsAny<TenantId>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ResourceUserPermission> { owner, conflictingGrant });
        _ownershipProvider
            .Setup(p => p.GetOwnerAsync(_tenantId, _userId, ResourceType, ResourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceOwnership(
                _userId, _tenantId, ResourceType, ResourceId, owner.Permissions, owner.ExpiresAt, owner.Id));

        var result = await CreateSut("AllowUnlessDenied").ResolveAsync(
            EffectivePermissionContext.ForResource(_userId, _tenantId, ResourceType, ResourceId));

        // Both grants are in force right now, but the more restrictive expiry (the
        // conflicting time-bound grant) bounds the override.
        result.OwnerOverride!.EffectiveThroughUtc.Should().Be(grantExpiry);
        result.Permissions.Should().Contain("read").And.Contain("write");
    }

    [Fact]
    public async Task ResolveAsync_IndefiniteOwner_CappedByEarlierTimeBoundGrantExpiry()
    {
        SetupNoData();
        var grantExpiry = SystemClock.UtcNow.AddMinutes(30);
        var owner = OwnerRow(["read"]); // indefinite ownership (ExpiresAt = null)
        owner.UserId = _userId;
        var timeBoundGrant = new ResourceUserPermission
        {
            Id = Guid.NewGuid(),
            TenantId = new TenantId(),
            UserId = _userId,
            ResourceType = ResourceType,
            ResourceId = ResourceId,
            Permissions = ["read"],
            GrantedByUserId = Guid.NewGuid(),
            ExpiresAt = grantExpiry
        };
        _resourceService
            .Setup(s => s.GetUserResourcesAsync(It.IsAny<TenantId>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ResourceUserPermission> { owner, timeBoundGrant });
        _ownershipProvider
            .Setup(p => p.GetOwnerAsync(_tenantId, _userId, ResourceType, ResourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceOwnership(
                _userId, _tenantId, ResourceType, ResourceId, owner.Permissions, owner.ExpiresAt, owner.Id));

        var result = await CreateSut("AllowUnlessDenied").ResolveAsync(
            EffectivePermissionContext.ForResource(_userId, _tenantId, ResourceType, ResourceId));

        result.OwnerOverride!.EffectiveThroughUtc.Should().Be(grantExpiry,
            "the more restrictive (earlier) expiry wins over the indefinite owner override");
    }

    [Fact]
    public async Task ResolveAsync_NoTimeBoundConflict_EffectiveBoundIsIndefinite()
    {
        SetupNoData();
        var owner = OwnerRow(["read"]);
        owner.UserId = _userId;
        SetupOwnerRow(owner);

        var result = await CreateSut("AllowUnlessDenied").ResolveAsync(
            EffectivePermissionContext.ForResource(_userId, _tenantId, ResourceType, ResourceId));

        result.OwnerOverride!.EffectiveThroughUtc.Should().BeNull();
    }

    // ── DAC resource-layer parity ──────────────────────────────────────────

    [Fact]
    public async Task DacPermissionResolver_ResourceLayerCheck_SucceedsForOwner()
    {
        SetupNoData();
        var owner = OwnerRow(["read", "write"]);
        owner.UserId = _userId;
        SetupOwnerRow(owner);

        var resolver = new DacPermissionResolver(CreateSut("AllowUnlessDenied"), _dacLogger);
        var query = new DacPermissionQuery
        {
            UserId = _userId,
            TenantId = _tenantId,
            ResourceType = ResourceType,
            ResourceId = ResourceId,
            Layer = PermissionLayer.Resource
        };

        (await resolver.HasPermissionAsync(query, "read")).Should().BeTrue();
        (await resolver.HasPermissionAsync(query, "write")).Should().BeTrue();
        (await resolver.HasPermissionAsync(query, "delete")).Should().BeFalse();
    }

    [Fact]
    public async Task DacPermissionResolver_ResourceLayerCheck_OffMode_FailsClosed()
    {
        SetupNoData();
        var owner = OwnerRow(["read"]);
        owner.UserId = _userId;
        SetupOwnerRow(owner);

        var resolver = new DacPermissionResolver(CreateSut("Off"), _dacLogger);
        var query = new DacPermissionQuery
        {
            UserId = _userId,
            TenantId = _tenantId,
            ResourceType = ResourceType,
            ResourceId = ResourceId,
            Layer = PermissionLayer.Resource
        };

        (await resolver.HasPermissionAsync(query, "read")).Should().BeFalse();
    }

    // ── Override-use audit (debug level, documented) ───────────────────────

    [Fact]
    public async Task ResolveAsync_OwnerOverrideUse_IsAuditedAtDebugLevel()
    {
        SetupNoData();
        var owner = OwnerRow(["read"]);
        owner.UserId = _userId;
        SetupOwnerRow(owner);

        await CreateSut("AllowUnlessDenied").ResolveAsync(
            EffectivePermissionContext.ForResource(_userId, _tenantId, ResourceType, ResourceId));

        _logger.Entries.Should().Contain(e =>
            e.Level == LogLevel.Debug && e.Message.Contains("Owner override applied"));
        // No durable audit noise: nothing above Debug/Warning was emitted for the use itself.
        _logger.Entries.Should().NotContain(e => e.Level == LogLevel.Information);
    }
}
