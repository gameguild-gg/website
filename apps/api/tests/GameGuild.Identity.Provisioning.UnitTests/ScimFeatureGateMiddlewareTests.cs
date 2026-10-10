using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace GameGuild.Identity.Provisioning.UnitTests;

/// <summary>
///     Feature-gate behavior: /scim requests get 404 unless Scim:Enabled is true, and
///     every other route is untouched regardless of the flag.
/// </summary>
public sealed class ScimFeatureGateMiddlewareTests
{
    [Theory]
    [InlineData("/scim/v2/Users", 404)]
    [InlineData("/scim/v2/ServiceProviderConfig", 404)]
    [InlineData("/SCIM/v2/Users", 404)]
    [InlineData("/api/v1/users", 200)]
    [InlineData("/health", 200)]
    public async Task DisabledFeature_Returns404ForScimPathsOnly(string path, int expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        var middleware = new ScimFeatureGateMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, Options.Create(new ScimProvisioningOptions { Enabled = false }));

        Assert.Equal(expected, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("/scim/v2/Users")]
    [InlineData("/scim/v2/Groups")]
    public async Task EnabledFeature_PassesScimRequestsThrough(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        var called = false;
        var middleware = new ScimFeatureGateMiddleware(_ => { called = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, Options.Create(new ScimProvisioningOptions { Enabled = true }));

        Assert.True(called);
        Assert.Equal(200, context.Response.StatusCode);
    }
}
