using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using GameGuild.Identity.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests.Services;

/// <summary>
///     Tests for the permission query surface after the #330 unification: effective
///     permission checks delegate to the shared IEffectivePermissionResolver and fail
///     closed on missing/invalid user or tenant context. Layer semantics themselves are
///     covered by EffectivePermissionResolverServiceTests.
/// </summary>
public class PermissionQueryServiceTests
{
    private readonly Mock<ITenantPermissionRepository> _repoMock = new();
    private readonly Mock<ITenantMembershipChecker> _membershipMock = new();
    private readonly Mock<IEffectivePermissionResolver> _resolverMock = new();
    private readonly PermissionQueryService _sut;

    public PermissionQueryServiceTests()
    {
        _sut = new PermissionQueryService(
            _repoMock.Object,
            _membershipMock.Object,
            _resolverMock.Object,
            NullLogger<PermissionQueryService>.Instance
        );
    }

    private void SetupResolvedPermissions(Guid userId, Guid tenantId, params string[] permissions)
    {
        _resolverMock
            .Setup(x => x.ResolveAsync(
                It.Is<EffectivePermissionContext>(c => c.UserId == userId && c.TenantId == tenantId && !c.HasResource),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EffectivePermissions
            {
                UserId = userId,
                TenantId = tenantId,
                Permissions = new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase),
                Sources = permissions.ToDictionary(
                    p => p,
                    _ => PermissionSource.DirectGrant,
                    StringComparer.OrdinalIgnoreCase)
            });
    }

    // ── HasTenantPermissionAsync ──────────────────────────────

