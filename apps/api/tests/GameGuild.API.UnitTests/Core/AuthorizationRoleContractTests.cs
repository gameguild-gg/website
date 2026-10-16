using FluentAssertions;
using GameGuild.Commerce.Billing;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Tenants;
using Microsoft.AspNetCore.Authorization;

namespace GameGuild.API.UnitTests.Core;

/// <summary>
///     Endpoint-to-permission gating matrix for the administrative (#294) and system
///     (#386) permission tiers. Every administrative surface (member management, role
///     administration, tenant settings, audit reads) and every system surface
///     (infrastructure configuration, backup/restore, data migration) must demand its
///     granular policy instead of the previous blanket SystemAdmin/TenantAdmin checks.
/// </summary>
public sealed class AuthorizationRoleContractTests
{
    [Theory]
    [InlineData(typeof(KeyRotationController), "class")]
    [InlineData(typeof(EventTransportAdminController), "class")]
    [InlineData(typeof(AuditReplayController), "class")]
    public void SystemTierControllers_Require_TheirGranularPolicies(Type controllerType, string _)
    {
        var authorize = controllerType
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Should()
            .ContainSingle()
            .Subject;

        authorize.Policy.Should().Be(
            controllerType == typeof(KeyRotationController)
                ? Policies.SystemConfigurationManage
                : Policies.SystemBackupRestore);
        authorize.Roles.Should().BeNullOrEmpty();
    }

    // ─── Issue #294: role administration ───

    [Fact]
    public void RolesController_EveryAction_Requires_RolesManagePolicy()
    {
        var administrativeEndpointNames = new[]
        {
            nameof(RolesController.GetAll),
            nameof(RolesController.GetById),
            nameof(RolesController.Create),
            nameof(RolesController.Update),
            nameof(RolesController.Delete),
            nameof(RolesController.GetUserRoles),
            nameof(RolesController.AssignRoleToUser),
            nameof(RolesController.BulkAssignRoles),
            nameof(RolesController.RemoveRoleFromUser)
        };

        foreach (var methodName in administrativeEndpointNames)
        {
            var authorize = typeof(RolesController)
                .GetMethod(methodName)!
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>()
                .Should()
                .ContainSingle()
                .Subject;

            authorize.Policy.Should().Be(
                Policies.RolesManage,
                $"role administration action {methodName} must require the granular Roles.Manage policy (#294)");
        }
    }

    // ─── Issue #294: tenant member management ───

    [Fact]
    public void UserMembershipsController_ManagementActions_Require_MembersManagePolicy()
    {
        var memberManagementEndpoints = new[]
        {
            nameof(UserMembershipsController.AddUserMembership),
            nameof(UserMembershipsController.UpdateUserMembershipRole),
            nameof(UserMembershipsController.DeactivateUserMembership),
            nameof(UserMembershipsController.ActivateUserMembership),
            nameof(UserMembershipsController.ResendUserMembershipInvite),
            nameof(UserMembershipsController.CancelUserMembershipInvite)
        };

        foreach (var methodName in memberManagementEndpoints)
        {
            var authorize = typeof(UserMembershipsController)
                .GetMethod(methodName)!
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
                .Cast<AuthorizeAttribute>()
                .Should()
                .ContainSingle()
                .Subject;

            authorize.Policy.Should().Be(
                Policies.MembersManage,
                $"member management action {methodName} must require the granular Members.Manage policy (#294)");
        }

        // The self-service membership reads keep the Users self-tier.
        typeof(UserMembershipsController)
            .GetMethod(nameof(UserMembershipsController.GetUserMemberships))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Should()
            .ContainSingle(attribute => attribute.Policy == Policies.UsersReadSelf);
    }

    // ─── Issue #294: tenant settings ───

    [Theory]
    [InlineData(nameof(TenantSettingsController.GetSettings), "Settings.Read")]
    [InlineData(nameof(TenantSettingsController.UpdateSettings), "Settings.Manage")]
    [InlineData(nameof(TenantSettingsController.ReplaceSettings), "Settings.Manage")]
    [InlineData(nameof(TenantSettingsController.GetFeatureFlags), "Settings.Read")]
    [InlineData(nameof(TenantSettingsController.UpdateFeatureFlags), "Settings.Manage")]
    [InlineData(nameof(TenantSettingsController.GetSystemLimits), "Settings.Read")]
    [InlineData(nameof(TenantSettingsController.UpdateSystemLimits), "Settings.Manage")]
    [InlineData(nameof(TenantSettingsController.GetIntegrationSettings), "Settings.Read")]
    [InlineData(nameof(TenantSettingsController.UpdateIntegrationSettings), "Settings.Manage")]
    public void TenantSettingsController_Actions_Require_SettingsTierPolicies(string actionName, string expectedPolicy)
    {
        var authorize = typeof(TenantSettingsController)
            .GetMethod(actionName)!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Should()
            .ContainSingle($"settings action {actionName} must carry a granular settings policy (#294)")
            .Subject;

        authorize.Policy.Should().Be(expectedPolicy);
    }

