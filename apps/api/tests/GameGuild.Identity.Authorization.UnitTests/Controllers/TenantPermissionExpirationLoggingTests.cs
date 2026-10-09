using System.Text.Json;
using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.CQRS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests.Controllers;

public sealed class TenantPermissionExpirationLoggingTests
{
    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(false, "ordinary reason")]
    [InlineData(true, "ordinary reason")]
    [InlineData(false, "reason\r\nFORGED EVENT")]
    [InlineData(true, "reason\r\nFORGED EVENT")]
    [InlineData(false, "reason\u0000\u001F\u007F\u0085\u009F\u2028\u2029FORGED EVENT")]
    [InlineData(true, "reason\u0000\u001F\u007F\u0085\u009F\u2028\u2029FORGED EVENT")]
    public async Task ExpirationAction_LogsOnlyTypedCountAndTenantId_AndPreservesCommand(bool extend, string? reason)
    {
        var tenantId = new TenantId(Guid.Parse("d75088c0-47a4-43d9-9cdc-56650961f0da"));
        Guid[] permissionIds = [Guid.NewGuid(), Guid.NewGuid()];
        IRequest<int> command = extend
            ? new ExtendTenantPermissionExpirationCommand
            {
                TenantId = tenantId,
                PermissionIds = permissionIds,
                Extension = TimeSpan.FromHours(2),
                Reason = reason
            }
            : new SetTenantPermissionExpirationCommand
            {
                TenantId = tenantId,
                PermissionIds = permissionIds,
                ExpiresAt = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Reason = reason
            };
        var sender = new Mock<ISender>();
        sender.Setup(dispatcher => dispatcher.Send(It.IsAny<IRequest<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        var logger = new Mock<ILogger<TenantPermissionsController>>();
        var controller = new TenantPermissionsController(sender.Object, logger.Object);
        using var cancellation = new CancellationTokenSource();

        var result = extend
            ? await controller.ExtendPermissionExpiration((ExtendTenantPermissionExpirationCommand)command, cancellation.Token)
            : await controller.SetPermissionExpiration((SetTenantPermissionExpirationCommand)command, cancellation.Token);

        var log = logger.Invocations.Single(invocation => invocation.Method.Name == nameof(ILogger.Log));
        var properties = ((IEnumerable<KeyValuePair<string, object?>>)log.Arguments[2]).ToArray();
        var template = extend
            ? "Extending expiration for {Count} permission grants in tenant {TenantId}"
            : "Setting expiration for {Count} permission grants in tenant {TenantId}";
        log.Arguments[0].Should().Be(LogLevel.Information);
        log.Arguments[3].Should().BeNull();
        properties.Select(property => property.Key).Should().BeEquivalentTo(new[] { "Count", "TenantId", "{OriginalFormat}" });
        properties.Single(property => property.Key == "Count").Value.Should().Be(2);
        properties.Single(property => property.Key == "TenantId").Value.Should().Be(tenantId);
        properties.Single(property => property.Key == "{OriginalFormat}").Value.Should().Be(template);
        var expectedMessage = extend
            ? "Extending expiration for 2 permission grants in tenant d75088c0-47a4-43d9-9cdc-56650961f0da"
            : "Setting expiration for 2 permission grants in tenant d75088c0-47a4-43d9-9cdc-56650961f0da";
        log.Arguments[2].ToString().Should().Be(expectedMessage);
        sender.Verify(dispatcher => dispatcher.Send(
            It.Is<IRequest<int>>(request => ReferenceEquals(request, command)), cancellation.Token), Times.Once);
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var responseProperty = extend ? "Extended" : "Updated";
        ok.Value!.GetType().GetProperty(responseProperty)!.GetValue(ok.Value).Should().Be(2);
        var actualReason = extend
            ? ((ExtendTenantPermissionExpirationCommand)command).Reason
            : ((SetTenantPermissionExpirationCommand)command).Reason;
        actualReason.Should().Be(reason);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExpirationCommand_ValidTenantGuid_IsPreservedByJsonDeserialization(bool extend)
    {
        var tenantValue = Guid.Parse("d75088c0-47a4-43d9-9cdc-56650961f0da");
        var permissionId = Guid.NewGuid();
        const string reason = "reason\r\nFORGED EVENT";
        var json = JsonSerializer.Serialize(new
        {
            TenantId = new { Value = tenantValue },
            PermissionIds = new[] { permissionId },
            ExpiresAt = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Extension = TimeSpan.FromHours(2),
            Reason = reason
        });

        if (extend)
        {
            var command = JsonSerializer.Deserialize<ExtendTenantPermissionExpirationCommand>(json)!;
            command.TenantId.Value.Should().Be(tenantValue);
            command.PermissionIds.Should().Equal(permissionId);
            command.Reason.Should().Be(reason);
            command.Extension.Should().Be(TimeSpan.FromHours(2));
        }
        else
        {
            var command = JsonSerializer.Deserialize<SetTenantPermissionExpirationCommand>(json)!;
            command.TenantId.Value.Should().Be(tenantValue);
            command.PermissionIds.Should().Equal(permissionId);
            command.Reason.Should().Be(reason);
            command.ExpiresAt.Should().Be(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        }
    }

    [Theory]
    [InlineData(false, "d75088c0-47a4-43d9-9cdc-56650961f0da\r\nFORGED EVENT")]
    [InlineData(true, "d75088c0-47a4-43d9-9cdc-56650961f0da\r\nFORGED EVENT")]
    [InlineData(false, "d75088c0-47a4-43d9-9cdc-56650961f0da\u0000\u2028FORGED EVENT")]
    [InlineData(true, "d75088c0-47a4-43d9-9cdc-56650961f0da\u0000\u2028FORGED EVENT")]
    public void ExpirationCommand_MalformedTenantGuid_IsRejectedByJsonDeserialization(bool extend, string tenantValue)
    {
        var json = JsonSerializer.Serialize(new
        {
            TenantId = new { Value = tenantValue },
            PermissionIds = new[] { Guid.NewGuid() },
            ExpiresAt = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Extension = TimeSpan.FromHours(2),
            Reason = "ordinary reason"
        });

        Action deserialize = () =>
        {
            if (extend)
            {
                JsonSerializer.Deserialize<ExtendTenantPermissionExpirationCommand>(json);
            }
            else
            {
                JsonSerializer.Deserialize<SetTenantPermissionExpirationCommand>(json);
            }
        };

        deserialize.Should().Throw<JsonException>();
    }
}
