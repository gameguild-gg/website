using System.Reflection;
using System.ComponentModel.DataAnnotations;
using GameGuild.Compliance.Audit;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

public sealed class AuditReplayControllerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 11, 0, 0, 0, TimeSpan.Zero);
    private static readonly CaptureAuditReplayRequest Request = new() { Start = Start, End = Start.AddSeconds(1) };

    private static AuditReplayController Controller(Mock<ISender> sender) => new(sender.Object)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    private static AuditReplaySnapshot Snapshot() => AuditReplaySnapshot.Capture(null, [], 0,
        Start, Start.AddSeconds(1), Start.AddSeconds(2));

    [Fact]
    public async Task ReturnsExactCanonicalJsonWithExpectedDigestAndNoStoreHeaders()
    {
        var snapshot = Snapshot();
        var sender = new Mock<ISender>(MockBehavior.Strict);
        using var cancellation = new CancellationTokenSource();
        sender.Setup(value => value.Send(It.Is<CaptureAuditReplayCommand>(command =>
            command.Request.Start == Request.Start && command.Request.End == Request.End),
            cancellation.Token)).ReturnsAsync(snapshot);
        var controller = Controller(sender);
        var result = Assert.IsType<ContentResult>(await controller.Capture(Request, cancellation.Token));
        Assert.Equal(snapshot.Json, result.Content);
        Assert.StartsWith("application/json", result.ContentType!);
        Assert.Equal(snapshot.Sha256, controller.Response.Headers["X-Audit-Snapshot-SHA256"].ToString());
        Assert.Equal("private, no-store", controller.Response.Headers.CacheControl.ToString());
        Assert.Equal("nosniff", controller.Response.Headers["X-Content-Type-Options"].ToString());
        Assert.True(AuditReplaySnapshot.MatchesDigest(result.Content!, snapshot.Sha256));
        sender.VerifyAll();
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task MissingRangeEndpointsNeverDispatch(int scenario)
    {
        var request = scenario switch
        {
            0 => null,
            1 => new CaptureAuditReplayRequest(),
            2 => new CaptureAuditReplayRequest { Start = Start },
            3 => new CaptureAuditReplayRequest { End = Start },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        var sender = new Mock<ISender>(MockBehavior.Strict);
        var controller = Controller(sender);
        var result = Assert.IsType<BadRequestObjectResult>(await controller.Capture(request!, default));
        Assert.Equal(400, result.StatusCode);
        Assert.IsType<ValidationProblemDetails>(result.Value);
        Assert.False(controller.Response.Headers.ContainsKey("X-Audit-Snapshot-SHA256"));
        sender.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0, 400)] [InlineData(1, 503)] [InlineData(2, 500)]
    public async Task CaptureFailuresReturnSafeStructuredErrorsWithoutDigest(int scenario, int expectedStatus)
    {
        Exception failure = scenario switch
        {
            0 => new ArgumentException("PRIVATE_DATABASE_DETAIL"),
            1 => new AuditReplayCaptureAuditException(),
            2 => new InvalidOperationException("PRIVATE_DATABASE_DETAIL"),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        var sender = new Mock<ISender>(MockBehavior.Strict);
        sender.Setup(value => value.Send(It.IsAny<CaptureAuditReplayCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);
        var controller = Controller(sender);
        var result = Assert.IsAssignableFrom<ObjectResult>(await controller.Capture(Request, default));
        Assert.Equal(expectedStatus, result.StatusCode);
        var problem = Assert.IsAssignableFrom<ProblemDetails>(result.Value);
        Assert.Equal(expectedStatus, problem.Status);
        Assert.DoesNotContain("PRIVATE_DATABASE_DETAIL", System.Text.Json.JsonSerializer.Serialize(problem));
        Assert.False(controller.Response.Headers.ContainsKey("X-Audit-Snapshot-SHA256"));
        Assert.Equal("private, no-store", controller.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task ServiceAuthorizationFailureIsForbiddenWithoutEvidence()
    {
        var sender = new Mock<ISender>(MockBehavior.Strict);
        sender.Setup(value => value.Send(It.IsAny<CaptureAuditReplayCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException());
        var controller = Controller(sender);
        Assert.IsType<ForbidResult>(await controller.Capture(Request, default));
        Assert.False(controller.Response.Headers.ContainsKey("X-Audit-Snapshot-SHA256"));
    }

    [Fact]
    public async Task CancellationPropagatesWithoutReturningSnapshotEvidence()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var sender = new Mock<ISender>(MockBehavior.Strict);
        sender.Setup(value => value.Send(It.IsAny<CaptureAuditReplayCommand>(), cancellation.Token))
            .ThrowsAsync(new OperationCanceledException(cancellation.Token));
        var controller = Controller(sender);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => controller.Capture(Request, cancellation.Token));
        Assert.False(controller.Response.Headers.ContainsKey("X-Audit-Snapshot-SHA256"));
    }

    [Fact]
    public void EndpointRequiresAdministratorPolicyAndExplicitRange()
    {
        Assert.Equal(Policies.SystemAdmin,
            typeof(AuditReplayController).GetCustomAttribute<AuthorizeAttribute>()!.Policy);
        Assert.NotNull(typeof(CaptureAuditReplayRequest).GetProperty(nameof(CaptureAuditReplayRequest.Start))!
            .GetCustomAttribute<RequiredAttribute>());
        Assert.NotNull(typeof(CaptureAuditReplayRequest).GetProperty(nameof(CaptureAuditReplayRequest.End))!
            .GetCustomAttribute<RequiredAttribute>());
        Assert.Equal(["End", "Start"], typeof(CaptureAuditReplayRequest).GetProperties().Select(p => p.Name).Order().ToArray());
    }

    [Fact]
    public async Task CommandHandlerForwardsRequestAndCancellationToAuthorizedCaptureService()
    {
        using var cancellation = new CancellationTokenSource();
        var request = new AuditReplayCaptureRequest(Start, Start.AddSeconds(1));
        var snapshot = Snapshot();
        var service = new Mock<IAuditReplayCaptureService>(MockBehavior.Strict);
        service.Setup(value => value.CaptureAsync(request, cancellation.Token)).ReturnsAsync(snapshot);
        var result = await new CaptureAuditReplayCommandHandler(service.Object)
            .Handle(new CaptureAuditReplayCommand(request), cancellation.Token);
        Assert.Same(snapshot, result);
        service.VerifyAll();
    }
}
