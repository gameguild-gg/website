using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization.Models;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests;

/// <summary>
///     Issues #294 and #386: the administrative permission tier (ManageMembers,
///     ManageRoles, ManageSettings, Audit) and the system permission tier
///     (SystemConfiguration, BackupRestore, DataMigration) must be first-class
///     permissions — registered in the central registry, exposed via the facade,
///     seeded as policies, and enforced by the CQRS authorization pipeline.
/// </summary>
public sealed class AdminSystemPermissionsTests
{
    // ─── Registry & facade: administrative tier (#294) ───

    [Fact]
    public void AdministrativePermission_Keys_AreRegisteredInTheCentralRegistry()
    {
        PermissionRegistry.Keys.Should().Contain(ManageMembersPermission.Keys.Manage);
        PermissionRegistry.Keys.Should().Contain(ManageRolesPermission.Keys.Manage);
        PermissionRegistry.Keys.Should().Contain(ManageSettingsPermission.Keys.Read);
        PermissionRegistry.Keys.Should().Contain(ManageSettingsPermission.Keys.Manage);
        PermissionRegistry.Keys.Should().Contain(AuditPermission.Keys.Read);
        PermissionRegistry.Keys.Should().Contain(AuditPermission.Keys.Export);
    }

    [Fact]
    public void AdministrativePermission_Instances_CarryTheirResources()
    {
        ManageMembersPermission.Manage.Key.Should().Be("members:manage");
        ManageRolesPermission.Manage.Key.Should().Be("roles:manage");
        ManageSettingsPermission.Read.Key.Should().Be("settings:read");
        ManageSettingsPermission.Manage.Key.Should().Be("settings:manage");
        AuditPermission.Read.Key.Should().Be("audit:read");
        AuditPermission.Export.Key.Should().Be("audit:export");

        ManageMembersPermission.Manage.Resource.Should().Be("members");
        ManageRolesPermission.Manage.Resource.Should().Be("roles");
        ManageSettingsPermission.Read.Resource.Should().Be("settings");
        ManageSettingsPermission.Manage.Resource.Should().Be("settings");
        AuditPermission.Read.Resource.Should().Be("audit");
        AuditPermission.Export.Resource.Should().Be("audit");

        foreach (var permission in new Permission[]
                 {
                     ManageMembersPermission.Manage, ManageRolesPermission.Manage,
                     ManageSettingsPermission.Read, ManageSettingsPermission.Manage,
                     AuditPermission.Read, AuditPermission.Export
                 })
        {
            PermissionRegistry.GetByKey(permission.Key).Should().NotBeNull();
        }
    }

    [Fact]
    public void Permissions_Facade_ExposesAdministrativeConstants()
    {
        Permissions.MembersManage.Should().Be(ManageMembersPermission.Keys.Manage);
        Permissions.RolesManage.Should().Be(ManageRolesPermission.Keys.Manage);
        Permissions.SettingsRead.Should().Be(ManageSettingsPermission.Keys.Read);
        Permissions.SettingsManage.Should().Be(ManageSettingsPermission.Keys.Manage);
        Permissions.AuditRead.Should().Be(AuditPermission.Keys.Read);
        Permissions.AuditExport.Should().Be(AuditPermission.Keys.Export);
    }

    [Fact]
    public void AdministrativePermission_WildcardScopes_AreRecognized()
    {
        // Tenant owners/admins hold members:*, roles:* and settings:* wildcard grants
        // (StaticRolePermissions); the registry must recognize those wildcards.
        PermissionRegistry.IsValidKey("members:*").Should().BeTrue();
        PermissionRegistry.IsValidKey("roles:*").Should().BeTrue();
        PermissionRegistry.IsValidKey("settings:*").Should().BeTrue();
        PermissionRegistry.IsValidKey("audit:*").Should().BeTrue();
    }

