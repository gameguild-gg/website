using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Http;

namespace GameGuild.Identity.Authorization.UnitTests;

public sealed class RequireSearchPermissionAttributeTests
{
    [Theory]
    [InlineData("", false)]
    [InlineData("?SearchText=", false)]
    [InlineData("?SearchText=%20", false)]
    [InlineData("?SearchText=needle", true)]
    [InlineData("?sEaRcHtExT=needle", true)]
    [InlineData("?SearchText=&SearchText=needle", true)]
    [InlineData("?SearchText=needle&SearchText=", true)]
    [InlineData("?Other=needle", false)]
    public void SearchRequirementMatchesAllValuesOfItsNamedParameter(string query, bool expected)
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString(query);
        Assert.Equal(expected, new RequireSearchPermissionAttribute("SearchText").ShouldApply(context));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void InvalidParameterNamesAreRejected(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => new RequireSearchPermissionAttribute(name!));
    }

    [Fact]
    public void SearchUsesTheExistingTenantDacContract()
    {
        var requirement = new RequireSearchPermissionAttribute("SearchText");
        Assert.Equal(PermissionLayer.Tenant, requirement.Layer);
        Assert.Equal(QueryPermission.Keys.Search, Assert.Single(requirement.RequiredPermissions));
        requirement.Validate();
    }
}
