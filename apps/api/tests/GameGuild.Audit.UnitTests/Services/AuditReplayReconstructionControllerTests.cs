using System.Reflection;
using System.Text.Json;
using GameGuild.Compliance.Audit;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

/// <summary>Controller surface for the replay GET capture alias and the reconstruct
/// custom action (issue #169).</summary>
public sealed class AuditReplayReconstructionControllerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 12, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid Tenant = Guid.Parse("30000000-0000-0000-0000-000000000290");
    private static readonly Guid Admin = Guid.Parse("30000000-0000-0000-0000-000000000291");

    private static AuditReplayController Controller(Mock<ISender> sender) => new(sender.Object)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    private static AuditReplaySnapshot Snapshot() => AuditReplaySnapshot.Capture(null,
    [
        new AuditLogDto
        {
            Id = Admin, TenantId = null, CreatedAt = Start.UtcDateTime, ActionType = "Read",
            ResourceType = "Document", ResourceId = "doc-1", UserId = Admin, SessionId = Admin,
            CorrelationId = "request-1", Success = false
        }
    ], 1, Start, Start.AddSeconds(1), Start.AddSeconds(2));

    private static (ReconstructAuditReplayRequest Request, AuditReplayReconstruction Reconstruction) RoundTrip()
    {
        var snapshot = Snapshot();
        using var json = JsonDocument.Parse(snapshot.Json);
        var root = json.RootElement;
        var document = new AuditReplaySnapshotDocument(
            root.GetProperty("schemaVersion").GetInt32(),
            root.GetProperty("tenantId").GetString(),
            root.GetProperty("totalCount").GetInt32(),
            root.GetProperty("startUtc").GetString()!,
            root.GetProperty("endUtc").GetString()!,
            root.GetProperty("capturedAtUtc").GetString()!,
            [.. root.GetProperty("events").EnumerateArray().Select(item => new AuditReplaySnapshotEvent(
                item.GetProperty("id").GetString()!, item.GetProperty("tenantId").GetString(),
                item.GetProperty("createdAt").GetString()!, item.GetProperty("actionType").GetString()!,
                item.GetProperty("resourceType").GetString()!, item.GetProperty("resourceId").GetString(),
                item.GetProperty("userId").GetString(), item.GetProperty("sessionId").GetString(),
                item.GetProperty("correlationId").GetString(), item.GetProperty("success").GetBoolean()))]);
        var reconstruction = new AuditReplayReconstruction(
            snapshot.Sha256, snapshot.TenantId, snapshot.TotalCount, 1,
            root.GetProperty("startUtc").GetString()!, root.GetProperty("endUtc").GetString()!,
            root.GetProperty("capturedAtUtc").GetString()!, snapshot.Events,
            [new AuditReplayUserJourney(Admin.ToString("D"), 1, 1,
                snapshot.Events[0].CreatedAt, snapshot.Events[0].CreatedAt, [Admin.ToString("D")])],
            [new AuditReplaySessionSummary(Admin.ToString("D"), 1, 1,
                snapshot.Events[0].CreatedAt, snapshot.Events[0].CreatedAt, [Admin.ToString("D")])]);
        return (new ReconstructAuditReplayRequest(snapshot.Sha256, document), reconstruction);
    }

    [Fact]
    public async Task GetCaptureDispatchesTheSameCommandAsPost()
    {
        var snapshot = Snapshot();
        var sender = new Mock<ISender>(MockBehavior.Strict);
        using var cancellation = new CancellationTokenSource();
        sender.Setup(value => value.Send(It.Is<CaptureAuditReplayCommand>(command =>
            command.Request.Start == Start && command.Request.End == Start.AddSeconds(1)),
            cancellation.Token)).ReturnsAsync(snapshot);
        var controller = Controller(sender);
        var result = Assert.IsType<ContentResult>(await controller.CaptureViaGet(Start, Start.AddSeconds(1), cancellation.Token));
        Assert.Equal(snapshot.Json, result.Content);
        Assert.Equal(snapshot.Sha256, controller.Response.Headers["X-Audit-Snapshot-SHA256"].ToString());
        Assert.Equal("private, no-store", controller.Response.Headers.CacheControl.ToString());
        sender.VerifyAll();
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task GetCaptureWithoutBothBoundsNeverDispatches(int scenario)
    {
        var sender = new Mock<ISender>(MockBehavior.Strict);
        var controller = Controller(sender);
        var result = Assert.IsType<BadRequestObjectResult>(await controller.CaptureViaGet(
            scenario == 1 ? null : Start,
            scenario == 0 ? null : (scenario == 2 ? null : Start.AddSeconds(1)),
            default));
        Assert.Equal(400, result.StatusCode);
        Assert.IsType<ValidationProblemDetails>(result.Value);
        Assert.False(controller.Response.Headers.ContainsKey("X-Audit-Snapshot-SHA256"));
        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReconstructReturnsTimelineWithDigestHeader()
    {
        var (request, reconstruction) = RoundTrip();
        var sender = new Mock<ISender>(MockBehavior.Strict);
        sender.Setup(value => value.Send(It.Is<ReconstructAuditReplayCommand>(command =>
                command.Sha256 == request.Sha256 && command.Document.TotalCount == 1),
            It.IsAny<CancellationToken>())).ReturnsAsync(reconstruction);
        var controller = Controller(sender);
        var result = Assert.IsType<OkObjectResult>(await controller.Reconstruct(request, default));
        var body = Assert.IsType<AuditReplayReconstructionResponse>(result.Value);
        Assert.Equal(reconstruction.Sha256, body.Sha256);
        Assert.Equal(1, body.TotalCount);
        Assert.Equal(1, body.FailedCount);
        Assert.Single(body.Users);
        Assert.Single(body.Sessions);
        Assert.Equal(reconstruction.Sha256, controller.Response.Headers["X-Audit-Snapshot-SHA256"].ToString());
        Assert.Equal("private, no-store", controller.Response.Headers.CacheControl.ToString());
        sender.VerifyAll();
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task ReconstructRejectsIncompleteBodiesWithoutDispatch(int scenario)
    {
        var (request, _) = RoundTrip();
        var malformed = scenario switch
        {
            0 => null,
            1 => request with { Sha256 = "  " },
            2 => request with { Document = null },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        var sender = new Mock<ISender>(MockBehavior.Strict);
        var controller = Controller(sender);
        var result = Assert.IsType<BadRequestObjectResult>(await controller.Reconstruct(malformed!, default));
        Assert.Equal(400, result.StatusCode);
        Assert.IsType<ValidationProblemDetails>(result.Value);
        Assert.False(controller.Response.Headers.ContainsKey("X-Audit-Snapshot-SHA256"));
        sender.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0, 403)] [InlineData(1, 400)] [InlineData(2, 400)] [InlineData(3, 503)] [InlineData(4, 500)]
    public async Task ReconstructFailuresReturnSafeStructuredErrors(int scenario, int expectedStatus)
    {
        var (request, _) = RoundTrip();
        Exception failure = scenario switch
        {
            0 => new UnauthorizedAccessException(),
            1 => new ArgumentException("PRIVATE_DETAIL"),
            2 => new AuditReplayIntegrityMismatchException(),
            3 => new AuditReplayCaptureAuditException(),
            4 => new InvalidOperationException("PRIVATE_DETAIL"),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        var sender = new Mock<ISender>(MockBehavior.Strict);
        sender.Setup(value => value.Send(It.IsAny<ReconstructAuditReplayCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);
        var controller = Controller(sender);
        var result = await controller.Reconstruct(request, default);
        if (expectedStatus == 403)
        {
            Assert.IsType<ForbidResult>(result);
        }
        else
        {
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(expectedStatus, objectResult.StatusCode);
            Assert.False(controller.Response.Headers.ContainsKey("X-Audit-Snapshot-SHA256"));
        }
    }

    [Fact]
    public void ControllerActionsDeclareAuthorizationAndRateLimits()
    {
        var type = typeof(AuditReplayController);
        Assert.NotNull(type.GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>());
        Assert.NotNull(type.GetCustomAttribute<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>());
        Assert.NotNull(type.GetMethod(nameof(AuditReplayController.CaptureViaGet))!
            .GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpGetAttribute>());
        Assert.NotNull(type.GetMethod(nameof(AuditReplayController.Reconstruct))!
            .GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpPostAttribute>());
        Assert.Equal("snapshots", type.GetMethod(nameof(AuditReplayController.CaptureViaGet))!
            .GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpGetAttribute>()!.Template);
        Assert.Equal("snapshots:reconstruct", type.GetMethod(nameof(AuditReplayController.Reconstruct))!
            .GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpPostAttribute>()!.Template);
    }
}
