using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests;

// ============================================================================
// Behavioral tests for Just-in-Time elevation enforcement (#341):
// time-bound permission grants that (a) require approval by a different user,
// (b) actually grant the elevated permission while in force, and (c) confer
// nothing outside their time window or once revoked/denied/expired.
// ============================================================================

public class JitElevationEntityTests
{
    private static JitElevationRequest NewRequest(
        ElevationRequestStatus status,
        DateTime? startsAt = null,
        int durationMinutes = 30
    )
    {
        var start = startsAt ?? DateTime.UtcNow.AddMinutes(-1);
        return new JitElevationRequest
        {
            RequesterId = Guid.NewGuid(),
            Permission = "billing:export",
            StartsAt = start,
            ExpiresAt = start.AddMinutes(durationMinutes),
            Status = status
        };
    }

    [Fact]
    public void Approve_SelfApproval_Throws()
    {
        var request = NewRequest(ElevationRequestStatus.Pending);
        var requester = request.RequesterId;

        var act = () => request.Approve(requester, "own request");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*self-approval*", "elevation requires approval by a different user");
        request.Status.Should().Be(ElevationRequestStatus.Pending);
    }

    [Fact]
    public void Approve_ByOtherUser_Succeeds()
    {
        var request = NewRequest(ElevationRequestStatus.Pending);

        request.Approve(Guid.NewGuid(), "approved");

        request.Status.Should().Be(ElevationRequestStatus.Active);
        request.ReviewerId.Should().NotBeNull();
    }

    [Theory]
    [InlineData(ElevationRequestStatus.Pending, false)]
    [InlineData(ElevationRequestStatus.Denied, false)]
    [InlineData(ElevationRequestStatus.Revoked, false)]
    [InlineData(ElevationRequestStatus.Expired, false)]
    public void IsGrantInForce_NonGrantingStatuses_ReturnFalse(ElevationRequestStatus status, bool expected)
    {
        var request = NewRequest(status);

        request.IsGrantInForce().Should().Be(expected);
    }

    [Fact]
    public void IsGrantInForce_ActiveWithinWindow_ReturnsTrue()
    {
        var request = NewRequest(ElevationRequestStatus.Active);

        request.IsGrantInForce().Should().BeTrue();
    }

    [Fact]
    public void IsGrantInForce_ActivePastExpiry_ReturnsFalse()
    {
        var request = NewRequest(ElevationRequestStatus.Active, startsAt: DateTime.UtcNow.AddHours(-2), durationMinutes: 30);

        request.IsGrantInForce().Should().BeFalse();
    }

    [Fact]
    public void IsGrantInForce_ApprovedWithFutureStart_ReturnsFalse()
    {
        var request = NewRequest(ElevationRequestStatus.Approved, startsAt: DateTime.UtcNow.AddMinutes(10));

        request.IsGrantInForce().Should().BeFalse();
    }

    [Fact]
    public void IsGrantInForce_ApprovedWithArrivedStart_ReturnsTrue()
    {
        // Approved with a start time that has arrived counts as in force (lazy
        // window entry) — approval grants the window, no separate activation step.
        var request = NewRequest(ElevationRequestStatus.Approved, startsAt: DateTime.UtcNow.AddMinutes(-1));

        request.IsGrantInForce().Should().BeTrue();
    }

    [Fact]
    public void MarkExpired_ApprovedPastExpiry_IsMarkedExpired()
    {
        var request = NewRequest(ElevationRequestStatus.Approved, startsAt: DateTime.UtcNow.AddHours(-2), durationMinutes: 30);

        request.MarkExpired();

        request.Status.Should().Be(ElevationRequestStatus.Expired);
    }

    [Fact]
    public void MarkExpired_ActivePastExpiry_IsMarkedExpired()
    {
        var request = NewRequest(ElevationRequestStatus.Active, startsAt: DateTime.UtcNow.AddHours(-2), durationMinutes: 30);

        request.MarkExpired();

        request.Status.Should().Be(ElevationRequestStatus.Expired);
    }
}

