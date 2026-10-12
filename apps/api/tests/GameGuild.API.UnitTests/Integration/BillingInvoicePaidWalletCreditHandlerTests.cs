using FluentAssertions;
using GameGuild.API.Core.Billing;
using GameGuild.Commerce.Billing;
using GameGuild.Commerce.Payments;
using GameGuild.Commerce.Subscriptions;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Options;
using Moq;

namespace GameGuild.API.UnitTests.Integration;

/// <summary>
///     Webhook-driven wallet credit tests (issue #406): a durable BillingInvoicePaidV1 event
///     credits the paying user's wallet through AddFundsCommand with an idempotent reference,
///     dispatched as the well-known system actor.
/// </summary>
public sealed class BillingInvoicePaidWalletCreditHandlerTests
{
    private readonly Mock<ISubscriptionQueryService> _subscriptions = new();
    private readonly Mock<ISender> _sender = new();
    private readonly ActorContextAccessor _actorContextAccessor = new();

    [Fact]
    public async Task Handle_WhenSubscriptionExists_ShouldDispatchAddFundsAsTheSystemActor()
    {
        var ownerUserId = Guid.NewGuid();
        var subscription = NewSubscription(ownerUserId);
        _subscriptions
            .Setup(service => service.GetByIdAsync(subscription.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        _sender
            .Setup(sender => sender.Send(It.IsAny<IRequest<WalletTransaction>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WalletTransaction());
        var handler = CreateHandler(enabled: true);

        var @event = NewEvent(subscription.Id, amount: 42.50m, currency: "USD", externalEventId: "evt_123");
        await handler.HandleAsync(@event);

        _sender.Verify(sender => sender.Send(
            It.Is<AddFundsCommand>(command =>
                command.UserId == ownerUserId
                && command.Amount == 42.50m
                && command.Currency == "USD"
                && command.ReferenceId == "billing-invoice-paid:stripe:evt_123"
                && command.Description.Contains("stripe")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldRestoreThePreviousActorContextAfterDispatch()
    {
        var ownerUserId = Guid.NewGuid();
        var subscription = NewSubscription(ownerUserId);
        _subscriptions
            .Setup(service => service.GetByIdAsync(subscription.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        _sender
            .Setup(sender => sender.Send(It.IsAny<IRequest<WalletTransaction>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WalletTransaction());
        var handler = CreateHandler(enabled: true);

        var previous = ActorContextBuilder.ForUser(Guid.NewGuid()).Build();
        _actorContextAccessor.SetActorContext(previous);

        await handler.HandleAsync(NewEvent(subscription.Id, 10m, "USD", "evt_1"));

        _actorContextAccessor.ActorContext.SubjectId.Should().Be(previous.SubjectId);
        _actorContextAccessor.ActorContext.IsSystemAdmin.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenSubscriptionIsUnknown_ShouldNotDispatch()
    {
        _subscriptions
            .Setup(service => service.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null);
        var handler = CreateHandler(enabled: true);

        await handler.HandleAsync(NewEvent(Guid.NewGuid(), 10m, "USD", "evt_2"));

        _sender.Verify(sender => sender.Send(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenWebhookCreditsAreDisabled_ShouldNotDispatch()
    {
        var handler = CreateHandler(enabled: false);

        await handler.HandleAsync(NewEvent(Guid.NewGuid(), 10m, "USD", "evt_3"));

        _subscriptions.Verify(service => service.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _sender.Verify(sender => sender.Send(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Handle_WhenAmountIsNotPositive_ShouldNotDispatch(decimal amount)
    {
        var handler = CreateHandler(enabled: true);

        await handler.HandleAsync(NewEvent(Guid.NewGuid(), amount, "USD", "evt_4"));

        _sender.Verify(sender => sender.Send(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldTruncateOverlongReferences()
    {
        var ownerUserId = Guid.NewGuid();
        var subscription = NewSubscription(ownerUserId);
        _subscriptions
            .Setup(service => service.GetByIdAsync(subscription.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        _sender
            .Setup(sender => sender.Send(It.IsAny<IRequest<WalletTransaction>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WalletTransaction());
        var handler = CreateHandler(enabled: true);

        var longEventId = new string('x', 200);
        await handler.HandleAsync(NewEvent(subscription.Id, 10m, "USD", longEventId));

        _sender.Verify(sender => sender.Send(
            It.Is<AddFundsCommand>(command => command.ReferenceId!.Length <= 100),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private BillingInvoicePaidWalletCreditHandler CreateHandler(bool enabled) =>
        new(
            _subscriptions.Object,
            _sender.Object,
            _actorContextAccessor,
            Options.Create(new WalletsOptions { WebhookCreditEnabled = enabled }));

    private static Subscription NewSubscription(Guid createdByUserId) =>
        new(Guid.NewGuid(), Guid.NewGuid(), createdByUserId, BillingCycle.Monthly, new Money(10m, "USD"), SystemClock.UtcNow);

    private static BillingInvoicePaidV1 NewEvent(Guid subscriptionId, decimal amount, string currency, string externalEventId) =>
        new(subscriptionId, "stripe", externalEventId, amount, currency)
        {
            TenantId = Guid.NewGuid(),
            AggregateType = "Subscription",
            AggregateId = subscriptionId.ToString()
        };
}
