using FluentAssertions;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Authorization;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Entities;

/// <summary>
///     Entity-contract tests for the content-type scope/hierarchy fields (issue #314):
///     parent links with self-parenting rejection, restriction/applies-to JSON
///     validation, and registration semantics.
/// </summary>
public class ContentTypePermissionScopeHierarchyTests
{
    [Fact]
    public void SetHierarchyParent_AcceptsValidParent()
    {
        var entity = new ContentTypePermission(null, Guid.NewGuid(), "Report");

        entity.SetHierarchyParent("Document");

        entity.ParentContentTypeName.Should().Be("Document");
    }

    [Fact]
    public void SetHierarchyParent_TrimsAndAcceptsNullToDetach()
    {
        var entity = new ContentTypePermission(null, Guid.NewGuid(), "Report");
        entity.SetHierarchyParent("Document");

        entity.SetHierarchyParent("  Base  ");

        entity.ParentContentTypeName.Should().Be("Base");

        entity.SetHierarchyParent(null);

        entity.ParentContentTypeName.Should().BeNull();
    }

    [Fact]
    public void SetHierarchyParent_RejectsSelfParenting()
    {
        var entity = new ContentTypePermission(null, Guid.NewGuid(), "Report");

        var act = () => entity.SetHierarchyParent("Report");

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SetHierarchyParent_RejectsWhitespaceParent(string parent)
    {
        var entity = new ContentTypePermission(null, Guid.NewGuid(), "Report");

        var act = () => entity.SetHierarchyParent(parent);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetRestrictions_AcceptsJsonObjectAndClears()
    {
        var entity = new ContentTypePermission(null, Guid.NewGuid(), "Report");

        entity.SetRestrictions(@"{""blockedPermissions"":[""Delete""],""maxGrantDurationHours"":720}");

        entity.Restrictions.Should().Contain("blockedPermissions");

        entity.SetRestrictions(null);

        entity.Restrictions.Should().BeNull();
    }

    [Theory]
    [InlineData("[1,2]")]
    [InlineData("not json")]
    public void SetRestrictions_RejectsNonObjectPayload(string payload)
    {
        var entity = new ContentTypePermission(null, Guid.NewGuid(), "Report");

        var act = () => entity.SetRestrictions(payload);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SetAppliesTo_AcceptsArrayOfSelectors()
    {
        var entity = new ContentTypePermission(null, Guid.NewGuid(), "Report");

        entity.SetAppliesTo(@"[""tenant:all"",""module:Content""]");

        entity.AppliesTo.Should().Contain("tenant:all");
    }

    [Theory]
    [InlineData(@"{""scope"":""tenant""}")]
    [InlineData("nope")]
    public void SetAppliesTo_RejectsNonArrayPayload(string payload)
    {
        var entity = new ContentTypePermission(null, Guid.NewGuid(), "Report");

        var act = () => entity.SetAppliesTo(payload);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Registration_DefaultsOff_AndTogglesWithActor()
    {
        var entity = new ContentTypePermission(null, Guid.NewGuid(), "Report");
        var admin = Guid.NewGuid();

        entity.IsRegistered.Should().BeFalse("unregistered rows keep exact-name semantics by default");

        entity.Register(admin);

        entity.IsRegistered.Should().BeTrue();
        entity.GrantedBy.Should().Be(admin);

        entity.Deregister(admin);

        entity.IsRegistered.Should().BeFalse();
    }
}

/// <summary>
///     Catalogue tests for the granular permission stragglers (issue #333):
///     EditMetadata, maintenance/monitoring and deployment permission values, typed
///     keys and facade constants. BackupRestore landed with #841 and is asserted here
///     alongside.
/// </summary>
public class GranularPermissionCatalogueStragglerTests
{
    [Fact]
    public void PermissionType_ContainsTheFourStragglers()
    {
        ((int)PermissionType.EditMetadata).Should().Be(211);
        ((int)PermissionType.Maintenance).Should().Be(212);
        ((int)PermissionType.Monitoring).Should().Be(213);
        ((int)PermissionType.Deployment).Should().Be(214);
    }

    [Fact]
    public void BackupRestore_LandedWithAdministrativeTier()
    {
        ((int)PermissionType.BackupRestore).Should().Be(209);
        Permissions.SystemBackupRestore.Should().Be("system:backup-restore");
    }

    [Fact]
    public void TypedKeys_ExposeTheStragglerPermissions()
    {
        ContentPermission.Keys.EditMetadata.Should().Be("content:edit-metadata");
        SystemMaintenancePermission.Keys.Execute.Should().Be("system:maintenance");
        SystemMaintenancePermission.Keys.Monitor.Should().Be("system:monitoring");
        DeploymentPermission.Keys.Deploy.Should().Be("system:deploy");
    }

    [Fact]
    public void Facade_RegistersTheStragglerConstants()
    {
        Permissions.ContentEditMetadata.Should().Be("content:edit-metadata");
        Permissions.SystemMaintenance.Should().Be("system:maintenance");
        Permissions.SystemMonitoring.Should().Be("system:monitoring");
        Permissions.SystemDeployment.Should().Be("system:deploy");
    }
}