    [Fact]
    public void PermissionType_HasAdministrativeMembers()
    {
        ((int)PermissionType.ManageMembers).Should().Be(204);
        ((int)PermissionType.ManageRoles).Should().Be(205);
        ((int)PermissionType.ManageSettings).Should().Be(206);
        ((int)PermissionType.AuditAccess).Should().Be(207);
        Enum.GetNames<PermissionType>().Should().Contain("ManageMembers");
        Enum.GetNames<PermissionType>().Should().Contain("ManageRoles");
        Enum.GetNames<PermissionType>().Should().Contain("ManageSettings");
        Enum.GetNames<PermissionType>().Should().Contain("AuditAccess");
    }

    // ─── Registry & facade: system tier (#386) ───

    [Fact]
    public void SystemPermission_Keys_AreRegisteredInTheCentralRegistry()
    {
        PermissionRegistry.Keys.Should().Contain(SystemConfigurationPermission.Keys.Manage);
        PermissionRegistry.Keys.Should().Contain(BackupRestorePermission.Keys.Operate);
        PermissionRegistry.Keys.Should().Contain(DataMigrationPermission.Keys.Operate);
    }

    [Fact]
    public void SystemPermission_Instances_CarryTheSystemResource()
    {
        SystemConfigurationPermission.Manage.Key.Should().Be("system:manage-configuration");
        BackupRestorePermission.Operate.Key.Should().Be("system:backup-restore");
        DataMigrationPermission.Operate.Key.Should().Be("system:data-migration");

        foreach (var permission in new Permission[]
                 {
                     SystemConfigurationPermission.Manage,
                     BackupRestorePermission.Operate,
                     DataMigrationPermission.Operate
                 })
        {
            permission.Resource.Should().Be("system");
            PermissionRegistry.GetByKey(permission.Key).Should().NotBeNull();
        }
    }

    [Fact]
    public void Permissions_Facade_ExposesSystemConstants()
    {
        Permissions.SystemManageConfiguration.Should().Be(SystemConfigurationPermission.Keys.Manage);
        Permissions.SystemBackupRestore.Should().Be(BackupRestorePermission.Keys.Operate);
        Permissions.SystemDataMigration.Should().Be(DataMigrationPermission.Keys.Operate);
    }

    [Fact]
    public void PermissionType_HasSystemMembers()
    {
        ((int)PermissionType.SystemConfiguration).Should().Be(208);
        ((int)PermissionType.BackupRestore).Should().Be(209);
        ((int)PermissionType.DataMigration).Should().Be(210);
        Enum.GetNames<PermissionType>().Should().Contain("SystemConfiguration");
        Enum.GetNames<PermissionType>().Should().Contain("BackupRestore");
        Enum.GetNames<PermissionType>().Should().Contain("DataMigration");
    }

    // ─── Policy registry & seeded definitions ───

    [Fact]
    public void Policies_ExposesAdministrativeAndSystemPolicyNames()
    {
        Policies.MembersManage.Should().Be("Members.Manage");
        Policies.RolesManage.Should().Be("Roles.Manage");
        Policies.SettingsRead.Should().Be("Settings.Read");
        Policies.SettingsManage.Should().Be("Settings.Manage");
        Policies.AuditRead.Should().Be("Audit.Read");
        Policies.AuditExport.Should().Be("Audit.Export");
        Policies.SystemConfigurationManage.Should().Be("SystemConfiguration.Manage");
        Policies.SystemBackupRestore.Should().Be("System.BackupRestore");
        Policies.SystemDataMigration.Should().Be("System.DataMigration");

        foreach (var policyName in new[]
                 {
                     Policies.MembersManage, Policies.RolesManage, Policies.SettingsRead, Policies.SettingsManage,
                     Policies.AuditRead, Policies.AuditExport, Policies.SystemConfigurationManage,
                     Policies.SystemBackupRestore, Policies.SystemDataMigration
                 })
        {
            Policies.All.Should().Contain(policyName);
            Policies.IsValid(policyName).Should().BeTrue();
        }
    }

    [Fact]
    public async Task PolicySeeder_SeedsAdministrativeAndSystemPolicies_WithGranularKeys()
    {
        var repo = new Mock<IPolicyDefinitionRepository>();
        repo.Setup(r => r.GetByNameAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PolicyDefinitionEntity?)null);

