using FluentAssertions;
using GameGuild.Identity.Users;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Handlers;

public sealed class DurableWelcomeEmailTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DurableDelivery_PropagatesBothQueueFailuresAndExceptions(bool throws)
    {
        var user = new User { Email = "recipient@example.test", Name = "Recipient" };
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var queue = NotificationQueueStub.Success();
        queue.SetReturnsDefault(throws
            ? Task.FromException<Result<Notification>>(new InvalidOperationException("Unavailable"))
            : Task.FromResult(Result.Failure<Notification>(Error.Failure("Queue.Unavailable", "Unavailable"))));
        var handler = new SendWelcomeEmailHandler(NullLogger<SendWelcomeEmailHandler>.Instance, queue.Object, users.Object);

        var act = () => handler.HandleAsync(new UserCreatedEvent(user.Id) { AggregateType = "User", AggregateId = user.Id.ToString() });

        await act.Should().ThrowAsync<InvalidOperationException>();
        users.Verify(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DurableDelivery_DeletedAccountDoesNotReceiveDelayedEmail()
    {
        var users = new Mock<IUserRepository>();
        var queue = new Mock<INotificationService>(MockBehavior.Strict);
        var handler = new SendWelcomeEmailHandler(NullLogger<SendWelcomeEmailHandler>.Instance, queue.Object, users.Object);
        var userId = Guid.NewGuid();
        await handler.HandleAsync(new UserCreatedEvent(userId) { AggregateType = "User", AggregateId = userId.ToString() });
        queue.VerifyNoOtherCalls();
    }

    [Fact]
    public void Composition_RegistersBothDurableWelcomeAndAnalyticsListeners()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton(Mock.Of<IUserRepository>());
        services.AddSingleton(NotificationQueueStub.Success().Object);
        services.AddAuthenticationApplication();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IIntegrationEventHandler<UserCreatedEvent>>().ToArray();
        handlers.Should().ContainSingle(x => x is SendWelcomeEmailHandler);
        handlers.Should().ContainSingle(x => x is LogAnalyticsEventHandler);
    }
}
