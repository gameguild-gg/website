using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using GameGuild.API.Core.CostAccounting;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.API.UnitTests.CostAccounting;

public sealed class CostTelemetryMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenTelemetryIsCanceled_PreservesSuccessfulResponse()
    {
        using var disconnected = new CancellationTokenSource();
        disconnected.Cancel();
        var producer = new Mock<IDurableEventProducer>();
        producer.Setup(service => service.RecordAsync(It.IsAny<IDurableIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        var httpContext = new DefaultHttpContext { RequestAborted = disconnected.Token };
        var middleware = new CostTelemetryMiddleware(context =>
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        }, NullLogger<CostTelemetryMiddleware>.Instance);

        await middleware.InvokeAsync(httpContext, producer.Object, new ActorContextAccessor(), new CostTelemetryContext());

        Assert.Equal(StatusCodes.Status204NoContent, httpContext.Response.StatusCode);
        producer.Verify(service => service.RecordAsync(It.IsAny<IDurableIntegrationEvent>(),
            It.Is<CancellationToken>(token => token != disconnected.Token && !token.IsCancellationRequested)), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WhenRequestAndTelemetryFail_PreservesOriginalException()
    {
        var original = new InvalidOperationException("original request failure");
        var producer = new Mock<IDurableEventProducer>();
        producer.Setup(service => service.RecordAsync(It.IsAny<IDurableIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        var middleware = new CostTelemetryMiddleware(_ => Task.FromException(original),
            NullLogger<CostTelemetryMiddleware>.Instance);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(
            new DefaultHttpContext(), producer.Object, new ActorContextAccessor(), new CostTelemetryContext()));

        Assert.Same(original, actual);
    }

    [Fact]
    public async Task InvokeAsync_WhenTelemetryPersistenceFails_DoesNotFailRequest()
    {
        var producer = new Mock<IDurableEventProducer>();
        producer.Setup(service => service.RecordAsync(
                It.IsAny<IDurableIntegrationEvent>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("pricing storage unavailable"));
        var middleware = new CostTelemetryMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            },
            NullLogger<CostTelemetryMiddleware>.Instance);
        var httpContext = new DefaultHttpContext();

        await middleware.InvokeAsync(
            httpContext,
            producer.Object,
            new ActorContextAccessor(),
            new CostTelemetryContext());

        producer.Verify(service => service.RecordAsync(
            It.Is<ApiRequestMeasuredEventV1>(measured => measured.StatusCode == StatusCodes.Status204NoContent),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