public class JitElevationServiceEnforcementTests
{
    private readonly Mock<IJitElevationRequestRepository> _repoMock = new();
    private readonly Mock<IPermissionAuditService> _auditMock = new();
    private readonly Mock<ITenantSecurityVersionStore> _versionStoreMock = new();
    private readonly JitElevationService _sut;

    public JitElevationServiceEnforcementTests()
    {
        _auditMock
            .Setup(a => a.LogPermissionChangeAsync(
                It.IsAny<PermissionOperationType>(), It.IsAny<Guid?>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PermissionAuditLog());
        _versionStoreMock
            .Setup(v => v.IncrementVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2L);
        _sut = new JitElevationService(
            _repoMock.Object, _auditMock.Object, _versionStoreMock.Object, NullLogger<JitElevationService>.Instance);
    }

    private static JitElevationRequest PendingRequest(Guid? tenantId = null)
    {
        var start = DateTime.UtcNow.AddMinutes(-1);
        return new JitElevationRequest
        {
            RequesterId = Guid.NewGuid(),
            TenantId = tenantId,
            Permission = "billing:export",
            StartsAt = start,
            ExpiresAt = start.AddMinutes(30),
            Status = ElevationRequestStatus.Pending
        };
    }

    [Fact]
    public async Task ApproveRequestAsync_SelfApproval_ThrowsAndPersistsNothing()
    {
        var request = PendingRequest();
        _repoMock
            .Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var act = () => _sut.ApproveRequestAsync(request.Id, request.RequesterId, "self approve");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*self-approval*");
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<JitElevationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _auditMock.Verify(a => a.LogPermissionChangeAsync(
            It.IsAny<PermissionOperationType>(), It.IsAny<Guid?>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
            It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        _versionStoreMock.Verify(v => v.IncrementVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApproveRequestAsync_ByOtherUser_ActivatesAuditsAndBumpsSecurityVersion()
    {
        var tenantId = Guid.NewGuid();
        var request = PendingRequest(tenantId);
        var reviewerId = Guid.NewGuid();
        _repoMock
            .Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var result = await _sut.ApproveRequestAsync(request.Id, reviewerId, "approved");

        result.Status.Should().Be(ElevationRequestStatus.Active);
        _versionStoreMock.Verify(
            v => v.IncrementVersionAsync(tenantId.ToString(), It.IsAny<CancellationToken>()),
            Times.Once,
            "approval activates a permission mutation and must invalidate cached permission views");
        _auditMock.Verify(a => a.LogPermissionChangeAsync(
            PermissionOperationType.Review,
            request.RequesterId,
            reviewerId,
            tenantId,
            "billing:export",
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<bool>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DenyRequestAsync_AuditsDenial()
    {
        var request = PendingRequest();
        var reviewerId = Guid.NewGuid();
        _repoMock
            .Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var result = await _sut.DenyRequestAsync(request.Id, reviewerId, "not justified");

        result.Status.Should().Be(ElevationRequestStatus.Denied);
        _auditMock.Verify(a => a.LogPermissionChangeAsync(
            PermissionOperationType.Deny,
            request.RequesterId,
            reviewerId,
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            "not justified",
            It.IsAny<bool>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RevokeElevationAsync_AuditsAndBumpsSecurityVersion()
    {
        var tenantId = Guid.NewGuid();
        var request = PendingRequest(tenantId);
        request.Approve(Guid.NewGuid());
        var revokedBy = Guid.NewGuid();
        _repoMock
            .Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var result = await _sut.RevokeElevationAsync(request.Id, revokedBy, "incident");

        result.Should().BeTrue();
        request.Status.Should().Be(ElevationRequestStatus.Revoked);
        _versionStoreMock.Verify(
            v => v.IncrementVersionAsync(tenantId.ToString(), It.IsAny<CancellationToken>()),
            Times.Once,
            "revocation removes an in-force grant and must invalidate cached permission views");
        _auditMock.Verify(a => a.LogPermissionChangeAsync(
            PermissionOperationType.Revoke,
            request.RequesterId,
            revokedBy,
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            "incident",
            It.IsAny<bool>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HasActiveElevationAsync_ApprovedElevationWithArrivedStart_ReturnsTrue()
    {
        var userId = Guid.NewGuid();
        var elevation = new JitElevationRequest
        {
            RequesterId = userId,
            Permission = "billing:export",
            StartsAt = DateTime.UtcNow.AddMinutes(-1),
            ExpiresAt = DateTime.UtcNow.AddMinutes(29),
            Status = ElevationRequestStatus.Approved
        };
        _repoMock
            .Setup(r => r.GetActiveByUserAsync(userId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([elevation]);

        var result = await _sut.HasActiveElevationAsync(userId, "billing:export", null);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasActiveElevationAsync_ExpiredElevation_ReturnsFalse()
    {
        var userId = Guid.NewGuid();
        var elevation = new JitElevationRequest
        {
            RequesterId = userId,
            Permission = "billing:export",
            StartsAt = DateTime.UtcNow.AddHours(-2),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-30),
            Status = ElevationRequestStatus.Active
        };
        _repoMock
            .Setup(r => r.GetActiveByUserAsync(userId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([elevation]);

        var result = await _sut.HasActiveElevationAsync(userId, "billing:export", null);

        result.Should().BeFalse();
    }
}

public class JitElevationPermissionEvaluationTests
{
    private readonly Mock<ITenantPermissionRepository> _repoMock = new();
    private readonly Mock<ITenantMembershipChecker> _membershipMock = new();
    private readonly Mock<IJitElevationRequestRepository> _jitMock = new();
    private readonly PermissionQueryService _sut;

    public JitElevationPermissionEvaluationTests()
    {
        _sut = new PermissionQueryService(
            _repoMock.Object,
            _membershipMock.Object,
            NullLogger<PermissionQueryService>.Instance,
            rolePermissionProviders: null,
            jitElevationRepository: _jitMock.Object);
    }

    private void SetupNoBaseGrants(Guid userId)
    {
        _repoMock
            .Setup(r => r.GetByUserAndTenantAsync(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantPermission?)null);
        _repoMock
            .Setup(r => r.GetByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantPermission>());
    }

    private static JitElevationRequest Elevation(
        Guid userId,
        string permission,
        ElevationRequestStatus status = ElevationRequestStatus.Active,
        Guid? resourceId = null,
        DateTime? startsAt = null,
        int durationMinutes = 30)
    {
        var start = startsAt ?? DateTime.UtcNow.AddMinutes(-1);
        return new JitElevationRequest
        {
            RequesterId = userId,
            Permission = permission,
            ResourceId = resourceId,
            StartsAt = start,
            ExpiresAt = start.AddMinutes(durationMinutes),
            Status = status
        };
    }

    // ── GetEffectivePermissionsAsync ─────────────────────────

    [Fact]
    public async Task GetEffectivePermissionsAsync_ActiveJitElevation_GrantsPermission()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        SetupNoBaseGrants(userId);
        _jitMock
            .Setup(j => j.GetActiveByUserAsync(userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Elevation(userId, "billing:export")]);

        var result = await _sut.GetEffectivePermissionsAsync(userId, tenantId);

        result.Should().Contain("billing:export",
            "an approved elevation inside its time window temporarily grants the permission");
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_ExpiredElevation_ConfersNothing()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        SetupNoBaseGrants(userId);
        _jitMock
            .Setup(j => j.GetActiveByUserAsync(userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Elevation(userId, "billing:export", startsAt: DateTime.UtcNow.AddHours(-2), durationMinutes: 30)]);

        var result = await _sut.GetEffectivePermissionsAsync(userId, tenantId);

        result.Should().NotContain("billing:export",
            "outside its time window an elevation confers nothing (time-bound grant)");
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_RevokedElevation_ConfersNothing()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        SetupNoBaseGrants(userId);
        _jitMock
            .Setup(j => j.GetActiveByUserAsync(userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Elevation(userId, "billing:export", status: ElevationRequestStatus.Revoked)]);

        var result = await _sut.GetEffectivePermissionsAsync(userId, tenantId);

        result.Should().NotContain("billing:export");
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_DenyWinsOverJitElevation()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        SetupNoBaseGrants(userId);
        _repoMock
            .Setup(r => r.GetByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantPermission>
            {
                new() { UserId = userId, TenantId = tenantId, Permissions = [], DenyPermissions = ["billing:export"] }
            });
        _jitMock
            .Setup(j => j.GetActiveByUserAsync(userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Elevation(userId, "billing:export")]);

        var result = await _sut.GetEffectivePermissionsAsync(userId, tenantId);

        result.Should().NotContain("billing:export",
            "explicit denies keep DENY-WINS precedence over temporary elevation grants");
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_ResourceScopedElevation_IsIgnoredForTenantLevelSet()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        SetupNoBaseGrants(userId);
        _jitMock
            .Setup(j => j.GetActiveByUserAsync(userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Elevation(userId, "billing:export", resourceId: Guid.NewGuid())]);

        var result = await _sut.GetEffectivePermissionsAsync(userId, tenantId);

        result.Should().NotContain("billing:export",
            "a resource-scoped elevation must not grant tenant-wide capability");
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_AdminWildcardElevation_IsIgnored()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        SetupNoBaseGrants(userId);
        _jitMock
            .Setup(j => j.GetActiveByUserAsync(userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Elevation(userId, "admin:*")]);

        var result = await _sut.GetEffectivePermissionsAsync(userId, tenantId);

        result.Should().NotContain("admin:*",
            "the universal wildcard is never grantable through elevation");
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_WithoutJitRepository_StillResolvesBaseLayers()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _repoMock
            .Setup(r => r.GetByUserAndTenantAsync(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantPermission?)null);
        _repoMock
            .Setup(r => r.GetByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantPermission>
            {
                new() { UserId = userId, TenantId = tenantId, Permissions = ["user-perm"], DenyPermissions = [] }
            });
        var sut = new PermissionQueryService(
            _repoMock.Object,
            _membershipMock.Object,
            NullLogger<PermissionQueryService>.Instance);

        var result = await sut.GetEffectivePermissionsAsync(userId, tenantId);

        result.Should().Contain("user-perm").And.HaveCount(1);
    }

    // ── HasTenantPermissionAsync ─────────────────────────────

    [Fact]
    public async Task HasTenantPermissionAsync_ActiveJitElevation_ReturnsTrue()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _repoMock
            .Setup(r => r.GetByUserAndTenantAsync(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantPermission?)null);
        _jitMock
            .Setup(j => j.GetActiveByUserAsync(userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Elevation(userId, "billing:export")]);

        var result = await _sut.HasTenantPermissionAsync(userId, tenantId, "billing:export");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasTenantPermissionAsync_ExpiredElevation_ReturnsFalse()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _repoMock
            .Setup(r => r.GetByUserAndTenantAsync(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantPermission?)null);
        _jitMock
            .Setup(j => j.GetActiveByUserAsync(userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Elevation(userId, "billing:export", startsAt: DateTime.UtcNow.AddHours(-2), durationMinutes: 30)]);

        var result = await _sut.HasTenantPermissionAsync(userId, tenantId, "billing:export");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasTenantPermissionAsync_DenyOverridesJitElevation_ReturnsFalse()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _repoMock
            .Setup(r => r.GetByUserAndTenantAsync(userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantPermission
            {
                UserId = userId,
                TenantId = tenantId,
                Permissions = [],
                DenyPermissions = ["billing:export"]
            });
        _jitMock
            .Setup(j => j.GetActiveByUserAsync(userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Elevation(userId, "billing:export")]);

        var result = await _sut.HasTenantPermissionAsync(userId, tenantId, "billing:export");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasTenantPermissionAsync_ResourceScopedElevation_DoesNotGrantTenantCapability()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _repoMock
            .Setup(r => r.GetByUserAndTenantAsync(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantPermission?)null);
        _jitMock
            .Setup(j => j.GetActiveByUserAsync(userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Elevation(userId, "billing:export", resourceId: Guid.NewGuid())]);

        var result = await _sut.HasTenantPermissionAsync(userId, tenantId, "billing:export");

        result.Should().BeFalse();
    }
}
