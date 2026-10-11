using GameGuild.Identity.Authentication;

namespace GameGuild.Identity.Authentication.UnitTests;

public sealed class LocalSignInPolicyOptionsTests
{
    [Fact]
    public void DefaultKeepsExistingUnverifiedSignInPolicy()
    {
        var options = new LocalSignInPolicyOptions();
        Assert.True(options.IsValid());
        Assert.False(options.RequiresVerifiedEmail(Guid.NewGuid()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GlobalRequirementCannotBeWeakenedByTenantList(bool listed)
    {
        var tenantId = Guid.NewGuid();
        var options = new LocalSignInPolicyOptions { RequireEmailVerification = true };
        if (listed) options.RequireEmailVerificationForTenants.Add(tenantId);
        Assert.True(options.RequiresVerifiedEmail(tenantId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TenantRequirementAppliesOnlyToTheResolvedTenant(bool listed)
    {
        var tenantId = Guid.NewGuid();
        var options = new LocalSignInPolicyOptions();
        options.RequireEmailVerificationForTenants.Add(listed ? tenantId : Guid.NewGuid());
        Assert.Equal(listed, options.RequiresVerifiedEmail(tenantId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidTenantListFailsClosed(bool missing)
    {
        var options = new LocalSignInPolicyOptions
        {
            RequireEmailVerificationForTenants = missing ? null! : [Guid.Empty]
        };
        Assert.False(options.IsValid());
        Assert.Throws<InvalidOperationException>(() => options.RequiresVerifiedEmail(Guid.NewGuid()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrEmptyResolvedTenantFailsClosed(bool empty)
    {
        var options = new LocalSignInPolicyOptions();
        Assert.Throws<ArgumentException>(() => options.RequiresVerifiedEmail(empty ? Guid.Empty : null));
    }

    [Fact]
    public void DuplicateConfiguredTenantsDoNotChangeRequirement()
    {
        var tenantId = Guid.NewGuid();
        var options = new LocalSignInPolicyOptions { RequireEmailVerificationForTenants = [tenantId, tenantId] };
        Assert.True(options.IsValid());
        Assert.True(options.RequiresVerifiedEmail(tenantId));
    }
}