        var added = new List<PolicyDefinitionEntity>();
        repo.Setup(r => r.AddAsync(It.IsAny<PolicyDefinitionEntity>(), It.IsAny<CancellationToken>()))
            .Callback<PolicyDefinitionEntity, CancellationToken>((p, _) => added.Add(p))
            .Returns(Task.CompletedTask);

        var seeder = new PolicyDefinitionSeeder(repo.Object, NullLogger<PolicyDefinitionSeeder>.Instance);
        await seeder.SeedAsync(CancellationToken.None).ConfigureAwait(false);

        var seeded = added.ToDictionary(policy => policy.PolicyName, policy => policy);

        // Administrative tier (#294): tenant match + (role fallback OR granular permission).
        seeded.Should().ContainKey(Policies.MembersManage);
        seeded[Policies.MembersManage].RulesJson.Should().Contain("members:manage");
        seeded[Policies.MembersManage].RulesJson.Should().Contain("TenantMatch");
        seeded[Policies.MembersManage].IsTenantScoped.Should().BeTrue();

        seeded.Should().ContainKey(Policies.RolesManage);
        seeded[Policies.RolesManage].RulesJson.Should().Contain("roles:manage");
        // Role administration deliberately has NO TenantAdmin role fallback: only the
        // SystemAdmin role or an explicit roles:manage grant passes.
        seeded[Policies.RolesManage].RulesJson.Should().NotContain("\"TenantAdmin\"");
        seeded[Policies.RolesManage].IsTenantScoped.Should().BeTrue();

        seeded.Should().ContainKey(Policies.SettingsRead);
        seeded[Policies.SettingsRead].RulesJson.Should().Contain("settings:read");
        seeded.Should().ContainKey(Policies.SettingsManage);
        seeded[Policies.SettingsManage].RulesJson.Should().Contain("settings:manage");

        seeded.Should().ContainKey(Policies.AuditRead);
        seeded[Policies.AuditRead].RulesJson.Should().Contain("audit:read");
        seeded[Policies.AuditRead].RulesJson.Should().Contain("TenantAdmin");
        seeded[Policies.AuditRead].IsTenantScoped.Should().BeTrue();

        // System tier (#386): no TenantMatch (platform-scoped) + role fallback or grant.
        seeded.Should().ContainKey(Policies.AuditExport);
        seeded[Policies.AuditExport].RulesJson.Should().Contain("audit:export");
        seeded[Policies.AuditExport].RulesJson.Should().NotContain("TenantMatch");
        seeded[Policies.AuditExport].RulesJson.Should().NotContain("\"TenantAdmin\"");

        seeded.Should().ContainKey(Policies.SystemConfigurationManage);
        seeded[Policies.SystemConfigurationManage].RulesJson.Should().Contain("system:manage-configuration");
        seeded[Policies.SystemConfigurationManage].RulesJson.Should().NotContain("TenantMatch");

        seeded.Should().ContainKey(Policies.SystemBackupRestore);
        seeded[Policies.SystemBackupRestore].RulesJson.Should().Contain("system:backup-restore");
        seeded[Policies.SystemBackupRestore].RulesJson.Should().NotContain("TenantMatch");

