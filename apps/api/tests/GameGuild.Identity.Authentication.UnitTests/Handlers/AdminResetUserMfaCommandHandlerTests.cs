using FluentAssertions;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Handlers;

public sealed class AdminResetUserMfaCommandHandlerTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    private readonly Mock<IMfaAttemptTrackingService> _tracking = new();
    private readonly Mock<IMfaRecoveryRequestStore> _store = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IActorContextAccessor> _actorAccessor = new();
    private readonly Mock<ITenantSecurityVersionStore> _versionStore = new();
    private readonly Mock<IPermissionAuditService> _audit = new();

    private readonly Guid _targetUserId = Guid.NewGuid();

    private AdminResetUserMfaCommandHandler CreateHandler() =>
        new(_tracking.Object, _store.Object, _users.Object, _actorAccessor.Object, _versionStore.Object,
            _audit.Object, NullLogger<AdminResetUserMfaCommandHandler>.Instance);

    private void SetupTargetUser(Guid? tenantId = null)
    {
        var user = new User { Id = _targetUserId, Email = "target@test.com", Username = "target" };
        user.SetTenantId(tenantId ?? TenantA);
        _users.Setup(r => r.GetByIdAsync(_targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
    }

    private void SetupActor(
        bool isAuthenticated = true,
        bool isSystemAdmin = false,
        bool isTenantAdmin = false,
        string? permission = null,
        Guid? tenantId = null)
    {
        var roles = new HashSet<string>();
        if (isSystemAdmin) { roles.Add("SystemAdmin"); }
        if (isTenantAdmin) { roles.Add("TenantAdmin"); }

        var permissions = new HashSet<string>();
        if (permission is not null) { permissions.Add(permission); }

        var actor = ActorContext.Anonymous with
        {
            ActorKind = ActorKind.User,
            IsAuthenticated = isAuthenticated,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = tenantId ?? TenantA,
            Roles = roles,
            Permissions = permissions
        };
        _actorAccessor.SetupGet(a => a.ActorContext).Returns(actor);
    }

    private void SetupSuccessEffects(bool mfaEnabled = true, bool liveRecovery = true)
    {
        _tracking.Setup(t => t.DisableMfaAsync(_targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mfaEnabled);
        _store.Setup(s => s.CancelLiveByUserAsync(_targetUserId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(liveRecovery ? new MfaRecoveryRequest { Id = Guid.NewGuid(), UserId = _targetUserId.ToString() } : null);
    }

    [Fact]
    public async Task SystemAdmin_CanResetUserInAnyTenant()
    {
        SetupActor(isSystemAdmin: true, tenantId: TenantB);
        SetupTargetUser(TenantA);
        SetupSuccessEffects();

        var result = await CreateHandler().Handle(new AdminResetUserMfaCommand { UserId = _targetUserId }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.MfaWasEnabled.Should().BeTrue();
        result.LiveRecoveryCancelled.Should().BeTrue();
    }

    [Fact]
    public async Task TenantAdmin_WithPermissionsManage_CanResetUserInOwnTenant()
    {
        SetupActor(isTenantAdmin: true, tenantId: TenantA);
        SetupTargetUser(TenantA);
        SetupSuccessEffects();

        var result = await CreateHandler().Handle(new AdminResetUserMfaCommand { UserId = _targetUserId }, CancellationToken.None);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task NonAdmin_WithPermissionsManage_CanResetUserInOwnTenant()
    {
        SetupActor(permission: SystemPermission.Keys.ManagePermissions, tenantId: TenantA);
        SetupTargetUser(TenantA);
        SetupSuccessEffects();

        var result = await CreateHandler().Handle(new AdminResetUserMfaCommand { UserId = _targetUserId }, CancellationToken.None);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task TenantAdmin_CrossTenantReset_IsDenied()
    {
        SetupActor(isTenantAdmin: true, tenantId: TenantA);
        SetupTargetUser(TenantB);
        SetupSuccessEffects();

        var act = () => CreateHandler().Handle(new AdminResetUserMfaCommand { UserId = _targetUserId }, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _tracking.Verify(t => t.DisableMfaAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _store.Verify(s => s.CancelLiveByUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _versionStore.Verify(v => v.IncrementVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _audit.Verify(a => a.LogPermissionChangeAsync(
            It.IsAny<PermissionOperationType>(), It.IsAny<Guid?>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
            It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PermissionsManageHolder_CrossTenantReset_IsDenied()
    {
        SetupActor(permission: SystemPermission.Keys.ManagePermissions, tenantId: TenantA);
        SetupTargetUser(TenantB);
        SetupSuccessEffects();

        var act = () => CreateHandler().Handle(new AdminResetUserMfaCommand { UserId = _targetUserId }, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _tracking.Verify(t => t.DisableMfaAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TenantAdmin_OwnTenant_WithoutPermissionsManage_CanReset()
    {
        SetupActor(isTenantAdmin: true, tenantId: TenantA);
        SetupTargetUser(TenantA);
        SetupSuccessEffects();

        var result = await CreateHandler().Handle(new AdminResetUserMfaCommand { UserId = _targetUserId }, CancellationToken.None);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task NonPrivilegedUser_OwnTenant_IsDenied()
    {
        SetupActor(tenantId: TenantA);
        SetupTargetUser(TenantA);
        SetupSuccessEffects();

        var act = () => CreateHandler().Handle(new AdminResetUserMfaCommand { UserId = _targetUserId }, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _tracking.Verify(t => t.DisableMfaAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _versionStore.Verify(v => v.IncrementVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UnauthenticatedActor_IsDenied()
    {
        SetupActor(isAuthenticated: false);
        SetupTargetUser(TenantA);
        SetupSuccessEffects();

        var act = () => CreateHandler().Handle(new AdminResetUserMfaCommand { UserId = _targetUserId }, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _users.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Success_DisablesMfaCancelsRecoveryBumpsVersionAndWritesAudit()
    {
        var actorId = Guid.NewGuid();
        SetupActor(isTenantAdmin: true, tenantId: TenantA);
        _actorAccessor.SetupGet(a => a.ActorContext)
            .Returns(ActorContext.Anonymous with
            {
                ActorKind = ActorKind.User,
                IsAuthenticated = true,
                SubjectId = actorId.ToString(),
                TenantId = TenantA,
                Roles = new HashSet<string> { "TenantAdmin" },
                Permissions = new HashSet<string>()
            });
        SetupTargetUser(TenantA);
        SetupSuccessEffects(mfaEnabled: true, liveRecovery: true);

        var result = await CreateHandler().Handle(new AdminResetUserMfaCommand { UserId = _targetUserId }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.MfaWasEnabled.Should().BeTrue();
        result.LiveRecoveryCancelled.Should().BeTrue();

        _tracking.Verify(t => t.DisableMfaAsync(_targetUserId, It.IsAny<CancellationToken>()), Times.Once);
        _store.Verify(s => s.CancelLiveByUserAsync(_targetUserId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
        _versionStore.Verify(v => v.IncrementVersionAsync(TenantA.ToString(), It.IsAny<CancellationToken>()), Times.Once);
        _audit.Verify(a => a.LogPermissionChangeAsync(
            PermissionOperationType.Revoke,
            _targetUserId,
            actorId,
            TenantA,
            It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnknownUser_ThrowsWithoutSideEffects()
    {
        SetupActor(isSystemAdmin: true, tenantId: TenantB);
        _users.Setup(r => r.GetByIdAsync(_targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var act = () => CreateHandler().Handle(new AdminResetUserMfaCommand { UserId = _targetUserId }, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _tracking.Verify(t => t.DisableMfaAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _versionStore.Verify(v => v.IncrementVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NoLiveRecovery_StillSucceedsWithoutCancellation()
    {
        SetupActor(isSystemAdmin: true);
        SetupTargetUser(TenantA);
        SetupSuccessEffects(mfaEnabled: true, liveRecovery: false);

        var result = await CreateHandler().Handle(new AdminResetUserMfaCommand { UserId = _targetUserId }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.LiveRecoveryCancelled.Should().BeFalse();
        _versionStore.Verify(v => v.IncrementVersionAsync(TenantA.ToString(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
