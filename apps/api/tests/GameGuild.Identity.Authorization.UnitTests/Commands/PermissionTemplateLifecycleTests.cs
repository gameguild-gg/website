using FluentAssertions;
using GameGuild.CQRS.Models;
using GameGuild.Identity.Authorization.UnitTests.TestSupport;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable.Moq;
using Moq;

namespace GameGuild.Identity.Authorization.UnitTests.Commands;

/// <summary>
///     Tests for the permission template lifecycle (issues #369/#370/#374): guarded +
/// versioned + audited CRUD, role templates, tenant instantiation with dry-run preview,
/// versioned migrations with progress and rollback, and the what-if permission
/// simulation.
/// </summary>
public class PermissionTemplateLifecycleTests
{
    private readonly Mock<IApplicationDbContext> _dbContext = new();
    private readonly Mock<ITenantSecurityVersionStore> _versionStore = new();
    private readonly Mock<IPermissionAuditService> _auditService = new();
    private readonly ActorContextAccessor _actorAccessor = new();
    private readonly Mock<IEffectivePermissionResolver> _resolver = new();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _targetUserId = Guid.NewGuid();

    private readonly List<PermissionTemplate> _templates = [];
    private readonly List<PermissionTemplateVersion> _versions = [];
    private readonly List<PermissionTemplateMigration> _migrations = [];
    private readonly List<PermissionTemplateInstantiation> _instantiations = [];
    private readonly List<TenantPermission> _tenantPermissions = [];
    private readonly List<DynamicRole> _roles = [];
    private readonly List<DynamicRoleAssignment> _roleAssignments = [];