        seeded.Should().ContainKey(Policies.SystemDataMigration);
        seeded[Policies.SystemDataMigration].RulesJson.Should().Contain("system:data-migration");
        seeded[Policies.SystemDataMigration].RulesJson.Should().NotContain("TenantMatch");
    }

    // ─── CQRS pipeline enforcement: administrative tier (#294) ───

    [Fact]
    public async Task AuthorizationBehavior_ManageMembers_WithPermission_IsAllowed()
        => await AssertRequestAllowedAsync<ManageMembersRequest>([ManageMembersPermission.Keys.Manage]);

    [Fact]
    public async Task AuthorizationBehavior_ManageMembers_WithoutPermission_IsDenied()
        => await AssertRequestDeniedAsync<ManageMembersRequest>(["unrelated:permission"]);

    [Fact]
    public async Task AuthorizationBehavior_ManageRoles_WithPermission_IsAllowed()
        => await AssertRequestAllowedAsync<ManageRolesRequest>([ManageRolesPermission.Keys.Manage]);

    [Fact]
    public async Task AuthorizationBehavior_ManageRoles_WithoutPermission_IsDenied()
        => await AssertRequestDeniedAsync<ManageRolesRequest>([]);

    [Fact]
    public async Task AuthorizationBehavior_ManageSettings_WithPermission_IsAllowed()
        => await AssertRequestAllowedAsync<ManageSettingsRequest>([ManageSettingsPermission.Keys.Manage]);

    [Fact]
    public async Task AuthorizationBehavior_ManageSettings_WithoutPermission_IsDenied()
        => await AssertRequestDeniedAsync<ManageSettingsRequest>([ManageSettingsPermission.Keys.Read]);

    [Fact]
    public async Task AuthorizationBehavior_AuditRead_WithPermission_IsAllowed()
        => await AssertRequestAllowedAsync<AuditReadRequest>([AuditPermission.Keys.Read]);

    [Fact]
    public async Task AuthorizationBehavior_AuditRead_WithoutPermission_IsDenied()
        => await AssertRequestDeniedAsync<AuditReadRequest>([]);

    [Fact]
    public async Task AuthorizationBehavior_AuditExport_WithPermission_IsAllowed()
        => await AssertRequestAllowedAsync<AuditExportRequest>([AuditPermission.Keys.Export]);

    [Fact]
    public async Task AuthorizationBehavior_AuditExport_WithoutPermission_IsDenied()
        => await AssertRequestDeniedAsync<AuditExportRequest>([AuditPermission.Keys.Read]);

    // ─── CQRS pipeline enforcement: system tier (#386) ───

    [Fact]
    public async Task AuthorizationBehavior_SystemConfiguration_WithPermission_IsAllowed()
        => await AssertRequestAllowedAsync<SystemConfigurationRequest>([SystemConfigurationPermission.Keys.Manage]);

    [Fact]
    public async Task AuthorizationBehavior_SystemConfiguration_WithoutPermission_IsDenied()
        => await AssertRequestDeniedAsync<SystemConfigurationRequest>([]);

    [Fact]
    public async Task AuthorizationBehavior_BackupRestore_WithPermission_IsAllowed()
        => await AssertRequestAllowedAsync<BackupRestoreRequest>([BackupRestorePermission.Keys.Operate]);

    [Fact]
    public async Task AuthorizationBehavior_BackupRestore_WithoutPermission_IsDenied()
        => await AssertRequestDeniedAsync<BackupRestoreRequest>([]);

    [Fact]
    public async Task AuthorizationBehavior_DataMigration_WithPermission_IsAllowed()
        => await AssertRequestAllowedAsync<DataMigrationRequest>([DataMigrationPermission.Keys.Operate]);

    [Fact]
    public async Task AuthorizationBehavior_DataMigration_WithoutPermission_IsDenied()
        => await AssertRequestDeniedAsync<DataMigrationRequest>([]);

    [Fact]
    public async Task AuthorizationBehavior_SystemTier_SystemAdminRole_Bypasses()
    {
        var actor = new ActorContext
        {
            IsAuthenticated = true,
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = Guid.NewGuid(),
            Permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            Roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SystemAdmin" }
        };
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(a => a.ActorContext).Returns(actor);

        foreach (var assert in new Func<Task>[]
                 {
                     () => RunAsync<SystemConfigurationRequest>(accessor.Object, "admin-ok"),
                     () => RunAsync<BackupRestoreRequest>(accessor.Object, "admin-ok"),
                     () => RunAsync<DataMigrationRequest>(accessor.Object, "admin-ok")
                 })
        {
            await assert.Should().NotThrowAsync("system administrators retain platform-wide authority");
        }
    }

    [Fact]
    public async Task AuthorizationBehavior_SystemTier_Unauthenticated_IsDenied()
    {
        var actor = new ActorContext
        {
            IsAuthenticated = false,
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            Permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            Roles = new HashSet<string>()
        };
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(a => a.ActorContext).Returns(actor);
        var behavior = new AuthorizationBehavior<BackupRestoreRequest, string>(accessor.Object, Mock.Of<IAccessControlListService>());

        var act = () => behavior.Handle(
            new BackupRestoreRequest(),
            () => Task.FromResult("ok"),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ─── Actor runtime checks ───

    [Fact]
    public void ActorContext_HasPermission_ResolvesEachKey()
    {
        var actor = new ActorContext
        {
            IsAuthenticated = true,
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = Guid.NewGuid(),
            Permissions = new HashSet<string>(
                new[]
                {
                    ManageMembersPermission.Keys.Manage,
                    ManageRolesPermission.Keys.Manage,
                    ManageSettingsPermission.Keys.Manage,
                    AuditPermission.Keys.Read,
                    SystemConfigurationPermission.Keys.Manage,
                    BackupRestorePermission.Keys.Operate,
                    DataMigrationPermission.Keys.Operate
                },
                StringComparer.OrdinalIgnoreCase),
            Roles = new HashSet<string>()
        };

        actor.HasPermission(ManageMembersPermission.Manage).Should().BeTrue();
        actor.HasPermission(ManageRolesPermission.Manage).Should().BeTrue();
        actor.HasPermission(ManageSettingsPermission.Manage).Should().BeTrue();
        actor.HasPermission(AuditPermission.Read).Should().BeTrue();
        actor.HasPermission(SystemConfigurationPermission.Manage).Should().BeTrue();
        actor.HasPermission(BackupRestorePermission.Operate).Should().BeTrue();
        actor.HasPermission(DataMigrationPermission.Operate).Should().BeTrue();
        actor.HasPermission("unknown:permission").Should().BeFalse();
    }

    // ─── Setup & fixtures ───

    private static async Task AssertRequestAllowedAsync<TRequest>(string[] permissions)
        where TRequest : IRequestBase, new()
    {
        var (accessor, acl) = SetupActor(permissions);
        var result = await RunAsync<TRequest>(accessor.Object, "ok", acl.Object);

        result.Should().Be("ok");
    }

    private static async Task AssertRequestDeniedAsync<TRequest>(string[] permissions)
        where TRequest : IRequestBase, new()
    {
        var (accessor, acl) = SetupActor(permissions);
        var behavior = new AuthorizationBehavior<TRequest, string>(accessor.Object, acl.Object);

        var act = () => behavior.Handle(
            new TRequest(),
            () => Task.FromResult("ok"),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    private static async Task<string> RunAsync<TRequest>(
        IActorContextAccessor accessor,
        string expected,
        IAccessControlListService? acl = null)
        where TRequest : IRequestBase, new()
    {
        var behavior = new AuthorizationBehavior<TRequest, string>(accessor, acl ?? Mock.Of<IAccessControlListService>());
        return await behavior.Handle(
            new TRequest(),
            () => Task.FromResult(expected),
            CancellationToken.None);
    }

    private static (Mock<IActorContextAccessor> Accessor, Mock<IAccessControlListService> Acl) SetupActor(
        string[] permissions)
    {
        var actor = new ActorContext
        {
            IsAuthenticated = true,
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = Guid.NewGuid(),
            Permissions = new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase),
            Roles = new HashSet<string>()
        };
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(a => a.ActorContext).Returns(actor);
        return (accessor, new Mock<IAccessControlListService>());
    }

    [AuthorizeRequest(ManageMembersPermission.Keys.Manage)]
    public sealed record ManageMembersRequest : IRequestBase;

    [AuthorizeRequest(ManageRolesPermission.Keys.Manage)]
    public sealed record ManageRolesRequest : IRequestBase;

    [AuthorizeRequest(ManageSettingsPermission.Keys.Manage)]
    public sealed record ManageSettingsRequest : IRequestBase;

    [AuthorizeRequest(AuditPermission.Keys.Read)]
    public sealed record AuditReadRequest : IRequestBase;

    [AuthorizeRequest(AuditPermission.Keys.Export)]
    public sealed record AuditExportRequest : IRequestBase;

    [AuthorizeRequest(SystemConfigurationPermission.Keys.Manage)]
    public sealed record SystemConfigurationRequest : IRequestBase;

    [AuthorizeRequest(BackupRestorePermission.Keys.Operate)]
    public sealed record BackupRestoreRequest : IRequestBase;

    [AuthorizeRequest(DataMigrationPermission.Keys.Operate)]
    public sealed record DataMigrationRequest : IRequestBase;
}
