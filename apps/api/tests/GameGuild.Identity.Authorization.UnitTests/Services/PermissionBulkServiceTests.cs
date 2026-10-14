using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using GameGuild.Identity.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests.Services;

public class PermissionBulkServiceTests
{
    private readonly Mock<IPermissionGrantService> _grantServiceMock = new();
    private readonly Mock<IPermissionQueryService> _queryServiceMock = new();
    private readonly PermissionBulkService _sut;

    public PermissionBulkServiceTests()
    {
        _sut = new PermissionBulkService(
            _grantServiceMock.Object,
            _queryServiceMock.Object,
            NullLogger<PermissionBulkService>.Instance
        );
    }

    // ── BulkGrantTenantPermissionAsync ────────────────────────

    [Fact]
    public async Task BulkGrantTenantPermissionAsync_GrantsToAllUsers()
    {
        var tenantId = Guid.NewGuid();
        var userIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var permissions = new[] { "read", "write" };

        _grantServiceMock
            .Setup(x => x.GrantTenantPermissionAsync(
                It.IsAny<Guid?>(), tenantId, permissions, It.IsAny<Guid?>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid? uid, Guid? tid, string[] p, Guid? _, DateTime? _, string? _, CancellationToken _) =>
                new TenantPermission { UserId = uid, TenantId = tid, Permissions = p });

        var results = await _sut.BulkGrantTenantPermissionAsync(userIds, tenantId, permissions);

        results.Should().HaveCount(3);
        _grantServiceMock.Verify(
            x => x.GrantTenantPermissionAsync(It.IsAny<Guid?>(), tenantId, permissions, It.IsAny<Guid?>(), null, null, It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task BulkGrantTenantPermissionAsync_EmptyUserIds_ReturnsEmptyList()
    {
        var results = await _sut.BulkGrantTenantPermissionAsync(Array.Empty<Guid>(), Guid.NewGuid(), new[] { "read" });
        results.Should().BeEmpty();
    }

    // ── JoinTenantAsync ──────────────────────────────────────

    [Fact]
    public async Task JoinTenantAsync_FetchesDefaultPermissionsAndGrants()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var defaults = new List<string> { "read", "comment" };

        _queryServiceMock
            .Setup(x => x.GetTenantDefaultPermissionsAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(defaults);

        _grantServiceMock
            .Setup(x => x.GrantTenantPermissionAsync(userId, tenantId, It.IsAny<string[]>(), It.IsAny<Guid?>(), null, "User joined tenant", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantPermission { UserId = userId, TenantId = tenantId, Permissions = defaults.ToArray() });

        var result = await _sut.JoinTenantAsync(userId, tenantId);

        result.Should().NotBeNull();
        _queryServiceMock.Verify(x => x.GetTenantDefaultPermissionsAsync(tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── LeaveTenantAsync ─────────────────────────────────────

    [Fact]
    public async Task LeaveTenantAsync_RevokesAllUserPermissions()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var userPermissions = new List<string> { "read", "write" };

        _queryServiceMock
            .Setup(x => x.GetTenantPermissionsAsync(userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userPermissions);

        _grantServiceMock
            .Setup(x => x.RevokeTenantPermissionAsync(userId, tenantId, It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.LeaveTenantAsync(userId, tenantId);

        result.Should().BeTrue();
        _grantServiceMock.Verify(
            x => x.RevokeTenantPermissionAsync(userId, tenantId, It.Is<string[]>(p => p.Contains("read") && p.Contains("write")), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
