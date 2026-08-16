using FluentAssertions;
using GameGuild.Identity.Tenants;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class TenantAccessContextResolverTests
{
    [Fact]
    public void Resolve_RequestedInactiveTenant_RejectsTenantSelectionWithoutElevatingAccess()
    {
        var requestedTenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var memberships = new GetUserMembershipsResponse
        {
            TotalCount = 2,
            Memberships =
            [
                new UserMembershipDto
                {
                    TenantId = requestedTenantId,
                    TenantName = "Requested",
                    TenantSlug = "requested",
                    TenantIsActive = false,
                    TenantIsDefault = true,
                    Role = "Member",
                    IsActive = true
                },
                new UserMembershipDto
                {
                    TenantId = otherTenantId,
                    TenantName = "Other",
                    TenantSlug = "other",
                    TenantIsActive = false,
                    Role = "SystemAdmin",
                    IsActive = true
                }
            ]
        };

        var result = TenantAccessContextResolver.Resolve(memberships, requestedTenantId);

        result.TenantId.Should().BeNull();
        result.AvailableTenants.Should().HaveCount(2);
        result.Roles.Should().BeEquivalentTo("User");
    }

    [Fact]
    public void Resolve_UnknownRequestedTenant_DoesNotFallBackToDefaultTenant()
    {
        var defaultTenantId = Guid.NewGuid();
        var memberships = new GetUserMembershipsResponse
        {
            TotalCount = 1,
            Memberships =
            [
                new UserMembershipDto
                {
                    TenantId = defaultTenantId,
                    TenantName = "Default",
                    TenantSlug = "default",
                    TenantIsActive = true,
                    TenantIsDefault = true,
                    Role = "SystemAdmin",
                    IsActive = true
                }
            ]
        };

        var result = TenantAccessContextResolver.Resolve(memberships, Guid.NewGuid());

        result.TenantId.Should().BeNull();
        result.AvailableTenants.Should().ContainSingle(tenant => tenant.Id == defaultTenantId);
        result.Roles.Should().BeEquivalentTo("User");
    }

    [Fact]
    public void Resolve_NoRequestedTenant_SelectsActiveDefaultTenant()
    {
        var defaultTenantId = Guid.NewGuid();
        var memberships = new GetUserMembershipsResponse
        {
            TotalCount = 1,
            Memberships =
            [
                new UserMembershipDto
                {
                    TenantId = defaultTenantId,
                    TenantName = "Default",
                    TenantSlug = "default",
                    TenantIsActive = true,
                    TenantIsDefault = true,
                    Role = "SystemAdmin",
                    IsActive = true
                }
            ]
        };

        var result = TenantAccessContextResolver.Resolve(memberships, null);

        result.TenantId.Should().Be(defaultTenantId);
        result.Roles.Should().BeEquivalentTo("SystemAdmin", "User");
    }
}
