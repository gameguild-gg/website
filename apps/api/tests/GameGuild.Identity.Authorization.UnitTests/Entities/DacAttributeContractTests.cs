using FluentAssertions;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests.Entities;

/// <summary>
///     Matrix tests for the <see cref="IDacAttribute"/> contract (issues #318/#320):
///     owner / owner-group / permission-list projections and the inheritance-policy
///     combinations across the concrete carriers — the resource-grant entity (owner vs
///     non-owner row), the content-type grant entity and the standalone
///     <see cref="DacAttributeInfo"/>.
/// </summary>
public class DacAttributeContractTests
{
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    // ── ResourceUserPermission: owner × permission-list × inheritance ──────

    [Fact]
    public void ResourceOwnerRow_ReportsOwnerAndInheritAndPropagate()
    {
        var row = new ResourceUserPermission
        {
            TenantId = new GameGuild.CQRS.Models.TenantId(Guid.NewGuid()),
            UserId = _ownerId,
            ResourceType = "Project",
            ResourceId = "42",
            Permissions = ["Read", "Write"],
            GrantedByUserId = _ownerId,
            IsOwner = true
        };
        var dac = (IDacAttribute)row;

        dac.Owner.Should().Be(_ownerId);
        dac.OwnerGroup.Should().BeNull();
        dac.PermissionList.Should().Equal("Read", "Write");
        // The owner row's set is honored at tenant parity and on the exact resource by
        // the engine's owner override: InheritAndPropagate.
        dac.InheritancePolicy.Should().Be(DacInheritancePolicy.InheritAndPropagate);
    }

    [Fact]
    public void ResourceNonOwnerRow_ReportsOwnerAndNoInheritance()
    {
        var row = new ResourceUserPermission
        {
            TenantId = new GameGuild.CQRS.Models.TenantId(Guid.NewGuid()),
            UserId = _userId,
            ResourceType = "Project",
            ResourceId = "42",
            Permissions = ["Read"],
            GrantedByUserId = _ownerId,
            IsOwner = false
        };
        var dac = (IDacAttribute)row;

        dac.Owner.Should().Be(_userId);
        dac.PermissionList.Should().Equal("Read");
        // A plain resource grant applies to the exact resource only.
        dac.InheritancePolicy.Should().Be(DacInheritancePolicy.None);
    }

    [Fact]
    public void ResourceRow_EmptyPermissionList_IsReportedFaithfully()
    {
        var row = new ResourceUserPermission
        {
            TenantId = new GameGuild.CQRS.Models.TenantId(Guid.NewGuid()),
            UserId = _userId,
            ResourceType = "Project",
            ResourceId = "42",
            Permissions = [],
            GrantedByUserId = _ownerId
        };
        var dac = (IDacAttribute)row;

        dac.PermissionList.Should().BeEmpty();
    }

    // ── ContentTypePermissionGrant: owner × permission-list × inheritance ──

    [Fact]
    public void ContentTypeGrant_UserRow_ReportsOwnerAndPropagateToChildren()
    {
        var grant = new ContentTypePermissionGrant
        {
            UserId = _userId,
            TenantId = Guid.NewGuid(),
            ContentTypeName = "Course",
            Permissions = ["Read"],
            DenyPermissions = []
        };
        var dac = (IDacAttribute)grant;

        dac.Owner.Should().Be(_userId);
        dac.OwnerGroup.Should().BeNull();
        dac.PermissionList.Should().Equal("Read");
        // Content-type grants flow down to every resource of the type.
        dac.InheritancePolicy.Should().Be(DacInheritancePolicy.PropagateToChildren);
    }

    [Fact]
    public void ContentTypeGrant_TenantDefaultRow_ReportsNoOwner()
    {
        var grant = new ContentTypePermissionGrant
        {
            UserId = null,
            TenantId = Guid.NewGuid(),
            ContentTypeName = "Course",
            Permissions = ["Read"]
        };
        var dac = (IDacAttribute)grant;

        dac.Owner.Should().BeNull("tenant-default rows apply to every user of the tenant");
        dac.PermissionList.Should().Equal("Read");
        dac.InheritancePolicy.Should().Be(DacInheritancePolicy.PropagateToChildren);
    }

    // ── DacAttributeInfo: standalone carrier matrix ────────────────────────

    [Theory]
    [InlineData(DacInheritancePolicy.None)]
    [InlineData(DacInheritancePolicy.InheritFromParent)]
    [InlineData(DacInheritancePolicy.PropagateToChildren)]
    [InlineData(DacInheritancePolicy.InheritAndPropagate)]
    public void DacAttributeInfo_CarriesEveryInheritancePolicy(DacInheritancePolicy policy)
    {
        var info = new DacAttributeInfo(_userId, "editors", ["Read", "Write"], policy);

        info.Owner.Should().Be(_userId);
        info.OwnerGroup.Should().Be("editors");
        info.PermissionList.Should().Equal("Read", "Write");
        info.InheritancePolicy.Should().Be(policy);
    }

    [Fact]
    public void DacAttributeInfo_ForOwnerFactory_UsesDefaults()
    {
        var info = DacAttributeInfo.ForOwner(_ownerId, "Read");

        info.Owner.Should().Be(_ownerId);
        info.OwnerGroup.Should().BeNull();
        info.PermissionList.Should().Equal("Read");
        info.InheritancePolicy.Should().Be(DacInheritancePolicy.None);
    }
}