    [Fact]
    public async Task HasTenantPermissionAsync_NoEffectivePermission_ReturnsFalse()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _resolverMock
            .Setup(x => x.HasPermissionAsync(userId, tenantId, "read", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.HasTenantPermissionAsync(userId, tenantId, "read");

        result.Should().BeFalse();
        _resolverMock.Verify(
            x => x.HasPermissionAsync(userId, tenantId, "read", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HasTenantPermissionAsync_EffectivePermission_ReturnsTrue()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _resolverMock
            .Setup(x => x.HasPermissionAsync(userId, tenantId, "read", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.HasTenantPermissionAsync(userId, tenantId, "read");

        result.Should().BeTrue();
        _resolverMock.Verify(
            x => x.HasPermissionAsync(userId, tenantId, "read", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HasTenantPermissionAsync_DeniedByResolver_ReturnsFalse()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _resolverMock
            .Setup(x => x.HasPermissionAsync(userId, tenantId, "read", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.HasTenantPermissionAsync(userId, tenantId, "read");

        result.Should().BeFalse();
        _resolverMock.Verify(
            x => x.HasPermissionAsync(userId, tenantId, "read", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("00000000-0000-0000-0000-000000000000", null)]
    [InlineData(null, "00000000-0000-0000-0000-000000000000")]
    public async Task HasTenantPermissionAsync_MissingOrInvalidContext_FailsClosedWithoutResolution(string? userIdText, string? tenantIdText)
    {
        Guid? userId = userIdText is null ? null : Guid.Parse(userIdText);
        Guid? tenantId = tenantIdText is null ? null : Guid.Parse(tenantIdText);

        var result = await _sut.HasTenantPermissionAsync(userId, tenantId, "read");

        result.Should().BeFalse();
        _resolverMock.Verify(
            x => x.HasPermissionAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _resolverMock.Verify(
            x => x.ResolveAsync(It.IsAny<EffectivePermissionContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HasTenantPermissionAsync_DelegatesToSharedResolver()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _resolverMock
            .Setup(x => x.HasPermissionAsync(userId, tenantId, "courses:update", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.HasTenantPermissionAsync(userId, tenantId, "courses:update");

        result.Should().BeTrue();
        _resolverMock.Verify(
            x => x.HasPermissionAsync(userId, tenantId, "courses:update", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Deny-by-default (#327): decisions come only from the shared resolver ──
    // Inactive/expired grant filtering for decisions is the resolver's contract
    // (EffectivePermissionResolverServiceTests.ResolveAsync_InactiveGrant_Excluded,
    // ResolveAsync_ExpiredGrant_Excluded, ResolveAsync_InactiveTenantDefaults_Excluded).
    // Here we pin the delegation itself: the query service must never fall back to
    // reading raw repository rows when deciding.

    [Fact]
    public async Task HasTenantPermissionAsync_NeverReadsRepositoryRows()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _resolverMock
            .Setup(x => x.HasPermissionAsync(userId, tenantId, "read", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.HasTenantPermissionAsync(userId, tenantId, "read");

        result.Should().BeFalse();
        _repoMock.Verify(
            x => x.GetByUserAndTenantAsync(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _repoMock.Verify(
            x => x.GetByUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_NeverReadsRepositoryRows()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _resolverMock
            .Setup(x => x.ResolveAsync(
                It.Is<EffectivePermissionContext>(c => c.UserId == userId && c.TenantId == tenantId && !c.HasResource),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EffectivePermissions
            {
                UserId = userId,
                TenantId = tenantId,
                Permissions = new HashSet<string>(["active:perm"], StringComparer.OrdinalIgnoreCase),
                Sources = new Dictionary<string, PermissionSource>(StringComparer.OrdinalIgnoreCase)
                {
                    ["active:perm"] = PermissionSource.DirectGrant
                }
            });

        var result = await _sut.GetEffectivePermissionsAsync(userId, tenantId);

        result.Should().Contain("active:perm");
        _repoMock.Verify(
            x => x.GetByUserAndTenantAsync(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _repoMock.Verify(
            x => x.GetByUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetTenantPermissionsAsync_InactiveGrant_ReturnsEmpty()
    {
        var permission = new TenantPermission
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Permissions = new[] { "read" },
            IsActive = false
        };

        _repoMock
            .Setup(x => x.GetByUserAndTenantAsync(permission.UserId, permission.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permission);

        var result = await _sut.GetTenantPermissionsAsync(permission.UserId, permission.TenantId);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTenantPermissionsAsync_ExpiredGrant_ReturnsEmpty()
    {
        var permission = new TenantPermission
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Permissions = new[] { "read" },
            ExpiresAt = SystemClock.UtcNow.AddMinutes(-1)
        };

        _repoMock
            .Setup(x => x.GetByUserAndTenantAsync(permission.UserId, permission.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permission);

        var result = await _sut.GetTenantPermissionsAsync(permission.UserId, permission.TenantId);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTenantPermissionsAsync_ActiveUnexpiredGrant_ReturnsPermissions()
    {
        var permission = new TenantPermission
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Permissions = new[] { "read" },
            ExpiresAt = SystemClock.UtcNow.AddHours(1)
        };

        _repoMock
            .Setup(x => x.GetByUserAndTenantAsync(permission.UserId, permission.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permission);

        var result = await _sut.GetTenantPermissionsAsync(permission.UserId, permission.TenantId);
        result.Should().BeEquivalentTo(new[] { "read" });
    }

    [Fact]
    public async Task GetTenantDefaultPermissionsAsync_InactiveDefaults_ReturnsEmpty()
    {
        var tenantId = Guid.NewGuid();
        _repoMock
            .Setup(x => x.GetByUserAndTenantAsync(null, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantPermission { Permissions = new[] { "read" }, IsActive = false });

        var result = await _sut.GetTenantDefaultPermissionsAsync(tenantId);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTenantDefaultPermissionsAsync_ExpiredDefaults_ReturnsEmpty()
    {
        var tenantId = Guid.NewGuid();
        _repoMock
            .Setup(x => x.GetByUserAndTenantAsync(null, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantPermission
            {
                Permissions = new[] { "read" },
                ExpiresAt = SystemClock.UtcNow.AddMinutes(-1)
            });

        var result = await _sut.GetTenantDefaultPermissionsAsync(tenantId);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetGlobalDefaultPermissionsAsync_InactiveDefaults_ReturnsEmpty()
    {
        _repoMock
            .Setup(x => x.GetByUserAndTenantAsync(null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantPermission { Permissions = new[] { "read" }, IsActive = false });

        var result = await _sut.GetGlobalDefaultPermissionsAsync();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetGlobalDefaultPermissionsAsync_ExpiredDefaults_ReturnsEmpty()
    {
        _repoMock
            .Setup(x => x.GetByUserAndTenantAsync(null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantPermission
            {
                Permissions = new[] { "read" },
                ExpiresAt = SystemClock.UtcNow.AddMinutes(-1)
            });

        var result = await _sut.GetGlobalDefaultPermissionsAsync();
        result.Should().BeEmpty();
    }

    // ── GetTenantPermissionsAsync ─────────────────────────────

    [Fact]
    public async Task GetTenantPermissionsAsync_NoRecord_ReturnsEmptyList()
    {
        _repoMock
            .Setup(x => x.GetByUserAndTenantAsync(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantPermission?)null);

        var result = await _sut.GetTenantPermissionsAsync(Guid.NewGuid(), Guid.NewGuid());
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTenantPermissionsAsync_HasPermissions_ReturnsAll()
    {
        var permission = new TenantPermission
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Permissions = new[] { "read", "write", "delete" }
        };

        _repoMock
            .Setup(x => x.GetByUserAndTenantAsync(permission.UserId, permission.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permission);

        var result = await _sut.GetTenantPermissionsAsync(permission.UserId, permission.TenantId);
        result.Should().BeEquivalentTo(new[] { "read", "write", "delete" });
    }

    // ── GetEffectivePermissionsAsync ──────────────────────────

    [Fact]
    public async Task GetEffectivePermissionsAsync_NoTenantId_ReturnsEmpty_FailClosed()
    {
        var result = await _sut.GetEffectivePermissionsAsync(Guid.NewGuid(), null);

        result.Should().BeEmpty();
        _resolverMock.Verify(
            x => x.ResolveAsync(It.IsAny<EffectivePermissionContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_EmptyTenantId_ReturnsEmpty_FailClosed()
    {
        var result = await _sut.GetEffectivePermissionsAsync(Guid.NewGuid(), Guid.Empty);

        result.Should().BeEmpty();
        _resolverMock.Verify(
            x => x.ResolveAsync(It.IsAny<EffectivePermissionContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_DelegatesToSharedResolver()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        SetupResolvedPermissions(userId, tenantId, "global-perm", "tenant-perm", "user-perm");

        var result = await _sut.GetEffectivePermissionsAsync(userId, tenantId);

        result.Should().BeEquivalentTo(new[] { "global-perm", "tenant-perm", "user-perm" });
        _resolverMock.Verify(
            x => x.ResolveAsync(
                It.Is<EffectivePermissionContext>(c => c.UserId == userId && c.TenantId == tenantId && !c.HasResource),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── GetGlobalDefaultPermissionsAsync ─────────────────────

    [Fact]
    public async Task GetGlobalDefaultPermissionsAsync_NoDefaults_ReturnsEmpty()
    {
        _repoMock
            .Setup(x => x.GetByUserAndTenantAsync(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantPermission?)null);

        var result = await _sut.GetGlobalDefaultPermissionsAsync();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetGlobalDefaultPermissionsAsync_HasDefaults_ReturnsThem()
    {
        _repoMock
            .Setup(x => x.GetByUserAndTenantAsync(null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantPermission { Permissions = new[] { "perm1", "perm2" } });

        var result = await _sut.GetGlobalDefaultPermissionsAsync();
        result.Should().BeEquivalentTo(new[] { "perm1", "perm2" });
    }

    // ── GetTenantDefaultPermissionsAsync ─────────────────────

    [Fact]
    public async Task GetTenantDefaultPermissionsAsync_NoDefaults_ReturnsEmpty()
    {
        var tenantId = Guid.NewGuid();

        _repoMock
            .Setup(x => x.GetByUserAndTenantAsync(null, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantPermission?)null);

        var result = await _sut.GetTenantDefaultPermissionsAsync(tenantId);
        result.Should().BeEmpty();
    }

    // ── IsUserInTenantAsync ──────────────────────────────────

    [Fact]
    public async Task IsUserInTenantAsync_DelegatesToMembershipChecker()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        _membershipMock
            .Setup(x => x.IsUserMemberOfTenantAsync(userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.IsUserInTenantAsync(userId, tenantId);

        result.Should().BeTrue();
        _membershipMock.Verify(x => x.IsUserMemberOfTenantAsync(userId, tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
