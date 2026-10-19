using GameGuild.CQRS;
using GameGuild.CQRS.Models;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests.Controllers;

public sealed class TenantPermissionExpirationLoggingTests
{
    [Fact]
    public async Task SetExpiration_LogsOnlyCountAndForwardsExactCommand()
    {
        var sender = new Mock<ISender>();
        var logger = new Mock<ILogger<TenantPermissionsController>>();
        using var cancellation = new CancellationTokenSource();
        var command = new SetTenantPermissionExpirationCommand
        {
            TenantId = new TenantId(Guid.NewGuid()),
            PermissionIds = [Guid.NewGuid(), Guid.NewGuid()],
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            Reason = "private-account@example.test\r\nFORGED-ENTRY"
        };
        sender.Setup(dispatcher => dispatcher.Send(command, cancellation.Token)).ReturnsAsync(2);

        var result = await new TenantPermissionsController(sender.Object, logger.Object)
            .SetPermissionExpiration(command, cancellation.Token);

        var response = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(2, JsonSerializer.SerializeToElement(response.Value).GetProperty("Updated").GetInt32());
        sender.Verify(dispatcher => dispatcher.Send(command, cancellation.Token), Times.Once);
        AssertCountLog(logger, "Setting expiration for {Count} permission grants in tenant {TenantId}", command.PermissionIds.Length, command.TenantId);
    }

    [Fact]
    public async Task ExtendExpiration_LogsOnlyCountAndForwardsExactCommand()
    {
        var sender = new Mock<ISender>();
        var logger = new Mock<ILogger<TenantPermissionsController>>();
        using var cancellation = new CancellationTokenSource();
        var command = new ExtendTenantPermissionExpirationCommand
        {
            TenantId = new TenantId(Guid.NewGuid()),
            PermissionIds = [Guid.NewGuid(), Guid.NewGuid()],
            Extension = TimeSpan.FromHours(2),
            Reason = "private-account@example.test\r\nFORGED-ENTRY"
        };
        sender.Setup(dispatcher => dispatcher.Send(command, cancellation.Token)).ReturnsAsync(2);

        var result = await new TenantPermissionsController(sender.Object, logger.Object)
            .ExtendPermissionExpiration(command, cancellation.Token);

        var response = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(2, JsonSerializer.SerializeToElement(response.Value).GetProperty("Extended").GetInt32());
        sender.Verify(dispatcher => dispatcher.Send(command, cancellation.Token), Times.Once);
        AssertCountLog(logger, "Extending expiration for {Count} permission grants in tenant {TenantId}", command.PermissionIds.Length, command.TenantId);
    }

    [Fact]
    public async Task SetExpiration_PropagatesAuthorizationFailure()
    {
        var sender = new Mock<ISender>();
        var logger = new Mock<ILogger<TenantPermissionsController>>();
        var command = new SetTenantPermissionExpirationCommand
        {
            TenantId = new TenantId(Guid.NewGuid()),
            PermissionIds = [Guid.NewGuid()]
        };
        var failure = new UnauthorizedAccessException("private-account@example.test\r\nFORGED-ENTRY");
        sender.Setup(dispatcher => dispatcher.Send(command, CancellationToken.None)).ThrowsAsync(failure);

        var actual = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new TenantPermissionsController(sender.Object, logger.Object).SetPermissionExpiration(command, CancellationToken.None));

        Assert.Same(failure, actual);
        AssertCountLog(logger, "Setting expiration for {Count} permission grants in tenant {TenantId}", 1, command.TenantId);
    }

    [Fact]
    public async Task ExtendExpiration_PropagatesAuthorizationFailure()
    {
        var sender = new Mock<ISender>();
        var logger = new Mock<ILogger<TenantPermissionsController>>();
        var command = new ExtendTenantPermissionExpirationCommand
        {
            TenantId = new TenantId(Guid.NewGuid()),
            PermissionIds = [Guid.NewGuid()],
            Extension = TimeSpan.FromHours(1)
        };
        var failure = new UnauthorizedAccessException("private-account@example.test\r\nFORGED-ENTRY");
        sender.Setup(dispatcher => dispatcher.Send(command, CancellationToken.None)).ThrowsAsync(failure);

        var actual = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new TenantPermissionsController(sender.Object, logger.Object).ExtendPermissionExpiration(command, CancellationToken.None));

        Assert.Same(failure, actual);
        AssertCountLog(logger, "Extending expiration for {Count} permission grants in tenant {TenantId}", 1, command.TenantId);
    }

    private static void AssertCountLog(Mock<ILogger<TenantPermissionsController>> logger, string template, int expectedCount, TenantId expectedTenantId)
    {
        var call = Assert.Single(logger.Invocations, invocation => invocation.Method.Name == "Log");
        Assert.Equal(LogLevel.Information, call.Arguments[0]);
        Assert.Null(call.Arguments[3]);
        var state = Assert.IsAssignableFrom<IEnumerable<KeyValuePair<string, object?>>>(call.Arguments[2]).ToArray();
        Assert.Equal(new[] { "Count", "TenantId", "{OriginalFormat}" }, state.Select(entry => entry.Key).ToArray());
        Assert.Equal(expectedCount, state.Single(entry => entry.Key == "Count").Value);
        Assert.Equal(expectedTenantId, state.Single(entry => entry.Key == "TenantId").Value);
        Assert.Equal(template, state.Single(entry => entry.Key == "{OriginalFormat}").Value);
    }
}
