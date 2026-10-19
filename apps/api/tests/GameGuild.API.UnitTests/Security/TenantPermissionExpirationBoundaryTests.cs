using System.Reflection;
using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace GameGuild.API.UnitTests.Security;

public sealed class TenantPermissionExpirationBoundaryTests
{
    [Theory]
    [InlineData(nameof(TenantPermissionsController.SetPermissionExpiration), typeof(SetTenantPermissionExpirationCommand))]
    [InlineData(nameof(TenantPermissionsController.ExtendPermissionExpiration), typeof(ExtendTenantPermissionExpirationCommand))]
    public void ExpirationEndpoint_PreservesAuthorizationAndGuardedCommand(string actionName, Type commandType)
    {
        var controllerType = typeof(TenantPermissionsController);
        Assert.NotEmpty(controllerType.GetCustomAttributes<AuthorizeAttribute>(inherit: true));
        Assert.Empty(controllerType.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true));
        var action = controllerType.GetMethod(actionName);
        Assert.NotNull(action);
        Assert.Empty(action.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true));
        Assert.NotEmpty(action.GetCustomAttributes<HttpPostAttribute>(inherit: true));
        Assert.Equal(typeof(Task<IActionResult>), action.ReturnType);

        var parameters = action.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal(commandType, parameters[0].ParameterType);
        Assert.NotNull(parameters[0].GetCustomAttribute<FromBodyAttribute>());
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
    }
}