    public PermissionTemplateLifecycleTests()
    {
        _versionStore
            .Setup(v => v.IncrementVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1L);
        _auditService
            .Setup(a => a.LogPermissionChangeAsync(
                It.IsAny<PermissionOperationType>(), It.IsAny<Guid?>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PermissionAuditLog());
        _resolver
            .Setup(r => r.ResolveAsync(It.IsAny<EffectivePermissionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EffectivePermissions
            {
                UserId = _targetUserId,
                TenantId = _tenantId,
                Permissions = new HashSet<string>(["read", "write"], StringComparer.OrdinalIgnoreCase),
                Sources = new Dictionary<string, PermissionSource>(StringComparer.OrdinalIgnoreCase)
                {
                    ["read"] = PermissionSource.DirectGrant,
                    ["write"] = PermissionSource.DirectGrant
                }
            });
    }

    private void ActAsTenantAdmin() => ActAs(_actorId, _tenantId, "TenantAdmin");

    private void ActAsSystemAdmin() => ActAs(_actorId, null, "SystemAdmin");

    private void ActAs(Guid? userId, Guid? tenantId, params string[] roles)
    {
        _actorAccessor.SetActorContext(new ActorContext
        {
            ActorKind = userId is null ? ActorKind.Anonymous : ActorKind.User,
            SubjectId = userId?.ToString(),
            TenantId = tenantId,
            Roles = new HashSet<string>(roles, StringComparer.OrdinalIgnoreCase),
            Permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            IsAuthenticated = userId is not null
        });
    }

    private void WireSets()
    {
        _dbContext.Setup(c => c.Set<PermissionTemplate>()).Returns(MockSet(_templates, t => _templates.Add(t)).Object);
        _dbContext.Setup(c => c.Set<PermissionTemplateVersion>()).Returns(MockSet(_versions, v => _versions.Add(v)).Object);
        _dbContext.Setup(c => c.Set<PermissionTemplateMigration>()).Returns(MockSet(_migrations, m => _migrations.Add(m)).Object);
        _dbContext.Setup(c => c.Set<PermissionTemplateInstantiation>()).Returns(MockSet(_instantiations, i => _instantiations.Add(i)).Object);
        _dbContext.Setup(c => c.Set<TenantPermission>()).Returns(MockSet(_tenantPermissions, p => _tenantPermissions.Add(p)).Object);
        _dbContext.Setup(c => c.Set<DynamicRole>()).Returns(MockSet(_roles, r => _roles.Add(r)).Object);
        _dbContext.Setup(c => c.Set<DynamicRoleAssignment>()).Returns(MockSet(_roleAssignments, a => _roleAssignments.Add(a)).Object);
        _dbContext
            .Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    private static Mock<Microsoft.EntityFrameworkCore.DbSet<T>> MockSet<T>(List<T> rows, Action<T> add)
        where T : class
    {
        var set = rows.AsQueryable().BuildMockDbSet();
        set.Setup(s => s.Add(It.IsAny<T>())).Callback<T>(add);
        set.Setup(s => s.AddRange(It.IsAny<IEnumerable<T>>())).Callback<IEnumerable<T>>(rs => rs.ToList().ForEach(add));
        // Deletes go through Remove + SaveChanges; reflect them in the shared row list so
        // post-handler assertions observe the persisted state.
        set.Setup(s => s.Remove(It.IsAny<T>())).Callback<T>(row => rows.Remove(row));
        return set;
    }

    private PermissionTemplate AddTemplate(
        string name = "editor-bundle",
        string[]? permissions = null,
        string[]? roleKeys = null,
        bool isSystem = false)
    {
        var template = new PermissionTemplate
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = $"{name} description",
            Permissions = permissions ?? ["read", "write"],
            RoleKeys = roleKeys ?? [],
            IsSystemTemplate = isSystem,
            IsActive = true
        };
        _templates.Add(template);
        _versions.Add(new PermissionTemplateVersion
        {
            TemplateId = template.Id,
            VersionNumber = 1,
            Name = template.Name,
            Description = template.Description,
            Permissions = template.Permissions,
            IsActive = true,
            ChangeType = TemplateChangeType.Major,
            AddedPermissions = template.Permissions,
            RemovedPermissions = [],
            UnchangedPermissions = [],
            Metadata = """{"AddedRoleKeys":[],"RemovedRoleKeys":[]}"""
        });
        return template;
    }

    private CreatePermissionTemplateCommandHandler CreateSut() => new(
        _dbContext.Object, _actorAccessor, _auditService.Object,
        NullLogger<CreatePermissionTemplateCommandHandler>.Instance);

    private UpdatePermissionTemplateCommandHandler UpdateSut() => new(
        _dbContext.Object, _actorAccessor, _versionStore.Object, _auditService.Object,
        NullLogger<UpdatePermissionTemplateCommandHandler>.Instance);

    private DeletePermissionTemplateCommandHandler DeleteSut() => new(
        _dbContext.Object, _actorAccessor, _auditService.Object,
        NullLogger<DeletePermissionTemplateCommandHandler>.Instance);

    private InstantiatePermissionTemplateCommandHandler InstantiateSut() => new(
        _dbContext.Object, _actorAccessor, _versionStore.Object, _auditService.Object,
        NullLogger<InstantiatePermissionTemplateCommandHandler>.Instance);

    private MigratePermissionTemplateCommandHandler MigrateSut() => new(
        _dbContext.Object, _actorAccessor, _versionStore.Object, _auditService.Object,
        NullLogger<MigratePermissionTemplateCommandHandler>.Instance);

    private RollbackPermissionTemplateMigrationCommandHandler RollbackSut() => new(
        _dbContext.Object, _actorAccessor, _versionStore.Object, _auditService.Object,
        NullLogger<RollbackPermissionTemplateMigrationCommandHandler>.Instance);

    private SimulatePermissionChangeCommandHandler SimulateSut() => new(
        _resolver.Object, _dbContext.Object, _actorAccessor,
        NullLogger<SimulatePermissionChangeCommandHandler>.Instance);

    // ── Create (#369) ──────────────────────────────────────────────────────

    [Fact]
    public async Task Create_AnonymousActor_IsRejected()
    {
        ActAs(null, null);
        WireSets();

        var act = () => CreateSut().Handle(new CreatePermissionTemplateCommand
        {
            Name = "x", Description = "d", Permissions = ["read"]
        }, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _templates.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_SystemTemplate_WithoutGlobalDefaultsPermission_IsRejected()
    {
        ActAsTenantAdmin();
        WireSets();

        var act = () => CreateSut().Handle(new CreatePermissionTemplateCommand
        {
            Name = "sys", Description = "d", Permissions = ["read"], IsSystemTemplate = true
        }, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Create_TenantAdmin_CreatesTemplateWithInitialVersionAndAudit()
    {
        ActAsTenantAdmin();
        WireSets();

        var result = await CreateSut().Handle(new CreatePermissionTemplateCommand
        {
            Name = "viewer", Description = "view-only", Permissions = ["read"]
        }, CancellationToken.None);

        result.Created.Should().BeTrue();
        result.Version.Should().Be(1);
        _templates.Should().ContainSingle(t => t.Name == "viewer");
        _versions.Should().ContainSingle(v => v.TemplateId == result.TemplateId && v.VersionNumber == 1);
        _auditService.Verify(a => a.LogPermissionChangeAsync(
            PermissionOperationType.Grant, It.IsAny<Guid?>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
            "template:viewer", It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_EmptyPermissionAndRoleSet_IsRejected()
    {
        ActAsTenantAdmin();
        WireSets();

        var act = () => CreateSut().Handle(new CreatePermissionTemplateCommand
        {
            Name = "empty", Description = "d"
        }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Create_DuplicateName_IsRejected()
    {
        ActAsTenantAdmin();
        AddTemplate("viewer");
        WireSets();

        var act = () => CreateSut().Handle(new CreatePermissionTemplateCommand
        {
            Name = "viewer", Description = "d", Permissions = ["read"]
        }, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── Update (#369/#370) ─────────────────────────────────────────────────

    [Fact]
    public async Task Update_SystemTemplate_IsImmutable()
    {
        ActAsSystemAdmin();
        var template = AddTemplate(isSystem: true);
        WireSets();

        var act = () => UpdateSut().Handle(new UpdatePermissionTemplateCommand
        {
            TemplateId = template.Id,
            Permissions = ["read", "write", "delete"]
        }, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Update_AppendsVersionSnapshotWithDiff_AndBumpsInstantiatedTenantVersion()
    {
        ActAsTenantAdmin();
        var template = AddTemplate(permissions: ["read", "write"]);
        _instantiations.Add(new PermissionTemplateInstantiation
        {
            TemplateId = template.Id,
            TenantId = _tenantId,
            UserId = _targetUserId,
            TemplateVersionNumber = 1
        });
        WireSets();

        var result = await UpdateSut().Handle(new UpdatePermissionTemplateCommand
        {
            TemplateId = template.Id,
            Permissions = ["read", "delete"]
        }, CancellationToken.None);

        result.Version.Should().Be(2);
        var snapshot = _versions.Single(v => v.TemplateId == template.Id && v.VersionNumber == 2);
        snapshot.AddedPermissions.Should().BeEquivalentTo(["delete"]);
        snapshot.RemovedPermissions.Should().BeEquivalentTo(["write"]);
        snapshot.UnchangedPermissions.Should().BeEquivalentTo(["read"]);
        snapshot.PermissionHash.Should().NotBeNullOrWhiteSpace();
        _versionStore.Verify(v => v.IncrementVersionAsync(_tenantId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
        _auditService.Verify(a => a.LogPermissionChangeAsync(
            PermissionOperationType.Update, It.IsAny<Guid?>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
            $"template:{template.Name}", It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Delete (#369) ──────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_SystemTemplate_IsRejected()
    {
        ActAsSystemAdmin();
        var template = AddTemplate(isSystem: true);
        WireSets();

        var act = () => DeleteSut().Handle(new DeletePermissionTemplateCommand { TemplateId = template.Id }, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Delete_TemplateWithActiveInstantiations_IsPinned()
    {
        ActAsTenantAdmin();
        var template = AddTemplate();
        _instantiations.Add(new PermissionTemplateInstantiation
        {
            TemplateId = template.Id, TenantId = _tenantId, UserId = _targetUserId, IsActive = true
        });
        WireSets();

        var act = () => DeleteSut().Handle(new DeletePermissionTemplateCommand { TemplateId = template.Id }, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _templates.Should().Contain(template);
    }

    [Fact]
    public async Task Delete_UnpinnedTemplate_RemovesRowsAndKeepsTerminalSnapshot()
    {
        ActAsTenantAdmin();
        var template = AddTemplate();
        WireSets();

        var deleted = await DeleteSut().Handle(new DeletePermissionTemplateCommand { TemplateId = template.Id }, CancellationToken.None);

        deleted.Should().BeTrue();
        _templates.Should().NotContain(template);
        _versions.Should().Contain(v => v.TemplateId == template.Id && v.VersionNumber == 2 && !v.IsActive);
    }

    // ── Instantiation + role templates (#369) ──────────────────────────────

    [Fact]
    public async Task Instantiate_CrossTenantTenantAdmin_IsRejected()
    {
        ActAs(_actorId, Guid.NewGuid(), "TenantAdmin");
        var template = AddTemplate();
        WireSets();

        var act = () => InstantiateSut().Handle(new InstantiatePermissionTemplateCommand
        {
            TemplateId = template.Id,
            TenantId = _tenantId,
            TargetUserIds = [_targetUserId]
        }, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Instantiate_DryRun_PlansWithoutMutating()
    {
        ActAsTenantAdmin();
        var template = AddTemplate(permissions: ["read", "write"]);
        WireSets();

        var result = await InstantiateSut().Handle(new InstantiatePermissionTemplateCommand
        {
            TemplateId = template.Id,
            TenantId = _tenantId,
            TargetUserIds = [_targetUserId],
            DryRun = true
        }, CancellationToken.None);

        result.Applied.Should().BeFalse();
        result.Entries.Should().ContainSingle();
        result.Entries[0].PermissionsToGrant.Should().BeEquivalentTo(["read", "write"]);
        _tenantPermissions.Should().BeEmpty();
        _instantiations.Should().BeEmpty();
        _dbContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _auditService.Verify(a => a.LogPermissionChangeAsync(
            It.IsAny<PermissionOperationType>(), It.IsAny<Guid?>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
            It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Instantiate_RoleTemplate_AssignsRolesAndWritesProvenance()
    {
        ActAsTenantAdmin();
        var roleId = Guid.NewGuid();
        _roles.Add(new DynamicRole { Id = roleId, Name = "Editor", TenantId = _tenantId, IsActive = true });
        var template = AddTemplate(permissions: [], roleKeys: ["Editor"]);
        WireSets();

        var result = await InstantiateSut().Handle(new InstantiatePermissionTemplateCommand
        {
            TemplateId = template.Id,
            TargetUserIds = [_targetUserId]
        }, CancellationToken.None);

        result.Applied.Should().BeTrue();
        result.RoleAssignments.Should().Be(1);
        _roleAssignments.Should().ContainSingle(a => a.RoleId == roleId && a.Reason == $"template:{template.Name}");
        _instantiations.Should().ContainSingle(i => i.UserId == _targetUserId && i.TemplateVersionNumber == 1);
        _versionStore.Verify(v => v.IncrementVersionAsync(_tenantId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Instantiate_RoleTemplateWithMissingRole_FailsClosed()
    {
        ActAsTenantAdmin();
        var template = AddTemplate(permissions: [], roleKeys: ["DoesNotExist"]);
        WireSets();

        var act = () => InstantiateSut().Handle(new InstantiatePermissionTemplateCommand
        {
            TemplateId = template.Id,
            TargetUserIds = [_targetUserId]
        }, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _roleAssignments.Should().BeEmpty();
    }

    // ── Migration + rollback (#370) ────────────────────────────────────────

    [Fact]
    public async Task Migrate_NonSystemAdmin_IsRejected()
    {
        ActAsTenantAdmin();
        var template = AddTemplate();
        WireSets();

        var act = () => MigrateSut().Handle(new MigratePermissionTemplateCommand
        {
            TemplateId = template.Id, ToVersion = 2
        }, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Migrate_ApppliesVersionDeltaInBatches_WithProgressAndRollback()
    {
        ActAsSystemAdmin();
        var template = AddTemplate(permissions: ["read", "write"]);
        var v2 = new PermissionTemplateVersion
        {
            TemplateId = template.Id,
            VersionNumber = 2,
            Name = template.Name,
            Permissions = ["read", "delete"],
            IsActive = true,
            ChangeType = TemplateChangeType.Minor,
            AddedPermissions = ["delete"],
            RemovedPermissions = ["write"],
            UnchangedPermissions = ["read"],
            Metadata = """{"AddedRoleKeys":[],"RemovedRoleKeys":[]}"""
        };
        _versions.Add(v2);
        _tenantPermissions.Add(new TenantPermission
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            UserId = _targetUserId,
            Permissions = ["read", "write"],
            IsActive = true
        });
        _instantiations.Add(new PermissionTemplateInstantiation
        {
            TemplateId = template.Id,
            TenantId = _tenantId,
            UserId = _targetUserId,
            TemplateVersionNumber = 1
        });
        WireSets();

        var dryRun = await MigrateSut().Handle(new MigratePermissionTemplateCommand
        {
            TemplateId = template.Id, ToVersion = 2, DryRun = true
        }, CancellationToken.None);

        dryRun.DryRun.Should().BeTrue();
        dryRun.Status.Should().Be(MigrationStatus.Planned);
        _tenantPermissions[0].Permissions.Should().BeEquivalentTo(["read", "write"], o => o.WithStrictOrdering());

        var migration = await MigrateSut().Handle(new MigratePermissionTemplateCommand
        {
            TemplateId = template.Id, ToVersion = 2, BatchSize = 1
        }, CancellationToken.None);

        migration.Status.Should().Be(MigrationStatus.Completed);
        migration.SuccessCount.Should().Be(1);
        migration.ProgressPercentage.Should().Be(100);
        _tenantPermissions[0].Permissions.Should().BeEquivalentTo(["read", "delete"]);
        _instantiations[0].TemplateVersionNumber.Should().Be(2);
        _versionStore.Verify(v => v.IncrementVersionAsync(_tenantId.ToString(), It.IsAny<CancellationToken>()), Times.Once);

        var rollback = await RollbackSut().Handle(new RollbackPermissionTemplateMigrationCommand
        {
            MigrationId = migration.MigrationId, Reason = "revert"
        }, CancellationToken.None);

        rollback.Status.Should().Be(MigrationStatus.RolledBack);
        _tenantPermissions[0].Permissions.Should().BeEquivalentTo(["read", "write"]);
        _instantiations[0].TemplateVersionNumber.Should().Be(1);
    }

    // ── Simulation (#374) ──────────────────────────────────────────────────

    [Fact]
    public async Task Simulate_AnonymousActor_IsRejected()
    {
        ActAs(null, null);
        WireSets();

        var act = () => SimulateSut().Handle(new SimulatePermissionChangeCommand
        {
            SubjectKind = PermissionSimulationSubjectKind.User,
            UserId = _targetUserId,
            Grants = ["delete"]
        }, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Simulate_NonAdminForAnotherUser_IsRejected()
    {
        ActAs(Guid.NewGuid(), _tenantId);
        WireSets();

        var act = () => SimulateSut().Handle(new SimulatePermissionChangeCommand
        {
            SubjectKind = PermissionSimulationSubjectKind.User,
            UserId = _targetUserId,
            Grants = ["delete"]
        }, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Simulate_UserSubject_ReturnsBeforeAfterDiff_WithoutPersisting()
    {
        ActAs(_targetUserId, _tenantId);
        WireSets();

        var result = await SimulateSut().Handle(new SimulatePermissionChangeCommand
        {
            SubjectKind = PermissionSimulationSubjectKind.User,
            UserId = _targetUserId,
            Grants = ["delete"],
            Revokes = ["write"]
        }, CancellationToken.None);

        result.Applied.Should().BeFalse();
        result.Before.Should().BeEquivalentTo(["read", "write"]);
        result.After.Should().BeEquivalentTo(["read", "delete"]);
        result.Granted.Should().BeEquivalentTo(["delete"]);
        result.Revoked.Should().BeEquivalentTo(["write"]);
        result.Unchanged.Should().BeEquivalentTo(["read"]);
        _dbContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _versionStore.Verify(v => v.IncrementVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Simulate_StaticGrant_IsNonRevocable()
    {
        ActAs(_targetUserId, _tenantId);
        _resolver
            .Setup(r => r.ResolveAsync(It.IsAny<EffectivePermissionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EffectivePermissions
            {
                UserId = _targetUserId,
                TenantId = _tenantId,
                Permissions = new HashSet<string>(["*"], StringComparer.OrdinalIgnoreCase),
                Sources = new Dictionary<string, PermissionSource>(StringComparer.OrdinalIgnoreCase)
                {
                    ["*"] = PermissionSource.Static
                }
            });
        WireSets();

        var result = await SimulateSut().Handle(new SimulatePermissionChangeCommand
        {
            SubjectKind = PermissionSimulationSubjectKind.User,
            UserId = _targetUserId,
            Revokes = ["*"]
        }, CancellationToken.None);

        result.NonRevocableRevokes.Should().BeEquivalentTo(["*"]);
        result.After.Should().Contain("*");
        result.Revoked.Should().BeEmpty();
    }

    [Fact]
    public async Task Simulate_OverlappingGrantAndRevoke_IsRejected()
    {
        ActAs(_targetUserId, _tenantId);
        WireSets();

        var act = () => SimulateSut().Handle(new SimulatePermissionChangeCommand
        {
            SubjectKind = PermissionSimulationSubjectKind.User,
            UserId = _targetUserId,
            Grants = ["read"],
            Revokes = ["read"]
        }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Simulate_TenantSubject_UsesTenantDefaults_WithoutPersisting()
    {
        ActAsTenantAdmin();
        _tenantPermissions.Add(new TenantPermission
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            UserId = null,
            Permissions = ["read"],
            IsActive = true
        });
        WireSets();

        var result = await SimulateSut().Handle(new SimulatePermissionChangeCommand
        {
            SubjectKind = PermissionSimulationSubjectKind.Tenant,
            Grants = ["write"]
        }, CancellationToken.None);

        result.Before.Should().BeEquivalentTo(["read"]);
        result.After.Should().BeEquivalentTo(["read", "write"]);
        _dbContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Simulate_RoleSubject_UsesStoredRoleSets()
    {
        ActAsTenantAdmin();
        _roles.Add(new DynamicRole
        {
            Id = Guid.NewGuid(),
            Name = "Editor",
            TenantId = _tenantId,
            IsActive = true,
            Permissions = ["read", "write"],
            DenyPermissions = ["write"]
        });
        WireSets();

        var result = await SimulateSut().Handle(new SimulatePermissionChangeCommand
        {
            SubjectKind = PermissionSimulationSubjectKind.Role,
            RoleKey = "Editor",
            Grants = ["share"]
        }, CancellationToken.None);

        // The role's deny of "write" is already applied to the before set.
        result.Before.Should().BeEquivalentTo(["read"]);
        result.After.Should().BeEquivalentTo(["read", "share"]);
    }
}
