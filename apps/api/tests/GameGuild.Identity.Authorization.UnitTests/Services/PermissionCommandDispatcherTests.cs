using FluentAssertions;
using GameGuild.CQRS;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests.Services;

/// <summary>
///     Tests for the dual-mode permission command dispatcher (issue #302): the same
///     command instance flows through the identical mediator pipeline whether dispatched
///     asynchronously or synchronously.
/// </summary>
public class PermissionCommandDispatcherTests
{
    private readonly Mock<ISender> _sender = new();

    [Fact]
    public async Task DispatchAsync_ForwardsCommandThroughPipeline()
    {
        var command = new DispatchProbeCommand { Marker = 42 };
        _sender
            .Setup(s => s.Send(It.Is<DispatchProbeCommand>(c => c.Marker == 42), It.IsAny<CancellationToken>()))
            .ReturnsAsync("ok");

        var sut = new PermissionCommandDispatcher(_sender.Object);

        var result = await sut.DispatchAsync<string>(command);

        result.Should().Be("ok");
    }

    [Fact]
    public void Dispatch_BlocksUntilTheHandlerCompletes_AndReturnsItsResult()
    {
        var command = new DispatchProbeCommand { Marker = 7 };
        _sender
            .Setup(s => s.Send(It.Is<DispatchProbeCommand>(c => c.Marker == 7), It.IsAny<CancellationToken>()))
            .ReturnsAsync("sync-ok");

        var sut = new PermissionCommandDispatcher(_sender.Object);

        var result = sut.Dispatch<string>(command);

        result.Should().Be("sync-ok");
        _sender.Verify(
            s => s.Send(It.Is<DispatchProbeCommand>(c => c.Marker == 7), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void Dispatch_VoidCommand_CompletesWithoutError()
    {
        var command = new VoidProbeCommand();
        _sender
            .Setup(s => s.Send(It.IsAny<VoidProbeCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new PermissionCommandDispatcher(_sender.Object);

        var act = () => sut.Dispatch(command);

        act.Should().NotThrow();
    }

    [Fact]
    public void Dispatch_HandlerFailure_PropagatesTheException()
    {
        _sender
            .Setup(s => s.Send(It.IsAny<DispatchProbeCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var sut = new PermissionCommandDispatcher(_sender.Object);

        var act = () => sut.Dispatch<string>(new DispatchProbeCommand());

        act.Should().Throw<InvalidOperationException>();
    }

    private sealed record DispatchProbeCommand : ICommand<string>
    {
        public int Marker { get; init; }
    }

    private sealed record VoidProbeCommand : ICommand;
}