    // ─── Issue #294: audit query surface ───

    [Theory]
    [InlineData(nameof(AuditController.GetAuditLogs), "Audit.Read")]
    [InlineData(nameof(AuditController.SearchAuditLogsByDateRange), "Audit.Read")]
    [InlineData(nameof(AuditController.SearchAuditLogsWithConditions), "Audit.Read")]
    [InlineData(nameof(AuditController.SearchAuditLogsByActionType), "Audit.Read")]
    [InlineData(nameof(AuditController.GetAuditActionTypeTaxonomy), "Audit.Read")]
    [InlineData(nameof(AuditController.GetAuditStatistics), "Audit.Read")]
    [InlineData(nameof(AuditController.ExportAuditLogsByActionType), "Audit.Export")]
    [InlineData(nameof(AuditController.ExportAuditLogs), "Audit.Export")]
    [InlineData(nameof(AuditController.ExportAuditLogsJson), "Audit.Export")]
    [InlineData(nameof(AuditController.GetAuditExportProgress), "Audit.Export")]
    [InlineData(nameof(AuditController.CreateScheduledAuditExport), "Audit.Export")]
    [InlineData(nameof(AuditController.GetScheduledAuditExports), "Audit.Export")]
    [InlineData(nameof(AuditController.DisableScheduledAuditExport), "Audit.Export")]
    [InlineData(nameof(AuditController.GetScheduledAuditExportHistory), "Audit.Export")]
    [InlineData(nameof(AuditController.DownloadScheduledAuditExport), "Audit.Export")]
    public void AuditController_Actions_Require_AuditTierPolicies(string actionName, string expectedPolicy)
    {
        var authorize = typeof(AuditController)
            .GetMethod(actionName)!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Should()
            .ContainSingle($"audit action {actionName} must carry a granular audit policy (#294)")
            .Subject;

        authorize.Policy.Should().Be(expectedPolicy);
    }

    [Theory]
    [InlineData(nameof(SecurityAuditController.GetSecurityAuditLogs), "Audit.Read")]
    // GetAuthenticationLogs intentionally stays on SystemAdmin: the authentication-attempt
    // surface has no tenant-scoped filter, so it cannot join the tenant-clamped tier.
    [InlineData(nameof(SecurityAuditController.GetAuthenticationLogs), "SystemAdmin")]
    [InlineData(nameof(SecurityAuditController.GetPermissionLogs), "Audit.Read")]
    [InlineData(nameof(SecurityAuditController.GetSecurityDashboard), "Audit.Read")]
    [InlineData(nameof(SecurityAuditController.ExportSecurityAuditLogs), "Audit.Export")]
    public void SecurityAuditController_Actions_Require_AuditTierPolicies(string actionName, string expectedPolicy)
    {
        var authorize = typeof(SecurityAuditController)
            .GetMethod(actionName)!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Should()
            .ContainSingle($"security audit action {actionName} must carry a granular audit policy (#294)")
            .Subject;

        authorize.Policy.Should().Be(expectedPolicy);
    }

    [Fact]
    public void AuditQueryController_Requires_AuditReadPolicy()
    {
        var authorize = typeof(AuditQueryController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Should()
            .ContainSingle()
            .Subject;

        // Audit queries expose tenant-wide audit data; the Audit.Read tier plus the
        // controller's built-in tenant-scope clamp govern the self-service access (#294).
        authorize.Policy.Should().Be(Policies.AuditRead);
    }

    // ─── Issue #386: billing provider configuration & migration ───

    [Theory]
    [InlineData(nameof(BillingExternalProvidersController.ListProviders), "SystemConfiguration.Manage")]
    [InlineData(nameof(BillingExternalProvidersController.GetProvider), "SystemConfiguration.Manage")]
    [InlineData(nameof(BillingExternalProvidersController.EnableProvider), "SystemConfiguration.Manage")]
    [InlineData(nameof(BillingExternalProvidersController.DisableProvider), "SystemConfiguration.Manage")]
    [InlineData(nameof(BillingExternalProvidersController.MigrationDryRun), "System.DataMigration")]
    public void BillingExternalProvidersController_Actions_Require_SystemTierPolicies(string actionName, string expectedPolicy)
    {
        var authorize = typeof(BillingExternalProvidersController)
            .GetMethod(actionName)!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Should()
            .ContainSingle($"action {actionName} must carry exactly one [Authorize]")
            .Subject;

        authorize.Policy.Should().Be(expectedPolicy);
        authorize.Roles.Should().BeNullOrEmpty();
    }
}
