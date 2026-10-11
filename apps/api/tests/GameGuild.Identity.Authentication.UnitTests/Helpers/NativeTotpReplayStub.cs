using GameGuild.Identity.Authentication;
using Moq;

// Existing service unit tests isolate this new persistence port. Replay and transaction guarantees
// are tested independently against the compiled native module and PostgreSQL, never by this stub.
internal static class NativeTotpReplayStub
{
    internal static ITotpReplayStore Create()
    {
        var store = new Mock<ITotpReplayStore>(MockBehavior.Strict);
        store.Setup(port => port.TryAcceptAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<long>(),
                It.IsAny<DateTimeOffset>(), It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns(async (Guid _, string _, long _, DateTimeOffset _, Func<CancellationToken, Task> accepted, CancellationToken cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                await accepted(cancellationToken);
                return true;
            });
        return store.Object;
    }
}
