using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authorization.Caching;
using GameGuild.Identity.Context.Actors;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Handlers;

/// <summary>
///     Security tests for the permission mutation command handlers:
///     template application, cache clearing, and bulk grant/revoke. These handlers
///     mutate permissions, so the tests assert an authorization guard, a tenant
///     security-version bump, and an audit entry where applicable.
/// </summary>
public sealed class PermissionCommandSecurityTests
{
    private readonly Mock<ITenantSecurityVersionStore> _versionStore = new();
    private readonly Mock<IPermissionAuditService> _auditService = new();
    private readonly Mock<ICacheInvalidationService> _cacheInvalidation = new();
    private readonly Mock<IActorContextAccessor> _actorAccessor = new();
    private readonly Mock<IPermissionBulkService> _bulkService = new();
    private readonly Mock<IPermissionGrantService> _grantService = new();

    public PermissionCommandSecurityTests()
    {
        _auditService
            .Setup(a => a.LogPermissionChangeAsync(
                It.IsAny<PermissionOperationType>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PermissionAuditLog());
        _versionStore
            .Setup(v => v.IncrementVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(7L);
        _cacheInvalidation
            .Setup(c => c.InvalidateTenantAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _cacheInvalidation
            .Setup(c => c.InvalidateUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _cacheInvalidation
            .Setup(c => c.InvalidateGlobalAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _bulkService
            .Setup(b => b.BulkGrantTenantPermissionAsync(
                It.IsAny<Guid[]>(),
                It.IsAny<Guid>(),
                It.IsAny<string[]>(),
                It.IsAny<Guid?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _grantService
            .Setup(g => g.RevokeTenantPermissionAsync(
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    // ─── A2: ApplyPermissionTemplateCommandHandler ───

    [Fact]
    public async Task ApplyTemplate_UnauthenticatedActor_IsDenied()
    {
        await using var db = new PermissionFacadeTestDb();
        var actor = ActorContext.Anonymous;
        SetActor(actor);
        var handler = BuildTemplateHandler(db);

        var act = () => handler.Handle(
            new ApplyPermissionTemplateCommand { UserId = Guid.NewGuid(), TenantId = Guid.NewGuid(), TemplateId = Guid.NewGuid() },
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*not authenticated*");
    }

    [Fact]
    public async Task ApplyTemplate_NonAdminWithoutPermission_IsDenied()
    {
        await using var db = new PermissionFacadeTestDb();
        var tenantId = Guid.NewGuid();
        var templateId = SeedTemplate(db, isSystemTemplate: false);
        SetActor(AuthenticatedActor(roles: [], permissions: [], tenantId: tenantId));
        var handler = BuildTemplateHandler(db);

        var act = () => handler.Handle(
            new ApplyPermissionTemplateCommand { UserId = Guid.NewGuid(), TenantId = tenantId, TemplateId = templateId },
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*permissions:manage*");
    }

    [Fact]
    public async Task ApplyTemplate_SystemTemplate_RequiresManageGlobalDefaults()
    {
        await using var db = new PermissionFacadeTestDb();
        var tenantId = Guid.NewGuid();
        var templateId = SeedTemplate(db, isSystemTemplate: true);
        SetActor(AuthenticatedActor(roles: ["TenantAdmin"], permissions: [], tenantId: tenantId));
        var handler = BuildTemplateHandler(db);

        var act = () => handler.Handle(
            new ApplyPermissionTemplateCommand { UserId = Guid.NewGuid(), TenantId = tenantId, TemplateId = templateId },
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*system:manage-global-defaults*");
    }

    [Fact]
    public async Task ApplyTemplate_TenantAdminInOwnTenant_IsAllowedAndAudited()
    {
        await using var db = new PermissionFacadeTestDb();
        var tenantId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var templateId = SeedTemplate(db, isSystemTemplate: false);
        SetActor(AuthenticatedActor(roles: ["TenantAdmin"], permissions: [], tenantId: tenantId));
        var handler = BuildTemplateHandler(db);

        var result = await handler.Handle(
            new ApplyPermissionTemplateCommand { UserId = targetUserId, TenantId = tenantId, TemplateId = templateId },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.PermissionsGranted.Should().BeGreaterThan(0);

        // Security version bumped for the tenant and audit entry written.
        _versionStore.Verify(v => v.IncrementVersionAsync(tenantId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
        _auditService.Verify(a => a.LogPermissionChangeAsync(
            PermissionOperationType.Grant,
            targetUserId,
            It.IsAny<Guid>(),
            tenantId,
            It.IsAny<string?>(),
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
    public async Task ApplyTemplate_SystemAdminGlobalScope_BumpsReservedGlobalVersion()
    {
        await using var db = new PermissionFacadeTestDb();
        var templateId = SeedTemplate(db, isSystemTemplate: true);
        SetActor(AuthenticatedActor(roles: ["SystemAdmin"], permissions: [], tenantId: null));
        var handler = BuildTemplateHandler(db);

        var result = await handler.Handle(
            new ApplyPermissionTemplateCommand { UserId = Guid.NewGuid(), TenantId = null, TemplateId = templateId },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        _versionStore.Verify(v => v.IncrementVersionAsync(Guid.Empty.ToString(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApplyTemplate_CrossTenantTarget_IsDenied()
    {
        await using var db = new PermissionFacadeTestDb();
        var actorTenant = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        var templateId = SeedTemplate(db, isSystemTemplate: false);
        SetActor(AuthenticatedActor(roles: ["TenantAdmin"], permissions: [], tenantId: actorTenant));
        var handler = BuildTemplateHandler(db);

        var act = () => handler.Handle(
            new ApplyPermissionTemplateCommand { UserId = Guid.NewGuid(), TenantId = otherTenant, TemplateId = templateId },
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*another tenant*system administration*");
    }

    [Fact]
    public async Task ApplyTemplate_PermissionsManagePermission_IsAllowed()
    {
        await using var db = new PermissionFacadeTestDb();
        var tenantId = Guid.NewGuid();
        var templateId = SeedTemplate(db, isSystemTemplate: false);
        SetActor(AuthenticatedActor(
            roles: [],
            permissions: [SystemPermission.Keys.ManagePermissions],
            tenantId: tenantId));
        var handler = BuildTemplateHandler(db);

        var result = await handler.Handle(
            new ApplyPermissionTemplateCommand { UserId = Guid.NewGuid(), TenantId = tenantId, TemplateId = templateId },
            CancellationToken.None);

        result.Success.Should().BeTrue();
    }

    // ─── A3: ClearPermissionCacheCommandHandler ───

    [Fact]
    public async Task ClearCache_NonAdminGlobalClear_IsDenied()
    {
        SetActor(AuthenticatedActor(roles: [], permissions: [], tenantId: Guid.NewGuid()));
        var handler = new ClearPermissionCacheCommandHandler(
            _actorAccessor.Object, _cacheInvalidation.Object,
            NullLogger<ClearPermissionCacheCommandHandler>.Instance);

        var act = () => handler.Handle(new ClearPermissionCacheCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ClearCache_SystemAdminGlobalClear_UsesDistributedInvalidation()
    {
        SetActor(AuthenticatedActor(roles: ["SystemAdmin"], permissions: [], tenantId: null));
        var handler = new ClearPermissionCacheCommandHandler(
            _actorAccessor.Object, _cacheInvalidation.Object,
            NullLogger<ClearPermissionCacheCommandHandler>.Instance);

        var result = await handler.Handle(new ClearPermissionCacheCommand(), CancellationToken.None);

        result.Should().BeTrue();
        _cacheInvalidation.Verify(c => c.InvalidateGlobalAsync(It.IsAny<CancellationToken>()), Times.Once);
        _versionStore.Verify(v => v.IncrementVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ClearCache_TenantAdminOfTargetTenant_UsesTenantInvalidationOnce()
    {
        var tenantId = Guid.NewGuid();
        SetActor(AuthenticatedActor(roles: ["TenantAdmin"], permissions: [], tenantId: tenantId));
        var handler = new ClearPermissionCacheCommandHandler(
            _actorAccessor.Object, _cacheInvalidation.Object,
            NullLogger<ClearPermissionCacheCommandHandler>.Instance);

        var result = await handler.Handle(
            new ClearPermissionCacheCommand { TenantId = tenantId },
            CancellationToken.None);

        result.Should().BeTrue();
        _cacheInvalidation.Verify(c => c.InvalidateTenantAsync(tenantId, It.IsAny<CancellationToken>()), Times.Once);
        _versionStore.Verify(v => v.IncrementVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ClearCache_TenantAdminUserScope_UsesUserInvalidationOnce()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetActor(AuthenticatedActor(roles: ["TenantAdmin"], permissions: [], tenantId: tenantId));
        var handler = new ClearPermissionCacheCommandHandler(
            _actorAccessor.Object, _cacheInvalidation.Object,
            NullLogger<ClearPermissionCacheCommandHandler>.Instance);

        var result = await handler.Handle(
            new ClearPermissionCacheCommand { TenantId = tenantId, UserId = userId },
            CancellationToken.None);

        result.Should().BeTrue();
        _cacheInvalidation.Verify(c => c.InvalidateUserAsync(userId, tenantId, It.IsAny<CancellationToken>()), Times.Once);
        _versionStore.Verify(v => v.IncrementVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ClearCache_UserScopeWithoutTenant_FailsClosed()
    {
        SetActor(AuthenticatedActor(roles: ["SystemAdmin"], permissions: [], tenantId: null));
        var handler = new ClearPermissionCacheCommandHandler(
            _actorAccessor.Object, _cacheInvalidation.Object,
            NullLogger<ClearPermissionCacheCommandHandler>.Instance);

        var act = () => handler.Handle(
            new ClearPermissionCacheCommand { UserId = Guid.NewGuid() },
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*requires the tenant*");
    }

    // ─── A4: Bulk tenant permission handlers ───

    [Fact]
    public async Task BulkGrant_NonTenantAdmin_IsDenied()
    {
        var tenantId = Guid.NewGuid();
        SetActor(AuthenticatedActor(roles: [], permissions: [], tenantId: tenantId));
        var handler = new BulkGrantTenantPermissionsCommandHandler(
            _bulkService.Object, _actorAccessor.Object,
            NullLogger<BulkGrantTenantPermissionsCommandHandler>.Instance);

        var act = () => handler.Handle(
            new BulkGrantTenantPermissionsCommand { UserIds = [Guid.NewGuid()], TenantId = tenantId, Permissions = [PermissionType.Read] },
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _bulkService.Verify(b => b.BulkGrantTenantPermissionAsync(
            It.IsAny<Guid[]>(),
            It.IsAny<Guid>(),
            It.IsAny<string[]>(),
            It.IsAny<Guid?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BulkGrant_TenantAdmin_DelegatesToBulkServiceAndCounts()
    {
        var tenantId = Guid.NewGuid();
        SetActor(AuthenticatedActor(roles: ["TenantAdmin"], permissions: [], tenantId: tenantId));
        var handler = new BulkGrantTenantPermissionsCommandHandler(
            _bulkService.Object, _actorAccessor.Object,
            NullLogger<BulkGrantTenantPermissionsCommandHandler>.Instance);

        var result = await handler.Handle(
            new BulkGrantTenantPermissionsCommand
            {
                UserIds = [Guid.NewGuid(), Guid.NewGuid(), Guid.Empty],
                TenantId = tenantId,
                Permissions = [PermissionType.Read]
            },
            CancellationToken.None);

        result.TotalRequested.Should().Be(2);
        result.Successful.Should().Be(2);
        result.Failed.Should().Be(0);
        _bulkService.Verify(b => b.BulkGrantTenantPermissionAsync(
            It.IsAny<Guid[]>(),
            tenantId,
            It.Is<string[]>(p => p.SequenceEqual(new[] { "Read" })),
            It.IsAny<Guid?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task BulkGrant_GlobalDefaults_RequiresManageGlobalDefaults()
    {
        SetActor(AuthenticatedActor(roles: ["TenantAdmin"], permissions: [], tenantId: Guid.NewGuid()));
        var handler = new BulkGrantTenantPermissionsCommandHandler(
            _bulkService.Object, _actorAccessor.Object,
            NullLogger<BulkGrantTenantPermissionsCommandHandler>.Instance);

        var act = () => handler.Handle(
            new BulkGrantTenantPermissionsCommand { UserIds = [Guid.NewGuid()], TenantId = Guid.Empty, Permissions = [PermissionType.Read] },
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*system:manage-global-defaults*");
    }

    [Fact]
    public async Task BulkResourceGrant_SystemAdminGlobalScope_BumpsReservedGlobalVersion()
    {
        await using var db = new ResourcePermissionTestDb();
        SetActor(AuthenticatedActor(roles: ["SystemAdmin"], permissions: [], tenantId: null));
        var handler = new BulkGrantResourcePermissionsCommandHandler(
            db,
            _actorAccessor.Object,
            _versionStore.Object,
            _auditService.Object,
            NullLogger<BulkGrantResourcePermissionsCommandHandler>.Instance);

        var result = await handler.Handle(
            new BulkGrantResourcePermissionsCommand
            {
                UserIds = [Guid.NewGuid()],
                TenantId = Guid.Empty,
                ResourceId = Guid.NewGuid(),
                ResourceType = "Document",
                Permissions = [PermissionType.Read]
            },
            CancellationToken.None);

        result.Successful.Should().Be(1);
        _versionStore.Verify(v => v.IncrementVersionAsync(Guid.Empty.ToString(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BulkGrant_PerUserFailures_AreCollected()
    {
        var tenantId = Guid.NewGuid();
        SetActor(AuthenticatedActor(roles: ["TenantAdmin"], permissions: [], tenantId: tenantId));
        _bulkService
            .Setup(b => b.BulkGrantTenantPermissionAsync(
                It.IsAny<Guid[]>(),
                It.IsAny<Guid>(),
                It.IsAny<string[]>(),
                It.IsAny<Guid?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("quota exceeded"));
        var handler = new BulkGrantTenantPermissionsCommandHandler(
            _bulkService.Object, _actorAccessor.Object,
            NullLogger<BulkGrantTenantPermissionsCommandHandler>.Instance);

        var result = await handler.Handle(
            new BulkGrantTenantPermissionsCommand { UserIds = [Guid.NewGuid()], TenantId = tenantId, Permissions = [PermissionType.Read] },
            CancellationToken.None);

        result.Failed.Should().Be(1);
        result.Successful.Should().Be(0);
        result.Failures.Should().ContainSingle().Which.Error.Should().Be("InvalidOperationException");
    }

    [Fact]
    public async Task BulkRevoke_TenantAdmin_DelegatesToGrantService()
    {
        var tenantId = Guid.NewGuid();
        SetActor(AuthenticatedActor(roles: ["TenantAdmin"], permissions: [], tenantId: tenantId));
        var handler = new BulkRevokeTenantPermissionsCommandHandler(
            _grantService.Object, _actorAccessor.Object,
            NullLogger<BulkRevokeTenantPermissionsCommandHandler>.Instance);

        var result = await handler.Handle(
            new BulkRevokeTenantPermissionsCommand
            {
                UserIds = [Guid.NewGuid()],
                TenantId = tenantId,
                Permissions = [PermissionType.Edit]
            },
            CancellationToken.None);

        result.Successful.Should().Be(1);
        _grantService.Verify(g => g.RevokeTenantPermissionAsync(
            It.IsAny<Guid?>(),
            tenantId,
            It.Is<string[]>(p => p.SequenceEqual(new[] { "Edit" })),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BulkRevoke_CrossTenantTarget_IsDenied()
    {
        SetActor(AuthenticatedActor(roles: ["TenantAdmin"], permissions: [], tenantId: Guid.NewGuid()));
        var handler = new BulkRevokeTenantPermissionsCommandHandler(
            _grantService.Object, _actorAccessor.Object,
            NullLogger<BulkRevokeTenantPermissionsCommandHandler>.Instance);

        var act = () => handler.Handle(
            new BulkRevokeTenantPermissionsCommand { UserIds = [Guid.NewGuid()], TenantId = Guid.NewGuid(), Permissions = [PermissionType.Read] },
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _grantService.Verify(g => g.RevokeTenantPermissionAsync(
            It.IsAny<Guid?>(),
            It.IsAny<Guid?>(),
            It.IsAny<string[]>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // ─── A6: legacy permission facade tracks mutations ───

    [Fact]
    public async Task LegacyPermissionService_Grant_BumpsVersionAndAudits()
    {
        await using var db = new PermissionFacadeTestDb();
        var tenantId = Guid.NewGuid();
        SetActor(AuthenticatedActor(roles: ["TenantAdmin"], permissions: [], tenantId: tenantId));
        var service = new PermissionService(db, _versionStore.Object, _auditService.Object, _actorAccessor.Object);

        var grant = await service.GrantTenantPermissionAsync(Guid.NewGuid(), tenantId, [PermissionType.Read]);
        grant.GrantedBy.Should().Be(_actorAccessor.Object.ActorContext.SubjectIdAsGuid);

        _versionStore.Verify(v => v.IncrementVersionAsync(tenantId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
        _auditService.Verify(a => a.LogPermissionChangeAsync(
            PermissionOperationType.Grant,
            It.IsAny<Guid?>(),
            _actorAccessor.Object.ActorContext.SubjectIdAsGuid!.Value,
            tenantId,
            It.IsAny<string?>(),
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
    public async Task LegacyPermissionService_Revoke_BumpsVersion()
    {
        await using var db = new PermissionFacadeTestDb();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetActor(AuthenticatedActor(roles: ["TenantAdmin"], permissions: [], tenantId: tenantId));
        var service = new PermissionService(db, _versionStore.Object, _auditService.Object, _actorAccessor.Object);

        var grant = await service.GrantTenantPermissionAsync(userId, tenantId, [PermissionType.Read]);
        grant.Version = 1; // The lightweight InMemory context does not apply the production version interceptor.
        await service.RevokeTenantPermissionAsync(userId, tenantId, [PermissionType.Read]);

        _versionStore.Verify(v => v.IncrementVersionAsync(tenantId.ToString(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RevokeTenantPermissionById_TenantAdminCannotRevokeAnotherTenantsGrant()
    {
        await using var db = new PermissionFacadeTestDb();
        var grantId = Guid.NewGuid();
        var actorTenantId = Guid.NewGuid();
        db.TenantPermissions.Add(new TenantPermission
        {
            Id = grantId,
            Version = 1,
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Permissions = ["tenant:read"]
        });
        await db.SaveChangesAsync();
        SetActor(ActorContextBuilder.ForUser(Guid.NewGuid())
            .WithTenantId(actorTenantId)
            .WithRole("TenantAdmin")
            .Build());
        var handler = new RevokeTenantPermissionByIdHandler(
            db, _actorAccessor.Object, _versionStore.Object, _auditService.Object);

        var act = () => handler.Handle(new RevokeTenantPermissionByIdCommand { GrantId = grantId }, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        db.TenantPermissions.Single(grant => grant.Id == grantId).DeletedAt.Should().BeNull();
        _versionStore.Verify(store => store.IncrementVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _auditService.Verify(audit => audit.LogPermissionChangeAsync(
            It.IsAny<PermissionOperationType>(), It.IsAny<Guid?>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
            It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RevokeTenantPermissionById_SameTenantAdminSoftDeletesAndTracksMutation()
    {
        await using var db = new PermissionFacadeTestDb();
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grantId = Guid.NewGuid();
        db.TenantPermissions.Add(new TenantPermission
        {
            Id = grantId,
            Version = 1,
            UserId = userId,
            TenantId = tenantId,
            Permissions = ["tenant:read"]
        });
        await db.SaveChangesAsync();
        SetActor(ActorContextBuilder.ForUser(actorId)
            .WithTenantId(tenantId)
            .WithRole("TenantAdmin")
            .Build());
        var handler = new RevokeTenantPermissionByIdHandler(
            db, _actorAccessor.Object, _versionStore.Object, _auditService.Object);

        await handler.Handle(new RevokeTenantPermissionByIdCommand { GrantId = grantId }, CancellationToken.None);

        db.TenantPermissions.Single(grant => grant.Id == grantId).DeletedAt.Should().NotBeNull();
        _versionStore.Verify(store => store.IncrementVersionAsync(tenantId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
        _auditService.Verify(audit => audit.LogPermissionChangeAsync(
            PermissionOperationType.Revoke,
            userId,
            actorId,
            tenantId,
            "Tenant",
            null,
            "TenantPermission",
            "tenant:read",
            null,
            "Tenant permission grant revoked by id",
            true,
            null,
            null,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LegacyPermissionService_GlobalDefaults_BumpsReservedGlobalVersion()
    {
        await using var db = new PermissionFacadeTestDb();
        SetActor(AuthenticatedActor(roles: ["SystemAdmin"], permissions: [], tenantId: null));
        var service = new PermissionService(db, _versionStore.Object, _auditService.Object, _actorAccessor.Object);

        await service.SetGlobalDefaultPermissionsAsync([PermissionType.Read]);

        _versionStore.Verify(v => v.IncrementVersionAsync(Guid.Empty.ToString(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── Setup helpers ───

    private static ActorContext AuthenticatedActor(string[] roles, string[] permissions, Guid? tenantId) => new()
    {
        IsAuthenticated = true,
        ActorKind = ActorKind.User,
        SubjectId = Guid.NewGuid().ToString(),
        TenantId = tenantId,
        Roles = new HashSet<string>(roles, StringComparer.OrdinalIgnoreCase),
        Permissions = new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase)
    };

    private void SetActor(ActorContext actor) =>
        _actorAccessor.SetupGet(a => a.ActorContext).Returns(actor);

    private ApplyPermissionTemplateCommandHandler BuildTemplateHandler(PermissionFacadeTestDb db) =>
        new(
            db,
            _actorAccessor.Object,
            _versionStore.Object,
            _auditService.Object,
            NullLogger<ApplyPermissionTemplateCommandHandler>.Instance);

    private static Guid SeedTemplate(PermissionFacadeTestDb db, bool isSystemTemplate)
    {
        var templateId = Guid.NewGuid();
        db.PermissionTemplates.Add(new PermissionTemplate
        {
            Id = templateId,
            Name = $"template-{templateId:N}",
            Description = "test template",
            Permissions = ["reports:read"],
            IsSystemTemplate = isSystemTemplate,
            IsActive = true
        });
        db.SaveChanges();
        return templateId;
    }

    private sealed class PermissionFacadeTestDb : DbContext, IApplicationDbContext
    {
        public DbSet<PermissionTemplate> PermissionTemplates { get; set; } = null!;
        public DbSet<TenantPermission> TenantPermissions { get; set; } = null!;

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Database.BeginTransactionAsync(cancellationToken);

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString("N"));
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PermissionTemplate>().Ignore(p => p.Metadata);
            modelBuilder.Entity<TenantPermission>().Ignore(p => p.Metadata);
        }
    }

    private sealed class ResourcePermissionTestDb : DbContext, IApplicationDbContext
    {
        public DbSet<GenericResourcePermission> GenericResourcePermissions { get; set; } = null!;

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Database.BeginTransactionAsync(cancellationToken);

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString("N"));
            }
        }

    }
}
