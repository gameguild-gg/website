using FluentAssertions;
using GameGuild.Identity.Tenants;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class TenantAccessContextResolverTests
{
    [Fact]
    public void Resolve_AllTenantsInactive_DoesNotGrantTenantAccess()
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
}
