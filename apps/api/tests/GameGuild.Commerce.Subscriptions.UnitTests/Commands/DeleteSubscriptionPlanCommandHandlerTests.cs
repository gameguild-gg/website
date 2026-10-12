using FluentAssertions;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Subscriptions.UnitTests.Commands;

/// <summary>
///     Issue #416: "Cannot delete plans with active subscriptions (must archive)" — the
///     guard must fail with an explicit archive-instead error instead of a raw FK failure.
/// </summary>
public class DeleteSubscriptionPlanCommandHandlerTests
{
    private readonly Mock<ISubscriptionPlanRepository> _repository = new();
    private readonly Mock<ISubscriptionPlanListCache> _planListCache = new();

    private DeleteSubscriptionPlanCommandHandler CreateHandler() =>
        new(_repository.Object, _planListCache.Object);

    [Fact]
    public async Task Handle_DeletesAndInvalidatesCache_WhenNoActiveSubscriptions()
    {
        var planId = Guid.NewGuid();
        _repository.Setup(r => r.GetByIdAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionPlan("Pro", "pro", 999));
        _repository.Setup(r => r.GetActiveSubscriptionCountAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        await CreateHandler().Handle(new DeleteSubscriptionPlanCommand(planId), CancellationToken.None);

        _repository.Verify(r => r.DeleteAsync(planId, It.IsAny<CancellationToken>()), Times.Once);
        _planListCache.Verify(cache => cache.Invalidate(), Times.Once);
    }

    [Fact]
    public async Task Handle_ThrowsWithArchiveGuidance_WhenActiveSubscriptionsExist()
    {
        var planId = Guid.NewGuid();
        _repository.Setup(r => r.GetByIdAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionPlan("Pro", "pro", 999));
        _repository.Setup(r => r.GetActiveSubscriptionCountAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);

        var act = () => CreateHandler().Handle(new DeleteSubscriptionPlanCommand(planId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*7 active subscription*")
            .WithMessage("*Archive*");
        _repository.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _planListCache.Verify(cache => cache.Invalidate(), Times.Never);
    }

    [Fact]
    public async Task Handle_ThrowsEntityNotFound_WhenPlanDoesNotExist()
    {
        var planId = Guid.NewGuid();
        _repository.Setup(r => r.GetByIdAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionPlan?)null);

        var act = () => CreateHandler().Handle(new DeleteSubscriptionPlanCommand(planId), CancellationToken.None);

        await act.Should().ThrowAsync<EntityNotFoundException>();
        _repository.Verify(r => r.GetActiveSubscriptionCountAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
